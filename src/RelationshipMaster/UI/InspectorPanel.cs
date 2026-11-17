using RelationshipMaster.Models;

namespace RelationshipMaster.UI;

/// <summary>Right-hand editor for the selected person.</summary>
public sealed class InspectorPanel : Panel
{
    private readonly FlowLayoutPanel _flow;
    private readonly Label _title, _empty;
    private readonly TextBox _first, _last, _role, _notes;
    private readonly OpinionSlider _fieldInfluence, _ourInfluence, _ourOpinion, _theirOpinion;
    private readonly Label _fieldInfluenceValue, _ourInfluenceValue, _ourOpinionValue, _theirOpinionValue;
    private readonly List<Control> _othersOnly = new();
    private readonly Button _delete, _askAi, _details, _record, _plan;
    private readonly Label _pending;
    private Person? _person;
    private bool _loading;

    public event EventHandler? PersonChanged;
    public event EventHandler<Person>? DeleteRequested;
    public event EventHandler<Person>? AskAiRequested;
    public event EventHandler<Person>? DetailsRequested;
    public event EventHandler<Person>? RecordEventRequested;
    public event EventHandler<Person>? PlanRequested;

    public InspectorPanel()
    {
        BackColor = Theme.Panel;
        Padding = Theme.Pad(16, 12, 12, 8);

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Panel,
        };
        Controls.Add(_flow);

        _empty = Theme.MakeLabel("Select a person on the canvas.\n\nDouble-click empty space to add someone,\nright-click for more options.", Theme.Body);
        _empty.AutoSize = false;
        _empty.Dock = DockStyle.Fill;
        Controls.Add(_empty);
        _empty.BringToFront();

        _title = Theme.MakeLabel("PERSON", Theme.Header, Theme.Text);
        _flow.Controls.Add(_title);

        _pending = Theme.MakeLabel("", Theme.SmallBold, Theme.Pending);
        _pending.Cursor = Cursors.Hand;
        _pending.Click += (_, _) => { if (_person != null) DetailsRequested?.Invoke(this, _person); };
        _flow.Controls.Add(_pending);

        _first = AddText("First name");
        _last = AddText("Last name", othersOnly: true);
        _role = AddText("Role / relationship");

        (_fieldInfluence, _fieldInfluenceValue) = AddSlider("Influence in this field", 1, 100, false, othersOnly: false);
        (_ourInfluence, _ourInfluenceValue) = AddSlider("My influence on them", 0, 100, false, othersOnly: true);
        (_ourOpinion, _ourOpinionValue) = AddSlider("How I see them", -100, 100, true, othersOnly: true);
        (_theirOpinion, _theirOpinionValue) = AddSlider("How they (supposedly) see me", -100, 100, true, othersOnly: true);

        _flow.Controls.Add(Theme.MakeLabel("Notes"));
        _notes = Theme.MakeTextBox(multiline: true);
        _notes.Height = Theme.Px(64);
        _notes.TextChanged += (_, _) => Commit();
        _flow.Controls.Add(_notes);

        _details = Theme.MakeButton("Key info & history");
        _details.Margin = Theme.Pad(0, 10, 0, 0);
        _details.Click += (_, _) => { if (_person != null) DetailsRequested?.Invoke(this, _person); };
        _flow.Controls.Add(_details);

