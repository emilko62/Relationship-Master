using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using RelationshipMaster.Models;

namespace RelationshipMaster.Services;

/// <summary>
/// AI relationship strategist backed by Claude. Keeps one conversation per field and
/// streams replies back to the UI.
/// </summary>
public sealed class RelationshipAdvisor
{
    public const string ModelId = "claude-opus-5-5";

    private const string SystemPrompt =
        "You are the built-in advisor of \"Relationship Master\", a desktop app where the user maps social " +
        "fields (work, family, clubs...) as circles: each person's circle size is their influence in the field, " +
        "their colour is the user's opinion of them, and the user can also record how each person supposedly " +
        "perceives them. Each user message starts with a fresh snapshot of the field.\n\n" +
        "Give grounded, practical and ethical social advice: build genuine goodwill, communicate honestly, " +
        "protect the user's interests and boundaries. Never suggest manipulation, deception, coercion or " +
        "harassment. Point out blind spots - especially gaps between how the user sees someone and how that " +
        "person supposedly sees the user, and influential people the user has neglected. Refer to people by name. " +
        "Keep answers concise and skimmable: short paragraphs or bullet points, no more than ~250 words unless " +
        "the user asks for more. Plain text only - no markdown headings or tables.";

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

    /// <summary>Sends a question about the field and streams the answer through <paramref name="onText"/>.</summary>
    public async Task AskAsync(string question, Field field, Person? selected,
        Action<string> onText, CancellationToken ct)
    {
        var client = _client ?? throw new InvalidOperationException("No API key configured.");
        if (_historyFieldId != field.Id) ResetConversation();
        _historyFieldId = field.Id;

        var userText = $"<field_snapshot>\n{FieldDescriber.Describe(field, selected)}</field_snapshot>\n\n{question}";
        var pending = new List<BetaMessageParam>(_history) { new BetaUserMessageParam { Content = userText } };

        var parameters = new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 16000,
            System = SystemPrompt,
            OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
            // Server-side refusal fallback: if a request is declined by a safety classifier,
            // the API re-serves it on a suitable fallback model within the same call.
            Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
            Fallbacks = new Default(),
            Messages = pending,
        };

        var events = new List<BetaRawMessageStreamEvent>();
        await foreach (var ev in client.Beta.Messages.CreateStreaming(parameters, ct).WithCancellation(ct))
        {
            events.Add(ev);
            if (ev.TryPickContentBlockDelta(out var delta) && delta.Delta.TryPickText(out var text))
                onText(text.Text);
        }

        // Rebuild the assistant turn (text + thinking blocks, unchanged) so follow-ups keep context.
        var blocks = AssistantBlocksFromStream(events);
        if (blocks.Count > 0)
        {
            _history.Add(pending[^1]);
            _history.Add(new BetaAssistantMessageParam { Content = blocks });
        }
    }

    /// <summary>Uses structured output to turn a free-text description into a new person.</summary>
    public async Task<Person> ParsePersonAsync(string description, Field field, CancellationToken ct)
    {
        var client = _client ?? throw new InvalidOperationException("No API key configured.");

        var schema = new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["properties"] = JsonSerializer.SerializeToElement(new
            {
                firstName = new { type = "string" },
                lastName = new { type = "string", description = "empty string if unknown" },
                role = new { type = "string", description = "short role/relationship, e.g. 'manager'" },
                fieldInfluence = new { type = "integer", description = "1..100 weight of this person in the field" },
                ourInfluence = new { type = "integer", description = "0..100 how much the user can sway them" },
                ourOpinion = new { type = "integer", description = "-100..100 the user's opinion of them" },
                theirOpinion = new { type = "integer", description = "-100..100 how they supposedly see the user" },
                notes = new { type = "string", description = "useful facts from the description" },
            }),
            ["required"] = JsonSerializer.SerializeToElement(new[]
            {
                "firstName", "lastName", "role", "fieldInfluence", "ourInfluence", "ourOpinion", "theirOpinion", "notes",
            }),
        };

        var parameters = new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 4000,
            System = "Extract one person from the user's description of someone in their social field " +
                     $"\"{field.Name}\". Infer sensible numeric scores from the wording; use 0 for an opinion " +
                     "that isn't implied and moderate values (20-40) for unknown influence.",
            OutputConfig = new BetaOutputConfig
            {
                Effort = Effort.Low,
                Format = new BetaJsonOutputFormat { Schema = schema },
            },
            Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
            Fallbacks = new Default(),
            Messages = [new BetaUserMessageParam { Content = description }],
        };

        BetaMessage response = await client.Beta.Messages.Create(parameters, ct);
        if (response.StopReason == "refusal")
            throw new InvalidOperationException("The AI declined to process this description.");

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        int I(string n, int min, int max) => Math.Clamp(r.GetProperty(n).GetInt32(), min, max);
        return new Person
        {
            FirstName = r.GetProperty("firstName").GetString() ?? "?",
            LastName = r.GetProperty("lastName").GetString() ?? "",
            Role = r.GetProperty("role").GetString() ?? "",
            Notes = r.GetProperty("notes").GetString() ?? "",
            FieldInfluence = I("fieldInfluence", 1, 100),
            OurInfluence = I("ourInfluence", 0, 100),
            OurOpinion = I("ourOpinion", -100, 100),
            TheirOpinion = I("theirOpinion", -100, 100),
        };
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

    private static List<BetaContentBlockParam> AssistantBlocksFromStream(List<BetaRawMessageStreamEvent> events)
    {
        var text = new Dictionary<long, System.Text.StringBuilder>();
        var thinking = new Dictionary<long, (System.Text.StringBuilder thought, System.Text.StringBuilder sig)>();
        var fallbacks = new Dictionary<long, BetaFallbackBlockParam>();
        var order = new List<long>();

        foreach (var ev in events)
        {
            if (ev.TryPickContentBlockStart(out var start))
            {
                if (start.ContentBlock.Value is BetaTextBlock t)
                {
                    text[start.Index] = new System.Text.StringBuilder(t.Text);
                    order.Add(start.Index);
                }
                else if (start.ContentBlock.Value is BetaFallbackBlock fb)
                {
                    // Marks where a refusal fallback switched models; must be sent back unchanged.
                    try
                    {
                        var param = JsonSerializer.Deserialize<BetaFallbackBlockParam>(JsonSerializer.Serialize(fb));
                        if (param != null) fallbacks[start.Index] = param;
                        order.Add(start.Index);
                    }
                    catch (Exception) { /* unknown shape - skip the marker */ }
                }
                else if (start.ContentBlock.Value is BetaThinkingBlock th)
                {
                    thinking[start.Index] = (new System.Text.StringBuilder(th.Thinking), new System.Text.StringBuilder(th.Signature));
                    order.Add(start.Index);
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
            if (text.TryGetValue(idx, out var sb))
            {
                if (sb.Length > 0) result.Add(new BetaTextBlockParam { Text = sb.ToString() });
            }
            else if (thinking.TryGetValue(idx, out var th))
            {
                result.Add(new BetaThinkingBlockParam { Thinking = th.thought.ToString(), Signature = th.sig.ToString() });
            }
            else if (fallbacks.TryGetValue(idx, out var fb))
            {
                result.Add(fb);
            }
        }
        return result;
    }
}
