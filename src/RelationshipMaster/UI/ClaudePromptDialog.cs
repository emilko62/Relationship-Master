using System.Diagnostics;

namespace RelationshipMaster.UI;

/// <summary>
/// Shows a ready-made prompt to copy into Claude (e.g. claude.ai with your own subscription) and, optionally,
/// a box to paste Claude's answer back into the app.
/// </summary>
public sealed class ClaudePromptDialog : DarkSizableDialog
{
    private readonly TextBox _prompt;
    private readonly TextBox? _answer;
    private readonly Label _status;
    private readonly Func<string, string?>? _accept;

    /// <summary>The pasted answer (only when a paste-back box was shown and accepted).</summary>
    public string? Answer { get; private set; }

    /// <param name="accept">
    /// When set, a paste-back box is shown. The function validates/consumes the answer and returns an error
    /// message, or null when the answer was accepted.
    /// </param>
    public ClaudePromptDialog(string title, string prompt, string? pasteLabel = null, Func<string, string?>? accept = null)
        : base(title, 820, accept != null ? 860 : 700)
    {
        _accept = accept;
        Row(Theme.MakeLabel("📋 " + title, Theme.Header, Theme.Text));
        var steps = Row(Theme.MakeLabel(accept != null
            ? "1. Copy the prompt   2. Paste it into Claude (claude.ai, the Claude app…)   3. Paste Claude's reply below"
            : "Copy the prompt and paste it into Claude (claude.ai, the Claude app…). You can edit it first.", Theme.Small));
        steps.MaximumSize = new Size(Theme.Px(760), 0);

        var actions = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Panel, Margin = Theme.Pad(0, 8, 0, 4) };
        var copy = Theme.MakeButton("Copy prompt", primary: true);
        copy.Margin = Theme.Pad(0, 0, 8, 0);
        copy.Click += (_, _) => CopyPrompt();
        var open = Theme.MakeButton("Open claude.ai");
        open.Margin = Theme.Pad(0, 0, 8, 0);
        open.Click += (_, _) => OpenClaude();
        _status = Theme.MakeLabel("", Theme.Small);
        _status.Margin = Theme.Pad(4, 9, 0, 0);
        actions.Controls.Add(copy);
        actions.Controls.Add(open);
        actions.Controls.Add(_status);
        Row(actions);

        // The prompt sits below the buttons so "Copy prompt" is always visible, even on small / high-DPI screens.
        _prompt = Theme.MakeTextBox(multiline: true);
        _prompt.Text = prompt.Replace("\r\n", "\n").Replace("\n", "\r\n");
        _prompt.Font = new Font("Consolas", 9f);
        _prompt.WordWrap = true;
        FillRow(_prompt, accept != null ? 200 : 360);

        if (accept != null)
        {
            Row(Theme.MakeLabel(pasteLabel ?? "Paste Claude's reply here", Theme.BodyBold, Theme.Text));
            _answer = Theme.MakeTextBox(multiline: true);
            FillRow(_answer, 160);
            var use = AddButton("Use this answer", primary: true);
            use.Click += (_, _) => UseAnswer();
            var paste = AddButton("Paste from clipboard");
            paste.Click += (_, _) => { if (Clipboard.ContainsText()) _answer.Text = Clipboard.GetText(); };
        }
        var close = AddButton(accept != null ? "Cancel" : "Close");
        close.Click += (_, _) => Close();
        CancelButton = close;
        Shown += (_, _) =>
        {
            _prompt.SelectionStart = 0;
            _prompt.SelectionLength = 0;
            _prompt.ScrollToCaret();
            if (accept == null) CopyPrompt();
        };
    }

    private void CopyPrompt()
    {
        try
        {
            Clipboard.SetText(_prompt.Text);
            _status.ForeColor = Theme.Good;
            _status.Text = "✓ Copied to clipboard";
        }
        catch (Exception ex)
        {
            _status.ForeColor = Theme.Bad;
            _status.Text = "Could not copy: " + ex.Message;
        }
    }

    private void OpenClaude()
    {
        try { Process.Start(new ProcessStartInfo("https://claude.ai/new") { UseShellExecute = true }); }
        catch (Exception ex) { _status.ForeColor = Theme.Bad; _status.Text = "Could not open the browser: " + ex.Message; }
    }

    private void UseAnswer()
    {
        var text = _answer!.Text.Trim();
        if (text.Length == 0) { _status.ForeColor = Theme.Bad; _status.Text = "Paste Claude's reply first."; _answer.Focus(); return; }
        var error = _accept!(text);
        if (error != null) { _status.ForeColor = Theme.Bad; _status.Text = error; return; }
        Answer = text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
