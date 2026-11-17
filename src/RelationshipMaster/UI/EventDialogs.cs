using System.Drawing.Drawing2D;
using RelationshipMaster.Models;
using RelationshipMaster.Services;

namespace RelationshipMaster.UI;

/// <summary>Logs something that already happened and how it changed the relationship (both sides).</summary>
public sealed class RecordEventDialog : DarkDialog
{
    private readonly Person _person;
    private readonly TextBox _title, _description;
    private readonly DateTimePicker _date;
    private readonly NumericUpDown _our, _their;
    private readonly Label _ourPreview, _theirPreview;

    public RelationshipEvent? Result { get; private set; }

    public RecordEventDialog(Person person) : base($"Record event - {person.FullName}", 480)
    {
        _person = person;
        Body.Controls.Add(Theme.MakeLabel($"What happened with {person.FirstName}?", Theme.Header, Theme.Text));

        Body.Controls.Add(Theme.MakeLabel("Short title"));
        _title = Theme.MakeTextBox();
        _title.Width = ContentWidth;
        _title.PlaceholderText = "e.g. Argued about the project deadline";
        Body.Controls.Add(_title);

        Body.Controls.Add(Theme.MakeLabel("Description - what happened and how it affected the relationship"));
        _description = Theme.MakeTextBox(multiline: true);
        _description.Size = new Size(ContentWidth, Theme.Px(100));
        Body.Controls.Add(_description);

        Body.Controls.Add(Theme.MakeLabel("When"));
        _date = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today, Width = Theme.Px(140) };
        Body.Controls.Add(_date);

        Body.Controls.Add(Theme.MakeLabel("Effect on the relationship  (small numbers like -1 or +2 for everyday things)", Theme.BodyBold, Theme.Text));
        (_our, _ourPreview) = DeltaRow("My opinion of them", person.OurOpinion);
        (_their, _theirPreview) = DeltaRow("How they (supposedly) see me", person.TheirOpinion);

        var ok = AddButton("Save to history", DialogResult.None, primary: true);
        ok.Click += (_, _) => Save();
        AddButton("Cancel", DialogResult.Cancel);
        UpdatePreviews();
    }

    private (NumericUpDown, Label) DeltaRow(string label, int current)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Theme.Pad(0, 6, 0, 0) };
        var l = Theme.MakeLabel(label, Theme.Body, Theme.Text);
        l.AutoSize = false;
        l.Size = Theme.Px(210, 26);
        l.TextAlign = ContentAlignment.MiddleLeft;
        l.Margin = Theme.Pad(0);
        var n = Theme.MakeNumber(-50, 50);
        n.Margin = Theme.Pad(0, 0, 10, 0);
        var preview = Theme.MakeLabel("", Theme.Small);
        preview.Margin = Theme.Pad(0, 6, 0, 0);
        n.ValueChanged += (_, _) => UpdatePreviews();
        row.Controls.Add(l);
        row.Controls.Add(n);
        row.Controls.Add(preview);
        Body.Controls.Add(row);
        return (n, preview);
    }

    private void UpdatePreviews()
    {
        Preview(_ourPreview, _person.OurOpinion, (int)_our.Value);
        Preview(_theirPreview, _person.TheirOpinion, (int)_their.Value);
    }

    internal static void Preview(Label l, int current, int delta)
    {
        int next = Math.Clamp(current + delta, -100, 100);
        l.Text = delta == 0
            ? $"stays {OpinionPalette.LabelFor(current)} ({Theme.Signed(current)})"
            : $"{Theme.Signed(current)} → {Theme.Signed(next)}  ({OpinionPalette.LabelFor(next)})";
        l.ForeColor = delta > 0 ? Theme.Good : delta < 0 ? Theme.Bad : Theme.TextDim;
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_title.Text) && string.IsNullOrWhiteSpace(_description.Text))
        {
            _title.Focus();
            return;
        }
        var title = _title.Text.Trim();
        if (title.Length == 0) title = Shorten(_description.Text, 60);
        var ev = new RelationshipEvent
        {
            Kind = EventKind.Recorded,
            Status = EventStatus.Done,
            Date = _date.Value.Date == DateTime.Today ? DateTime.Now : _date.Value.Date.AddHours(12),
            Title = title,
            Description = _description.Text.Trim(),
        };
        (ev.OurOpinionDelta, ev.TheirOpinionDelta) = _person.ApplyDeltas((int)_our.Value, (int)_their.Value);
        _person.History.Add(ev);
        Result = ev;
        DialogResult = DialogResult.OK;
        Close();
    }

    internal static string Shorten(string s, int max)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
    }
}

