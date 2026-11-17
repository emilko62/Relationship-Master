using System.Text;
using RelationshipMaster.Models;

namespace RelationshipMaster.Services;

/// <summary>Turns a field (and its people's key info and history) into a compact text snapshot for the AI.</summary>
public static class FieldDescriber
{
    public static string Describe(Field field, Person? selected)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"FIELD: {field.Name}");
        if (!string.IsNullOrWhiteSpace(field.Description))
            sb.AppendLine($"Description: {field.Description}");
        var me = field.Me;
        sb.AppendLine($"The user (\"Me\") has field influence {me.FieldInfluence}/100 and authority {me.Authority}/100.");
        if (!string.IsNullOrWhiteSpace(me.Notes)) sb.AppendLine($"Notes about the user: {OneLine(me.Notes)}");
        AppendFacts(sb, me, "  ");
        sb.AppendLine();
        sb.AppendLine("Scales: field influence 1..100 (informal weight in this field); our influence 0..100 (how much the user can sway them);");
        sb.AppendLine("authority 0..100 (formal power, e.g. a boss; 0 = none, 100 = maximum);");
        sb.AppendLine("opinion -100 (enemy) .. 0 (neutral) .. +100 (alliance).");
        sb.AppendLine();
        sb.AppendLine("PEOPLE:");
        foreach (var p in field.Others.OrderByDescending(p => p.FieldInfluence))
        {
            sb.Append("- ").Append(Summary(p));
            if (selected != null && p.Id == selected.Id) sb.Append("  <-- currently selected");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(p.Notes)) sb.AppendLine($"    notes: {OneLine(p.Notes)}");
            AppendFacts(sb, p, "    ");
            foreach (var e in p.History.Where(e => e.Status == EventStatus.Done).OrderByDescending(e => e.Date).Take(3))
                sb.AppendLine($"    recent: {EventLine(e)}");
        }
        if (!field.Others.Any()) sb.AppendLine("(nobody yet)");
        return sb.ToString();
    }

    /// <summary>Full dossier of one person: key info and the whole history.</summary>
    public static string Dossier(Person p)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"TARGET PERSON: {Summary(p)}");
        if (!string.IsNullOrWhiteSpace(p.Notes)) sb.AppendLine($"Notes: {OneLine(p.Notes)}");
        sb.AppendLine("Key info:");
        if (p.Facts.Count == 0) sb.AppendLine("  (none recorded)");
        AppendFacts(sb, p, "  ");
        sb.AppendLine("History (oldest first):");
        var done = p.History.Where(e => e.Status == EventStatus.Done).OrderBy(e => e.Date).TakeLast(20).ToList();
        if (done.Count == 0) sb.AppendLine("  (nothing recorded yet)");
        foreach (var e in done) sb.AppendLine($"  {EventLine(e)}");
        return sb.ToString();
    }

    private static string Summary(Person p)
    {
        var sb = new StringBuilder(p.FullName);
        if (!string.IsNullOrWhiteSpace(p.Role)) sb.Append($" ({p.Role})");
        sb.Append($": field influence {p.FieldInfluence}, our influence over them {p.OurInfluence}, ");
        sb.Append($"our opinion of them {Signed(p.OurOpinion)} [{OpinionPalette.LabelFor(p.OurOpinion)}], ");
        sb.Append($"how they supposedly see us {Signed(p.TheirOpinion)} [{OpinionPalette.LabelFor(p.TheirOpinion)}]");
        sb.Append($", authority {p.Authority} [{AuthorityPalette.LabelFor(p.Authority)}]");
        return sb.ToString();
    }

    private static void AppendFacts(StringBuilder sb, Person p, string indent)
    {
        foreach (var group in p.Facts.Where(f => !string.IsNullOrWhiteSpace(f.Text)).GroupBy(f => f.Category))
            sb.AppendLine($"{indent}{KeyFact.Label(group.Key).ToLowerInvariant()}: {string.Join("; ", group.Select(f => OneLine(f.Text)))}");
    }

    private static string EventLine(RelationshipEvent e)
    {
        var s = $"{e.Date:yyyy-MM-dd} {e.Title}";
        if (!string.IsNullOrWhiteSpace(e.Description)) s += $" - {OneLine(e.Description)}";
        if (e.SuccessPercent is int pct) s += $" (planned interaction, {pct}% success)";
        return s + $" [my opinion {Signed(e.OurOpinionDelta)}, their opinion of me {Signed(e.TheirOpinionDelta)}]";
    }

    private static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();
    private static string OneLine(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();
}
