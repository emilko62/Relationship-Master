using RelationshipMaster.Models;
using RelationshipMaster.Services;

namespace RelationshipMaster.UI;

/// <summary>Chat with the AI relationship strategist about the current field.</summary>
public sealed class AdvisorPanel : Panel
{
    private readonly RelationshipAdvisor _advisor;
    private readonly RichTextBox _output;
    private readonly TextBox _input;
    private readonly Button _send, _newChat;
    private readonly Label _status;
    private readonly FlowLayoutPanel _quick;
    private CancellationTokenSource? _cts;

    public Func<Field?>? FieldProvider { get; set; }
    public Func<Person?>? SelectionProvider { get; set; }
    public event EventHandler? SettingsRequested;

    public AdvisorPanel(RelationshipAdvisor advisor)
    {
        _advisor = advisor;
        BackColor = Theme.Panel;
        Padding = new Padding(16, 8, 12, 12);

        var header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.Panel };
        var title = Theme.MakeLabel("✨ AI ADVISOR", Theme.Header, Theme.Text);
        title.Location = new Point(0, 4);
        header.Controls.Add(title);
        _newChat = Theme.MakeButton("New chat");
        _newChat.Height = 26;
        _newChat.Font = Theme.Small;
        _newChat.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _newChat.Click += (_, _) => NewChat();
        header.Controls.Add(_newChat);
        header.Resize += (_, _) => _newChat.Location = new Point(header.Width - _newChat.Width, 2);

        _status = Theme.MakeLabel("", Theme.Small);
        _status.AutoSize = false;
        _status.Height = 22;
        _status.Dock = DockStyle.Top;
        _status.Cursor = Cursors.Hand;
        _status.Click += (_, _) => { if (!_advisor.IsConfigured) SettingsRequested?.Invoke(this, EventArgs.Empty); };

        _quick = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, BackColor = Theme.Panel, Padding = new Padding(0, 4, 0, 4) };
        AddQuick("Analyze field", "Give me a strategic read of this field: who matters most, where I stand, and the top 3 moves I should make.");
        AddQuick("Blind spots", "Where are the biggest gaps between how I see people and how they supposedly see me? What might I be missing, and how can I check?");
        AddQuick("Advise on selected", null);
        AddQuick("Who to invest in", "Which relationships here would give me the most benefit if I invested time in them, and how should I approach each one?");

        _output = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.PanelAlt,
            ForeColor = Theme.Text,
            Font = Theme.Body,
            DetectUrls = false,
        };
        var outputHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Theme.PanelAlt };
        outputHost.Controls.Add(_output);

        var inputRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 64, ColumnCount = 2, BackColor = Theme.Panel, Padding = new Padding(0, 8, 0, 0) };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _input = Theme.MakeTextBox(multiline: true);
        _input.Dock = DockStyle.Fill;
        _input.PlaceholderText = "Ask about this field…  (Ctrl+Enter to send)";
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; _ = SendAsync(_input.Text); }
        };
        _send = Theme.MakeButton("Send", primary: true);
        _send.Dock = DockStyle.Fill;
        _send.Margin = new Padding(6, 0, 0, 0);
        _send.Click += (_, _) =>
        {
            if (_cts != null) _cts.Cancel();
            else _ = SendAsync(_input.Text);
        };
        inputRow.Controls.Add(_input, 0, 0);
        inputRow.Controls.Add(_send, 1, 0);

        Controls.Add(outputHost);
        Controls.Add(inputRow);
        Controls.Add(_quick);
        Controls.Add(_status);
        Controls.Add(header);

        RefreshStatus();
        AppendSystem("Ask me anything about the people in this field. I see the current map - sizes, colours, " +
                     "influence and how people supposedly see you - every time you ask.");
    }

    private void AddQuick(string label, string? prompt)
    {
        var b = Theme.MakeButton(label);
        b.Height = 26;
        b.Font = Theme.Small;
        b.Margin = new Padding(0, 0, 6, 6);
        b.Click += (_, _) =>
        {
            if (prompt != null) { _ = SendAsync(prompt); return; }
            var sel = SelectionProvider?.Invoke();
            if (sel == null || sel.IsMe) { AppendSystem("Select a person on the canvas first."); return; }
            AskAbout(sel);
        };
        _quick.Controls.Add(b);
    }

    public void AskAbout(Person p) =>
        _ = SendAsync($"Focus on {p.FullName}. How do things really stand between us, what risks or opportunities " +
                      "do you see, and what concrete steps should I take with them in the next few weeks?");

    public void RefreshStatus()
    {
        if (_advisor.IsConfigured)
        {
            _status.Text = $"Connected · {RelationshipAdvisor.ModelId}";
            _status.ForeColor = Theme.TextDim;
        }
        else
        {
            _status.Text = "⚠ No API key - click here to set up the AI advisor";
            _status.ForeColor = Theme.Me;
        }
    }

    public void NewChat()
    {
        _cts?.Cancel();
        _advisor.ResetConversation();
        _output.Clear();
        AppendSystem("New conversation started.");
    }

    /// <summary>Called when the user switches field - the conversation is field-specific.</summary>
    public void OnFieldChanged(Field? f)
    {
        _cts?.Cancel();
        _advisor.ResetConversation();
        _output.Clear();
        if (f != null) AppendSystem($"Advising on \"{f.Name}\".");
    }

    private async Task SendAsync(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || _cts != null) return;
        var field = FieldProvider?.Invoke();
        if (field == null) return;
        if (!_advisor.IsConfigured) { SettingsRequested?.Invoke(this, EventArgs.Empty); if (!_advisor.IsConfigured) return; }

        _input.Clear();
        AppendBlock("You", text, Theme.Accent);
        AppendHeader("Advisor", Color.FromArgb(120, 200, 160));

        _cts = new CancellationTokenSource();
        _send.Text = "Stop";
        _status.Text = "Thinking…";
        try
        {
            await _advisor.AskAsync(text, field, SelectionProvider?.Invoke(), chunk =>
            {
                if (InvokeRequired) BeginInvoke(() => AppendText(chunk, Theme.Text));
                else AppendText(chunk, Theme.Text);
            }, _cts.Token);
        }
        catch (Exception ex)
        {
            AppendText("\n" + RelationshipAdvisor.Describe(ex), Color.FromArgb(240, 120, 110));
        }
        finally
        {
            AppendText("\n\n", Theme.Text);
            _cts.Dispose();
            _cts = null;
            _send.Text = "Send";
            RefreshStatus();
        }
    }

    private void AppendSystem(string text) => AppendBlock(null, text, Theme.TextDim);

    private void AppendBlock(string? who, string text, Color color)
    {
        if (who != null) AppendHeader(who, color);
        AppendText(text + "\n\n", who == null ? Theme.TextDim : Theme.Text);
    }

    private void AppendHeader(string who, Color color)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionFont = Theme.BodyBold;
        _output.SelectionColor = color;
        _output.AppendText(who + "\n");
    }

    private void AppendText(string text, Color color)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionFont = Theme.Body;
        _output.SelectionColor = color;
        _output.AppendText(text);
        _output.ScrollToCaret();
    }
}
