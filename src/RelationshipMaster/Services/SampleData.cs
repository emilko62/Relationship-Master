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
        FirstName = first, LastName = last, Role = role, X = x, Y = y,
        FieldInfluence = fieldInfluence, OurInfluence = ourInfluence,
        OurOpinion = ourOpinion, TheirOpinion = theirOpinion,
    };
}
