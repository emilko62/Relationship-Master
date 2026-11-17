using System.Drawing.Drawing2D;
using System.Drawing.Text;
using RelationshipMaster.Models;

namespace RelationshipMaster.UI;

/// <summary>
/// Custom-drawn, zoomable and pannable view of one field. People are circles: size = influence in the
/// field, colour = opinion (ours of them, or theirs of us, depending on <see cref="View"/>).
/// Links from "Me" show how much influence we have over each person.
/// </summary>
public sealed class FieldCanvas : Control
{
    private Field? _field;
    private PerceptionView _view = PerceptionView.Ours;
    private float _viewBlend;           // 0 = ours, 1 = theirs (animated)
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 15 };

    private float _zoom = 1f;
    private PointF _pan;                // screen offset of world origin from the control centre
    private Person? _selected, _hover;

    private enum DragMode { None, Person, Pan }
    private DragMode _drag;
    private Point _dragStart;
    private PointF _dragOrigin;         // person position or pan at drag start
    private bool _dragMoved;

    private RectangleF _viewChipRect;

    public event EventHandler? SelectionChanged;
    public event EventHandler? FieldModified;
    public event EventHandler? ViewChanged;
    public event EventHandler<Person>? EditRequested;
    public event EventHandler<PointF>? AddRequested;
    public event EventHandler<CanvasContextEventArgs>? ContextRequested;

    public FieldCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Theme.Canvas;
        _anim.Tick += (_, _) =>
        {
            float target = _view == PerceptionView.Theirs ? 1f : 0f;
            _viewBlend += Math.Sign(target - _viewBlend) * 0.08f;
            if (Math.Abs(target - _viewBlend) < 0.08f) { _viewBlend = target; _anim.Stop(); }
            Invalidate();
        };
    }

    public Field? Field
    {
        get => _field;
        set
        {
            _field = value;
            _selected = _hover = null;
            FitToView();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public PerceptionView View
    {
        get => _view;
        set
        {
            if (_view == value) return;
            _view = value;
            _anim.Start();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ToggleView() => View = _view == PerceptionView.Ours ? PerceptionView.Theirs : PerceptionView.Ours;

    public Person? Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ───────────────────────────── geometry ─────────────────────────────

    public static float RadiusOf(Person p) => 18f + Math.Clamp(p.FieldInfluence, 1, 100) * 0.6f;

    private PointF ToScreen(float x, float y) =>
        new(Width / 2f + _pan.X + x * _zoom, Height / 2f + _pan.Y + y * _zoom);

    public PointF ToWorld(Point s) =>
        new((s.X - Width / 2f - _pan.X) / _zoom, (s.Y - Height / 2f - _pan.Y) / _zoom);

    private IEnumerable<Person> DrawOrder()
    {
        if (_field == null) return Enumerable.Empty<Person>();
        // Big circles first so smaller ones stay visible; selection always on top.
        return _field.People
            .OrderBy(p => p == _selected)
            .ThenByDescending(RadiusOf);
    }

    private Person? HitTest(Point screen)
    {
        var w = ToWorld(screen);
        foreach (var p in DrawOrder().Reverse())
        {
            float dx = w.X - p.X, dy = w.Y - p.Y, r = RadiusOf(p);
            if (dx * dx + dy * dy <= r * r) return p;
        }
        return null;
    }

    public void FitToView()
    {
        if (_field == null || _field.People.Count == 0 || Width < 50 || Height < 50)
        {
            _zoom = 1f; _pan = PointF.Empty; Invalidate(); return;
        }
        float minX = _field.People.Min(p => p.X - RadiusOf(p)), maxX = _field.People.Max(p => p.X + RadiusOf(p));
        float minY = _field.People.Min(p => p.Y - RadiusOf(p)), maxY = _field.People.Max(p => p.Y + RadiusOf(p));
        float w = Math.Max(200, maxX - minX), h = Math.Max(200, maxY - minY);
        _zoom = Math.Clamp(Math.Min((Width - 140) / w, (Height - 170) / h), 0.25f, 2.0f);
        _pan = new PointF(-(minX + maxX) / 2f * _zoom, -(minY + maxY) / 2f * _zoom + 10);
        Invalidate();
    }

    /// <summary>Finds a free spot near the given world position for a new person.</summary>
    public PointF FindFreeSpot(PointF near, float radius)
    {
        if (_field == null) return near;
        for (int ring = 0; ring < 30; ring++)
        {
            int steps = Math.Max(1, ring * 6);
            for (int i = 0; i < steps; i++)
            {
                double a = i * Math.PI * 2 / steps + ring;
                var c = new PointF(near.X + (float)Math.Cos(a) * ring * 45, near.Y + (float)Math.Sin(a) * ring * 45);
                bool free = _field.People.All(p =>
                {
                    float dx = p.X - c.X, dy = p.Y - c.Y, min = RadiusOf(p) + radius + 14;
                    return dx * dx + dy * dy >= min * min;
                });
                if (free) return c;
            }
        }
        return near;
    }

    // ───────────────────────────── painting ─────────────────────────────

    private Color ColorOf(Person p)
    {
        if (p.IsMe) return Theme.Me;
        var ours = OpinionPalette.ColorFor(p.OurOpinion);
        var theirs = OpinionPalette.ColorFor(p.TheirOpinion);
        return OpinionPalette.Lerp(ours, theirs, _viewBlend);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        DrawBackground(g);
        if (_field == null)
        {
            DrawCentered(g, "Create a field on the left to get started.", Theme.Header, Theme.TextDim);
            return;
        }

        var me = _field.Me;
        foreach (var p in _field.Others) DrawLink(g, me, p);
        foreach (var p in DrawOrder()) DrawPerson(g, p);

        DrawHeader(g);
        DrawLegend(g);
        if (_hover != null && _drag == DragMode.None) DrawTooltip(g, _hover);
        if (!_field.Others.Any())
            DrawCentered(g, "Double-click anywhere to add a person", Theme.Body, Theme.TextDim, 90);
    }

    private void DrawBackground(Graphics g)
    {
        using (var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(24, 27, 36), Color.FromArgb(16, 18, 24), 90f))
            g.FillRectangle(bg, ClientRectangle);

        // vignette glow in the centre
        var c = ToScreen(0, 0);
        float glowR = Math.Max(Width, Height) * 0.6f;
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(c.X - glowR, c.Y - glowR, glowR * 2, glowR * 2);
            using var pgb = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(28, 124, 92, 255),
                SurroundColors = new[] { Color.FromArgb(0, 124, 92, 255) },
            };
            g.FillPath(pgb, path);
        }

        // dot grid
        float step = 40 * _zoom;
        while (step < 18) step *= 2;
        var origin = ToScreen(0, 0);
        float startX = origin.X % step, startY = origin.Y % step;
        using var dot = new SolidBrush(Theme.GridDot);
        for (float x = startX; x < Width; x += step)
            for (float y = startY; y < Height; y += step)
                g.FillEllipse(dot, x - 1.2f, y - 1.2f, 2.4f, 2.4f);
    }

    private void DrawLink(Graphics g, Person me, Person p)
    {
        var a = ToScreen(me.X, me.Y);
        var b = ToScreen(p.X, p.Y);
        float dx = b.X - a.X, dy = b.Y - a.Y, len = MathF.Sqrt(dx * dx + dy * dy);
        float ra = RadiusOf(me) * _zoom, rb = RadiusOf(p) * _zoom;
        if (len < ra + rb + 4) return;
        float ux = dx / len, uy = dy / len;
        var start = new PointF(a.X + ux * (ra + 3), a.Y + uy * (ra + 3));
        var end = new PointF(b.X - ux * (rb + 5), b.Y - uy * (rb + 5));

        bool focus = p == _selected || p == _hover;
        float influence = p.OurInfluence / 100f;
        int alpha = (int)(focus ? 230 : 50 + influence * 130);
        var color = Color.FromArgb(alpha, ColorOf(p));
        float width = (1f + influence * 7f) * Math.Clamp(_zoom, 0.5f, 1.4f);

        using var pen = new Pen(color, width) { StartCap = LineCap.Round };
        if (p.OurInfluence < 25) pen.DashStyle = DashStyle.Dash;
        if (p.OurInfluence > 0)
            pen.CustomEndCap = new AdjustableArrowCap(3.2f, 3.2f, true);
        g.DrawLine(pen, start, end);

        if (focus)
        {
            var mid = new PointF((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            DrawPill(g, $"our influence {p.OurInfluence}%", mid, Theme.Small, color);
        }
    }

    private void DrawPerson(Graphics g, Person p)
    {
        var c = ToScreen(p.X, p.Y);
        float r = RadiusOf(p) * _zoom;
        var color = ColorOf(p);
        var rect = new RectangleF(c.X - r, c.Y - r, r * 2, r * 2);

        // soft shadow / glow
        bool focus = p == _selected || p == _hover;
        float glow = focus ? r * 0.45f : r * 0.22f;
        using (var gp = new GraphicsPath())
        {
            gp.AddEllipse(c.X - r - glow, c.Y - r - glow + 3, (r + glow) * 2, (r + glow) * 2);
            using var pgb = new PathGradientBrush(gp)
            {
                CenterColor = Color.FromArgb(focus ? 120 : 70, color),
                SurroundColors = new[] { Color.FromArgb(0, color) },
                FocusScales = new PointF(r / (r + glow), r / (r + glow)),
            };
            g.FillPath(pgb, gp);
        }

        // body: radial gradient for a soft 3-D look
        using (var body = new GraphicsPath())
        {
            body.AddEllipse(rect);
            using var pgb = new PathGradientBrush(body)
            {
                CenterPoint = new PointF(c.X - r * 0.3f, c.Y - r * 0.35f),
                CenterColor = OpinionPalette.Lighten(color, 0.28f),
                SurroundColors = new[] { OpinionPalette.Darken(color, 0.25f) },
            };
            g.FillPath(pgb, body);
        }

        using (var rim = new Pen(OpinionPalette.Lighten(color, 0.45f), p.IsMe ? 3f : 1.5f))
            g.DrawEllipse(rim, rect);

        if (p.IsMe)
        {
            using var halo = new Pen(Color.FromArgb(140, Theme.Me), 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawEllipse(halo, RectangleF.Inflate(rect, 6, 6));
        }

        if (p == _selected)
        {
            using var sel = new Pen(Color.White, 2.2f);
            g.DrawEllipse(sel, RectangleF.Inflate(rect, 4, 4));
        }

        // name (+ role if room)
        string name = p.DisplayName;
        float fontSize = Math.Clamp(r * 0.32f, 7f, 18f);
        using var font = new Font("Segoe UI Semibold", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        var size = g.MeasureString(name, font);
        bool inside = size.Width < r * 1.85f && r > 14;
        var textColor = Brightness(color) > 0.62f ? Color.FromArgb(25, 25, 30) : Color.White;
        if (inside)
        {
            float ty = c.Y - size.Height / 2;
            string role = p.IsMe ? "you" : p.Role;
            Font? roleFont = null;
            SizeF roleSize = SizeF.Empty;
            if (!string.IsNullOrWhiteSpace(role) && r > 34)
            {
                roleFont = new Font("Segoe UI", Math.Max(7f, fontSize * 0.62f), GraphicsUnit.Pixel);
                role = Ellipsize(g, role, roleFont, r * 1.6f);
                roleSize = g.MeasureString(role, roleFont);
                ty -= roleSize.Height / 2;
            }
            using (var tb = new SolidBrush(textColor))
                g.DrawString(name, font, tb, c.X - size.Width / 2, ty);
            if (roleFont != null)
            {
                using var rb = new SolidBrush(Color.FromArgb(190, textColor));
                g.DrawString(role, roleFont, rb, c.X - roleSize.Width / 2, ty + size.Height - 2);
                roleFont.Dispose();
            }
        }
        else
        {
            DrawPill(g, name, new PointF(c.X, c.Y + r + 12), Theme.Small, Color.FromArgb(200, Theme.PanelAlt));
        }
    }

    private void DrawHeader(Graphics g)
    {
        if (_field == null) return;
        using (var tb = new SolidBrush(Theme.Text))
            g.DrawString(_field.Name, Theme.Title, tb, 18, 12);
        var sub = _field.Description;
        if (!string.IsNullOrWhiteSpace(sub))
        {
            using var sb = new SolidBrush(Theme.TextDim);
            g.DrawString(sub, Theme.Body, sb, new RectangleF(20, 50, Math.Max(100, Width - 380), 40));
        }

        // view toggle chip (clickable)
        string label = _view == PerceptionView.Ours ? "How I see them" : "How they (supposedly) see me";
        var size = g.MeasureString(label, Theme.BodyBold);
        _viewChipRect = new RectangleF(Width - size.Width - 66, 16, size.Width + 46, 34);
        using (var path = OpinionSlider.Rounded(_viewChipRect, 17))
        {
            using var fill = new SolidBrush(_view == PerceptionView.Ours ? Theme.PanelAlt : Color.FromArgb(60, 48, 110));
            g.FillPath(fill, path);
            using var border = new Pen(Theme.Accent, 1.4f);
            g.DrawPath(border, path);
        }
        // two-tone dot: left half = my view, right half = their view; the active half is bright
        var dot = new RectangleF(_viewChipRect.X + 14, _viewChipRect.Y + 11, 12, 12);
        using (var on = new SolidBrush(Theme.Accent))
        using (var off = new SolidBrush(Theme.Border))
        {
            g.FillPie(_view == PerceptionView.Ours ? on : off, dot.X, dot.Y, dot.Width, dot.Height, 90, 180);
            g.FillPie(_view == PerceptionView.Theirs ? on : off, dot.X, dot.Y, dot.Width, dot.Height, 270, 180);
        }
        using (var tb = new SolidBrush(Theme.Text))
            g.DrawString(label, Theme.BodyBold, tb, _viewChipRect.X + 32, _viewChipRect.Y + (34 - size.Height) / 2);
        using (var hint = new SolidBrush(Theme.TextDim))
        {
            const string h = "click to switch view  (V)";
            var hs = g.MeasureString(h, Theme.Small);
            g.DrawString(h, Theme.Small, hint, _viewChipRect.Right - hs.Width - 6, _viewChipRect.Bottom + 3);
        }
    }

    private void DrawLegend(Graphics g)
    {
        var area = new RectangleF(18, Height - 66, 300, 52);
        using (var path = OpinionSlider.Rounded(area, 10))
        {
            using var bg = new SolidBrush(Color.FromArgb(200, Theme.Panel));
            g.FillPath(bg, path);
        }
        var bar = new RectangleF(area.X + 14, area.Y + 26, area.Width - 28, 9);
        using (var brush = new LinearGradientBrush(bar, Color.Black, Color.Black, 0f))
        {
            var stops = OpinionPalette.LegendStops;
            brush.InterpolationColors = new ColorBlend
            {
                Colors = stops.Select(s => s.color).ToArray(),
                Positions = stops.Select(s => (s.at + 100) / 200f).ToArray(),
            };
            using var bp = OpinionSlider.Rounded(bar, 4);
            g.FillPath(brush, bp);
        }
        using var tb = new SolidBrush(Theme.TextDim);
        g.DrawString(_view == PerceptionView.Ours ? "My opinion of them" : "Their (supposed) opinion of me",
            Theme.Small, tb, area.X + 12, area.Y + 6);
        string[] labels = { "Enemy", "Neutral", "Friend", "Ally" };
        float[] pos = { 0f, 0.5f, 0.8f, 1f };
        for (int i = 0; i < labels.Length; i++)
        {
            var s = g.MeasureString(labels[i], Theme.Small);
            float x = Math.Clamp(bar.X + bar.Width * pos[i] - s.Width / 2, area.X + 6, area.Right - s.Width - 4);
            if (i == 2) x -= 8;
            g.DrawString(labels[i], Theme.Small, tb, x, bar.Bottom + 1);
        }
        const string size = "circle size = influence in field   ·   arrow = my influence on them";
        g.DrawString(size, Theme.Small, tb, area.Right + 14, area.Bottom - 18);
    }

    private void DrawTooltip(Graphics g, Person p)
    {
        var lines = new List<string> { string.IsNullOrWhiteSpace(p.FullName) ? p.DisplayName : p.FullName };
        if (!string.IsNullOrWhiteSpace(p.Role)) lines.Add(p.Role);
        lines.Add($"Influence in field: {p.FieldInfluence}");
        if (!p.IsMe)
        {
            lines.Add($"My influence on them: {p.OurInfluence}");
            lines.Add($"I see them: {OpinionPalette.LabelFor(p.OurOpinion)} ({p.OurOpinion:+#;-#;0})");
            lines.Add($"They see me: {OpinionPalette.LabelFor(p.TheirOpinion)} ({p.TheirOpinion:+#;-#;0})");
            int gap = Math.Abs(p.OurOpinion - p.TheirOpinion);
            if (gap >= 40) lines.Add($"Perception gap: {gap} - worth checking");
        }
        string text = string.Join("\n", lines);
        var size = g.MeasureString(text, Theme.Body);
        var c = ToScreen(p.X, p.Y);
        float r = RadiusOf(p) * _zoom;
        var box = new RectangleF(c.X + r + 12, c.Y - size.Height / 2 - 8, size.Width + 20, size.Height + 16);
        if (box.Right > Width - 8) box.X = c.X - r - 12 - box.Width;
        box.Y = Math.Clamp(box.Y, 8, Math.Max(8, Height - box.Height - 8));
        using (var path = OpinionSlider.Rounded(box, 8))
        {
            using var bg = new SolidBrush(Color.FromArgb(235, 32, 35, 46));
            g.FillPath(bg, path);
            using var border = new Pen(Color.FromArgb(160, ColorOf(p)), 1.2f);
            g.DrawPath(border, path);
        }
        using var tb = new SolidBrush(Theme.Text);
        g.DrawString(text, Theme.Body, tb, box.X + 10, box.Y + 8);
    }

    private static void DrawPill(Graphics g, string text, PointF center, Font font, Color accent)
    {
        var s = g.MeasureString(text, font);
        var r = new RectangleF(center.X - s.Width / 2 - 8, center.Y - s.Height / 2 - 3, s.Width + 16, s.Height + 6);
        using var path = OpinionSlider.Rounded(r, r.Height / 2);
        using var bg = new SolidBrush(Color.FromArgb(225, 30, 33, 43));
        g.FillPath(bg, path);
        using var border = new Pen(Color.FromArgb(Math.Min(255, accent.A + 40), accent), 1f);
        g.DrawPath(border, path);
        using var tb = new SolidBrush(Theme.Text);
        g.DrawString(text, font, tb, r.X + 8, r.Y + 3);
    }

    private void DrawCentered(Graphics g, string text, Font font, Color color, float dy = 0)
    {
        var s = g.MeasureString(text, font);
        using var b = new SolidBrush(color);
        g.DrawString(text, font, b, (Width - s.Width) / 2, (Height - s.Height) / 2 + dy);
    }

    private static string Ellipsize(Graphics g, string s, Font f, float max)
    {
        if (g.MeasureString(s, f).Width <= max) return s;
        while (s.Length > 1 && g.MeasureString(s + "…", f).Width > max) s = s[..^1];
        return s + "…";
    }

    private static float Brightness(Color c) => (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;

    // ───────────────────────────── interaction ─────────────────────────────

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (_field == null) return;

        if (e.Button == MouseButtons.Left && _viewChipRect.Contains(e.Location))
        {
            ToggleView();
            return;
        }

        var hit = HitTest(e.Location);
        _dragStart = e.Location;
        _dragMoved = false;

        if (e.Button == MouseButtons.Left && hit != null)
        {
            Selected = hit;
            _drag = DragMode.Person;
            _dragOrigin = new PointF(hit.X, hit.Y);
        }
        else if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
        {
            _drag = DragMode.Pan;
            _dragOrigin = _pan;
            Cursor = Cursors.SizeAll;
        }
        else if (e.Button == MouseButtons.Right)
        {
            if (hit != null) Selected = hit;
            ContextRequested?.Invoke(this, new CanvasContextEventArgs(hit, e.Location, ToWorld(e.Location)));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_field == null) return;
        int dx = e.X - _dragStart.X, dy = e.Y - _dragStart.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > 3) _dragMoved = true;

        switch (_drag)
        {
            case DragMode.Person when _selected != null && _dragMoved:
                _selected.X = _dragOrigin.X + dx / _zoom;
                _selected.Y = _dragOrigin.Y + dy / _zoom;
                Invalidate();
                break;
            case DragMode.Pan when _dragMoved:
                _pan = new PointF(_dragOrigin.X + dx, _dragOrigin.Y + dy);
                Invalidate();
                break;
            case DragMode.None:
                var hit = HitTest(e.Location);
                if (hit != _hover) { _hover = hit; Invalidate(); }
                Cursor = hit != null || _viewChipRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
                break;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_drag == DragMode.Person && _dragMoved) FieldModified?.Invoke(this, EventArgs.Empty);
        if (_drag == DragMode.Pan && !_dragMoved && e.Button == MouseButtons.Left) Selected = null;
        _drag = DragMode.None;
        Cursor = Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != null) { _hover = null; Invalidate(); }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_field == null || e.Button != MouseButtons.Left || _viewChipRect.Contains(e.Location)) return;
        var hit = HitTest(e.Location);
        if (hit != null) EditRequested?.Invoke(this, hit);
        else AddRequested?.Invoke(this, ToWorld(e.Location));
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var before = ToWorld(e.Location);
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.12f : 1 / 1.12f), 0.2f, 4f);
        var after = ToScreen(before.X, before.Y);
        _pan = new PointF(_pan.X + e.X - after.X, _pan.Y + e.Y - after.Y);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.V) { ToggleView(); e.Handled = true; }
        else if (e.KeyCode is Keys.F or Keys.Home) { FitToView(); e.Handled = true; }
        else if (_selected != null && e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            float step = e.Shift ? 40 : 8;
            if (e.KeyCode == Keys.Left) _selected.X -= step;
            if (e.KeyCode == Keys.Right) _selected.X += step;
            if (e.KeyCode == Keys.Up) _selected.Y -= step;
            if (e.KeyCode == Keys.Down) _selected.Y += step;
            Invalidate();
            FieldModified?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _anim.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class CanvasContextEventArgs : EventArgs
{
    public CanvasContextEventArgs(Person? person, Point screen, PointF world)
    {
        Person = person; Screen = screen; World = world;
    }
    public Person? Person { get; }
    public Point Screen { get; }
    public PointF World { get; }
}
