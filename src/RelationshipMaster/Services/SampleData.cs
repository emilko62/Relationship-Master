using RelationshipMaster.Models;

namespace RelationshipMaster.Services;

/// <summary>A small example so the first launch isn't an empty screen.</summary>
public static class SampleData
{
    public static AppData Create()
    {
        var work = Field.CreateNew("Work");
        work.Description = "The office - product team and management.";
        work.Me.FieldInfluence = 30;
        work.People.AddRange(new[]
        {
            P("Laura", "Bennett", "Head of department", -10, -230, 90, 35, 40, 25),
            P("Mark", "Owens", "Team lead", 210, -60, 60, 55, 70, 60),
            P("Sofia", "Petrova", "Colleague", -220, 40, 30, 80, 92, 88),
            P("Daniel", "Kraus", "Colleague, competing for promotion", 150, 190, 45, 15, -55, -30),
            P("Nina", "Haas", "Intern", -90, 220, 12, 70, 20, 50),
        });

        var sofia = work.People.First(p => p.FirstName == "Sofia");
        sofia.Facts.Add(new KeyFact { Category = FactCategory.Likes, Text = "Dogs - especially Great Danes (has one called Bruno)" });
        sofia.Facts.Add(new KeyFact { Category = FactCategory.Skill, Text = "Excellent at data analysis" });
        sofia.History.Add(new RelationshipEvent
        {
            Date = DateTime.Now.AddDays(-12), Title = "Helped me finish the quarterly report",
            Description = "Stayed late to help me fix the numbers before the deadline.", OurOpinionDelta = 3, TheirOpinionDelta = 1,
        });

        var daniel = work.People.First(p => p.FirstName == "Daniel");
        daniel.Facts.Add(new KeyFact { Category = FactCategory.Skill, Text = "Very good presenter, charismatic in meetings" });
        daniel.Facts.Add(new KeyFact { Category = FactCategory.Weakness, Text = "Often misses deadlines and details" });
        daniel.Facts.Add(new KeyFact { Category = FactCategory.Quirk, Text = "Takes credit for team work" });
        daniel.Facts.Add(new KeyFact { Category = FactCategory.Goal, Text = "Wants the same team-lead promotion as me" });
        daniel.History.Add(new RelationshipEvent
        {
            Date = DateTime.Now.AddDays(-5), Title = "Presented my idea as his own",
            Description = "In the Monday meeting he pitched the dashboard idea I had shared with him.", OurOpinionDelta = -8, TheirOpinionDelta = -1,
        });

        var family = Field.CreateNew("Family");
        family.Description = "Extended family.";
        family.People.AddRange(new[]
        {
            P("Anna", "Dimitrova", "Sister", -170, -120, 50, 60, 85, 75),
            P("Peter", "Dimitrov", "Uncle", 190, -40, 70, 20, -25, 10),
            P("Maria", "Ivanova", "Cousin", 40, 200, 25, 40, 5, 0),
        });

        return new AppData { Fields = { work, family }, LastFieldId = work.Id };
    }

    private static Person P(string first, string last, string role, float x, float y,
        int fieldInfluence, int ourInfluence, int ourOpinion, int theirOpinion) => new()
    {
        Authority = role switch
        {
            var r when r.Contains("Head") => 90,
            var r when r.Contains("lead") => 65,
            var r when r.Contains("Intern") => 5,
            var r when r.Contains("Uncle") => 45,
            _ => 25,
        },
        FirstName = first, LastName = last, Role = role, X = x, Y = y,
        FieldInfluence = fieldInfluence, OurInfluence = ourInfluence,
        OurOpinion = ourOpinion, TheirOpinion = theirOpinion,
    };
}
