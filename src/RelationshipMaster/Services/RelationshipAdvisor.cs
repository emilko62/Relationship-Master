using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using RelationshipMaster.Models;

namespace RelationshipMaster.Services;

/// <summary>Result of the AI judging how a planned interaction went.</summary>
public sealed record OutcomeAssessment(int SuccessPercent, int OurOpinionDelta, int TheirOpinionDelta, string Assessment);

/// <summary>
/// AI relationship strategist backed by Claude: field chat, interaction planning, outcome scoring and
/// person extraction.
/// </summary>
public sealed class RelationshipAdvisor
{
    public const string ModelId = "claude-opus-5-5";

    internal const string Ethics =
        "Give grounded, practical and ethical social advice: build genuine goodwill, communicate honestly, " +
        "protect the user's interests and boundaries. Competition is fine - win by outperforming, preparing better, " +
        "building alliances and showcasing strengths - but never suggest sabotage, deception, manipulation, " +
        "coercion, harassment, or exploiting someone's personal vulnerabilities to hurt them. When the user notes a " +
        "rival's weaknesses, use them to find where the user can shine by contrast, not to attack the person.";

    internal const string ChatSystem =
        "You are the built-in advisor of \"Relationship Master\", a desktop app where the user maps social " +
        "fields (work, family, clubs...) as circles: each person's circle size is their influence in the field, " +
        "their colour is the user's opinion of them, and the user also records how each person supposedly " +
        "perceives them, key facts about them and a history of events. Each user message starts with a fresh " +
        "snapshot of the field.\n\n" + Ethics + "\n\n" +
        "Point out blind spots - especially gaps between how the user sees someone and how that person supposedly " +
        "sees the user, and influential people the user has neglected. Refer to people by name. Keep answers " +
        "concise and skimmable: short paragraphs or bullet points, no more than ~250 words unless the user asks " +
        "for more. Plain text only - no markdown headings or tables.";

    internal const string PlanSystem =
        "You are the interaction planner of \"Relationship Master\". The user wants to plan one concrete " +
        "interaction with a specific person. You receive the field snapshot, a dossier of the target (key info " +
        "such as likes, skills, quirks, weaknesses, plus relationship history), the goal, the situation and an " +
        "intensity: how strongly the user wants this interaction to move the relationship (0 = it doesn't matter, " +
        "~5 = small gesture, ~10 = noticeable effort, 20+ = grandiose gesture). Scale the size, cost and boldness " +
        "of your suggestions to that intensity.\n\n" + Ethics + "\n\n" +
        "Use the recorded key info explicitly (e.g. if they love Great Danes, use that) and name which facts you " +
        "used. Structure, in plain text without markdown headings:\n" +
        "PLAN - 3 to 6 numbered concrete steps (what to say/do, when, where)\n" +
        "WHY IT WORKS - which recorded facts or history you are building on\n" +
        "RISKS - what could backfire and how to avoid it\n" +
        "SIGNS OF SUCCESS - what to watch for afterwards\n" +
        "Keep it under ~350 words.";

    internal const string OutcomeSystem =
        "You evaluate how a planned social interaction actually went, for the app \"Relationship Master\". You get " +
        "the target's dossier, the goal, the plan, the intensity (maximum number of opinion points this " +
        "interaction may move, 0 = no effect at all) and the user's story of what happened. Judge honestly - do not " +
        "flatter the user.\n" +
        "- successPercent: 0..100, how well the goal was achieved.\n" +
        "- theirOpinionDelta: change in how the target now (supposedly) sees the user.\n" +
        "- ourOpinionDelta: change in the user's own opinion of the target, based on how the target behaved.\n" +
        "Both deltas must be integers whose absolute value does not exceed the intensity. A partial success gives a " +
        "partial change; a backfire gives a negative change.\n" +
        "- assessment: 2-4 sentences explaining the rating and one suggested next step. Plain text.";

    private AnthropicClient? _client;
    private readonly List<BetaMessageParam> _history = new();
    private Guid? _historyFieldId;

    public bool IsConfigured => _client != null;

    public void Configure(string? apiKey)
    {
        _client = string.IsNullOrWhiteSpace(apiKey) ? null : new AnthropicClient { ApiKey = apiKey };
    }

    public void ResetConversation()
    {
        _history.Clear();
        _historyFieldId = null;
    }

