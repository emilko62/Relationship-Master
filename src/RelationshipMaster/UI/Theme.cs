namespace RelationshipMaster.UI;

/// <summary>
/// Dark, modern colour scheme shared by every control, plus DPI helpers.
/// All layout sizes in the code are written for 100 % (96 DPI) and pass through <see cref="Px(int)"/>,
/// so the UI keeps its proportions at 125 %, 150 %, 200 % … (fonts are in points and scale by themselves).
/// </summary>
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
    public static readonly Color Selection = Color.FromArgb(52, 48, 84);
    public static readonly Color Me = Color.FromArgb(245, 190, 70);
    public static readonly Color Pending = Color.FromArgb(255, 170, 60);
    public static readonly Color Good = Color.FromArgb(90, 200, 130);
    public static readonly Color Bad = Color.FromArgb(240, 110, 100);
    public static readonly Color GridDot = Color.FromArgb(38, 43, 54);

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font BodyBold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font SmallBold = new("Segoe UI Semibold", 8.5f);
    public static readonly Font Header = new("Segoe UI Semibold", 12f);
    public static readonly Font Big = new("Segoe UI Semibold", 20f);
    public static readonly Font Title = new("Segoe UI Light", 20f);

    /// <summary>System DPI scale factor (1.0 at 100 %, 2.0 at 200 %). The app is SystemAware.</summary>
    public static float Scale { get; } = ReadScale();

    private static float ReadScale()
    {
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return Math.Max(1f, g.DpiX / 96f);
        }
        catch { return 1f; }
    }

    public static int Px(int v) => (int)Math.Round(v * Scale);
    public static float Px(float v) => v * Scale;
    public static Size Px(int w, int h) => new(Px(w), Px(h));
    public static Padding Pad(int all) => new(Px(all));
    public static Padding Pad(int l, int t, int r, int b) => new(Px(l), Px(t), Px(r), Px(b));

    public static Button MakeButton(string text, bool primary = false, bool small = false)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : PanelAlt,
            ForeColor = Text,
            Font = small ? SmallBold : BodyBold,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, Px(small ? 26 : 32)),
            Padding = Pad(6, 0, 6, 0),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            UseMnemonic = false,
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
        Margin = Pad(0, 6, 0, 2),
        UseMnemonic = false,
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

    public static RichTextBox MakeRichText(bool readOnly) => new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = PanelAlt,
        ForeColor = Text,
        Font = Body,
        ReadOnly = readOnly,
        DetectUrls = false,
    };

    public static NumericUpDown MakeNumber(int min, int max, int value = 0) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Clamp(value, min, max),
        BackColor = PanelAlt,
        ForeColor = Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = BodyBold,
        Width = Px(70),
        TextAlign = HorizontalAlignment.Center,
    };

    public static ComboBox MakeCombo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = PanelAlt,
        ForeColor = Text,
        Font = Body,
    };

    public static void StyleForm(Form f)
    {
        f.BackColor = Panel;
        f.ForeColor = Text;
        f.Font = Body;
        f.AutoScaleMode = AutoScaleMode.None; // we scale explicitly with Px()
    }

    public static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();
}
