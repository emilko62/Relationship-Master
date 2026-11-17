using RelationshipMaster.Models;
using RelationshipMaster.Services;

namespace RelationshipMaster.UI;

/// <summary>Small helper base for consistently styled, auto-sized modal dialogs.</summary>
public class DarkDialog : Form
{
    protected readonly FlowLayoutPanel Body;
    protected readonly FlowLayoutPanel Buttons;
    protected readonly int ContentWidth;

    public DarkDialog(string title, int width)
    {
        Text = title;
        Theme.StyleForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = Theme.Pad(18);
        ContentWidth = Theme.Px(width);

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        Body = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, MinimumSize = new Size(ContentWidth, 0) };
        Buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, MinimumSize = new Size(ContentWidth, 0), Margin = Theme.Pad(0, 14, 0, 0) };
        root.Controls.Add(Body);
        root.Controls.Add(Buttons);
        Controls.Add(root);
    }

    protected Button AddButton(string text, DialogResult result, bool primary = false)
    {
        var b = Theme.MakeButton(text, primary);
        b.DialogResult = result;
        b.Margin = Theme.Pad(6, 0, 0, 0);
        Buttons.Controls.Add(b);
        if (primary) AcceptButton = b;
        if (result == DialogResult.Cancel) CancelButton = b;
        return b;
    }
}

/// <summary>Asks for a single line of text (field names etc.).</summary>
public sealed class PromptDialog : DarkDialog
{
    private readonly TextBox _box;
    public string Value => _box.Text.Trim();

    public PromptDialog(string title, string label, string initial = "", string? secondLabel = null, string secondInitial = "")
        : base(title, 360)
    {
        Body.Controls.Add(Theme.MakeLabel(label, Theme.BodyBold, Theme.Text));
        _box = Theme.MakeTextBox();
        _box.Width = ContentWidth;
        _box.Text = initial;
        Body.Controls.Add(_box);
        if (secondLabel != null)
        {
            Body.Controls.Add(Theme.MakeLabel(secondLabel));
            SecondBox = Theme.MakeTextBox(multiline: true);
            SecondBox.Size = new Size(ContentWidth, Theme.Px(70));
            SecondBox.Text = secondInitial;
            Body.Controls.Add(SecondBox);
        }
        AddButton("OK", DialogResult.OK, primary: true);
        AddButton("Cancel", DialogResult.Cancel);
    }

    public TextBox? SecondBox { get; }

    public static (string name, string description)? Ask(IWin32Window owner, string title, string label,
        string initial = "", string? descriptionLabel = null, string descriptionInitial = "")
    {
        using var d = new PromptDialog(title, label, initial, descriptionLabel, descriptionInitial);
        if (d.ShowDialog(owner) != DialogResult.OK || d.Value.Length == 0) return null;
        return (d.Value, d.SecondBox?.Text.Trim() ?? "");
    }
}

/// <summary>Stores the Anthropic API key (encrypted for the current Windows user).</summary>
public sealed class ApiKeyDialog : DarkDialog
{
    private readonly TextBox _key;
    public string? Key => string.IsNullOrWhiteSpace(_key.Text) ? null : _key.Text.Trim();

    public ApiKeyDialog(string? current) : base("AI settings", 440)
    {
        Body.Controls.Add(Theme.MakeLabel("Anthropic API key", Theme.BodyBold, Theme.Text));
        var info = Theme.MakeLabel(
            "The AI advisor uses Claude. Create a key at console.anthropic.com → API keys. " +
            "It is stored encrypted for your Windows account only (or set ANTHROPIC_API_KEY).", Theme.Small);
        info.MaximumSize = new Size(ContentWidth, 0);
        Body.Controls.Add(info);
        _key = Theme.MakeTextBox();
        _key.Width = ContentWidth;
        _key.UseSystemPasswordChar = true;
        _key.Text = current ?? "";
        Body.Controls.Add(_key);

        var show = new CheckBox { Text = "Show key", ForeColor = Theme.TextDim, AutoSize = true, Margin = Theme.Pad(0, 6, 0, 0) };
        show.CheckedChanged += (_, _) => _key.UseSystemPasswordChar = !show.Checked;
        Body.Controls.Add(show);

        AddButton("Save", DialogResult.OK, primary: true);
        AddButton("Cancel", DialogResult.Cancel);
    }
}

