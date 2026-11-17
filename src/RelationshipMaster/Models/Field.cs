using System.Text.Json.Serialization;

namespace RelationshipMaster.Models;

/// <summary>A place / social arena (work, family, club...) containing people.</summary>
public sealed class Field
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New field";
    public string Description { get; set; } = "";
    public List<Person> People { get; set; } = new();

    [JsonIgnore]
    public Person Me
    {
        get
        {
            var me = People.FirstOrDefault(p => p.IsMe);
            if (me == null)
            {
                me = new Person { IsMe = true, FirstName = "Me", FieldInfluence = 35, OurInfluence = 100 };
                People.Insert(0, me);
            }
            return me;
        }
    }

    [JsonIgnore]
    public IEnumerable<Person> Others => People.Where(p => !p.IsMe);

    public static Field CreateNew(string name)
    {
        var f = new Field { Name = name };
        _ = f.Me; // ensure "Me" exists at the origin
        return f;
    }
}

public sealed class AppData
{
    public List<Field> Fields { get; set; } = new();
    public Guid? LastFieldId { get; set; }
    public string MyName { get; set; } = "Me";
}
