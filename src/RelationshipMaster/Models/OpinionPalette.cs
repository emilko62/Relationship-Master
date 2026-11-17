namespace RelationshipMaster.Models;

/// <summary>
/// Maps an opinion score (-100..100) to a colour:
/// deep red (hostile) → red → grey (neutral) → green (friend) → blue (alliance).
/// </summary>
public static class OpinionPalette
{
    private static readonly (int at, Color color)[] Stops =
    {
        (-100, Color.FromArgb(150, 20, 35)),
        (-55,  Color.FromArgb(214, 64, 64)),
        (-20,  Color.FromArgb(196, 120, 110)),
        (0,    Color.FromArgb(128, 132, 142)),
        (25,   Color.FromArgb(110, 170, 120)),
        (60,   Color.FromArgb(46, 184, 92)),
        (80,   Color.FromArgb(32, 160, 150)),
        (100,  Color.FromArgb(52, 120, 235)),
    };

    public static Color ColorFor(int opinion)
    {
        opinion = Math.Clamp(opinion, -100, 100);
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            var (a, ca) = Stops[i];
            var (b, cb) = Stops[i + 1];
            if (opinion >= a && opinion <= b)
            {
                float t = (opinion - a) / (float)(b - a);
                return Lerp(ca, cb, t);
            }
        }
        return Stops[^1].color;
    }

    public static string LabelFor(int opinion) => opinion switch
    {
        <= -75 => "Enemy",
        <= -40 => "Negative",
        <= -12 => "Wary",
        < 12 => "Neutral",
        < 40 => "Friendly",
        < 70 => "Friend",
        < 88 => "Close friend",
        _ => "Alliance",
    };

    public static Color Lerp(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.A + (b.A - a.A) * t),
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    public static Color Lighten(Color c, float amount) => Lerp(c, Color.White, amount);
    public static Color Darken(Color c, float amount) => Lerp(c, Color.Black, amount);

    /// <summary>Stops for drawing a legend gradient.</summary>
    public static IReadOnlyList<(int at, Color color)> LegendStops => Stops;
}
