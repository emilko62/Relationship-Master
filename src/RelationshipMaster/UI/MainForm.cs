using RelationshipMaster.Models;
using RelationshipMaster.Services;

namespace RelationshipMaster.UI;

public sealed class MainForm : Form
{
    private readonly AppData _data;
    private readonly RelationshipAdvisor _advisor = new();
    private readonly FieldCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly ListBox _fieldList;
    private readonly InspectorPanel _inspector = new() { Dock = DockStyle.Fill };
    private readonly AdvisorPanel _advisorPanel;
    private readonly Button _viewButton;
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 700 };
    private readonly PersonActions _actions;
    private readonly TabStrip _tabs;

    private Field? CurrentField => _fieldList.SelectedItem as Field;

    public MainForm()
    {
        Text = "Relationship Master";
        Theme.StyleForm(this);
        BackColor = Theme.Background;
        // Size relative to the screen: at 200 % a 1080p monitor is only 960×540 "logical" pixels.
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        MinimumSize = new Size(Math.Min(Theme.Px(820), wa.Width), Math.Min(Theme.Px(520), wa.Height));
        Size = new Size(Math.Min(Theme.Px(1480), wa.Width - 40), Math.Min(Theme.Px(900), wa.Height - 40));
        StartPosition = FormStartPosition.CenterScreen;
        if (Theme.Px(1300) > wa.Width || Theme.Px(800) > wa.Height) WindowState = FormWindowState.Maximized;
        KeyPreview = true;

        _data = DataStore.Load();
        _advisor.Configure(DataStore.LoadApiKey());
        _advisorPanel = new AdvisorPanel(_advisor)
        {
            Dock = DockStyle.Fill,
            FieldProvider = () => CurrentField,
            SelectionProvider = () => _canvas.Selected,
        };
        _advisorPanel.SettingsRequested += (_, _) => OpenApiSettings();
        _actions = new PersonActions(_advisor, owner => EnsureAi(owner));

        // ── top bar ──
        // Wraps onto a second line when the window is narrow (e.g. at high display scaling).
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Panel,
            Padding = Theme.Pad(14, 10, 14, 4), WrapContents = true,
        };
        var logo = Theme.MakeLabel("◎ Relationship Master", new Font("Segoe UI Semibold", 13f), Theme.Text);
        logo.Margin = Theme.Pad(0, 4, 24, 6);
        top.Controls.Add(logo);
        top.Controls.Add(TopButton("+ Person", (_, _) => AddPersonNear(PointF.Empty), primary: true));
        top.Controls.Add(TopButton("✨ Quick add (AI)", (_, _) => QuickAdd(PointF.Empty)));
        _viewButton = TopButton("", (_, _) => _canvas.ToggleView());
        top.Controls.Add(_viewButton);
        top.Controls.Add(TopButton("Fit view", (_, _) => _canvas.FitToView()));
        top.Controls.Add(TopButton("Export…", (_, _) => Export()));
        top.Controls.Add(TopButton("Import…", (_, _) => Import()));
        top.Controls.Add(TopButton("⚙ AI settings", (_, _) => OpenApiSettings()));

        // ── left: fields ──
        var left = new Panel { Dock = DockStyle.Left, Width = Theme.Px(210), BackColor = Theme.Panel, Padding = Theme.Pad(12, 14, 12, 12) };
        var fieldsTitle = Theme.MakeLabel("FIELDS", Theme.BodyBold, Theme.TextDim);
        fieldsTitle.Dock = DockStyle.Top;
        _fieldList = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = Math.Min(255, Theme.Px(48)),
            IntegralHeight = false,
        };
        _fieldList.DrawItem += DrawFieldItem;
        _fieldList.SelectedIndexChanged += (_, _) => OnFieldSelected();
        _fieldList.DoubleClick += (_, _) => RenameField();
        _fieldList.MouseDown += FieldListMouseDown;
        var fieldButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = Theme.Panel, Padding = Theme.Pad(0, 8, 0, 0) };
        fieldButtons.Controls.Add(TopButton("+ Field", (_, _) => AddField(), primary: true));
        fieldButtons.Controls.Add(TopButton("Edit", (_, _) => RenameField()));
        fieldButtons.Controls.Add(TopButton("Delete", (_, _) => DeleteField()));
        left.Controls.Add(_fieldList);
        left.Controls.Add(fieldButtons);
        left.Controls.Add(fieldsTitle);

        // ── right: tabs "Person" | "AI Advisor" - each gets the full height (important on small / high-DPI screens) ──
        var right = new Panel { Dock = DockStyle.Right, Width = Theme.Px(360), BackColor = Theme.Panel };
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
        content.Controls.Add(_inspector);
        content.Controls.Add(_advisorPanel);
        _tabs = new TabStrip(("Person", _inspector), ("✨ AI Advisor", _advisorPanel)) { Dock = DockStyle.Top };
        right.Controls.Add(content);
        right.Controls.Add(_tabs);

        // Dock order: fill first, then sides, then the top bar.
        Controls.Add(_canvas);
        Controls.Add(new Panel { Dock = DockStyle.Left, Width = Theme.Px(1), BackColor = Theme.Border });
        Controls.Add(left);
        Controls.Add(new Panel { Dock = DockStyle.Right, Width = Theme.Px(1), BackColor = Theme.Border });
        Controls.Add(right);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = Theme.Px(1), BackColor = Theme.Border });
        Controls.Add(top);

        Load += (_, _) =>
        {
            _canvas.FitToView();
        };

        // ── wiring ──
        _canvas.SelectionChanged += (_, _) =>
        {
            _inspector.Show(_canvas.Selected);
            if (_canvas.Selected != null) _tabs.Select(0);
        };
        _canvas.FieldModified += (_, _) => ScheduleSave();
        _canvas.ViewChanged += (_, _) => UpdateViewButton();
        _canvas.EditRequested += (_, p) => { _canvas.Selected = p; _inspector.FocusName(); };
        _canvas.AddRequested += (_, w) => AddPersonNear(w, exact: true);
        _canvas.ContextRequested += ShowCanvasMenu;
        _inspector.PersonChanged += (_, _) => { _canvas.Invalidate(); ScheduleSave(); };
        _inspector.DeleteRequested += (_, p) => DeletePerson(p);
        _inspector.AskAiRequested += (_, p) => AskAi(p);
        _inspector.DetailsRequested += (_, p) => OpenDetails(p);
        _inspector.RecordEventRequested += (_, p) => RecordEvent(p);
        _inspector.PlanRequested += (_, p) => PlanInteraction(p);
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };
        FormClosing += (_, _) => Save();

        RefreshFieldList(_data.Fields.FirstOrDefault(f => f.Id == _data.LastFieldId) ?? _data.Fields.FirstOrDefault());
        UpdateViewButton();
    }

    private static Button TopButton(string text, EventHandler onClick, bool primary = false)
    {
        var b = Theme.MakeButton(text, primary);
        b.Margin = Theme.Pad(0, 0, 8, 6);
        b.Click += onClick;
        return b;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool typing = ActiveControl is TextBoxBase || (ActiveControl as ContainerControl)?.ActiveControl is TextBoxBase
                      || FindFocused(this) is TextBoxBase;
        if (!typing)
        {
            if (keyData == Keys.Delete && _canvas.Selected is { IsMe: false } p) { DeletePerson(p); return true; }
            if (keyData == Keys.N) { AddPersonNear(PointF.Empty); return true; }
        }
        if (keyData == (Keys.Control | Keys.S)) { Save(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static Control? FindFocused(Control c)
    {
        foreach (Control child in c.Controls)
        {
            if (child.Focused) return child;
            var f = FindFocused(child);
            if (f != null) return f;
        }
        return null;
    }

    // ───────────────────────────── fields ─────────────────────────────

    private void RefreshFieldList(Field? select)
    {
        _fieldList.BeginUpdate();
        _fieldList.Items.Clear();
        foreach (var f in _data.Fields) _fieldList.Items.Add(f);
        _fieldList.EndUpdate();
        if (select != null) _fieldList.SelectedItem = select;
        else OnFieldSelected();
    }

    private void OnFieldSelected()
    {
        var f = CurrentField;
        _canvas.Field = f;
        _data.LastFieldId = f?.Id;
        _advisorPanel.OnFieldChanged(f);
        _inspector.Show(null);
    }

    private void DrawFieldItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var f = (Field)_fieldList.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(Theme.Panel)) g.FillRectangle(bg, e.Bounds);
        var r = new RectangleF(e.Bounds.X, e.Bounds.Y + Theme.Px(3f), e.Bounds.Width - Theme.Px(2f), e.Bounds.Height - Theme.Px(6f));
        if (selected)
        {
            using var path = OpinionSlider.Rounded(r, Theme.Px(8f));
            using var fill = new SolidBrush(Theme.Selection);
            g.FillPath(fill, path);
            using var accent = new SolidBrush(Theme.Accent);
            g.FillRectangle(accent, r.X, r.Y + Theme.Px(8f), Theme.Px(3f), r.Height - Theme.Px(16f));
        }

        // little preview dots coloured by our opinion
        float dot = Theme.Px(8f);
        float x = r.Right - Theme.Px(12f);
        foreach (var p in f.Others.OrderByDescending(p => p.FieldInfluence).Take(4))
        {
            using var b = new SolidBrush(OpinionPalette.ColorFor(p.OurOpinion));
            g.FillEllipse(b, x - dot, r.Y + r.Height / 2 - dot / 2, dot, dot);
            x -= dot + Theme.Px(3f);
        }

        int textX = (int)(r.X + Theme.Px(12f));
        var nameRect = new Rectangle(textX, (int)r.Y + Theme.Px(4), Math.Max(10, (int)x - textX), (int)(r.Height / 2));
        TextRenderer.DrawText(g, f.Name, Theme.BodyBold, nameRect, Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.Bottom);
        int count = f.Others.Count();
        int pending = f.Others.Sum(p => p.PendingCount);
        var sub = (count == 1 ? "1 person" : $"{count} people") + (pending > 0 ? $" · {pending} pending" : "");
        var subRect = new Rectangle(textX, (int)(r.Y + r.Height / 2) + Theme.Px(1), Math.Max(10, (int)x - textX), (int)(r.Height / 2) - Theme.Px(2));
        TextRenderer.DrawText(g, sub, Theme.Small, subRect, pending > 0 ? Theme.Pending : Theme.TextDim, TextFormatFlags.EndEllipsis | TextFormatFlags.Top);
    }

    private void FieldListMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        int idx = _fieldList.IndexFromPoint(e.Location);
        if (idx >= 0) _fieldList.SelectedIndex = idx;
        var menu = DarkMenuRenderer.CreateMenu();
        menu.Items.Add("New field…", null, (_, _) => AddField());
        if (idx >= 0)
        {
            menu.Items.Add("Edit name / description…", null, (_, _) => RenameField());
            menu.Items.Add("Duplicate", null, (_, _) => DuplicateField());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete", null, (_, _) => DeleteField());
        }
        menu.Show(_fieldList, e.Location);
    }

    private void AddField()
    {
        var r = PromptDialog.Ask(this, "New field", "Name of the place / group (e.g. Work, Family, Football club)",
            "", "Description (optional)");
        if (r == null) return;
        var f = Field.CreateNew(r.Value.name);
        f.Description = r.Value.description;
        f.Me.FirstName = _data.MyName;
        _data.Fields.Add(f);
        RefreshFieldList(f);
        ScheduleSave();
    }

    private void RenameField()
    {
        var f = CurrentField;
        if (f == null) return;
        var r = PromptDialog.Ask(this, "Edit field", "Name", f.Name, "Description", f.Description);
        if (r == null) return;
        f.Name = r.Value.name;
        f.Description = r.Value.description;
        _fieldList.Invalidate();
        _canvas.Invalidate();
        ScheduleSave();
    }

    private void DuplicateField()
    {
        var f = CurrentField;
        if (f == null) return;
        var json = System.Text.Json.JsonSerializer.Serialize(f);
        var copy = System.Text.Json.JsonSerializer.Deserialize<Field>(json)!;
        copy.Id = Guid.NewGuid();
        copy.Name = f.Name + " (copy)";
        foreach (var p in copy.People) p.Id = Guid.NewGuid();
        _data.Fields.Add(copy);
        RefreshFieldList(copy);
        ScheduleSave();
    }

    private void DeleteField()
    {
        var f = CurrentField;
        if (f == null) return;
        if (MessageBox.Show(this, $"Delete the field \"{f.Name}\" and everyone in it?", "Delete field",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        int idx = _data.Fields.IndexOf(f);
        _data.Fields.Remove(f);
        RefreshFieldList(_data.Fields.Count == 0 ? null : _data.Fields[Math.Min(idx, _data.Fields.Count - 1)]);
        ScheduleSave();
    }

    // ───────────────────────────── people ─────────────────────────────

    private void AddPersonNear(PointF world, bool exact = false)
    {
        var f = CurrentField;
        if (f == null) { AddField(); return; }
        var p = new Person { FirstName = "New", LastName = "Person", FieldInfluence = 30, OurInfluence = 20 };
        var pos = exact ? world : _canvas.FindFreeSpot(world, FieldCanvas.RadiusOf(p));
        p.X = pos.X; p.Y = pos.Y;
        f.People.Add(p);
        _canvas.Selected = p;
        _canvas.Invalidate();
        _fieldList.Invalidate();
        _inspector.FocusName();
        ScheduleSave();
    }

    private void QuickAdd(PointF world)
    {
        var f = CurrentField;
        if (f == null) { AddField(); return; }
        if (!EnsureAi(this)) return;
        using var dlg = new QuickAddDialog(_advisor, f);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result == null) return;
        var p = dlg.Result;
        var pos = _canvas.FindFreeSpot(world, FieldCanvas.RadiusOf(p));
        p.X = pos.X; p.Y = pos.Y;
        f.People.Add(p);
        _canvas.Selected = p;
        _canvas.Invalidate();
        _fieldList.Invalidate();
        ScheduleSave();
    }

    private void DeletePerson(Person p)
    {
        var f = CurrentField;
        if (f == null || p.IsMe) return;
        if (MessageBox.Show(this, $"Remove {p.FullName} from \"{f.Name}\"?", "Remove person",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        f.People.Remove(p);
        _canvas.Selected = null;
        _canvas.Invalidate();
        _fieldList.Invalidate();
        ScheduleSave();
    }

    private void ShowCanvasMenu(object? sender, CanvasContextEventArgs e)
    {
        var menu = DarkMenuRenderer.CreateMenu();
        if (e.Person is { } p)
        {
            menu.Items.Add($"Edit {p.DisplayName}", null, (_, _) => _inspector.FocusName());
            if (p.IsMe) menu.Items.Add("My key info…", null, (_, _) => OpenDetails(p));
            if (!p.IsMe)
            {
                menu.Items.Add("Record event…", null, (_, _) => RecordEvent(p));
                menu.Items.Add("✨ Plan interaction with AI…", null, (_, _) => PlanInteraction(p));
                var pendingEvents = p.History.Where(h => h.Status == EventStatus.Pending).ToList();
                if (pendingEvents.Count > 0)
                {
                    var complete = new ToolStripMenuItem($"Enter result ({pendingEvents.Count} awaiting)");
                    foreach (var ev in pendingEvents)
                        complete.DropDownItems.Add(ev.Title, null, (_, _) => OpenEvent(p, ev));
                    complete.DropDown.BackColor = Theme.Panel;
                    complete.DropDown.Renderer = new DarkMenuRenderer();
                    menu.Items.Add(complete);
                }
                menu.Items.Add("Key info & history…", null, (_, _) => OpenDetails(p));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add($"✨ Ask AI about {p.DisplayName}", null, (_, _) => AskAi(p));
                var opinion = new ToolStripMenuItem("Set my opinion");
                foreach (var (label, v) in new[] { ("Alliance", 95), ("Close friend", 75), ("Friend", 50), ("Friendly", 25), ("Neutral", 0), ("Wary", -25), ("Negative", -55), ("Enemy", -90) })
                    opinion.DropDownItems.Add(label, null, (_, _) => { p.OurOpinion = v; Changed(p); });
                opinion.DropDown.BackColor = Theme.Panel;
                opinion.DropDown.Renderer = new DarkMenuRenderer();
                menu.Items.Add(opinion);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Remove", null, (_, _) => DeletePerson(p));
            }
        }
        else
        {
            menu.Items.Add("Add person here", null, (_, _) => AddPersonNear(e.World, exact: true));
            menu.Items.Add("✨ Quick add with AI…", null, (_, _) => QuickAdd(e.World));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Switch view", null, (_, _) => _canvas.ToggleView());
            menu.Items.Add("Fit to view", null, (_, _) => _canvas.FitToView());
        }
        menu.Show(_canvas, e.Screen);
    }

    private void AskAi(Person p)
    {
        _tabs.Select(1);
        _advisorPanel.AskAbout(p);
    }

    private void RecordEvent(Person p)
    {
        if (_actions.RecordEvent(this, p)) Changed(p);
    }

    private void PlanInteraction(Person p)
    {
        if (CurrentField is { } f && _actions.PlanInteraction(this, f, p) != null) Changed(p);
    }

    private void OpenEvent(Person p, RelationshipEvent ev)
    {
        if (CurrentField is { } f && _actions.OpenEvent(this, f, p, ev)) Changed(p);
    }

    private void OpenDetails(Person p)
    {
        if (CurrentField is { } f && _actions.OpenDetails(this, f, p)) Changed(p);
    }

    /// <summary>Makes sure an API key is configured, offering the settings dialog if not.</summary>
    private bool EnsureAi(IWin32Window owner)
    {
        if (_advisor.IsConfigured) return true;
        OpenApiSettings(owner);
        return _advisor.IsConfigured;
    }

    private void Changed(Person p)
    {
        if (_canvas.Selected == p) _inspector.Show(p);
        _canvas.Invalidate();
        _fieldList.Invalidate();
        ScheduleSave();
    }

    private void UpdateViewButton() =>
        _viewButton.Text = _canvas.View == PerceptionView.Ours ? "👁 View: how I see them" : "👁 View: how they see me";

    // ───────────────────────────── persistence / settings ─────────────────────────────

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
        _fieldList.Invalidate();
    }

    private void Save()
    {
        try { DataStore.Save(_data); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save: " + ex.Message, "Relationship Master"); }
    }

    private void Export()
    {
        using var dlg = new SaveFileDialog { Filter = "Relationship Master (*.json)|*.json", FileName = "relationships.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        DataStore.Export(_data, dlg.FileName);
    }

    private void Import()
    {
        using var dlg = new OpenFileDialog { Filter = "Relationship Master (*.json)|*.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var imported = DataStore.Import(dlg.FileName);
            if (imported == null || imported.Fields.Count == 0) { MessageBox.Show(this, "No fields found in that file."); return; }
            var existing = _data.Fields.Select(f => f.Id).ToHashSet();
            foreach (var f in imported.Fields)
            {
                if (existing.Contains(f.Id)) f.Id = Guid.NewGuid();
                _ = f.Me;
                _data.Fields.Add(f);
            }
            RefreshFieldList(imported.Fields[0]);
            ScheduleSave();
        }
        catch (Exception ex) { MessageBox.Show(this, "Import failed: " + ex.Message); }
    }

    private void OpenApiSettings() => OpenApiSettings(this);

    private void OpenApiSettings(IWin32Window owner)
    {
        using var dlg = new ApiKeyDialog(DataStore.LoadApiKey());
        if (dlg.ShowDialog(owner) != DialogResult.OK) return;
        try { DataStore.SaveApiKey(dlg.Key); }
        catch (Exception ex) { MessageBox.Show(this, "Could not store the key: " + ex.Message); }
        _advisor.Configure(dlg.Key ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        _advisorPanel.RefreshStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _saveTimer.Dispose();
        base.Dispose(disposing);
    }
}
