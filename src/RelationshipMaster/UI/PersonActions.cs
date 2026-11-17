using RelationshipMaster.Models;
using RelationshipMaster.Services;

namespace RelationshipMaster.UI;

/// <summary>Opens the per-person dialogs (record event, plan, complete, details). Returns true when data changed.</summary>
public sealed class PersonActions
{
    private readonly RelationshipAdvisor _advisor;
    private readonly Func<IWin32Window, bool> _ensureAi;

    public PersonActions(RelationshipAdvisor advisor, Func<IWin32Window, bool> ensureAi)
    {
        _advisor = advisor;
        _ensureAi = ensureAi;
    }

    public bool RecordEvent(IWin32Window owner, Person p)
    {
        using var d = new RecordEventDialog(p);
        return d.ShowDialog(owner) == DialogResult.OK;
    }

    /// <summary>Plans an interaction; returns the new pending event, or null.</summary>
    public RelationshipEvent? PlanInteraction(IWin32Window owner, Field f, Person p)
    {
        using var d = new PlanInteractionDialog(f, p, _advisor, () => _ensureAi(owner));
        d.ShowDialog(owner);
        return d.Result;
    }

    /// <summary>
    /// Opens an event: recorded events open in the edit dialog; planned ones open the result dialog
    /// (enter the result when pending, edit it when done, read-only when cancelled).
    /// </summary>
    public bool OpenEvent(IWin32Window owner, Field f, Person p, RelationshipEvent ev)
    {
        if (ev.Kind == EventKind.Recorded)
        {
            using var edit = new RecordEventDialog(p, ev);
            return edit.ShowDialog(owner) == DialogResult.OK;
        }
        string draftBefore = ev.ResultStory;
        using var d = new OutcomeDialog(f, p, ev, _advisor, () => _ensureAi(owner));
        bool done = d.ShowDialog(owner) == DialogResult.OK;
        return done || ev.ResultStory != draftBefore;
    }

    /// <summary>Shows a ready-made briefing about one person for the user's own Claude chat.</summary>
    public void ShowPersonPrompt(IWin32Window owner, Field f, Person p)
    {
        using var d = new ClaudePromptDialog($"Prompt about {p.FullName}", PromptBuilder.PersonBriefing(f, p));
        d.ShowDialog(owner);
    }

    /// <summary>Shows a ready-made briefing about the whole field for the user's own Claude chat.</summary>
    public void ShowFieldPrompt(IWin32Window owner, Field f, Person? selected, string question = "")
    {
        using var d = new ClaudePromptDialog($"Prompt about \"{f.Name}\"", PromptBuilder.FieldQuestion(f, selected, question));
        d.ShowDialog(owner);
    }

    public bool OpenDetails(IWin32Window owner, Field f, Person p)
    {
        using var d = new PersonDetailsForm(f, p, this);
        d.ShowDialog(owner);
        return d.Changed;
    }
}
