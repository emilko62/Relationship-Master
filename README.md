# Relationship Master

A Windows Forms desktop app for mapping and managing your relationships, with a built-in AI advisor powered by Claude.

## Concepts

- **Fields** are places or groups (Work, Family, Football club…). Each field has its own map of people, and **you** appear in every field as the gold circle.
- **People** are circles labelled with their first name and last initial ("Mark O.").
  - **Circle size** shows how much influence they have in the field.
  - **Colour** shows the opinion: grey is neutral, green shades are friends, blue is an alliance, and red shades are negative.
  - **Arrows from you** show how much influence *you* have over each person. Thicker means more, and dashed means weak.
- **Two views.** Click the chip in the top-right corner of the map, press **V**, or use the toolbar button to switch between
  - *How I see them*: colours show your opinion of each person.
  - *How they (supposedly) see me*: colours show how you believe each person sees you.
  The colours animate between the two, so perception gaps are easy to spot. Hover over a circle for details.

## Key info & history

- **Key info.** For every person you can store facts in categories: *likes, dislikes, skills, strengths, weaknesses, quirks, goals* (e.g. "Likes dogs - especially Great Danes"). Open it with **Key info & history** in the Person panel, or right-click the person.
- **Record event.** Log something that happened and how it changed the relationship on either side, e.g. *my opinion −1*, *how they see me +2*. The change is applied and kept in the person's history. Deleting an event can undo its effect.
- **Plan interaction (AI).** Choose a goal (*win them over*, *challenge / compete*, *repair*), describe the situation, and set **how much** it should move the relationship: 0 means it doesn't matter, ~5 is a small gesture, 20+ is a grandiose gesture. Claude writes a concrete plan that draws on the recorded key info, and the plan is saved as **awaiting result** (orange badge on the circle).
- **Enter the result.** Later, write the story of what actually happened. Claude rates the **success %** and proposes the opinion changes (never more than the chosen intensity). You can adjust the numbers, then **Apply result** to update the relationship and log it.

## AI advisor (Claude)

- **Chat** about the current field. Every question includes a snapshot of the map, including key info and recent history. Quick actions: *Analyze field*, *Blind spots*, *Advise on selected*, *Who to invest in*, *Pending plans*.
- **Interaction planning and outcome scoring**, described above.
- **Quick add with AI**: describe someone in plain words and the app creates the person with estimated scores and key facts.
- Set the key in **⚙ AI settings** (stored encrypted with Windows DPAPI for your user account), or set the `ANTHROPIC_API_KEY` environment variable.
- Uses `claude-opus-5-5` through the official [Anthropic C# SDK](https://www.nuget.org/packages/Anthropic). Replies are streamed, and server-side refusal fallback is enabled.

## Controls

| Action | How |
|---|---|
| Add a person | Double-click empty space, the **+ Person** button, or **N** |
| Edit a person | Click it and use the panel on the right. Double-click focuses the name |
| Move | Drag a circle, or use the arrow keys (Shift for bigger steps) |
| Pan / zoom | Drag empty space or middle-drag / mouse wheel |
| Fit everything | **F** or **Fit view** |
| Delete | **Del** or right-click → Remove |
| More options | Right-click a person or empty space |
| Record event / plan / enter result | Right-click a person, or use the buttons in the Person panel |

The app scales with Windows display settings (100 %–200 %+), and on small or high-DPI screens the window and dialogs fit the screen and scroll. Errors are logged to `%AppData%\RelationshipMaster\error.log`.

Data is saved automatically to `%AppData%\RelationshipMaster\fields.json`. Use Export/Import for backups. On first launch, a sample field is created.

## Build & run

Requires the .NET 8 SDK (or newer) on Windows.

```bash
dotnet run --project src/RelationshipMaster
```

You can also open `RelationshipMaster.sln` in Visual Studio 2022.