    private AnthropicClient Client => _client ?? throw new InvalidOperationException("No API key configured. Open AI settings first.");

    // ───────────────────────────── chat ─────────────────────────────

    /// <summary>Sends a question about the field and streams the answer through <paramref name="onText"/>.</summary>
    public async Task AskAsync(string question, Field field, Person? selected,
        Action<string> onText, CancellationToken ct)
    {
        if (_historyFieldId != field.Id) ResetConversation();
        _historyFieldId = field.Id;

        var userText = $"<field_snapshot>\n{FieldDescriber.Describe(field, selected)}</field_snapshot>\n\n{question}";
        var pending = new List<BetaMessageParam>(_history) { new BetaUserMessageParam { Content = userText } };

        var blocks = await StreamAsync(ChatSystem, pending, Effort.Medium, onText, ct);
        if (blocks.Count > 0)
        {
            _history.Add(pending[^1]);
            _history.Add(new BetaAssistantMessageParam { Content = blocks });
        }
    }

    // ───────────────────────────── interaction planning ─────────────────────────────

    /// <summary>Streams a concrete plan for one interaction with <paramref name="target"/>.</summary>
    public async Task PlanInteractionAsync(Field field, Person target, InteractionGoal goal, string situation,
        int intensity, Action<string> onText, CancellationToken ct)
    {
        var prompt = PlanUserPrompt(field, target, goal, situation, intensity);

        await StreamAsync(PlanSystem, [new BetaUserMessageParam { Content = prompt }], Effort.High, onText, ct);
    }

    // ───────────────────────────── outcome scoring ─────────────────────────────

    /// <summary>Rates the success of a planned interaction from the user's story and proposes opinion changes.</summary>
    public async Task<OutcomeAssessment> EvaluateOutcomeAsync(Field field, Person target, RelationshipEvent ev,
        string story, CancellationToken ct)
    {
        var prompt = OutcomeUserPrompt(field, target, ev, story);

        var schema = Schema(new
        {
            successPercent = new { type = "integer", description = "0..100" },
            ourOpinionDelta = new { type = "integer" },
            theirOpinionDelta = new { type = "integer" },
            assessment = new { type = "string" },
        }, "successPercent", "ourOpinionDelta", "theirOpinionDelta", "assessment");

        using var doc = await JsonAsync(OutcomeSystem, prompt, schema, Effort.Medium, ct);
        return OutcomeFromJson(doc.RootElement, ev.Intensity);
    }

    /// <summary>Reads an outcome JSON object (from the API or pasted from claude.ai), clamping deltas to the intensity.</summary>
    public static OutcomeAssessment OutcomeFromJson(JsonElement r, int intensity)
    {
        int max = Math.Max(0, intensity);
        return new OutcomeAssessment(
            Math.Clamp(r.GetProperty("successPercent").GetInt32(), 0, 100),
            Math.Clamp(r.GetProperty("ourOpinionDelta").GetInt32(), -max, max),
            Math.Clamp(r.GetProperty("theirOpinionDelta").GetInt32(), -max, max),
            r.TryGetProperty("assessment", out var a) ? a.GetString() ?? "" : "");
    }

    internal static string OutcomeUserPrompt(Field field, Person target, RelationshipEvent ev, string story) =>
        new StringBuilder()
            .AppendLine("<target_dossier>").Append(FieldDescriber.Dossier(target)).AppendLine("</target_dossier>")
            .AppendLine($"Field: {field.Name}")
            .AppendLine($"Goal: {RelationshipEvent.GoalLabel(ev.Goal)}")
            .AppendLine($"Intensity (max opinion points): {ev.Intensity}")
            .AppendLine("Situation:").AppendLine(ev.Situation)
            .AppendLine("Plan:").AppendLine(string.IsNullOrWhiteSpace(ev.AiPlan) ? "(no written plan)" : ev.AiPlan)
            .AppendLine("What actually happened, in the user's words:").AppendLine(story)
            .ToString();

    internal static string PlanUserPrompt(Field field, Person target, InteractionGoal goal, string situation, int intensity) =>
        new StringBuilder()
            .AppendLine("<field_snapshot>").Append(FieldDescriber.Describe(field, target)).AppendLine("</field_snapshot>")
            .AppendLine("<target_dossier>").Append(FieldDescriber.Dossier(target)).AppendLine("</target_dossier>")
            .AppendLine($"Goal: {RelationshipEvent.GoalLabel(goal)}")
            .AppendLine($"Intensity: {intensity} ({RelationshipEvent.IntensityLabel(intensity)})")
            .AppendLine("Situation, in the user's words:")
            .AppendLine(situation)
            .ToString();

