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
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Height = 26;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    /// <summary>Sets the value without raising <see cref="ValueChanged"/>.</summary>
    public void SetSilently(int v)
    {
        _value = Math.Clamp(v, Minimum, Maximum);
        Invalidate();
    }

    private RectangleF Track => new(9, Height / 2f - 3, Width - 18, 6);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = Track;
        if (track.Width <= 0) return;
        float t = (_value - Minimum) / (float)Math.Max(1, Maximum - Minimum);
        float knobX = track.Left + t * track.Width;

        using (var path = Rounded(track, 3))
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
                using var fp = Rounded(filled, 3);
                g.FillPath(fill, fp);
            }
        }

        var knobColor = OpinionScale ? OpinionPalette.ColorFor(_value) : Theme.Accent;
        using var knob = new SolidBrush(knobColor);
        using var ring = new Pen(Color.White, 2f);
        g.FillEllipse(knob, knobX - 8, Height / 2f - 8, 16, 16);
        g.DrawEllipse(ring, knobX - 8, Height / 2f - 8, 16, 16);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
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

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += Math.Sign(e.Delta) * Math.Max(1, (Maximum - Minimum) / 40);
    }

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
