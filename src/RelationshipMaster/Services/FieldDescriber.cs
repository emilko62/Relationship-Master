using System.Text;
using RelationshipMaster.Models;

namespace RelationshipMaster.Services;

/// <summary>Turns a field into a compact plain-text snapshot the AI can reason about.</summary>
public static class FieldDescriber
{
    public static string Describe(Field field, Person? selected)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"FIELD: {field.Name}");
        if (!string.IsNullOrWhiteSpace(field.Description))
            sb.AppendLine($"Description: {field.Description}");
        var me = field.Me;
        sb.AppendLine($"The user (\"Me\") has field influence {me.FieldInfluence}/100.");
        if (!string.IsNullOrWhiteSpace(me.Notes)) sb.AppendLine($"Notes about the user: {me.Notes}");
        sb.AppendLine();
        sb.AppendLine("Scales: field influence 1..100 (weight in this field); our influence 0..100 (how much the user can sway them);");
        sb.AppendLine("opinion -100 (enemy) .. 0 (neutral) .. +100 (alliance).");
        sb.AppendLine();
        sb.AppendLine("PEOPLE:");
        foreach (var p in field.Others.OrderByDescending(p => p.FieldInfluence))
        {
            sb.Append($"- {p.FullName}");
            if (!string.IsNullOrWhiteSpace(p.Role)) sb.Append($" ({p.Role})");
            sb.Append($": field influence {p.FieldInfluence}, our influence over them {p.OurInfluence}, ");
            sb.Append($"our opinion of them {p.OurOpinion:+#;-#;0} [{OpinionPalette.LabelFor(p.OurOpinion)}], ");
            sb.Append($"how they supposedly see us {p.TheirOpinion:+#;-#;0} [{OpinionPalette.LabelFor(p.TheirOpinion)}]");
            if (selected != null && p.Id == selected.Id) sb.Append("  <-- currently selected");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(p.Notes)) sb.AppendLine($"    notes: {p.Notes.Replace("\r", " ").Replace("\n", " ")}");
        }
        if (!field.Others.Any()) sb.AppendLine("(nobody yet)");
        return sb.ToString();
    }
}
