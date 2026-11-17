using System.Drawing.Drawing2D;
using RelationshipMaster.Models;
using RelationshipMaster.Services;

namespace RelationshipMaster.UI;

/// <summary>
/// Logs something that already happened and how it changed the relationship (both sides),
/// or edits an existing history entry (text, date and the relationship changes).
/// Enter makes a new line in the description; only the Done button saves.
/// </summary>
public sealed class RecordEventDialog : DarkDialog
{
    private readonly Person _person;
    private readonly RelationshipEvent? _existing;
    private readonly TextBox _title, _description;
    private readonly DateTimePicker _date;
    private readonly NumericUpDown _our, _their;
    private readonly Label _ourPreview, _theirPreview, _error;

    public RelationshipEvent? Result { get; private set; }

    public RecordEventDialog(Person person, RelationshipEvent? existing = null)
        : base(existing == null ? $"Record event - {person.FullName}" : $"Edit event - {person.FullName}", 500)
    {
        _person = person;
        _existing = existing;
        Body.Controls.Add(Theme.MakeLabel(existing == null ? $"What happened with {person.FirstName}?" : "Edit event",
            Theme.Header, Theme.Text));

        Body.Controls.Add(Theme.MakeLabel("Short title"));
        _title = Theme.MakeTextBox();
        _title.Width = ContentWidth;
        _title.PlaceholderText = "e.g. Argued about the project deadline";
        _title.Text = existing?.Title ?? "";
        Body.Controls.Add(_title);

        Body.Controls.Add(Theme.MakeLabel("Description - what happened and how it affected the relationship  (Enter = new line)"));
        _description = Theme.MakeTextBox(multiline: true);
        _description.Size = new Size(ContentWidth, Theme.Px(120));
        _description.Text = (existing?.Description ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
        Body.Controls.Add(_description);

        Body.Controls.Add(Theme.MakeLabel("When"));
        _date = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = existing?.Date ?? DateTime.Today, Width = Theme.Px(140) };
        Body.Controls.Add(_date);

        Body.Controls.Add(Theme.MakeLabel(existing == null
            ? "Effect on the relationship  (small numbers like -1 or +2 for everyday things)"
            : "Effect on the relationship  (changing it adjusts the current values by the difference)", Theme.BodyBold, Theme.Text));
        (_our, _ourPreview) = DeltaRow("My opinion of them", existing?.OurOpinionDelta ?? 0);
        (_their, _theirPreview) = DeltaRow("How they (supposedly) see me", existing?.TheirOpinionDelta ?? 0);

        _error = Theme.MakeLabel("", Theme.Small, Theme.Bad);
        Body.Controls.Add(_error);

        var done = AddButton("Done", DialogResult.None, primary: true);
        done.Click += (_, _) => Save();
        AddButton("Cancel", DialogResult.Cancel);
        AcceptButton = null; // Enter must never close the dialog - only "Done" (or Ctrl+Enter) saves.
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; Save(); } };
        UpdatePreviews();
    }

    private (NumericUpDown, Label) DeltaRow(string label, int value)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Theme.Pad(0, 6, 0, 0) };
        var l = Theme.MakeLabel(label, Theme.Body, Theme.Text);
        l.AutoSize = false;
        l.Size = Theme.Px(210, 26);
        l.TextAlign = ContentAlignment.MiddleLeft;
        l.Margin = Theme.Pad(0);
        var n = Theme.MakeNumber(-50, 50, value);
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
        // When editing, only the difference to the already-applied change moves the current value.
        Preview(_ourPreview, _person.OurOpinion, (int)_our.Value - (_existing?.OurOpinionDelta ?? 0));
        Preview(_theirPreview, _person.TheirOpinion, (int)_their.Value - (_existing?.TheirOpinionDelta ?? 0));
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
            _error.Text = "Write a title or a description first.";
            _title.Focus();
            return;
        }
        var title = _title.Text.Trim();
        if (title.Length == 0) title = Shorten(_description.Text, 60);
        var date = _date.Value.Date == DateTime.Today ? DateTime.Now : _date.Value.Date.AddHours(12);

        var ev = _existing ?? new RelationshipEvent { Kind = EventKind.Recorded, Status = EventStatus.Done };
        if (_existing != null && _existing.Date.Date == _date.Value.Date) date = _existing.Date; // keep the original time
        ev.Title = title;
        ev.Description = _description.Text.Trim();
        ev.Date = date;
        ApplyDeltaChange(_person, ev, (int)_our.Value, (int)_their.Value);
        if (_existing == null) _person.History.Add(ev);
        Result = ev;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// Sets an event's relationship changes to the requested values, applying only the difference to the person
    /// (and recording what was actually applied after clamping to -100..100).
    /// </summary>
    internal static void ApplyDeltaChange(Person p, RelationshipEvent ev, int newOur, int newTheir)
    {
        var (our, their) = p.ApplyDeltas(newOur - ev.OurOpinionDelta, newTheir - ev.TheirOpinionDelta);
        ev.OurOpinionDelta += our;
        ev.TheirOpinionDelta += their;
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
        var copyPrompt = Theme.MakeButton("📋 Prompt for my Claude…");
        copyPrompt.Margin = Theme.Pad(8, 0, 0, 0);
        copyPrompt.Click += (_, _) => PlanViaPrompt();
        genRow.Controls.Add(_generate);
        genRow.Controls.Add(copyPrompt);
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

    /// <summary>Builds the planning prompt for the user's own Claude chat; the pasted reply becomes the plan.</summary>
    private void PlanViaPrompt()
    {
        if (string.IsNullOrWhiteSpace(_situation.Text)) { _situation.Focus(); _status.Text = "Describe the situation first."; return; }
        var prompt = PromptBuilder.Plan(_field, _person, Goal, _situation.Text.Trim(), _intensity.Value);
        using var d = new ClaudePromptDialog($"Plan with your Claude - {_person.FullName}", prompt,
            "Paste Claude's plan here", _ => null);
        if (d.ShowDialog(this) == DialogResult.OK && d.Answer != null)
        {
            _plan.Text = d.Answer;
            _status.ForeColor = Theme.TextDim;
            _status.Text = "Plan pasted. Edit it if you like, then save it.";
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
    private readonly TextBox? _titleBox;
    private readonly RichTextBox? _planBox;
    private readonly SuccessBar _bar;
    private readonly Label _assessment, _status, _ourPreview, _theirPreview;
    private readonly NumericUpDown _our, _their, _success;
    private readonly Button? _evaluate;
    private CancellationTokenSource? _cts;

    public OutcomeDialog(Field field, Person person, RelationshipEvent ev, RelationshipAdvisor advisor, Func<bool> ensureAi)
        : base(ev.Status == EventStatus.Pending ? $"Result - {person.FullName}" : $"Event - {person.FullName}", 780, 900)
    {
        _field = field; _person = person; _ev = ev; _advisor = advisor; _ensureAi = ensureAi;
        bool pending = ev.Status == EventStatus.Pending;
        bool editable = ev.Status != EventStatus.Cancelled; // pending: enter the result; done: edit it later
        bool planned = ev.Kind == EventKind.Planned;

        var header = Row(Theme.MakeLabel(pending ? $"Enter the result - {person.FullName}" : $"{(editable ? "Edit event" : "Event")} - {person.FullName}",
            Theme.Header, Theme.Text));
        header.MaximumSize = new Size(Theme.Px(720), 0);
        if (editable)
        {
            Row(Theme.MakeLabel("Title"));
            _titleBox = Row(Theme.MakeTextBox());
            _titleBox.Text = ev.Title;
        }
        else
        {
            var title = Row(Theme.MakeLabel(ev.Title, Theme.BodyBold, Theme.Text));
            title.MaximumSize = new Size(Theme.Px(720), 0);
        }
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
            if (!string.IsNullOrWhiteSpace(ev.AiPlan) || editable)
            {
                Row(Theme.MakeLabel(editable ? "Plan (editable)" : "Plan"));
                _planBox = Theme.MakeRichText(readOnly: !editable);
                _planBox.Text = ev.AiPlan;
                Row(Boxed(_planBox), 150);
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
            Row(Theme.MakeLabel(editable ? "What happened? Tell the story - what you did, how they reacted…  (Enter = new line)" : "What happened"));
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
            var copy = Theme.MakeButton("📋 Prompt for my Claude…");
            copy.Margin = Theme.Pad(8, 0, 0, 0);
            copy.Click += (_, _) => EvaluateViaPrompt();
            evalRow.Controls.Add(copy);
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
        Row(Theme.MakeLabel(pending ? "Result to apply (adjust if you disagree)"
            : editable ? "Effect on the relationship (changing it adjusts the current values by the difference)"
            : "Effect on the relationship", Theme.BodyBold, Theme.Text));
        Row(grid);

        foreach (var n in new[] { _success, _our, _their }) n.Enabled = editable;
        _success.ValueChanged += (_, _) => _bar.Value = (int)_success.Value;
        _our.ValueChanged += (_, _) => UpdatePreviews();
        _their.ValueChanged += (_, _) => UpdatePreviews();
        UpdatePreviews();

        if (pending)
        {
            var apply = AddButton("Done - apply result", primary: true);
            apply.Click += (_, _) => Apply();
            var cancelEvent = AddButton("Cancel this event");
            cancelEvent.Click += (_, _) => CancelEvent();
        }
        else if (editable)
        {
            var save = AddButton("Save changes", primary: true);
            save.Click += (_, _) => Apply();
        }
        var close = AddButton(pending ? "Later" : editable ? "Cancel" : "Close");
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
        if (_ev.Status == EventStatus.Cancelled)
        {
            _ourPreview.Text = _theirPreview.Text = "cancelled - no effect";
            return;
        }
        // Only the difference to what is already applied moves the current values (pending events have 0 applied).
        RecordEventDialog.Preview(_ourPreview, _person.OurOpinion, (int)_our.Value - _ev.OurOpinionDelta);
        RecordEventDialog.Preview(_theirPreview, _person.TheirOpinion, (int)_their.Value - _ev.TheirOpinionDelta);
    }

    /// <summary>Copy a prompt into the user's own Claude chat and paste the JSON answer back.</summary>
    private void EvaluateViaPrompt()
    {
        if (string.IsNullOrWhiteSpace(_story.Text)) { _story.Focus(); _status.Text = "Write what happened first."; return; }
        var evForPrompt = new RelationshipEvent
        {
            Goal = _ev.Goal, Intensity = _ev.Intensity, Situation = _ev.Situation,
            AiPlan = _planBox?.Text ?? _ev.AiPlan,
        };
        var prompt = PromptBuilder.Outcome(_field, _person, evForPrompt, _story.Text.Trim());
        using var d = new ClaudePromptDialog("Evaluate the result with your Claude", prompt,
            "Paste Claude's reply (it contains a small JSON block with the scores)", answer =>
            {
                try
                {
                    using var doc = PromptBuilder.ExtractJson(answer);
                    var result = RelationshipAdvisor.OutcomeFromJson(doc.RootElement, _ev.Intensity);
                    ShowAssessment(result);
                    return null;
                }
                catch (Exception ex) when (ex is FormatException or KeyNotFoundException or InvalidOperationException)
                {
                    return ex is FormatException ? ex.Message : "The JSON is missing successPercent / ourOpinionDelta / theirOpinionDelta.";
                }
            });
        d.ShowDialog(this);
    }

    private void ShowAssessment(OutcomeAssessment result)
    {
        _success.Value = result.SuccessPercent;
        _our.Value = result.OurOpinionDelta;
        _their.Value = result.TheirOpinionDelta;
        _assessment.Text = result.Assessment;
        _status.ForeColor = Theme.TextDim;
        _status.Text = "Review the numbers, then click Done.";
    }

    /// <summary>Keeps a half-written story when the user closes and comes back later.</summary>
    private bool _saved;

    private void SaveDraft()
    {
        if (_saved || _ev.Status != EventStatus.Pending) return;
        _ev.ResultStory = _story.Text;
        _ev.AiAssessment = _assessment.Text;
        if (_planBox != null) _ev.AiPlan = _planBox.Text;
        if (_titleBox != null && !string.IsNullOrWhiteSpace(_titleBox.Text)) _ev.Title = _titleBox.Text.Trim();
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
            var evForAi = new RelationshipEvent
            {
                Goal = _ev.Goal, Intensity = _ev.Intensity, Situation = _ev.Situation,
                AiPlan = _planBox?.Text ?? _ev.AiPlan,
            };
            var result = await _advisor.EvaluateOutcomeAsync(_field, _person, evForAi, _story.Text.Trim(), _cts.Token);
            if (IsDisposed) return;
            ShowAssessment(result);
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
        if (_titleBox != null && !string.IsNullOrWhiteSpace(_titleBox.Text)) _ev.Title = _titleBox.Text.Trim();
        if (_planBox != null) _ev.AiPlan = _planBox.Text.Trim();
        if (_ev.Kind == EventKind.Planned) _ev.SuccessPercent = (int)_success.Value;
        _ev.AiAssessment = _assessment.Text;
        RecordEventDialog.ApplyDeltaChange(_person, _ev, (int)_our.Value, (int)_their.Value);
        if (_ev.Status == EventStatus.Pending)
        {
            _ev.Status = EventStatus.Done;
            _ev.CompletedAt = DateTime.Now;
        }
        if (string.IsNullOrWhiteSpace(_ev.Description) || _ev.Kind == EventKind.Planned)
            _ev.Description = RecordEventDialog.Shorten(_ev.ResultStory, 300);
        _saved = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelEvent()
    {
        if (MessageBox.Show(this, "Mark this planned interaction as cancelled? The relationship stays unchanged.",
                "Cancel event", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _ev.ResultStory = _story.Text.Trim();
        _saved = true;
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
