namespace RelationshipMaster.UI;

/// <summary>Flat segmented tab header that shows one of several docked pages.</summary>
public sealed class TabStrip : Control
{
    private readonly (string title, Control page)[] _tabs;
    private int _selected;
    private int _hover = -1;

    public TabStrip(params (string title, Control page)[] tabs)
    {
        _tabs = tabs;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
        Height = Theme.Px(44);
        Cursor = Cursors.Hand;
        Select(0);
    }

    public int SelectedIndex => _selected;

    public void Select(int index)
    {
        _selected = Math.Clamp(index, 0, _tabs.Length - 1);
        for (int i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].page.Dock = DockStyle.Fill;
            _tabs[i].page.Visible = i == _selected;
        }
        Invalidate();
    }

    private Rectangle TabRect(int i)
    {
        int pad = Theme.Px(10);
        int w = (Width - pad * 2) / _tabs.Length;
        return new Rectangle(pad + i * w, Theme.Px(8), w, Height - Theme.Px(12));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        var all = Rectangle.Union(TabRect(0), TabRect(_tabs.Length - 1));
        using (var path = OpinionSlider.Rounded(all, Theme.Px(8f)))
        using (var bg = new SolidBrush(Theme.PanelAlt))
            g.FillPath(bg, path);
        for (int i = 0; i < _tabs.Length; i++)
        {
            var r = TabRect(i);
            if (i == _selected || i == _hover)
            {
                var inner = Rectangle.Inflate(r, -Theme.Px(3), -Theme.Px(3));
                using var path = OpinionSlider.Rounded(inner, Theme.Px(6f));
                using var fill = new SolidBrush(i == _selected ? Theme.Accent : Theme.Border);
                g.FillPath(fill, path);
            }
            TextRenderer.DrawText(g, _tabs[i].title, Theme.BodyBold, r, i == _selected ? Color.White : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = Enumerable.Range(0, _tabs.Length).FirstOrDefault(i => TabRect(i).Contains(e.Location), -1);
        if (h != _hover) { _hover = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        for (int i = 0; i < _tabs.Length; i++)
            if (TabRect(i).Contains(e.Location)) { Select(i); return; }
    }
}