/// <summary>A resizable dark dialog with a single-column table layout.</summary>
public class DarkSizableDialog : Form
{
    protected readonly TableLayoutPanel Table;
    protected readonly FlowLayoutPanel Buttons;

    public DarkSizableDialog(string title, int width, int height)
    {
        Text = title;
        Theme.StyleForm(this);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(Theme.Px(width), wa.Width - 40), Math.Min(Theme.Px(height), wa.Height - 40));
        MinimumSize = new Size(Math.Min(Theme.Px(520), wa.Width - 40), Math.Min(Theme.Px(480), wa.Height - 40));
        Padding = Theme.Pad(18, 14, 18, 14);

        Table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, BackColor = Theme.Panel };
        Table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = Theme.Pad(0, 10, 0, 0), BackColor = Theme.Panel };
        Controls.Add(Table);
        Controls.Add(Buttons);
    }

    protected T Row<T>(T c, int height = 0) where T : Control
    {
        Table.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, Theme.Px(height)) : new RowStyle(SizeType.AutoSize));
        c.Dock = height > 0 || c is not Label ? DockStyle.Fill : DockStyle.None;
        Table.Controls.Add(c, 0, Table.RowCount++);
        return c;
    }

    /// <summary>
    /// A row that takes the dialog's spare height (at least <paramref name="minHeight"/>). Rows are sized
    /// explicitly so the dialog scrolls instead of squashing on small or high-DPI screens.
    /// </summary>
    protected Control FillRow(Control c, int minHeight)
    {
        var style = new RowStyle(SizeType.Absolute, Theme.Px(minHeight));
        Table.RowStyles.Add(style);
        c.Dock = DockStyle.Fill;
        Table.Controls.Add(c, 0, Table.RowCount++);
        _fill.Add((style, Theme.Px(minHeight)));
        return c;
    }

    private readonly List<(RowStyle style, int min)> _fill = new();

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Table.Resize += (_, _) => DistributeSpare();
        DistributeSpare();
    }

    private void DistributeSpare()
    {
        if (_fill.Count == 0) return;
        foreach (var (style, min) in _fill) style.Height = min;
        Table.PerformLayout();
        int used = Table.GetRowHeights().Sum();
        int spare = Table.ClientSize.Height - used - Theme.Px(4);
        if (spare <= 0) return;
        foreach (var (style, min) in _fill) style.Height = min + spare / _fill.Count;
    }

    protected static Control Boxed(Control inner)
    {
        var host = new Panel { Padding = Theme.Pad(8), BackColor = Theme.PanelAlt };
        inner.Dock = DockStyle.Fill;
        host.Controls.Add(inner);
        return host;
    }

    protected Button AddButton(string text, bool primary = false)
    {
        var b = Theme.MakeButton(text, primary);
        b.Margin = Theme.Pad(6, 0, 0, 0);
        Buttons.Controls.Add(b);
        return b;
    }

    protected static void AppendStream(RichTextBox box, string text)
    {
        box.SelectionStart = box.TextLength;
        box.SelectionLength = 0;
        box.SelectionColor = Theme.Text;
        box.AppendText(text);
        box.ScrollToCaret();
    }
}

/// <summary>
/// Plans an interaction with a person: the user describes the situation and how much it should move the
/// relationship; the AI builds a plan from the person's key info. Saved as a pending event awaiting the result.
/// </summary>
public sealed class PlanInteractionDialog : DarkSizableDialog
{
    private readonly Field _field;
    private readonly Person _person;
    private readonly RelationshipAdvisor _advisor;
    private readonly Func<bool> _ensureAi;
    private readonly TextBox _title, _situation;
    private readonly ComboBox _goal;
    private readonly OpinionSlider _intensity;
    private readonly Label _intensityLabel, _status;
    private readonly RichTextBox _plan;
    private readonly Button _generate;
    private CancellationTokenSource? _cts;

    public RelationshipEvent? Result { get; private set; }

