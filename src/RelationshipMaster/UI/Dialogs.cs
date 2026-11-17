using RelationshipMaster.Models;
using RelationshipMaster.Services;

namespace RelationshipMaster.UI;

/// <summary>Small helper base for consistently styled modal dialogs.</summary>
public class DarkDialog : Form
{
    protected readonly FlowLayoutPanel Body;
    protected readonly FlowLayoutPanel Buttons;

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
        Padding = new Padding(18);

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        Body = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, MinimumSize = new Size(width, 0) };
        Buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, MinimumSize = new Size(width, 0), Margin = new Padding(0, 14, 0, 0) };
        root.Controls.Add(Body);
        root.Controls.Add(Buttons);
        Controls.Add(root);
    }

    protected Button AddButton(string text, DialogResult result, bool primary = false)
    {
        var b = Theme.MakeButton(text, primary);
        b.DialogResult = result;
        b.Margin = new Padding(6, 0, 0, 0);
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
        _box.Width = 360;
        _box.Text = initial;
        Body.Controls.Add(_box);
        if (secondLabel != null)
        {
            Body.Controls.Add(Theme.MakeLabel(secondLabel));
            SecondBox = Theme.MakeTextBox(multiline: true);
            SecondBox.Size = new Size(360, 60);
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

    public ApiKeyDialog(string? current) : base("AI settings", 420)
    {
        Body.Controls.Add(Theme.MakeLabel("Anthropic API key", Theme.BodyBold, Theme.Text));
        Body.Controls.Add(Theme.MakeLabel(
            "The AI advisor uses Claude. Create a key at console.anthropic.com → API keys.\n" +
            "It is stored encrypted for your Windows account only (or set ANTHROPIC_API_KEY).", Theme.Small));
        _key = Theme.MakeTextBox();
        _key.Width = 420;
        _key.UseSystemPasswordChar = true;
        _key.Text = current ?? "";
        Body.Controls.Add(_key);

        var show = new CheckBox { Text = "Show key", ForeColor = Theme.TextDim, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
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

    public QuickAddDialog(RelationshipAdvisor advisor, Field field) : base("✨ Quick add with AI", 440)
    {
        _advisor = advisor;
        _field = field;
        Body.Controls.Add(Theme.MakeLabel("Describe the person in your own words", Theme.BodyBold, Theme.Text));
        Body.Controls.Add(Theme.MakeLabel(
            "e.g. \"Tom Becker, the CFO. Very powerful here. I don't trust him much and I think he\n" +
            "finds me annoying since the budget meeting. I can't really sway him.\"", Theme.Small));
        _text = Theme.MakeTextBox(multiline: true);
        _text.Size = new Size(440, 110);
        Body.Controls.Add(_text);
        _status = Theme.MakeLabel("", Theme.Small);
        Body.Controls.Add(_status);

        _go = Theme.MakeButton("Add person", primary: true);
        _go.Margin = new Padding(6, 0, 0, 0);
        _go.Click += async (_, _) => await RunAsync();
        Buttons.Controls.Add(_go);
        AddButton("Cancel", DialogResult.Cancel);
        FormClosing += (_, _) => _cts?.Cancel();
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
            _status.ForeColor = Color.FromArgb(240, 120, 110);
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
