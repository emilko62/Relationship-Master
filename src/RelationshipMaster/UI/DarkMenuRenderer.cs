namespace RelationshipMaster.UI;

/// <summary>Dark colours for context menus.</summary>
public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextDim;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Text;
        base.OnRenderArrow(e);
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color ImageMarginGradientBegin => Theme.Panel;
        public override Color ImageMarginGradientMiddle => Theme.Panel;
        public override Color ImageMarginGradientEnd => Theme.Panel;
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Theme.Accent;
        public override Color MenuItemSelected => Color.FromArgb(52, 48, 84);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(52, 48, 84);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(52, 48, 84);
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }

    public static ContextMenuStrip CreateMenu() => new()
    {
        Renderer = new DarkMenuRenderer(),
        BackColor = Theme.Panel,
        ForeColor = Theme.Text,
        Font = Theme.Body,
        ShowImageMargin = false,
    };
}