    public PlanInteractionDialog(Field field, Person person, RelationshipAdvisor advisor, Func<bool> ensureAi)
        : base($"Plan interaction - {person.FullName}", 780, 820)
    {
        _field = field; _person = person; _advisor = advisor; _ensureAi = ensureAi;

        Row(Theme.MakeLabel($"✨ Plan an interaction with {person.FullName}", Theme.Header, Theme.Text));
        var facts = person.Facts.Count == 0
            ? "Tip: add key info about them (likes, skills, quirks, weaknesses) first - the AI builds the plan on it."
            : $"The AI will use {person.Facts.Count} recorded fact(s) and {person.History.Count(h => h.Status == EventStatus.Done)} history entries about {person.FirstName}.";
        var tip = Row(Theme.MakeLabel(facts, Theme.Small));
        tip.MaximumSize = new Size(Theme.Px(720), 0);

        Row(Theme.MakeLabel("Goal"));
        _goal = Row(Theme.MakeCombo());
        foreach (var g in Enum.GetValues<InteractionGoal>()) _goal.Items.Add(new GoalItem(g));
        _goal.SelectedIndex = person.OurOpinion < -20 || person.TheirOpinion < -40 ? 1 : 0;

        Row(Theme.MakeLabel("Describe the situation - what do you want to achieve?"));
        _situation = Theme.MakeTextBox(multiline: true);
        _situation.PlaceholderText = person.OurOpinion < -20
            ? $"e.g. {person.FirstName} and I both want the team-lead promotion. Decision is in 3 weeks…"
            : $"e.g. {person.FirstName} is neutral towards me and I'd like us to get closer…";
        Row(_situation, 100);

        var intensityHeader = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, BackColor = Theme.Panel };
        intensityHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        intensityHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        intensityHeader.Controls.Add(Theme.MakeLabel("How much should this interaction affect the relationship?"), 0, 0);
        _intensityLabel = Theme.MakeLabel("", Theme.BodyBold, Theme.Text);
        intensityHeader.Controls.Add(_intensityLabel, 1, 0);
        Row(intensityHeader);
        _intensity = new OpinionSlider { Minimum = 0, Maximum = 30, BackColor = Theme.Panel };
        _intensity.SetSilently(5);
        _intensity.ValueChanged += (_, _) => UpdateIntensity();
        Row(_intensity, 30);

        var genRow = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Panel, Margin = Theme.Pad(0, 10, 0, 4) };
        _generate = Theme.MakeButton("✨ Generate plan with AI", primary: true);
        _generate.Click += async (_, _) => await GenerateAsync();
        _status = Theme.MakeLabel("", Theme.Small);
        _status.Margin = Theme.Pad(10, 9, 0, 0);
        genRow.Controls.Add(_generate);
        genRow.Controls.Add(_status);
        Row(genRow);

        Row(Theme.MakeLabel("Plan (editable - write your own if you prefer)"));
        _plan = Theme.MakeRichText(readOnly: false);
        FillRow(Boxed(_plan), 160);

        Row(Theme.MakeLabel("Title in history"));
        _title = Row(Theme.MakeTextBox());
        _title.PlaceholderText = "optional - generated from the situation";

        var save = AddButton("Save - awaiting result", primary: true);
        save.Click += (_, _) => Save();
        var cancel = AddButton("Cancel");
        cancel.Click += (_, _) => Close();
        CancelButton = cancel;
        FormClosing += (_, _) => _cts?.Cancel();
        UpdateIntensity();
    }

    private InteractionGoal Goal => ((GoalItem)_goal.SelectedItem!).Goal;

    private void UpdateIntensity()
    {
        int v = _intensity.Value;
        _intensityLabel.Text = v == 0 ? "0 - doesn't matter" : $"±{v} points - {RelationshipEvent.IntensityLabel(v)}";
    }

    private async Task GenerateAsync()
    {
        if (_cts != null) { _cts.Cancel(); return; }
        if (string.IsNullOrWhiteSpace(_situation.Text)) { _situation.Focus(); _status.Text = "Describe the situation first."; return; }
        if (!_ensureAi()) return;

        _cts = new CancellationTokenSource();
        _plan.Clear();
        _generate.Text = "Stop";
        _status.ForeColor = Theme.TextDim;
        _status.Text = "Thinking about the best approach…";
        try
        {
            await _advisor.PlanInteractionAsync(_field, _person, Goal, _situation.Text.Trim(), _intensity.Value,
                chunk =>
                {
                    if (IsDisposed) return;
                    if (InvokeRequired) BeginInvoke(() => AppendStream(_plan, chunk));
                    else AppendStream(_plan, chunk);
                }, _cts.Token);
            _status.Text = "Done. Edit the plan if you like, then save it.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed) { _status.ForeColor = Theme.Bad; _status.Text = RelationshipAdvisor.Describe(ex); }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            if (!IsDisposed) _generate.Text = "✨ Generate plan with AI";
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_situation.Text)) { _situation.Focus(); _status.Text = "Describe the situation first."; return; }
        var title = _title.Text.Trim();
        if (title.Length == 0)
        {
            var prefix = Goal switch
            {
                InteractionGoal.WinOver => "Win over",
                InteractionGoal.Challenge => "Challenge",
                InteractionGoal.Repair => "Repair",
                _ => "Plan",
            };
            title = $"{prefix}: {RecordEventDialog.Shorten(_situation.Text, 55)}";
        }
        Result = new RelationshipEvent
        {
            Kind = EventKind.Planned,
            Status = EventStatus.Pending,
            Date = DateTime.Now,
            Title = title,
            Goal = Goal,
            Situation = _situation.Text.Trim(),
            Intensity = _intensity.Value,
            AiPlan = _plan.Text.Trim(),
        };
        _person.History.Add(Result);
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record GoalItem(InteractionGoal Goal)
    {
        public override string ToString() => RelationshipEvent.GoalLabel(Goal);
    }
}

