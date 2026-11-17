using System.Text;
using System.Text.Json;
using RelationshipMaster.Models;

namespace RelationshipMaster.Services;

/// <summary>
/// Builds self-contained prompts (instructions + context + task) that the user can paste into any Claude chat,
/// e.g. on claude.ai with their own subscription, instead of calling the API from the app.
/// Answers that the app must read back (outcome scores, quick add) are requested as a JSON block.
/// </summary>
public static class PromptBuilder
{
    private static string Wrap(string instructions, string context, string task, string? answerFormat = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("I'm using an app called \"Relationship Master\" to map my social relationships. Below are your instructions, my data from the app, and my request.");
        sb.AppendLine();
        sb.AppendLine("<instructions>");
        sb.AppendLine(instructions.Trim());
        sb.AppendLine("</instructions>");
        sb.AppendLine();
        sb.AppendLine(context.Trim());
        sb.AppendLine();
        sb.AppendLine("<request>");
        sb.AppendLine(task.Trim());
        sb.AppendLine("</request>");
        if (answerFormat != null)
        {
            sb.AppendLine();
            sb.AppendLine(answerFormat.Trim());
        }
        return sb.ToString();
    }

    /// <summary>Whole-field briefing plus a question (the advisor chat, or a free-form request).</summary>
    public static string FieldQuestion(Field field, Person? selected, string question) => Wrap(
        RelationshipAdvisor.ChatSystem,
        $"<field_snapshot>\n{FieldDescriber.Describe(field, selected)}</field_snapshot>",
        string.IsNullOrWhiteSpace(question) ? "(write your question here)" : question);

    /// <summary>Everything known about one person, to use as context for a conversation about them.</summary>
    public static string PersonBriefing(Field field, Person person) => Wrap(
        "You are my relationship strategist. " + RelationshipAdvisor.Ethics +
        " Use the field snapshot and the dossier below as context. Refer to people by name and build on the " +
        "recorded key info (likes, skills, quirks, weaknesses) and history.",
        $"<field_snapshot>\n{FieldDescriber.Describe(field, person)}</field_snapshot>\n\n" +
        $"<target_dossier>\n{FieldDescriber.Dossier(person)}</target_dossier>",
        $"First, summarise in 3-5 bullet points where I stand with {person.FullName} and what matters most. " +
        "Then wait for my questions.\n(Replace this with your own question if you like.)");

    public static string Plan(Field field, Person target, InteractionGoal goal, string situation, int intensity) => Wrap(
        RelationshipAdvisor.PlanSystem,
        RelationshipAdvisor.PlanUserPrompt(field, target, goal, situation, intensity),
        $"Write the plan for my interaction with {target.FullName}.");

    public static string Outcome(Field field, Person target, RelationshipEvent ev, string story) => Wrap(
        RelationshipAdvisor.OutcomeSystem,
        RelationshipAdvisor.OutcomeUserPrompt(field, target, ev, story),
        "Evaluate how this interaction went.",
        "Answer ONLY with one JSON code block in exactly this shape (integers, no extra keys), so the app can read it:\n" +
        "```json\n{\"successPercent\": 0, \"ourOpinionDelta\": 0, \"theirOpinionDelta\": 0, \"assessment\": \"...\"}\n```");

    public static string QuickAdd(Field field, string description) => Wrap(
        RelationshipAdvisor.QuickAddSystem(field) +
        "\nScales: fieldInfluence 1..100, ourInfluence 0..100, ourOpinion -100..100, theirOpinion -100..100, authority 0..100. " +
        "Fact categories: " + string.Join(", ", Enum.GetNames<FactCategory>()) + ".",
        "",
        $"Description of the person:\n{description}",
        "Answer ONLY with one JSON code block in exactly this shape, so the app can read it:\n" +
        "```json\n{\"firstName\": \"\", \"lastName\": \"\", \"role\": \"\", \"fieldInfluence\": 30, \"ourInfluence\": 20, " +
        "\"ourOpinion\": 0, \"theirOpinion\": 0, \"authority\": 20, \"notes\": \"\", " +
        "\"facts\": [{\"category\": \"Likes\", \"text\": \"\"}]}\n```");

    /// <summary>Finds and parses the JSON object in a pasted answer (tolerates ```json fences and surrounding text).</summary>
    public static JsonDocument ExtractJson(string answer)
    {
        int start = answer.IndexOf('{');
        int end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new FormatException("No JSON block found in the pasted answer. Paste Claude's whole reply.");
        try
        {
            return JsonDocument.Parse(answer[start..(end + 1)]);
        }
        catch (JsonException ex)
        {
            throw new FormatException("The pasted JSON could not be read: " + ex.Message);
        }
    }
}