        var history = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Panel, Margin = Theme.Pad(0, 6, 0, 0) };
        _record = Theme.MakeButton("+ Record event", small: true);
        _record.Margin = Theme.Pad(0, 0, 6, 0);
        _record.Click += (_, _) => { if (_person != null) RecordEventRequested?.Invoke(this, _person); };
        _plan = Theme.MakeButton("✨ Plan interaction", primary: true, small: true);
        _plan.Margin = Theme.Pad(0, 0, 6, 0);
        _plan.Click += (_, _) => { if (_person != null) PlanRequested?.Invoke(this, _person); };
        history.Controls.Add(_record);
        history.Controls.Add(_plan);
        _flow.Controls.Add(history);
        _othersOnly.Add(history);

        var buttons = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Panel, Margin = Theme.Pad(0, 6, 0, 0) };
        _askAi = Theme.MakeButton("✨ Ask AI about them", small: true);
        _askAi.Margin = Theme.Pad(0, 0, 6, 0);
        _askAi.Click += (_, _) => { if (_person != null) AskAiRequested?.Invoke(this, _person); };
        _delete = Theme.MakeButton("Remove", small: true);
        _delete.Click += (_, _) => { if (_person != null) DeleteRequested?.Invoke(this, _person); };
        buttons.Controls.Add(_askAi);
        buttons.Controls.Add(_delete);
        _flow.Controls.Add(buttons);
        _othersOnly.Add(buttons);

        _flow.Resize += (_, _) => LayoutWidths();
        Show(null);
    }

    private TextBox AddText(string label, bool othersOnly = false)
    {
        var l = Theme.MakeLabel(label);
        var t = Theme.MakeTextBox();
        t.TextChanged += (_, _) => Commit();
        _flow.Controls.Add(l);
        _flow.Controls.Add(t);
        if (othersOnly) { _othersOnly.Add(l); _othersOnly.Add(t); }
        return t;
    }

    private (OpinionSlider, Label) AddSlider(string label, int min, int max, bool opinion, bool othersOnly)
    {
        var header = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Theme.Panel, Margin = Theme.Pad(0, 8, 0, 0) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var name = Theme.MakeLabel(label);
        name.Margin = Theme.Pad(0, 2, 0, 0);
        var value = Theme.MakeLabel("", Theme.BodyBold, Theme.Text);
        value.Margin = Theme.Pad(0, 2, 0, 0);
        header.Controls.Add(name, 0, 0);
        header.Controls.Add(value, 1, 0);

        var slider = new OpinionSlider { Minimum = min, Maximum = max, OpinionScale = opinion, BackColor = Theme.Panel };
        slider.ValueChanged += (_, _) => Commit();

        _flow.Controls.Add(header);
        _flow.Controls.Add(slider);
        if (othersOnly) { _othersOnly.Add(header); _othersOnly.Add(slider); }
        return (slider, value);
    }

    private void LayoutWidths()
    {
        int w = _flow.ClientSize.Width - Theme.Px(8) - (_flow.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0);
        if (w < Theme.Px(60)) return;
        foreach (Control c in _flow.Controls)
            if (c is TextBox or OpinionSlider or TableLayoutPanel) c.Width = w;
    }

    public Person? Person => _person;

    public void Show(Person? p)
    {
        _person = p;
        _loading = true;
        _flow.Visible = p != null;
        _empty.Visible = p == null;
        if (p != null)
        {
            _title.Text = p.IsMe ? "YOU" : "PERSON";
            _first.Text = p.FirstName;
            _last.Text = p.LastName;
            _role.Text = p.Role;
            _notes.Text = p.Notes;
            _fieldInfluence.SetSilently(p.FieldInfluence);
            _ourInfluence.SetSilently(p.OurInfluence);
            _ourOpinion.SetSilently(p.OurOpinion);
            _theirOpinion.SetSilently(p.TheirOpinion);
            foreach (var c in _othersOnly) c.Visible = !p.IsMe;
            UpdateValueLabels();
            RefreshSummary();
            LayoutWidths();
        }
        _loading = false;
    }

    /// <summary>Updates the history/key-info summary after changes made in dialogs.</summary>
    public void RefreshSummary()
    {
        if (_person == null) return;
        _loading = true;
        _ourOpinion.SetSilently(_person.OurOpinion);
        _theirOpinion.SetSilently(_person.TheirOpinion);
        UpdateValueLabels();
        _loading = false;
        int events = _person.History.Count(h => h.Status == EventStatus.Done);
        _details.Text = $"Key info ({_person.Facts.Count})  &  history ({events})";
        int pending = _person.PendingCount;
        _pending.Visible = pending > 0;
        _pending.Text = pending == 1 ? "● 1 planned interaction awaiting its result - click to complete"
                                     : $"● {pending} planned interactions awaiting results - click to complete";
        _pending.MaximumSize = new Size(Math.Max(Theme.Px(100), _flow.ClientSize.Width - Theme.Px(12)), 0);
    }

    public void FocusName()
    {
        _first.Focus();
        _first.SelectAll();
    }

    private void UpdateValueLabels()
    {
        _fieldInfluenceValue.Text = _fieldInfluence.Value.ToString();
        _ourInfluenceValue.Text = $"{_ourInfluence.Value}%";
        _ourOpinionValue.Text = $"{OpinionPalette.LabelFor(_ourOpinion.Value)}  {_ourOpinion.Value:+#;-#;0}";
        _theirOpinionValue.Text = $"{OpinionPalette.LabelFor(_theirOpinion.Value)}  {_theirOpinion.Value:+#;-#;0}";
        _ourOpinionValue.ForeColor = OpinionPalette.Lighten(OpinionPalette.ColorFor(_ourOpinion.Value), 0.25f);
        _theirOpinionValue.ForeColor = OpinionPalette.Lighten(OpinionPalette.ColorFor(_theirOpinion.Value), 0.25f);
    }

    private void Commit()
    {
        if (_loading || _person == null) return;
        _person.FirstName = _first.Text;
        _person.LastName = _last.Text;
        _person.Role = _role.Text;
        _person.Notes = _notes.Text;
        _person.FieldInfluence = _fieldInfluence.Value;
        _person.OurInfluence = _ourInfluence.Value;
        _person.OurOpinion = _ourOpinion.Value;
        _person.TheirOpinion = _theirOpinion.Value;
        UpdateValueLabels();
        PersonChanged?.Invoke(this, EventArgs.Empty);
    }
}
