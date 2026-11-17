namespace RelationshipMaster.UI;

/// <summary>Dark, modern colour scheme shared by every control.</summary>
public static class Theme
{
    public static readonly Color Background = Color.FromArgb(18, 20, 26);
    public static readonly Color Canvas = Color.FromArgb(22, 25, 32);
    public static readonly Color Panel = Color.FromArgb(28, 31, 40);
    public static readonly Color PanelAlt = Color.FromArgb(36, 40, 51);
    public static readonly Color Border = Color.FromArgb(52, 57, 70);
    public static readonly Color Text = Color.FromArgb(230, 233, 240);
    public static readonly Color TextDim = Color.FromArgb(145, 152, 168);
    public static readonly Color Accent = Color.FromArgb(124, 92, 255);
    public static readonly Color AccentSoft = Color.FromArgb(80, 124, 92, 255);
    public static readonly Color Me = Color.FromArgb(245, 190, 70);
    public static readonly Color GridDot = Color.FromArgb(38, 43, 54);

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font BodyBold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Header = new("Segoe UI Semibold", 12f);
    public static readonly Font Title = new("Segoe UI Light", 20f);

    public static Button MakeButton(string text, bool primary = false)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : PanelAlt,
            ForeColor = Text,
            Font = BodyBold,
            Height = 32,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 0, 8, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = primary ? Accent : Border;
        b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(146, 118, 255) : Color.FromArgb(48, 53, 67);
        b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(104, 74, 230) : Color.FromArgb(58, 64, 80);
        return b;
    }

    public static Label MakeLabel(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Font = font ?? Body,
        ForeColor = color ?? TextDim,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 6, 0, 2),
    };

    public static TextBox MakeTextBox(bool multiline = false) => new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = PanelAlt,
        ForeColor = Text,
        Font = Body,
        Multiline = multiline,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
    };

    public static void StyleForm(Form f)
    {
        f.BackColor = Panel;
        f.ForeColor = Text;
        f.Font = Body;
    }
}
