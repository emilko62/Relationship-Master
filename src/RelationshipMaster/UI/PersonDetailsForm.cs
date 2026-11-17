using RelationshipMaster.Models;

namespace RelationshipMaster.UI;

/// <summary>Key information and relationship history of one person.</summary>
public sealed class PersonDetailsForm : Form
{
    private readonly Field _field;
    private readonly Person _person;
    private readonly PersonActions _actions;
    private readonly ListBox _facts, _history;
    private readonly ComboBox _category;
    private readonly TextBox _factText;
    private readonly Button _addFact;
    private readonly Label _header, _summary;
    private KeyFact? _editing;

    public bool Changed { get; private set; }

    public PersonDetailsForm(Field field, Person person, PersonActions actions)
    {
        _field = field; _person = person; _actions = actions;
        Text = $"{person.FullName} - key info & history";
        Theme.StyleForm(this);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(Theme.Px(1020), wa.Width - 40), Math.Min(Theme.Px(680), wa.Height - 40));
        MinimumSize = new Size(Math.Min(Theme.Px(640), wa.Width - 40), Math.Min(Theme.Px(420), wa.Height - 40));
        Padding = Theme.Pad(18, 12, 18, 14);

        // header
        var top = new Panel { Dock = DockStyle.Top, Height = Theme.Px(62), BackColor = Theme.Panel };
        _header = Theme.MakeLabel(person.FullName, Theme.Title, Theme.Text);
        _header.Location = new Point(0, 0);
        _summary = Theme.MakeLabel("", Theme.Body);
        _summary.Location = new Point(Theme.Px(2), Theme.Px(40));
        top.Controls.Add(_header);
        top.Controls.Add(_summary);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Panel };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ── key info ──
        var left = new Panel { Dock = DockStyle.Fill, Padding = Theme.Pad(0, 0, 12, 0), BackColor = Theme.Panel };
        var factsTitle = Theme.MakeLabel("KEY INFO  -  likes, skills, quirks, weaknesses…", Theme.BodyBold, Theme.TextDim);
        factsTitle.Dock = DockStyle.Top;
        _facts = MakeList();
        _facts.MeasureItem += MeasureFact;
        _facts.DrawItem += DrawFact;
        _facts.DoubleClick += (_, _) => BeginEditFact();
        _facts.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveFact(); };

        var addRow = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 3, AutoSize = true, BackColor = Theme.Panel, Padding = Theme.Pad(0, 8, 0, 0) };
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _category = Theme.MakeCombo();
        foreach (var c in Enum.GetValues<FactCategory>()) _category.Items.Add(new CategoryItem(c));
        _category.SelectedIndex = 0;
        _category.Width = Theme.Px(130);
        _category.Margin = Theme.Pad(0, 2, 6, 0);
        _factText = Theme.MakeTextBox();
        _factText.Dock = DockStyle.Fill;
        _factText.Margin = Theme.Pad(0, 2, 6, 0);
        _factText.PlaceholderText = "e.g. Loves dogs, especially Great Danes";
        _factText.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CommitFact(); } };
        _addFact = Theme.MakeButton("Add", primary: true, small: true);
        _addFact.Click += (_, _) => CommitFact();
        addRow.Controls.Add(_category, 0, 0);
        addRow.Controls.Add(_factText, 1, 0);
        addRow.Controls.Add(_addFact, 2, 0);

        var factButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = Theme.Panel, Padding = Theme.Pad(0, 6, 0, 0) };
        factButtons.Controls.Add(SmallButton("Edit", (_, _) => BeginEditFact()));
        factButtons.Controls.Add(SmallButton("Remove", (_, _) => RemoveFact()));

        left.Controls.Add(Boxed(_facts));
        left.Controls.Add(factButtons);
        left.Controls.Add(addRow);
        left.Controls.Add(factsTitle);

        // ── history ──
        var right = new Panel { Dock = DockStyle.Fill, Padding = Theme.Pad(12, 0, 0, 0), BackColor = Theme.Panel };
        var historyTitle = Theme.MakeLabel("HISTORY  -  newest first  ·  double-click to edit", Theme.BodyBold, Theme.TextDim);
        historyTitle.Dock = DockStyle.Top;
        _history = MakeList();
        _history.MeasureItem += MeasureEvent;
        _history.DrawItem += DrawEvent;
        _history.DoubleClick += (_, _) => OpenSelected();
        _history.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); if (e.KeyCode == Keys.Enter) OpenSelected(); };

        var historyButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = Theme.Panel, Padding = Theme.Pad(0, 8, 0, 0) };
        historyButtons.Controls.Add(SmallButton("+ Record event", (_, _) => RecordEvent(), primary: true));
        historyButtons.Controls.Add(SmallButton("✨ Plan interaction", (_, _) => Plan(), primary: true));
        historyButtons.Controls.Add(SmallButton("Edit / enter result", (_, _) => OpenSelected()));
        historyButtons.Controls.Add(SmallButton("Delete", (_, _) => DeleteSelected()));

        right.Controls.Add(Boxed(_history));
        right.Controls.Add(historyButtons);
        right.Controls.Add(historyTitle);

        grid.Controls.Add(left, 0, 0);
        grid.Controls.Add(right, 1, 0);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, BackColor = Theme.Panel, Padding = Theme.Pad(0, 10, 0, 0) };
        var close = Theme.MakeButton("Close");
        close.Click += (_, _) => Close();
        CancelButton = close;
        bottom.Controls.Add(close);
        var prompt = Theme.MakeButton("📋 Prompt for my Claude…");
        prompt.Margin = Theme.Pad(0, 0, 8, 0);
        prompt.Click += (_, _) => _actions.ShowPersonPrompt(this, _field, _person);
        bottom.Controls.Add(prompt);

        Controls.Add(grid);
        Controls.Add(bottom);
        Controls.Add(top);

        Resize += (_, _) => { _facts.Refresh(); _history.Refresh(); RemeasureLists(); };
        Reload();
    }

    private static ListBox MakeList() => new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = Theme.PanelAlt,
        ForeColor = Theme.Text,
        DrawMode = DrawMode.OwnerDrawVariable,
        IntegralHeight = false,
        Font = Theme.Body,
    };

    private static Control Boxed(Control inner)
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = Theme.Pad(6), BackColor = Theme.PanelAlt };
        host.Controls.Add(inner);
        return host;
    }

    private static Button SmallButton(string text, EventHandler click, bool primary = false)
    {
        var b = Theme.MakeButton(text, primary, small: true);
        b.Margin = Theme.Pad(0, 0, 6, 0);
        b.Click += click;
        return b;
    }

    private void Reload()
    {
        var p = _person;
        _summary.Text = $"{(string.IsNullOrWhiteSpace(p.Role) ? "" : p.Role + "   ·   ")}" +
                        $"Authority: {AuthorityPalette.LabelFor(p.Authority)} ({p.Authority})   ·   " +
                        $"I see them: {OpinionPalette.LabelFor(p.OurOpinion)} ({Theme.Signed(p.OurOpinion)})   ·   " +
                        $"They see me: {OpinionPalette.LabelFor(p.TheirOpinion)} ({Theme.Signed(p.TheirOpinion)})" +
                        (p.PendingCount > 0 ? $"   ·   {p.PendingCount} awaiting result" : "");

        var selFact = _facts.SelectedItem;
        _facts.BeginUpdate();
        _facts.Items.Clear();
        foreach (var f in p.Facts.OrderBy(f => f.Category)) _facts.Items.Add(f);
        _facts.EndUpdate();
        if (selFact != null && _facts.Items.Contains(selFact)) _facts.SelectedItem = selFact;

        var selEvent = _history.SelectedItem;
        _history.BeginUpdate();
        _history.Items.Clear();
        foreach (var e in p.History
                     .OrderByDescending(e => e.Status == EventStatus.Pending)
                     .ThenByDescending(e => e.CompletedAt ?? e.Date))
            _history.Items.Add(e);
        _history.EndUpdate();
        if (selEvent != null && _history.Items.Contains(selEvent)) _history.SelectedItem = selEvent;
    }

    /// <summary>Variable item heights depend on the list width - re-add items so heights are measured again.</summary>
    private void RemeasureLists()
    {
        if (!IsHandleCreated) return;
        BeginInvoke(Reload);
    }

    private void MarkChanged()
    {
        Changed = true;
        Reload();
    }

    // ── facts ──

    private void CommitFact()
    {
        var text = _factText.Text.Trim();
        if (text.Length == 0) return;
        var cat = ((CategoryItem)_category.SelectedItem!).Category;
        if (_editing != null)
        {
            _editing.Text = text;
            _editing.Category = cat;
            _editing = null;
            _addFact.Text = "Add";
        }
        else
        {
            _person.Facts.Add(new KeyFact { Category = cat, Text = text });
        }
        _factText.Clear();
        _factText.Focus();
        MarkChanged();
    }

    private void BeginEditFact()
    {
        if (_facts.SelectedItem is not KeyFact f) return;
        _editing = f;
        _category.SelectedIndex = (int)f.Category;
        _factText.Text = f.Text;
        _addFact.Text = "Update";
        _factText.Focus();
        _factText.SelectAll();
    }

    private void RemoveFact()
    {
        if (_facts.SelectedItem is not KeyFact f) return;
        _person.Facts.Remove(f);
        if (_editing == f) { _editing = null; _addFact.Text = "Add"; _factText.Clear(); }
        MarkChanged();
    }

    private void MeasureFact(object? sender, MeasureItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _facts.Items.Count) return;
        var f = (KeyFact)_facts.Items[e.Index];
        int textWidth = Math.Max(Theme.Px(80), _facts.ClientSize.Width - Theme.Px(120));
        var size = TextRenderer.MeasureText(e.Graphics, f.Text, Theme.Body, new Size(textWidth, 0), TextFormatFlags.WordBreak);
        e.ItemHeight = Math.Min(255, Math.Max(Theme.Px(30), size.Height + Theme.Px(12)));
    }

    private void DrawFact(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _facts.Items.Count) return;
        var f = (KeyFact)_facts.Items[e.Index];
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(selected ? Theme.Selection : Theme.PanelAlt)) g.FillRectangle(bg, e.Bounds);

        var color = KeyFact.ColorOf(f.Category);
        var chip = new RectangleF(e.Bounds.X + Theme.Px(4), e.Bounds.Y + Theme.Px(6), Theme.Px(100), Theme.Px(20));
        using (var path = OpinionSlider.Rounded(chip, chip.Height / 2))
        using (var cb = new SolidBrush(Color.FromArgb(60, color)))
        using (var cp = new Pen(color))
        {
            g.FillPath(cb, path);
            g.DrawPath(cp, path);
        }
        TextRenderer.DrawText(g, KeyFact.Label(f.Category), Theme.SmallBold, Rectangle.Round(chip), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var textRect = new Rectangle(e.Bounds.X + Theme.Px(112), e.Bounds.Y + Theme.Px(6),
            e.Bounds.Width - Theme.Px(118), e.Bounds.Height - Theme.Px(8));
        TextRenderer.DrawText(g, f.Text, Theme.Body, textRect, Theme.Text, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }

    // ── history ──

    private void RecordEvent()
    {
        if (_actions.RecordEvent(this, _person)) MarkChanged();
    }

    private void Plan()
    {
        var ev = _actions.PlanInteraction(this, _field, _person);
        if (ev == null) return;
        MarkChanged();
        _history.SelectedItem = ev;
    }

    private void OpenSelected()
    {
        if (_history.SelectedItem is not RelationshipEvent ev) return;
        if (_actions.OpenEvent(this, _field, _person, ev)) MarkChanged();
    }

    private void DeleteSelected()
    {
        if (_history.SelectedItem is not RelationshipEvent ev) return;
        bool hasEffect = ev.Status == EventStatus.Done && (ev.OurOpinionDelta != 0 || ev.TheirOpinionDelta != 0);
        if (hasEffect)
        {
            var answer = MessageBox.Show(this,
                $"Delete \"{ev.Title}\"?\n\nAlso undo its effect on the relationship " +
                $"(my opinion {Theme.Signed(-ev.OurOpinionDelta)}, their opinion of me {Theme.Signed(-ev.TheirOpinionDelta)})?",
                "Delete event", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return;
            if (answer == DialogResult.Yes) _person.ApplyDeltas(-ev.OurOpinionDelta, -ev.TheirOpinionDelta);
        }
        else if (MessageBox.Show(this, $"Delete \"{ev.Title}\"?", "Delete event",
                     MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _person.History.Remove(ev);
        MarkChanged();
    }

    private string EventBody(RelationshipEvent ev)
    {
        if (ev.Status == EventStatus.Pending)
            return $"Awaiting result  ·  {RelationshipEvent.GoalLabel(ev.Goal)}  ·  ±{ev.Intensity}\n{RecordEventDialog.Shorten(ev.Situation, 160)}";
        var text = ev.Kind == EventKind.Planned && !string.IsNullOrWhiteSpace(ev.ResultStory) ? ev.ResultStory : ev.Description;
        return RecordEventDialog.Shorten(text, 220);
    }

    private void MeasureEvent(object? sender, MeasureItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _history.Items.Count) return;
        var ev = (RelationshipEvent)_history.Items[e.Index];
        int w = Math.Max(Theme.Px(80), _history.ClientSize.Width - Theme.Px(24));
        var body = TextRenderer.MeasureText(e.Graphics, EventBody(ev), Theme.Small, new Size(w, 0), TextFormatFlags.WordBreak);
        e.ItemHeight = Math.Min(255, Theme.Px(60) + (string.IsNullOrWhiteSpace(EventBody(ev)) ? 0 : body.Height));
    }

    private void DrawEvent(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _history.Items.Count) return;
        var ev = (RelationshipEvent)_history.Items[e.Index];
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        var b = e.Bounds;
        using (var bg = new SolidBrush(Theme.PanelAlt)) g.FillRectangle(bg, b);
        var card = new RectangleF(b.X + Theme.Px(2), b.Y + Theme.Px(3), b.Width - Theme.Px(4), b.Height - Theme.Px(6));
        Color stripe = ev.Status switch
        {
            EventStatus.Pending => Theme.Pending,
            EventStatus.Cancelled => Theme.TextDim,
            _ => ev.TheirOpinionDelta + ev.OurOpinionDelta > 0 ? Theme.Good : ev.TheirOpinionDelta + ev.OurOpinionDelta < 0 ? Theme.Bad : Theme.Border,
        };
        using (var path = OpinionSlider.Rounded(card, Theme.Px(6f)))
        using (var cb = new SolidBrush(selected ? Theme.Selection : Theme.Panel))
            g.FillPath(cb, path);
        using (var sb = new SolidBrush(stripe))
            g.FillRectangle(sb, card.X, card.Y + Theme.Px(6), Theme.Px(3), card.Height - Theme.Px(12));

        int x = (int)card.X + Theme.Px(12);
        int y = (int)card.Y + Theme.Px(5);
        int right = (int)card.Right - Theme.Px(8);

        // right side: badges
        var badges = new List<(string text, Color color)>();
        if (ev.Status == EventStatus.Pending) badges.Add(("AWAITING RESULT", Theme.Pending));
        else if (ev.Status == EventStatus.Cancelled) badges.Add(("CANCELLED", Theme.TextDim));
        else
        {
            if (ev.SuccessPercent is int pct) badges.Add(($"{pct}% success", pct >= 50 ? Theme.Good : Theme.Bad));
            badges.Add(($"me {Theme.Signed(ev.OurOpinionDelta)}", DeltaColor(ev.OurOpinionDelta)));
            badges.Add(($"them {Theme.Signed(ev.TheirOpinionDelta)}", DeltaColor(ev.TheirOpinionDelta)));
        }
        int bx = right;
        foreach (var (text, color) in Enumerable.Reverse(badges))
        {
            var size = TextRenderer.MeasureText(g, text, Theme.SmallBold);
            var r = new Rectangle(bx - size.Width - Theme.Px(10), y, size.Width + Theme.Px(10), Theme.Px(20));
            using (var path = OpinionSlider.Rounded(r, r.Height / 2f))
            using (var fill = new SolidBrush(Color.FromArgb(50, color)))
                g.FillPath(fill, path);
            TextRenderer.DrawText(g, text, Theme.SmallBold, r, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            bx = r.Left - Theme.Px(5);
        }

        var titleRect = new Rectangle(x, y, Math.Max(10, bx - x - Theme.Px(4)), Theme.Px(22));
        TextRenderer.DrawText(g, ev.Title, Theme.BodyBold, titleRect, Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

        string date = ev.Status == EventStatus.Done && ev.CompletedAt is DateTime done ? $"{done:g}  (planned {ev.Date:d})" : ev.Date.ToString("g");
        if (ev.Kind == EventKind.Planned && ev.Status == EventStatus.Done) date += "  ·  planned interaction";
        TextRenderer.DrawText(g, date, Theme.Small, new Point(x, y + Theme.Px(22)), Theme.TextDim);

        var body = EventBody(ev);
        if (!string.IsNullOrWhiteSpace(body))
        {
            var bodyRect = new Rectangle(x, y + Theme.Px(40), right - x, (int)card.Bottom - (y + Theme.Px(40)) - Theme.Px(2));
            TextRenderer.DrawText(g, body, Theme.Small, bodyRect, Color.FromArgb(205, 210, 220), TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }

    private static Color DeltaColor(int d) => d > 0 ? Theme.Good : d < 0 ? Theme.Bad : Theme.TextDim;

    private sealed record CategoryItem(FactCategory Category)
    {
        public override string ToString() => KeyFact.Label(Category);
    }
}
