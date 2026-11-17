namespace RelationshipMaster.Models;

public enum FactCategory { Likes, Dislikes, Skill, Strength, Weakness, Quirk, Goal, Other }

/// <summary>One piece of key information about a person, e.g. "Likes dogs - especially Great Danes".</summary>
public sealed class KeyFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FactCategory Category { get; set; } = FactCategory.Other;
    public string Text { get; set; } = "";

    public static string Label(FactCategory c) => c switch
    {
        FactCategory.Likes => "Likes",
        FactCategory.Dislikes => "Dislikes",
        FactCategory.Skill => "Skill",
        FactCategory.Strength => "Strength",
        FactCategory.Weakness => "Weakness",
        FactCategory.Quirk => "Quirk",
        FactCategory.Goal => "Goal / ambition",
        _ => "Other",
    };

    public static Color ColorOf(FactCategory c) => c switch
    {
        FactCategory.Likes => Color.FromArgb(90, 200, 130),
        FactCategory.Dislikes => Color.FromArgb(230, 110, 100),
        FactCategory.Skill => Color.FromArgb(90, 160, 240),
        FactCategory.Strength => Color.FromArgb(70, 190, 200),
        FactCategory.Weakness => Color.FromArgb(240, 150, 70),
        FactCategory.Quirk => Color.FromArgb(200, 130, 230),
        FactCategory.Goal => Color.FromArgb(245, 200, 80),
        _ => Color.FromArgb(150, 155, 170),
    };
}

public enum EventKind
{
    /// <summary>Something that already happened, logged by hand.</summary>
    Recorded,
    /// <summary>An interaction planned (usually with AI advice) that awaits its result.</summary>
    Planned,
}

public enum EventStatus { Done, Pending, Cancelled }

public enum InteractionGoal { WinOver, Challenge, Repair, Other }

/// <summary>An entry in a person's relationship history.</summary>
public sealed class RelationshipEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Now;
    public EventKind Kind { get; set; } = EventKind.Recorded;
    public EventStatus Status { get; set; } = EventStatus.Done;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Change applied to MY opinion of them.</summary>
    public int OurOpinionDelta { get; set; }
    /// <summary>Change applied to how they (supposedly) see ME.</summary>
    public int TheirOpinionDelta { get; set; }

    // ── planned interactions ──
    public InteractionGoal Goal { get; set; } = InteractionGoal.WinOver;
    public string Situation { get; set; } = "";
    /// <summary>0..30 - how much the interaction should move the relationship (0 = doesn't matter, high = grandiose gesture).</summary>
    public int Intensity { get; set; }
    public string AiPlan { get; set; } = "";
    public string ResultStory { get; set; } = "";
    public int? SuccessPercent { get; set; }
    public string AiAssessment { get; set; } = "";
    public DateTime? CompletedAt { get; set; }

    public static string GoalLabel(InteractionGoal g) => g switch
    {
        InteractionGoal.WinOver => "Win them over / bring closer",
        InteractionGoal.Challenge => "Challenge / compete with them",
        InteractionGoal.Repair => "Repair the relationship",
        _ => "Other goal",
    };

    public static string IntensityLabel(int v) => v switch
    {
        0 => "doesn't matter - no effect on the relationship",
        <= 5 => "small gesture",
        <= 12 => "noticeable effort",
        <= 20 => "big gesture",
        _ => "grandiose gesture",
    };
}