/// <summary>
/// Completes a planned interaction: the user tells the story of what happened, the AI rates the success and
/// proposes opinion changes (bounded by the intensity), and the result is applied to the relationship.
/// Opens read-only for finished events.
/// </summary>
public sealed class OutcomeDialog : DarkSizableDialog
{
    private readonly Field _field;
    private readonly Person _person;
    private readonly RelationshipEvent _ev;
    private readonly RelationshipAdvisor _advisor;
    private readonly Func<bool> _ensureAi;
    private readonly TextBox _story;
    private readonly SuccessBar _bar;
    private readonly Label _assessment, _status, _ourPreview, _theirPreview;
    private readonly NumericUpDown _our, _their, _success;
    private readonly Button? _evaluate;
    private CancellationTokenSource? _cts;

    public OutcomeDialog(Field field, Person person, RelationshipEvent ev, RelationshipAdvisor advisor, Func<bool> ensureAi)
        : base(ev.Status == EventStatus.Pending ? $"Result - {person.FullName}" : $"Event - {person.FullName}", 780, 860)
    {
        _field = field; _person = person; _ev = ev; _advisor = advisor; _ensureAi = ensureAi;
        bool editable = ev.Status == EventStatus.Pending;
        bool planned = ev.Kind == EventKind.Planned;

        var title = Row(Theme.MakeLabel(ev.Title, Theme.Header, Theme.Text));
        title.MaximumSize = new Size(Theme.Px(720), 0);
        var meta = planned
            ? $"{RelationshipEvent.GoalLabel(ev.Goal)}  ·  intensity ±{ev.Intensity} ({RelationshipEvent.IntensityLabel(ev.Intensity)})  ·  planned {ev.Date:g}"
            : $"Recorded event  ·  {ev.Date:g}";
        if (ev.Status == EventStatus.Cancelled) meta += "  ·  CANCELLED";
        Row(Theme.MakeLabel(meta, Theme.Small));

        if (planned)
        {
            Row(Theme.MakeLabel("Situation"));
            var situation = Row(Theme.MakeLabel(ev.Situation, Theme.Body, Theme.Text));
            situation.MaximumSize = new Size(Theme.Px(720), 0);
            if (!string.IsNullOrWhiteSpace(ev.AiPlan))
            {
                Row(Theme.MakeLabel("Plan"));
                var plan = Theme.MakeRichText(readOnly: true);
                plan.Text = ev.AiPlan;
                Row(Boxed(plan), editable ? 150 : 200);
            }
        }
        else if (!string.IsNullOrWhiteSpace(ev.Description))
        {
            var d = Row(Theme.MakeLabel(ev.Description, Theme.Body, Theme.Text));
            d.MaximumSize = new Size(Theme.Px(720), 0);
        }

        _story = Theme.MakeTextBox(multiline: true);
        _story.Text = ev.ResultStory;
        _story.ReadOnly = !editable;
        if (planned)
        {
            Row(Theme.MakeLabel(editable ? "What happened? Tell the story - what you did, how they reacted…" : "What happened"));
            _story.PlaceholderText = "e.g. I brought up Great Danes at lunch, she showed me 20 photos of Bruno and invited me to the dog park on Sunday…";
            FillRow(_story, 110);
        }

        var evalRow = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Panel, Margin = Theme.Pad(0, 10, 0, 4) };
        _status = Theme.MakeLabel("", Theme.Small);
        _status.Margin = Theme.Pad(10, 9, 0, 0);
        if (editable && planned)
        {
            _evaluate = Theme.MakeButton("✨ Evaluate with AI", primary: true);
            _evaluate.Click += async (_, _) => await EvaluateAsync();
            evalRow.Controls.Add(_evaluate);
        }
        evalRow.Controls.Add(_status);
        Row(evalRow);

