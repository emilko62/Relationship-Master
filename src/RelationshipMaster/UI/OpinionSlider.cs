using System.Drawing.Drawing2D;
using RelationshipMaster.Models;

namespace RelationshipMaster.UI;

/// <summary>
/// A flat custom-drawn slider. In "opinion" mode its track shows the red → grey → green → blue scale.
/// </summary>
public sealed class OpinionSlider : Control
{
    private int _value;
    private bool _dragging;

    public int Minimum { get; set; } = 0;
    public int Maximum { get; set; } = 100;
    public bool OpinionScale { get; set; }

    public int Value
    {
        get => _value;
        set
        {
            var v = Math.Clamp(value, Minimum, Maximum);
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    public OpinionSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Height = Theme.Px(26);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    /// <summary>Sets the value without raising <see cref="ValueChanged"/>.</summary>
    public void SetSilently(int v)
    {
        _value = Math.Clamp(v, Minimum, Maximum);
        Invalidate();
    }

    private RectangleF Track => new(Theme.Px(9f), Height / 2f - Theme.Px(3f), Width - Theme.Px(18f), Theme.Px(6f));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = Track;
        if (track.Width <= 0) return;
        float t = (_value - Minimum) / (float)Math.Max(1, Maximum - Minimum);
        float knobX = track.Left + t * track.Width;

        using (var path = Rounded(track, Theme.Px(3f)))
        {
            if (OpinionScale)
            {
                using var brush = new LinearGradientBrush(track, Color.Black, Color.Black, 0f);
                var stops = OpinionPalette.LegendStops;
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = stops.Select(s => s.color).ToArray(),
                    Positions = stops.Select(s => (s.at + 100) / 200f).ToArray(),
                };
                g.FillPath(brush, path);
            }
            else
            {
                using var bg = new SolidBrush(Theme.Border);
                g.FillPath(bg, path);
                using var fill = new SolidBrush(Theme.Accent);
                var filled = new RectangleF(track.Left, track.Top, Math.Max(0.1f, knobX - track.Left), track.Height);
                using var fp = Rounded(filled, Theme.Px(3f));
                g.FillPath(fill, fp);
            }
        }

        var knobColor = OpinionScale ? OpinionPalette.ColorFor(_value) : Theme.Accent;
        using var knob = new SolidBrush(knobColor);
        float k = Theme.Px(8f);
        using var ring = new Pen(Focused ? Theme.Accent : Color.White, Theme.Px(Focused ? 3f : 2f));
        g.FillEllipse(knob, knobX - k, Height / 2f - k, k * 2, k * 2);
        g.DrawEllipse(ring, knobX - k, Height / 2f - k, k * 2, k * 2);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        _dragging = true;
        Capture = true;
        SetFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) SetFromX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    // No mouse-wheel handling on purpose: scrolling the inspector must never change an opinion by accident.
    // Keyboard (arrows / PageUp / PageDown) adjusts the value when the slider has focus.

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int step = e.KeyCode is Keys.PageUp or Keys.PageDown ? Math.Max(1, (Maximum - Minimum) / 10) : 1;
        if (e.KeyCode is Keys.Right or Keys.Up or Keys.PageUp) { Value += step; e.Handled = true; }
        if (e.KeyCode is Keys.Left or Keys.Down or Keys.PageDown) { Value -= step; e.Handled = true; }
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    private void SetFromX(int x)
    {
        var track = Track;
        float t = Math.Clamp((x - track.Left) / track.Width, 0f, 1f);
        Value = (int)Math.Round(Minimum + t * (Maximum - Minimum));
    }

    internal static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