    // ───────────────────────────── quick add ─────────────────────────────

    /// <summary>Uses structured output to turn a free-text description into a new person.</summary>
    public async Task<Person> ParsePersonAsync(string description, Field field, CancellationToken ct)
    {
        var schema = Schema(new
        {
            firstName = new { type = "string" },
            lastName = new { type = "string", description = "empty string if unknown" },
            role = new { type = "string", description = "short role/relationship, e.g. 'manager'" },
            fieldInfluence = new { type = "integer", description = "1..100 weight of this person in the field" },
            ourInfluence = new { type = "integer", description = "0..100 how much the user can sway them" },
            ourOpinion = new { type = "integer", description = "-100..100 the user's opinion of them" },
            theirOpinion = new { type = "integer", description = "-100..100 how they supposedly see the user" },
            authority = new { type = "integer", description = "0..100 formal authority/power (boss = high, intern = low)" },
            notes = new { type = "string", description = "anything useful that is not a key fact" },
            facts = new
            {
                type = "array",
                description = "key facts: likes, dislikes, skills, strengths, weaknesses, quirks, goals",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        category = new { type = "string", @enum = Enum.GetNames<FactCategory>() },
                        text = new { type = "string" },
                    },
                    required = new[] { "category", "text" },
                },
            },
        }, "firstName", "lastName", "role", "fieldInfluence", "ourInfluence", "ourOpinion", "theirOpinion", "authority", "notes", "facts");

        using var doc = await JsonAsync(QuickAddSystem(field), description, schema, Effort.Low, ct);
        return PersonFromJson(doc.RootElement);
    }

    internal static string QuickAddSystem(Field field) =>
        "Extract one person from the user's description of someone in their social field " +
        $"\"{field.Name}\". Infer sensible numeric scores from the wording; use 0 for an opinion " +
        "that isn't implied and moderate values (20-40) for unknown influence or authority. Put likes, skills, " +
        "quirks, weaknesses etc. into facts.";

    /// <summary>Builds a person from a quick-add JSON object (from the API or pasted from claude.ai).</summary>
    public static Person PersonFromJson(JsonElement r)
    {
        static string S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        int I(string n, int min, int max, int fallback) =>
            r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? Math.Clamp(v.GetInt32(), min, max) : fallback;
        var first = S(r, "firstName");
        var person = new Person
        {
            FirstName = first.Length > 0 ? first : "?",
            LastName = S(r, "lastName"),
            Role = S(r, "role"),
            Notes = S(r, "notes"),
            FieldInfluence = I("fieldInfluence", 1, 100, 30),
            OurInfluence = I("ourInfluence", 0, 100, 20),
            OurOpinion = I("ourOpinion", -100, 100, 0),
            TheirOpinion = I("theirOpinion", -100, 100, 0),
            Authority = I("authority", 0, 100, 20),
        };
        if (r.TryGetProperty("facts", out var facts) && facts.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in facts.EnumerateArray())
            {
                var text = S(f, "text");
                if (string.IsNullOrWhiteSpace(text)) continue;
                person.Facts.Add(new KeyFact
                {
                    Category = Enum.TryParse<FactCategory>(S(f, "category"), true, out var c) ? c : FactCategory.Other,
                    Text = text,
                });
            }
        }
        return person;
    }

    /// <summary>Human-friendly message for an exception thrown by the SDK.</summary>
    public static string Describe(Exception ex) => ex switch
    {
        OperationCanceledException => "Stopped.",
        AnthropicUnauthorizedException => "The API key was rejected. Open AI settings and check it.",
        AnthropicRateLimitException => "Rate limited by the API - wait a moment and try again.",
        Anthropic5xxException => "The AI service is temporarily unavailable. Try again shortly.",
        AnthropicApiException api => $"API error: {api.Message}",
        HttpRequestException => "Network error - check your internet connection.",
        _ => ex.Message,
    };

    // ───────────────────────────── plumbing ─────────────────────────────

    private static Dictionary<string, JsonElement> Schema(object properties, params string[] required) => new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["properties"] = JsonSerializer.SerializeToElement(properties),
        ["required"] = JsonSerializer.SerializeToElement(required),
    };

    private async Task<JsonDocument> JsonAsync(string system, string user, Dictionary<string, JsonElement> schema,
        Effort effort, CancellationToken ct)
    {
        var parameters = new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 8000,
            System = system,
            OutputConfig = new BetaOutputConfig
            {
                Effort = effort,
                Format = new BetaJsonOutputFormat { Schema = schema },
            },
            // Server-side refusal fallback: a request declined by a safety classifier is re-served
            // on a suitable fallback model within the same call.
            Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
            Fallbacks = new Default(),
            Messages = [new BetaUserMessageParam { Content = user }],
        };

        BetaMessage response = await Client.Beta.Messages.Create(parameters, ct);
        if (response.StopReason == "refusal")
            throw new InvalidOperationException("The AI declined this request.");
        if (response.StopReason == "max_tokens")
            throw new InvalidOperationException("The AI response was cut off - please try again.");

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        return JsonDocument.Parse(json);
    }

    /// <summary>Streams a reply and returns the assistant content blocks (for conversation history).</summary>
    private async Task<List<BetaContentBlockParam>> StreamAsync(string system, List<BetaMessageParam> messages,
        Effort effort, Action<string> onText, CancellationToken ct)
    {
        var parameters = new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 16000,
            System = system,
            OutputConfig = new BetaOutputConfig { Effort = effort },
            Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
            Fallbacks = new Default(),
            Messages = messages,
        };

        var events = new List<BetaRawMessageStreamEvent>();
        await foreach (var ev in Client.Beta.Messages.CreateStreaming(parameters, ct).WithCancellation(ct))
        {
            events.Add(ev);
            if (ev.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
                onText(text.Text);
        }
        return AssistantBlocksFromStream(events);
    }

    /// <summary>Rebuilds the assistant turn (text, thinking and fallback blocks, unchanged) from stream events.</summary>
    private static List<BetaContentBlockParam> AssistantBlocksFromStream(List<BetaRawMessageStreamEvent> events)
    {
        var text = new Dictionary<long, StringBuilder>();
        var thinking = new Dictionary<long, (StringBuilder thought, StringBuilder sig)>();
        var fallbacks = new Dictionary<long, BetaFallbackBlockParam>();
        var order = new List<long>();

        foreach (var ev in events)
        {
            if (ev.TryPickContentBlockStart(out var start))
            {
                switch (start.ContentBlock.Value)
                {
                    case BetaTextBlock t:
                        text[start.Index] = new StringBuilder(t.Text);
                        order.Add(start.Index);
                        break;
                    case BetaThinkingBlock th:
                        thinking[start.Index] = (new StringBuilder(th.Thinking), new StringBuilder(th.Signature));
                        order.Add(start.Index);
                        break;
                    case BetaFallbackBlock fb:
                        // Marks where a refusal fallback switched models; must be sent back unchanged.
                        try
                        {
                            var param = JsonSerializer.Deserialize<BetaFallbackBlockParam>(JsonSerializer.Serialize(fb));
                            if (param != null) { fallbacks[start.Index] = param; order.Add(start.Index); }
                        }
                        catch (Exception) { /* unknown shape - skip the marker */ }
                        break;
                }
            }
            else if (ev.TryPickContentBlockDelta(out var d))
            {
                if (d.Delta.TryPickText(out var td) && text.TryGetValue(d.Index, out var sb)) sb.Append(td.Text);
                else if (d.Delta.TryPickThinking(out var thd) && thinking.TryGetValue(d.Index, out var th1)) th1.thought.Append(thd.Thinking);
                else if (d.Delta.TryPickSignature(out var sd) && thinking.TryGetValue(d.Index, out var th2)) th2.sig.Append(sd.Signature);
            }
        }

        var result = new List<BetaContentBlockParam>();
        foreach (var idx in order)
        {
            if (text.TryGetValue(idx, out var sb)) { if (sb.Length > 0) result.Add(new BetaTextBlockParam { Text = sb.ToString() }); }
            else if (thinking.TryGetValue(idx, out var th)) result.Add(new BetaThinkingBlockParam { Thinking = th.thought.ToString(), Signature = th.sig.ToString() });
            else if (fallbacks.TryGetValue(idx, out var fb)) result.Add(fb);
        }
        return result;
    }
}