/// <summary>"Describe someone in your own words" → the AI fills in a new person.</summary>
public sealed class QuickAddDialog : DarkDialog
{
    private readonly TextBox _text;
    private readonly Label _status;
    private readonly Button _go;
    private readonly RelationshipAdvisor _advisor;
    private readonly Field _field;
    private CancellationTokenSource? _cts;

    public Person? Result { get; private set; }

    public QuickAddDialog(RelationshipAdvisor advisor, Field field) : base("✨ Quick add with AI", 520)
    {
        _advisor = advisor;
        _field = field;
        Body.Controls.Add(Theme.MakeLabel("Describe the person in your own words", Theme.BodyBold, Theme.Text));
        var example = Theme.MakeLabel(
            "e.g. \"Tom Becker, the CFO. Very powerful here. I don't trust him much and I think he finds me " +
            "annoying since the budget meeting. Loves golf, terrible at small talk.\"", Theme.Small);
        example.MaximumSize = new Size(ContentWidth, 0);
        Body.Controls.Add(example);
        _text = Theme.MakeTextBox(multiline: true);
        _text.Size = new Size(ContentWidth, Theme.Px(120));
        Body.Controls.Add(_text);
        _status = Theme.MakeLabel("", Theme.Small);
        _status.MaximumSize = new Size(ContentWidth, 0);
        Body.Controls.Add(_status);

        _go = Theme.MakeButton("✨ Add with AI", primary: true);
        _go.Margin = Theme.Pad(6, 0, 0, 0);
        _go.Click += async (_, _) => await RunAsync();
        Buttons.Controls.Add(_go);
        var viaPrompt = Theme.MakeButton("📋 Use my Claude…");
        viaPrompt.Margin = Theme.Pad(6, 0, 0, 0);
        viaPrompt.Click += (_, _) => ViaPrompt();
        Buttons.Controls.Add(viaPrompt);
        AddButton("Cancel", DialogResult.Cancel);
        AcceptButton = null; // Enter makes a new line in the description
        FormClosing += (_, _) => _cts?.Cancel();
    }

    /// <summary>Same extraction through the user's own Claude chat: copy the prompt, paste the JSON reply.</summary>
    private void ViaPrompt()
    {
        if (string.IsNullOrWhiteSpace(_text.Text)) { _status.Text = "Describe the person first."; _text.Focus(); return; }
        Person? parsed = null;
        using var d = new ClaudePromptDialog("Add a person with your Claude", PromptBuilder.QuickAdd(_field, _text.Text.Trim()),
            "Paste Claude's reply (it contains a JSON block describing the person)", answer =>
            {
                try
                {
                    using var doc = PromptBuilder.ExtractJson(answer);
                    parsed = RelationshipAdvisor.PersonFromJson(doc.RootElement);
                    return null;
                }
                catch (FormatException ex) { return ex.Message; }
                catch (InvalidOperationException) { return "That JSON doesn't look like a person - paste Claude's whole reply."; }
            });
        if (d.ShowDialog(this) == DialogResult.OK && parsed != null)
        {
            Result = parsed;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(_text.Text) || _cts != null) return;
        _cts = new CancellationTokenSource();
        _go.Enabled = _text.Enabled = false;
        _status.ForeColor = Theme.TextDim;
        _status.Text = "Reading your description…";
        try
        {
            Result = await _advisor.ParsePersonAsync(_text.Text, _field, _cts.Token);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _status.ForeColor = Theme.Bad;
            _status.Text = RelationshipAdvisor.Describe(ex);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            if (!IsDisposed) _go.Enabled = _text.Enabled = true;
        }
    }
}