        _bar = new SuccessBar { Value = ev.SuccessPercent };
        if (planned) Row(_bar, 46);
        _assessment = Row(Theme.MakeLabel(ev.AiAssessment, Theme.Body, Theme.Text));
        _assessment.MaximumSize = new Size(Theme.Px(720), 0);

        var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, BackColor = Theme.Panel, Margin = Theme.Pad(0, 8, 0, 0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _success = Theme.MakeNumber(0, 100, ev.SuccessPercent ?? 50);
        _our = Theme.MakeNumber(-50, 50, ev.OurOpinionDelta);
        _their = Theme.MakeNumber(-50, 50, ev.TheirOpinionDelta);
        _ourPreview = Theme.MakeLabel("", Theme.Small);
        _theirPreview = Theme.MakeLabel("", Theme.Small);
        int r = 0;
        if (planned) AddGridRow(grid, r++, "Success %", _success, Theme.MakeLabel("", Theme.Small));
        AddGridRow(grid, r++, "My opinion of them", _our, _ourPreview);
        AddGridRow(grid, r, "How they (supposedly) see me", _their, _theirPreview);
        Row(Theme.MakeLabel(editable ? "Result to apply (adjust if you disagree)" : "Effect on the relationship", Theme.BodyBold, Theme.Text));
        Row(grid);

        foreach (var n in new[] { _success, _our, _their }) n.Enabled = editable;
        _success.ValueChanged += (_, _) => _bar.Value = (int)_success.Value;
        _our.ValueChanged += (_, _) => UpdatePreviews();
        _their.ValueChanged += (_, _) => UpdatePreviews();
        UpdatePreviews();

        if (editable)
        {
            var apply = AddButton("Apply result", primary: true);
            apply.Click += (_, _) => Apply();
            var cancelEvent = AddButton("Cancel this event");
            cancelEvent.Click += (_, _) => CancelEvent();
        }
        var close = AddButton(editable ? "Later" : "Close");
        close.Click += (_, _) => { SaveDraft(); Close(); };
        CancelButton = close;
        FormClosing += (_, _) => { _cts?.Cancel(); SaveDraft(); };
    }

    private static void AddGridRow(TableLayoutPanel grid, int row, string label, Control number, Label preview)
    {
        var l = Theme.MakeLabel(label, Theme.Body, Theme.Text);
        l.Margin = Theme.Pad(0, 8, 14, 0);
        number.Margin = Theme.Pad(0, 4, 10, 4);
        preview.Margin = Theme.Pad(0, 8, 0, 0);
        grid.Controls.Add(l, 0, row);
        grid.Controls.Add(number, 1, row);
        grid.Controls.Add(preview, 2, row);
    }

    private void UpdatePreviews()
    {
        if (_ev.Status != EventStatus.Pending)
        {
            _ourPreview.Text = _theirPreview.Text = "applied";
            return;
        }
        RecordEventDialog.Preview(_ourPreview, _person.OurOpinion, (int)_our.Value);
        RecordEventDialog.Preview(_theirPreview, _person.TheirOpinion, (int)_their.Value);
    }

