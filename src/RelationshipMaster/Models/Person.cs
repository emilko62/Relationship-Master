using System.Text.Json.Serialization;

namespace RelationshipMaster.Models;

/// <summary>A person placed inside a field. One person per field is "Me" (the user).</summary>
public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool IsMe { get; set; }

    /// <summary>World-space position of the circle centre.</summary>
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>1..100 - how much weight this person carries in the field. Drawn as circle size.</summary>
    public int FieldInfluence { get; set; } = 30;

    /// <summary>0..100 - how much influence WE have over this person. Drawn as the link from "Me".</summary>
    public int OurInfluence { get; set; } = 20;

    /// <summary>-100..100 - our opinion of them (red = negative, grey = neutral, green = friend, blue = ally).</summary>
    public int OurOpinion { get; set; }

    /// <summary>-100..100 - how they (supposedly) perceive us.</summary>
    public int TheirOpinion { get; set; }

    /// <summary>0..100 - formal authority / power over others (0 = none, 100 = maximum, e.g. the boss).</summary>
    public int Authority { get; set; } = 20;

    public string Role { get; set; } = "";
    public string Notes { get; set; } = "";

    /// <summary>Key information: likes, skills, quirks, weaknesses…</summary>
    public List<KeyFact> Facts { get; set; } = new();

    /// <summary>Everything that happened between us, newest last.</summary>
    public List<RelationshipEvent> History { get; set; } = new();

    [JsonIgnore]
    public int PendingCount => History.Count(e => e.Status == EventStatus.Pending);

    /// <summary>Applies opinion changes (clamped to -100..100) and returns the effective change.</summary>
    public (int our, int their) ApplyDeltas(int ourDelta, int theirDelta)
    {
        int oldOur = OurOpinion, oldTheir = TheirOpinion;
        OurOpinion = Math.Clamp(OurOpinion + ourDelta, -100, 100);
        TheirOpinion = Math.Clamp(TheirOpinion + theirDelta, -100, 100);
        return (OurOpinion - oldOur, TheirOpinion - oldTheir);
    }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (IsMe) return string.IsNullOrWhiteSpace(FirstName) ? "Me" : FirstName;
            var last = LastName.Trim();
            return last.Length > 0 ? $"{FirstName.Trim()} {char.ToUpper(last[0])}." : FirstName.Trim();
        }
    }

    [JsonIgnore]
    public string FullName => $"{FirstName} {LastName}".Trim();

    public int ValueFor(PerceptionView view) => view switch
    {
        PerceptionView.Ours => OurOpinion,
        PerceptionView.Theirs => TheirOpinion,
        _ => Authority,
    };
}

/// <summary>What the circle colours on the map show.</summary>
public enum PerceptionView
{
    /// <summary>How WE perceive the others.</summary>
    Ours,
    /// <summary>How the others (supposedly) perceive US.</summary>
    Theirs,
    /// <summary>How much formal authority each person has (no authority → maximum).</summary>
    Authority,
}

public static class ViewInfo
{
    public static readonly PerceptionView[] All = Enum.GetValues<PerceptionView>();

    public static string ShortName(PerceptionView v) => v switch
    {
        PerceptionView.Ours => "I see them",
        PerceptionView.Theirs => "They see me",
        _ => "Authority",
    };

    public static string LongName(PerceptionView v) => v switch
    {
        PerceptionView.Ours => "How I see them",
        PerceptionView.Theirs => "How they (supposedly) see me",
        _ => "Authority - from none to maximum",
    };

    public static Color ColorFor(PerceptionView v, int value) =>
        v == PerceptionView.Authority ? AuthorityPalette.ColorFor(value) : OpinionPalette.ColorFor(value);

    public static string LabelFor(PerceptionView v, int value) =>
        v == PerceptionView.Authority ? AuthorityPalette.LabelFor(value) : OpinionPalette.LabelFor(value);

    public static PerceptionView Next(PerceptionView v) => All[(Array.IndexOf(All, v) + 1) % All.Length];
}