    /// <summary>Keeps a half-written story when the user closes and comes back later.</summary>
    private void SaveDraft()
    {
        if (_ev.Status == EventStatus.Pending) _ev.ResultStory = _story.Text;
    }

    private async Task EvaluateAsync()
    {
        if (_cts != null) { _cts.Cancel(); return; }
        if (string.IsNullOrWhiteSpace(_story.Text)) { _story.Focus(); _status.Text = "Write what happened first."; return; }
        if (!_ensureAi()) return;

        _cts = new CancellationTokenSource();
        _evaluate!.Text = "Stop";
        _status.ForeColor = Theme.TextDim;
        _status.Text = "Evaluating the outcome…";
        try
        {
            var result = await _advisor.EvaluateOutcomeAsync(_field, _person, _ev, _story.Text.Trim(), _cts.Token);
            if (IsDisposed) return;
            _success.Value = result.SuccessPercent;
            _our.Value = result.OurOpinionDelta;
            _their.Value = result.TheirOpinionDelta;
            _assessment.Text = result.Assessment;
            _ev.AiAssessment = result.Assessment;
            _status.Text = "Review the numbers, then apply the result.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed) { _status.ForeColor = Theme.Bad; _status.Text = RelationshipAdvisor.Describe(ex); }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            if (!IsDisposed) _evaluate.Text = "✨ Evaluate with AI";
        }
    }

    private void Apply()
    {
        if (_ev.Kind == EventKind.Planned && string.IsNullOrWhiteSpace(_story.Text))
        {
            _story.Focus();
            _status.Text = "Write what happened first.";
            return;
        }
        _ev.ResultStory = _story.Text.Trim();
        _ev.SuccessPercent = (int)_success.Value;
        (_ev.OurOpinionDelta, _ev.TheirOpinionDelta) = _person.ApplyDeltas((int)_our.Value, (int)_their.Value);
        _ev.Status = EventStatus.Done;
        _ev.CompletedAt = DateTime.Now;
        if (string.IsNullOrWhiteSpace(_ev.Description)) _ev.Description = RecordEventDialog.Shorten(_ev.ResultStory, 300);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelEvent()
    {
        if (MessageBox.Show(this, "Mark this planned interaction as cancelled? The relationship stays unchanged.",
                "Cancel event", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _ev.ResultStory = _story.Text.Trim();
        _ev.Status = EventStatus.Cancelled;
        _ev.OurOpinionDelta = _ev.TheirOpinionDelta = 0;
        _ev.CompletedAt = DateTime.Now;
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>Big horizontal success meter (red → amber → green).</summary>
public sealed class SuccessBar : Control
{
    private int? _value;

    public int? Value
    {
        get => _value;
        set { _value = value is int v ? Math.Clamp(v, 0, 100) : null; Invalidate(); }
    }

    public SuccessBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Panel;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        string label = _value is int v ? $"{v}%" : "– %";
        var ls = g.MeasureString(label, Theme.Big);
        using (var tb = new SolidBrush(_value is int vv ? ColorFor(vv) : Theme.TextDim))
            g.DrawString(label, Theme.Big, tb, 0, (Height - ls.Height) / 2);

        float x = Math.Max(ls.Width, Theme.Px(90f)) + Theme.Px(10f);
        var track = new RectangleF(x, Height / 2f - Theme.Px(6f), Math.Max(10, Width - x - Theme.Px(4f)), Theme.Px(12f));
        using (var path = OpinionSlider.Rounded(track, track.Height / 2))
        using (var bg = new SolidBrush(Theme.Border))
            g.FillPath(bg, path);
        if (_value is int p && p > 0)
        {
            var fill = new RectangleF(track.X, track.Y, Math.Max(track.Height, track.Width * p / 100f), track.Height);
            using var path = OpinionSlider.Rounded(fill, track.Height / 2);
            using var b = new SolidBrush(ColorFor(p));
            g.FillPath(b, path);
        }
        using var hint = new SolidBrush(Theme.TextDim);
        g.DrawString("success", Theme.Small, hint, x, track.Bottom + Theme.Px(2f));
    }

    private static Color ColorFor(int p) => p < 50
        ? OpinionPalette.Lerp(Theme.Bad, Color.FromArgb(240, 190, 70), p / 50f)
        : OpinionPalette.Lerp(Color.FromArgb(240, 190, 70), Theme.Good, (p - 50) / 50f);
}
