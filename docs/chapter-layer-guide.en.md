# Chapter layer guide

> ⚠ **ked-story-director (2026-10-29 · D2):** the Excel parts of this document — chapter · script workbooks, the old [Script] tab (Excel script editor), [Import script/chapter],
> workbook output · locking — were removed from this repository. The source of truth is a single project, and dialogue lives in the cards of the [Script] tab.
> The workbook specs in §6 · §8 are kept as a record of that time.
> The authoritative description of current behavior is [`docs/design/two-graphs.md`](design/two-graphs.md) (Korean).
>
> **Screen names (2026-11-05):** the tabs are **[Structure]** (formerly [Chapter graph]) and **[Script]** (formerly [Staging graph]). The non-Excel screen descriptions (§0 ·
> §2–§5 · §9 · §10) were updated to the screens of 2026-11-18.
>
> This is the English version of `chapter-layer-guide.md`. Screen names follow the English UI, and new items get their starting names in the screen
> language. The record of the workbook days below keeps the Korean sheet and column names as they appeared in the files.

How to use the layer where chapters (episode structure · branches · conditions) are built. It's a different layer from the
[Writer's guide](writer-guide.en.md), where you write dialogue line by line — **the Writer's guide covers the [Script] tab (dialogue · choices · branches);
this document covers the [Structure] tab (episode structure · conditions · stats)**.

As of: 2026-09-17 (R7 — choices can now be drawn in [Staging graph] too, and **the "free scene" kind is gone**). Screen descriptions as of 2026-11-18.

> ### ⚠ Three changes on 2026-09-17 (R7)
>
> 1. **Choices are drawn in [Script]** (then [Staging graph]). Write text in the 3 slots under a card and drag it onto another card,
>    and that's a path (edge) — **the same single edge** shown in [Structure],
>    not new data. ⛔ Back then they could also be drawn in [Chapter graph] — since 2026-11-06, [Structure] doesn't connect or disconnect paths;
>    it only edits their conditions · stat changes.
> 2. **Free scenes are gone.** Every dialogue card on a chapter board **is an episode**. Putting a card on the board
>    is making an episode (only **branch cards**, visited through a branch, are separate — 2026-11-17).
> 3. **Staging scenes hung on edges (`ViaNodeId`) were spread out into the middle of the path** — `A —text→ staging —auto→ B`.
>    The playback order is the same.

> **There are two kinds of names — this is where people get confused most.**
> The **episode ID** is edited in the `Episode ID` box of the "Inspector" on the right after selecting a card in [Structure] (edit and press Enter —
> paths · cards follow). The **title** (shown name) is edited in the [Script] tab (the Inspector's name box · double-click in the Hierarchy).
> The **choice text** ("Take the left path") is the button text the player sees, and it's edited in the [Script] tab — the choice slot under a card,
> or click the line and use the "Text" box in the Inspector. [Structure]'s path panel only shows the text as a header ("Choice · text").
> Double-click the line to go to that choice in the [Script] tab.

---

## 0. One thing to know first

**The source of truth is the project folder.** (R-F, reversed on 2026-09-16 — before that it was Excel.)
Edits in the tool are kept in the project (`project.vnproject.json` and the board files in `story/`), and the progression JSON · Yarn dialogue
in the work folder `exported/` are **outputs written automatically** from it (§5 ⑤).
⛔ The route of exporting to Excel workbooks (`chapters/*.xlsx` · `episodes/**/*.xlsx`) and bringing them back with **[Import chapter] · [Import script]**
was removed in D2 (2026-10-29).

So these properties follow:

- **Undo with Ctrl+Z** (2026-09-18). ~~Tool edits have no Ctrl+Z~~ — that was true for a long time,
  and a few rules were built on that premise. Now moving or deleting a card steps back in one go.
- **File → Save (Ctrl+S)** writes to the files, and if anything changed, autosave also runs every 10 minutes.
  ⛔ Pushing the script workbook aside as `.bak` when deleting an episode, and Excel locking, were removed in D2 together with the workbooks —
  anything deleted comes back as it was with undo.
- Dialogue is written in **the card writing boxes of the [Script] tab** (§5 ④).

---

## 1. Reference material — what to read when

| Document | What's in it | When to read it |
|---|---|---|
| **This document** | Screen operations · workflow of the [Structure] tab · (old) Excel spec summary | When actually using the tool |
| [`handoff/2026-09-18-handoff.md`](handoff/2026-09-18-handoff.md) (Korean) | **The latest handover.** How the chapter graph became a board · branches · rules whose premise disappeared | **First**, when a new session takes over the work |
| [`handoff/current-state.md`](handoff/current-state.md) (Korean) | **Implementation history** (up to 2026-08) — purpose · layer rules · screens · code map. ⚠ Not the current state: R-F reversed its premise, and the header points out what changed | When asking "why was it built this way" |
| [`work-orders/chapter-graph-orders.md`](work-orders/chapter-graph-orders.md) (Korean) | **The spec source.** §2 the owner's 15 decisions (G-1–G-8, including revisions) · §3 Excel spec (sheets · columns · span rules) · §4 features G1–G9 | When you need the exact wording of the spec |
| [`run-log.md`](run-log.md) (Korean) | A narrative record of the whole process. Decisions made along the way, their reasons, and pitfalls found | When asking "why did it end up like this" |
| [`ch01.chapter.json`](../tests/fixtures/chapter-samples/ch01.chapter.json) | **The current contract sample** (project format). Scene · Auto · conditions · stats · EventKey. It was `ch01.contract-sample.xlsx` until 2026-10-29 | When looking at a new chapter and the shape of runtime output |
| [`ch05.chapter.json`](../tests/fixtures/chapter-samples/ch05.chapter.json) | A sample for screen · episode regression tests (project format). It was `chapter-graph-sample.xlsx` until 2026-10-29 | When looking at existing cases |
| [`handoff/io-reference.md`](handoff/io-reference.md) · [`runtime-contract.md`](runtime-contract.md) (Korean) | The input/output contract with the runtime | When putting exported JSON into the game |
| [`writer-guide.en.md`](writer-guide.en.md) | For writers, **the [Script] tab** (dialogue layer) | When writing dialogue · choices · branches |

> The `ch05` sample has **one intended error** (`branch05.02A` is unreachable). It's not a bug — it's a demonstration of
> what the reachability proof catches. See the M3+M4 entry in the run-log for the details.

---

## 2. Where the files are

```
{project folder}/                  ← what you choose with File → [New project…] · [Open…] (a folder)
├─ project.vnproject.json          ← the project — chapters (episodes · paths · conditions · stats) · speakers
├─ story/*.vnstory.json            ← boards — one per chapter, the cards and dialogue of the [Script] tab
├─ runtime.target.json             ← where to send (when set in [⚙ Settings] "Export")
└─ exported/                       ← the work folder ([📂 Work folder]) — written automatically (only chapters that pass validation)
   ├─ {chapterId}.progression.json
   └─ yarn/{chapter}_{scene}.yarn   ← dialogue-only Yarn (one per scene)
```

The tool builds and edits all of them — you never need to make them by hand. ⛔ The chapter · episode workbooks (`chapters/{chapterId}.xlsx` ·
`episodes/{chapterId}/{episodeId}.xlsx`) were removed in D2 (2026-10-29).

---

## 3. The screen

**The [Structure] tab** (formerly [Chapter graph]). (2026-08-18: the order was changed so you go left to right; next to it is [Script] — formerly [Staging graph], before that "Scenario graph". Since 2026-11-19 the window opens on [Script].)

- **Left** — the **Hierarchy**. Expand and collapse project → chapter → scene → episode (branch cards are "⑂ branch card ID" rows under the calling episode — "⑂ title" with "Show IDs" off). One chapter = one board (1:1).
  - Click a row and the board selects it, and the tab on the right follows (see **Right** below). Clicking a chapter row switches to that chapter.
    ↑↓ select · ← collapse · → expand. Collapsing is shared with the [Script] tab. "‹" at its head folds the whole panel.
  - **The [Structure] tab's Hierarchy only selects and collapses** (2026-11-06) — adding, deleting and renaming chapters · scenes · episodes (right-click · double-click to rename · drag to move) is in the **[Script] tab**. IDs of new items are made automatically by the tool (`ch01` · `scene01` · `ch01_scene01_new01` …).
  - With "Show IDs" in the toolbar (on at first), rows show IDs with the shown names beside them. Turn it off and they show names, like the [Script] tab (remembered on this PC).
  - **Changing an ID** is done in the box in the "Inspector" tab after selecting the row (or card) — chapter → `Chapter ID` (the board · condition plumbing · progression JSON file name follow ·
    selecting a chapter opens the "Conditions & stats" tab, so open the "Inspector" tab by hand), scene → `Scene ID` (every episode of that scene and the scene name · note follow —
    it won't change to an ID another scene uses), episode → `Episode ID` (paths · cards follow). It's applied with Enter or by clicking elsewhere; Esc returns to the value before applying.
    `exported/{oldId}.progression.json`, exported under the old chapter ID, is removed by the next export (2026-11-06 — §5 ⑤).
    Shown names (chapter name · scene name · episode title) are edited in the [Script] tab.
  - **Deleting a chapter** is done in the [Script] tab — select the chapter and press **[Delete]** in the toolbar ("Delete chapter") → click once more in the confirmation box beside the button (2026-11-18).
    It removes the chapter with its scenes · episodes · the cards on the board · condition plumbing, and undo (Ctrl+Z) brings it back. ~~It used to be "this can't be undone, so
    delete it from the folder yourself"~~ — that route **deletes only half**: the board and nodes stay in the project and later blow up with "no calling node".
- **Center** — the graph board. Card = episode, line = path (edge).
  - Chips on the first row of a card's header: blue `Scene start` = where the scene is entered from outside (the chapter's starting episode — the first in the chapter's episode list — gets this chip too) ·
    purple `{scene ID}` (the scene name with "Show IDs" off) · `🔒` = an incoming path has a show/unlock condition · `⚠` = an error or unreachable (there's a path, but a condition stops it from being reached).
    The second row is the episode ID (the title with it off). **A faded card** = not yet connected from the start — a warning because it's work in progress, and it doesn't block export (2026-11-17)
    (the `★` ending badge disappeared in v14 — the event key is a value the tool doesn't interpret). ⛔ The old `ROOT` chip · `SCENE ROOT` are today's `Scene start`.
  - On a line: a text box — the choice text in the middle (`AUTO` for an auto path, `⚠ No text` if empty), "Show: …" · "Unlock: …" above, stat changes below
  - **Ways out are below the card, ways in are above it** (2026-09-18) — on the bottom edge
    **three points stand in a row**, and those paths go into **one point on the top edge** of the target card.
    The same handling as the [Script] tab.
    - Three points is **the minimum** — with four paths, all four are drawn. Empty slots are **hollow points**
    - Point colors: **orange** = connected choice · **blue** = auto-advance · **red** = no text (needs fixing) ·
      **gray hollow** = a slot not used yet
    - **Branches** don't appear here — they leave as purple **dotted lines** from the branch point on the card's side edge (the side visited) and go into a small chip of the branch card (§4 branches)
- **Right** — three tabs: **"Inspector" · "Speakers" · "Conditions & stats"** (2026-11-18). "›" at the end of the tab row folds the whole panel.
  - Selecting something goes to the tab that shows it — the project row in the Hierarchy → "Speakers", a chapter row → "Conditions & stats", anything else (scene · episode rows ·
    cards · lines · chips on the board) → "Inspector". Clicking empty board to clear the selection or switching chapters with the combo leaves the tab as is, and you can open a tab by hand at any time.
  - **"Inspector"** — the one thing selected now. Episode → `Episode ID` box · list of choices (read-only) · dialogue preview; choice line → path panel (show condition ·
    unlock condition · locked hint · stat change); branch chip → branch panel (§4 branches); scene → `Scene ID` box · that scene's episodes; chapter → `Chapter ID` box.
  - **"Speakers"** — project-wide. Link a character key to each speaker (names are added and edited in the [Script] tab). Adding a speaker gives it a **temporary key**
    — `temp_` + the romanized name (하린 → `temp_harin` · standard romanization, so 카스미 is `temp_kaseumi` · on a clash `temp_harin2` · with nothing to romanize
    `temp_char01` · 2026-11-19). It doesn't follow renames. Speakers with a temporary key get a ⚠ in this tab (they don't appear in the validation report).
    Speakers who appear in that chapter's dialogue but have **no** key show as a 🟡 warning in the validation report below and get a ⚠ in this tab too (it doesn't block the progression JSON —
    2026-11-18). Entering a key that matches the art clears it.
  - **"Conditions & stats"** — per chapter (the small chapter drop-down at the top right shows the current chapter). A condition table (name box → expression box) and a stat table (key box · type ·
    values) — names · keys are edited in their boxes. Under each table there's always a name box + [＋ Add] (§5 ③).

> ⛔ The table separating **what the tool writes and what Excel writes** (dialogue entry · X/Y · event key · the `조건` · `스탯` · `화자` · `선택지` sheets were Excel-only) and
> choosing path text from a drop-down of the chapter's `선택지` sheet (the text dictionary) were removed in D2 together with the workbooks.

- **Top toolbar** — chapter combo (showing the chapter name) · [Auto layout] · [Fit to screen] · "Hide empty choices" · "Show IDs".
  Episodes are added and deleted in the [Script] tab ([＋ Episode] · [Delete] · the Delete key · right-click in the Hierarchy — §4).
  > ⛔ [Reload] and the test start combo were removed on 2026-08-24, and there's no [Open folder] either. When the project changes, the screen redraws itself —
  > it reloads when an edit reports a change (there's no folder watching).
- **Bottom** — [Validation report]. Even collapsed, it always shows as one line, with 🔴 errors · 🟡 warnings in its header.

---

## 3-1. Scenes — the lifetime boundary that groups episodes

**A scene isn't a display name. It's the unit the game rolls back, commits and saves.**

```text
scenario (playthrough) > chapter > scene > episode
```

Episodes with the same `scene ID` form **one scene**. In the game:

| Move | What happens |
|---|---|
| To the next episode **within the same scene** | Nothing — choices aren't committed yet (they can be undone) |
| **To another scene** | Choices made so far are **committed**, the stage is reset, and the new scene opens |
| Rollback / backlog jump | Replays from the root **within the same scene**. Nothing is committed |

In other words, **everything can be taken back within a scene, and it's committed when the scene ends.**

### Where it's set

**Which scene an episode belongs to is decided in the [Script] tab** — [Structure] doesn't change scenes.

- A new episode belongs to a scene from the start — right-click a scene row in the [Script] Hierarchy → "Add episode" · right-click empty space inside a scene's frame on the board → "Add episode"
  add it to that scene; the toolbar's [＋ Episode] decides in the order selected scene → selected card's scene → new scene.
- To move it to another scene, **drag** the episode row in the [Script] Hierarchy **onto another scene row in the same chapter**.
- New scenes are created with right-click a chapter row → "Add scene" · right-click empty space inside a chapter's frame (outside scenes) on the board → "Add scene".
- A scene's ID (`Scene ID` — the name of the save · rollback boundary) is edited in the `Scene ID` box of the "Inspector" tab after selecting the scene row in [Structure] — every
  episode of that scene follows. The scene name is edited in the [Script] tab.

⛔ The `장면ID` column of the `에피소드` sheet (D2), the per-episode `Scene ID` box in the Inspector, and **[Bulk scene]** were removed.

### Two rules — the tool enforces them

1. ⛔ **A scene has only one entrance from outside** (= the scene root). Rollback returns there and
   Continue resumes there, so with two landing points, "where does this scene start" would differ
   by route.
   - The chapter start counts as that one.
   - Several paths coming into the same root is **normal**.
   - Leaving the scene and **coming back to the same root is normal too** (a hub structure).
   - If broken, validation flags **the path that made the second entrance** as an error and blocks that chapter's export.
2. **Auto paths can cross scenes** (2026-11-18). What happens at the boundary (clearing the stage · committing · saving) is the same as crossing with a choice that asks;
   only no box appears. ⛔ Before that they couldn't cross (you had to give them text and turn off auto).

### If left empty

⛔ It can't be left empty. The old behavior where each episode with an empty scene ID became its own scene (**one episode = one scene**) was removed —
see the last item of "On screen" below.

### On screen

- **Cards**: chips on the first row of the header — a blue `Scene start` on the card where a scene starts, and a purple `{scene ID}` — with the episode ID on the second row
  (2026-11-18 — ⛔ before that it was `장면 · {scene ID}`, and on the same day it was briefly shortened to `시작점` before going back to `장면 시작점`).
  Turn off **Show IDs** in the toolbar (on at first) and the Hierarchy and cards show names (scene names · episode titles), like the [Script] tab — remembered on this PC.
- **Folded board** ([Script] tab — double-click a chapter label): the inside of the board is shown **grouped by scene**. Branch cards no episode calls gather at the bottom under `Outside scenes`.
- **Hierarchy** (both tabs): expands and collapses as `project → chapter → scene → episode`.
- **An episode always belongs to a scene** (2026-11-17 · reading since 2026-11-18). Episodes with an empty scene ID are neither created nor read — ⛔ before that they were shown separately as
  "no scene" · "directly under the chapter".

---

## 4. Handling (the graph board)

| What you want | How |
|---|---|
| Select an episode | **Click** a card → the "Inspector" tab on the right |
| **Make an episode** | Done in the [Script] tab — the toolbar's **[＋ Episode]** (selected scene → selected card's scene → new scene) · **right-click** a scene row in the Hierarchy **→ "Add episode"** · right-click empty space inside a scene's frame on the board → "Add episode" → a new episode is created in that scene with no path. Connect by dragging a choice slot under a card on the [Script] board (2026-11-18 — ⛔ before that it was added from the episode row and connected after it with a "계속" path). It isn't added in the [Structure] tab |
| **Delete an episode** | Done in the [Script] tab — select the card and press **[Delete]** in the toolbar ("Delete episode" → the confirmation box beside the button) · the **Delete** key (doesn't ask) · right-click an episode row in the Hierarchy → "Delete episode". Its paths · card go with it, and one Ctrl+Z brings it back. A chapter's last episode isn't deleted |
| **Go write the script** | **Double-click** a card → goes to that card in the [Script] tab (switches tabs and selects that card). Write the dialogue in that card's writing box (§5 ④). ⛔ Opening that episode's Excel file was removed in D2 |
| Edit a path | **Click a choice line or its text** → the path panel in the "Inspector" (show condition · unlock condition · locked hint · stat change) |
| Go edit the choice text | **Double-click** a choice line or its text → goes to that choice in the [Script] tab (the text is edited there). ⛔ The `A → B` link at the top of the path panel was removed on 2026-11-17 |
| Select an area | **Drag** on empty board → the cards caught become a group, and dragging any card of the group moves them together (one undo step when dropped) |
| Clear the selection | **Click empty** board |
| **Connect · disconnect** (in the [Script] tab only) | **Write text** in one of the 3 slots under a card and **drag its point onto another card** to make a choice path (connecting without text makes an auto path). To disconnect, drop it on empty space. What's made here shows in [Structure] as **the same path**. ⛔ Connecting and disconnecting in [Structure] was removed on 2026-11-06 |

**People decide positions (v4, 2026-09-18).** **Drag** cards to move them, and the position you drop them at is
saved as is — the same handling as the [Script] tab. If you moved one by mistake, one **Ctrl+Z** (one drag
is one undo). Zoom with the wheel, drag with the middle button to move the board, and the toolbar's **[Fit to screen]** adjusts only the zoom and scroll so all
of this chapter's cards fit on one screen (card positions stay).

**The board (white) and outside the board (gray)** — the board is as big as this chapter's cards plus margins, and if it's smaller than the screen, the surroundings are the gray outside. The outside accepts input
like the board: click and drag to select an area, and cards and the selection rectangle dragged outside the board stay visible. Drop a card outside the board — not only to the right · down,
but also dragging the leftmost · topmost card further left · up — and the board widens that way while every card stays where it is on screen (if the board exceeds the screen, scroll bars
appear). The board is centered on the screen at first · when switching chapters · with [Fit to screen] · when changing zoom. (2026-11-19 · usability UX22)

If the board gets tangled, the toolbar's **[Auto layout]** re-lays it out modeled on how the cards are placed in the [Script] tab (positions moved by hand are lost — for a chapter with no
cards in [Script], in flow order, top to bottom). This also steps back in one go. ⛔ Laying out in flow order (left to right) was changed on 2026-10-29 · 30.

### Branches — a detour that's always visited (2026-09-18)

**Branches** opened on lines in the [Script] tab are drawn on this board as **purple dotted lines**. They're a different kind from
choices (solid lines):

| | Choice (solid) | Branch (dotted) |
|---|---|---|
| Who picks | **The player** | Nobody — **it's always visited once** |
| Text | Required | None |
| Stat change | Committed once when this path is taken | None |
| Condition | Show · unlock conditions on **this path** | **At the start of the branch card visited** |

⭐ **What's gated is the side visited.** The branch is always taken — when visited, the condition stops it, it plays nothing
and comes straight back.

**The main place to choose the condition is the [Script] tab** — select a branch card and a **"⑂ Condition"** box appears above the script (2026-11-17).
In [Structure], clicking the branch card's **chip** opens the same **single condition box** in the "Inspector" on the right. The choice is written at the start of that branch card,
and **its content plays only when the condition holds**. "(always)" means it always plays. One undo (Ctrl+Z)
brings it back. The dotted line and the branch point are **display only** — clicking does nothing, and hovering says where it goes (2026-11-17).

**What's visited is a branch card** (2026-11-17). A branch card isn't an episode, so it doesn't stand on the board by itself; it's attached to the right of the calling episode
as a **small chip** ("⑂ branch card ID" — "⑂ title" with "Show IDs" off · with a condition, the chip grows to two rows with "Condition: label" on top), and the dotted line goes into it. Clicking the chip shows **branch card ID · called from · condition · script** (read-only)
on the right — the script and title are edited in the [Script] tab. The **branch card ID** (= the game's Yarn node name, automatically `ch01_scene01_branch01`) is edited in that box,
then Enter — it won't change to a name another node in the project uses. It also appears in the Hierarchy as a "⑂ branch card ID" row under the calling episode (title beside it · "⑂ title" with "Show IDs" off). Branch cards have no choices and
aren't in the progression JSON.

> ⚠ **Opening and removing branches is the [Script] tab's job.** This board only **reads and draws** branches
> and accepts only conditions — if the same thing had two owners, they would diverge.

> ⚠ If the start of that branch card was **written by hand in the dialogue editor**, this box is locked and the reason
> appears. Overwriting it here would make a script with mismatched pairs — open that card in the [Script] tab and edit it there.


---

## 5. Workflow

### ① Make a chapter

It's made in the [Script] tab — right-click the project row · empty space in the Hierarchy → **"Add chapter"**, or right-click empty space outside chapters on the board → "Add chapter".
If there are no chapters, **[＋ Create first chapter]** appears in the middle of the board (the same button in [Structure] takes you to the [Script] tab and creates it there).
A "New chapter" (ID automatic — `ch01` …) is created with a "New scene" scene and one episode, and selected — one undo step (2026-11-18).
The chapter name is edited in the [Script] tab (the `Chapter name` box at the top of the "Conditions & stats" tab · double-click in the Hierarchy), and the chapter ID in the
`Chapter ID` box of the [Structure] "Inspector". ⛔ The route of [＋] on the left → chapter Id → `chapters/{Id}.xlsx` being created was removed (workbooks in D2, the [＋] at the top of the Hierarchy on 2026-11-18).
A new chapter starts **without stats** — stats belong to the chapter and are added in the [Structure] tab (2026-11-17 · ⛔ before that they were filled from the variables of `game.definition.json`,
and deleted stats came back in every new chapter).

> **⚠ Keep stats to 2–5, with narrow ranges around 0–5.**
>
> This isn't taste, it's a **computational limit**. To prove "no playthrough can reach this episode",
> the tool goes through **every possible state**, and that number multiplies with the number of stats and their ranges:
>
> | Stats | Range | States to search |
> |---|---|---|
> | 2 | 0–5 | 36 |
> | 3 | 0–10 | 1,331 |
> | 5 | 0–100 | **about 10 billion** — it never finishes |
>
> Beyond that, the proof stops partway and "unreachable" is downgraded **from a certainty to a warning**
> (it won't call something it doesn't know an error). And **if you start wide and narrow it later, every condition expression
> already written loses its meaning** — starting narrow is far cheaper.

### ② Build the branches — the heart of this layer

The hands that build branches are in **the [Script] tab**. [Structure] is the place to look at the shape and set conditions · stats.

1. In the [Script] tab, click the starting card (the first time, the starting episode with the `Scene start` chip).
2. The toolbar's **[＋ Episode]** (it goes in the selected card's scene) or **right-click** a scene row in the Hierarchy **→ "Add episode"** → it's created with no path, with a placeholder Id
   (`ch01_scene01_new01`… — containing the chapter · scene) (2026-11-18 — ⛔ before that, adding from an episode row created the path in one go too).
3. Write text (e.g. "Follow Laru") in a choice slot under the starting card, and **drag** its point onto the new card to create a choice path. Connecting without text makes
   an auto path.
4. Names: the title is set in the [Script] tab (the Inspector's name box · double-click in the Hierarchy), and the episode ID in the `Episode ID` box of the [Structure] "Inspector".
   ⛔ The [Rename] button on the right was removed.

Connecting to an existing episode (merging · looping) works the same — drag the choice point onto that card. ⛔ [Connect] at the bottom of the panel was removed.

### ③ Set conditions

A condition is registered once in a **label ↔ expression** table, and called **only by its label** everywhere.

1. In the box under the condition table of the **"Conditions & stats" tab** on the right (per chapter — the small chapter drop-down at the top right is the current chapter), type a name (e.g. `HighTrust`) and press
   **[＋ Add]** (or Enter) — it starts as "Undecided". Write something like `trust >= 3` in that row's **expression** box (Enter · click elsewhere).
   An expression is "stat key · comparison · value" — five comparisons `>=` · `<=` · `>` · `<` · `==`, integer values (`true` · `false` for on/off stats), and several are
   joined with `;` (all must hold to pass). If an expression doesn't hold, the [Validation report] below says "the expression doesn't hold" and why (`=>` → `>=`, `=` → `==`,
   `&&` · `,` → `;`, `$key` → `key`, and for a key not in the stat table, the list of existing keys), and the box below shows **examples using this chapter's stats**
   (2026-11-23). Conditions already set in the [Script] tab also change the condition line (`<<if …>>`) in that card's writing box as soon as the expression is filled in — ⛔ before that they stayed
   `<<if true>>` and you had to choose the condition again. An expression that names a stat not yet created goes onto the board the moment that stat is added.
   A condition's **name** is edited in the row's name box — choices using that name follow (Esc cancels). Deleting is done in the [Script] tab's "Conditions & stats"
   tab. A condition with an empty expression is "Undecided" — it always passes and only warns.
   Stats too: type a key in the box under the stat table in the same tab and press [＋ Add] (it starts as a number (integer) 0, 0–100) — the type ("Number (integer)" · "On/Off") and values are edited
   on its row, and the **key** in the row's key box. **Write keys in English** — start with a letter and use only letters · digits · `_` (e.g. `courage` ·
   `met_harin`). It's the statKey that goes into Yarn as is, so Hangul · spaces · symbols aren't accepted, and the status bar says why (2026-11-23). Hangul keys from old projects
   are flagged as warnings by the [Validation report] — fix them to English in the key box and paths · conditions follow. Entering the key box shows a warning on that row —
   the key is the name stored in saves, so changing it after release returns that stat to its initial value in playthroughs already saved.
   > ⛔ The old [Conditions] tab's [Add/edit condition] was removed. Since 2026-11-16, condition names are handled by the [Script] tab (section 7 Q), and since 2026-11-18 both tabs'
   > side panels have three tabs ("Inspector" · "Speakers" · "Conditions & stats") (section 7 AS). On the same day, [Structure]'s [＋ Condition] · [＋ Stat] flyouts and the stat
   > key's [✎] were replaced by an always-present add row and name boxes ([`two-graphs.md`](design/two-graphs.md) section 7 BD, Korean).
2. **Click a choice line** on the board → choose that name in one of the two combos of the path panel (the condition's expression box appears below the combo, and [＋ New condition] beside it
   creates the name and expression and sets it right away).
   - **Show condition** = if not met, **the choice itself isn't shown**
   - **Unlock condition** = if not met, **it's shown locked** with the `Locked hint` (e.g. "Not enough trust")

   > ⛔ **The `hide when locked` checkbox was abolished on 2026-08-24** (owner: *"since there are already show conditions and
   > unlock conditions, removing it makes no functional difference"*). It was a box that said the same thing twice
   > — **unlock condition + hide** has the same result as writing that expression in the **show condition**.
   > To hide, use the show condition; to show it locked, use the unlock condition.
   > The `잠금시 숨김` column of old workbooks is removed when the app opens them (leaving a `.bak`). ⚠ **Rows where it was on
   > aren't moved** — if the show condition already has another expression, one of the two would have to be dropped,
   > and it isn't the tool's place to choose between gates a person wrote. Those rows become **shown locked**.
3. That's it — **there's no [Apply]** (2026-08-17). It applies the moment you choose (one undo step). Only text
   boxes (locked hint · stat values) apply when you click elsewhere or press Enter. File → Save (Ctrl+S) · autosave write it to the file.

> ⛔ Tool edits locking automatically while Excel had a chapter file open (2026-08-24) was removed in D2 together with the workbooks.

**Show/unlock conditions belong to the `path` (edge), not the episode** (v8). The two combos in the path panel are
**drop-downs** of the chapter's condition names, so typos can't happen — a newly added condition
appears in the list automatically (in the Excel days these were columns E·F of the `간선` sheet).

They're on paths rather than episodes because when two routes come into the same room, each route's conditions
can differ. Put on the episode, there'd be no place to tell the two apart.

### ④ Write the script

**Double-click** a card in [Structure] to go to that card in the [Script] tab. Dialogue is written in that card's **writing box** —
one line is one line of dialogue (`Speaker: dialogue`), applied when you pause or leave the box (one undo step). You can also edit it in the dialogue editor (line table) of the
"Inspector" on the right. The tool keeps each line's identity (LineId), so what's attached to a line follows it even when you edit the dialogue.
[Structure]'s "Inspector" shows a read-only preview of the dialogue. For details, see the [Writer's guide](writer-guide.en.md) and
[`two-graphs.md`](design/two-graphs.md) §3.4 (Korean).

> ⛔ Excel scripts (double-clicking a card to open the episode workbook · editing only the speaker · content columns with [🔒 dialogue locked] · `.xlsm` in Google Sheets) were
> removed in D2 (2026-10-29).

### ⑤ Validate and export

The [Validation report] below runs automatically. Besides structural errors, the **reachability proof** runs,
telling you "no route can reach this node" **and pointing to the condition that causes it**.

Episodes with no path connected yet (work in progress · unfinished) are **a warning, not an error** (`not connected`) and don't block export
(2026-11-17). Episodes that have paths but can't be reached **because of conditions** are a design bug, so they're errors — fix them by fixing the condition or stat change the proof points to.
⛔ The `allow unreachable` column (flag) that downgraded that error to a notice was removed the same day.

**Loops with no way out are warned about too** (2026-11-23). Going around a loop is fine in itself — if there's a choice that leads out even when you go back, like "Ask again", or
a path that opens as stats accumulate, nothing is said. Only when, once entered, whichever choice you pick you go around inside and can't finish the chapter does it say
"A loop with no way out" (and says so if you're trapped only depending on stats); a loop made only of auto paths gets "A loop made only of auto paths — it goes around A → B → A
forever without asking …", attached to the episode closest to the start. A spot with no choice to pick is where the game ends the chapter, so it isn't a loop.
It doesn't block export.

**Export happens automatically** (2026-08-17 — the button was abolished). Every time chapters are reloaded, `exported/{chapterId}.progression.json` of **every**
chapter is updated. Chapters whose progression JSON is written also get dialogue-only Yarn at the same time
(`exported/yarn/{chapter}_{scene}.yarn` — one per scene) (2026-11-17). If only chapters waited for a person's hand, stale files from the moment of the click
would remain, and if only the selected chapter went out, the rest would freeze. **[📂 Work folder]** in the top bar opens `exported/`.

The rule stays: **if there are validation errors, that chapter's files aren't touched**, "Progression JSON wasn't written" appears at the top of the validation report,
and `· progression JSON not written` is added to its header
(visible even when collapsed). Below that, the blocked chapters' errors appear — even if it isn't the chapter selected now, and even if it's a reason the progression core refused (the chapter ID at the
start of the line tells you which chapter · 2026-11-18). Even if a stale file remains, it's better than overwriting with garbage — what the runtime
reads is always a version that "was right once". Test starts are test data, so they aren't mixed in.

**A chapter with no episodes isn't blocked** (2026-11-18). There's simply nothing to write, so it's skipped (its file isn't touched) and no conclusion ·
`· progression JSON not written` is shown. That chapter's validation report says "No episodes yet". ⛔ Before that, the progression core refused it with "the starting
episode is empty", so every newly made chapter showed not-written with no reason in the report.

**Old files of deleted or renamed chapters are removed** (2026-11-06). When exporting, `{chapterId}.progression.json` of chapters no longer in the
project is deleted from `exported/` — if left, the game keeps loading chapters that don't exist. `exported/` is where the tool builds from the model, so removing them loses nothing (undoing the deletion ·
rename writes it again on the next export). Removed files appear on the "Progression JSON →" line of the validation report; files that couldn't be removed (another program holds them)
appear at the top of the report.

**Copies to mirror folders happen only on save** (2026-11-17). In [⚙ Settings] → "Export (choose mirror folders)", set a folder outside `exported/` for each format — the progression
JSON's mirror folder is the game folder (the runtime's `ChapterProgression`; the compositor reads the same field too), and Yarn dialogue can be set separately
(⛔ scene files were removed on 2026-11-18). File → Save (Ctrl+S) mirrors the freshly written `exported/` there — intermediate states while you edit don't go out. Old files are
removed **only if they're exactly what the tool sent (when the bytes match)**; files edited by a person or from elsewhere · the old mirror folder before a change are left alone. Folders that
don't exist aren't created. Yarn dialogue isn't sent to the same place as the game folder (its node names would clash with the staged Yarn). Where it was copied appears in the validation report
right after saving ("Copied to →"). ⛔ Before that, [🎮 Game folder…] in the top bar wrote only the progression JSON, on every edit.

---

## 6. Excel spec summary

> A record of the time when workbooks were the source (removed in D2, 2026-10-29). Sheet and column names are kept in Korean as they appeared in the files.

### Chapter workbook — `chapters/{chapterId}.xlsx` (6 sheets + 1 optional)

| Sheet | Columns |
|---|---|
| `에피소드` (episodes) | EpisodeId · **dialogue entry** · title · **event key** · **X · Y** · memo — **reordered** in v14 (identity · content → key handed over → coordinates · side notes). `종류` (kind) was abolished on 2026-08-25 |
| `간선` (paths) | from · to · **stat change** · choice · show condition · unlock condition · locked hint — **one row = one choice** (`잠금시 숨김` · `종류` · `연출` · `엔딩키` abolished) |
| `조건` (conditions) | label · **stat · operator · value** · description |
| `스탯` (stats) | **type** (int/bool) · stat key · display name · initial · min · max (2–5 rows) — in v14 **type moved to the front** (its value decides how the rest is read). Empty means int |
| `선택지` (choices) | index · script · memo — **a text dictionary shared by the whole chapter** (a vocabulary, not wiring) |
| `화자` (speakers) | name · **character key** · memo — the name is the script's drop-down; the character key is the key for portraits · expressions |
| ~~`픽스처`~~ (test starts) | Abolished — read if present but not created anew, and **not shown on screen** (2026-08-24). The sheet isn't deleted, so anything already written stays |

> **A path's `엔딩키` (ending key) became the episode's `이벤트키` (event key) in v14 (2026-08-26).** What this tool
> guarantees goes as far as **branches and graph structure within a chapter** — which ending a chapter is · what the next
> chapter is belongs to the scenario layer, and will be defined again in that layer's spec when it's built.
> In old files, the app automatically moved a path's ending key to the target episode's event key when opening it
> (the original stays in `.bak`).

#### The `이벤트키` (event key) of `에피소드` — an index handed to Unity

An index that lets Unity raise its own events or rewards **when that episode has been fully watched**.
**The tool doesn't interpret this value at all** — no badge on the graph, no validation, no effect on progression.
Written in Excel, it's simply carried out as the node's `EventKey` in the export. Left empty, nothing happens.

- **Filling in the `캐릭터키` (character key) of `화자` lets you attach expressions** (2026-08-17). In the staging graph's
  set node, [Expression] opened for that speaker's row. Left empty, the speaker was a name only with nothing to attach to,
  so the button was locked (⛔ 2026-11-17 — speakers and character keys live in the project's speaker list and are linked in the "Speakers" tab of the [Structure] tab.
  Set nodes aren't drawn, and staging is the compositor's job, so this tool has no [Expression]).
- **The starting episode = the first row of the `에피소드` sheet** (the sheet has no start column).

#### Abolished columns

⛔ **The `종류` (kind) and `연출` (staging) columns were abolished** (2026-08-24). `종류` had nothing left to ask once paths that move on without text
went away — **every path is a choice**, and empty text is an error —
and the authoring place for `연출` moved to the **staging graph** (below). **A path's `엔딩키`
was dropped in v14 (2026-08-26) too** — it became the episode's `이벤트키` (above), and the end of a chapter is simply the fact
that it has no outgoing paths.

#### Staging in the middle of a path — kept as **one episode slot** (revised 2026-09-17)

A scene to play first between one episode and another (pure staging) is kept by **putting one more episode
in that place**:

```text
A —"text"→ staging —auto→ B
```

When the player chooses, the staging plays, then it goes on to B without asking. You make it like any other episode,
and leave the later slot's text empty as an **auto path**.

> **⚠ Scenes used to be hung on paths** (the contract's `ViaNodeId`). It was
> **a second way** of saying the same playback order, and saying the same thing in two places makes them diverge — so it was removed.
> **The contract field was also removed on both sides on 2026-09-17** — it no longer exists. The device that upgraded old projects
> was removed with it (owner: *"all previous projects are discarded"*), so old boards that still have hung scenes
> **are redrawn by a person in the shape above.**

⚠ **Don't use the same text twice in one episode.** The player sees two identical buttons,
and since a path's identity is (from, to, text), the tool refuses it.

#### "Add branch" — a fork you visit and return from

A side branch that **splits from a line and comes back** is made in the [Script] tab (formerly [Staging graph]) by clicking a card and using the line `＋` → **"＋ Add branch"**
in the dialogue editor on the right.

| | |
|---|---|
| In the script | One `⑂ Conditional branch` card is inserted **before** that line — **it has no content** |
| On the board | A port opens on that card, and **the branch card to visit (purple · "⑂ branch") is created with it and already connected** |
| That branch card | Isn't an episode — **it has no choice slots** and isn't in the progression JSON. You visit it and return to the same spot (2026-11-17) |

> **The condition isn't written here.** The calling line only marks *"where it splits"* — whether to go
> and what the condition is are decided **at the start of the branch card visited** ("⑂ Condition" above the script when that branch card is selected — §4 branches). If the condition doesn't hold,
> it returns right away. Splitting **after** an episode ends is the job of the choice slots under the episode card.
>
> Branch points connect **only to branch cards** — dropping one on an episode is refused (while visiting and returning, that episode's choices
> wouldn't appear in the game). **Drop** an unconnected branch point **on empty space** to create a new branch card there.

⚠ **It can't be placed after the last line** — the mark attaches *before* a line. Add one more line and
place it before that. Splitting **after** an episode ends is the job of the 3 slots under the card.

> **A pure staging scene can have empty dialogue.** A line that turns the dialogue box off and only runs commands is exactly that —
> the tool fills it with one invisible character on export, so Yarn reads it normally. But if the box is left on while
> empty, an empty speech balloon appears, so empty lines are **flagged as a warning**.

- If a path's choice text in `간선` is empty, it's a normal progression with no fork.
- **The `스탯변화` (stat change) of `간선` (e.g. `trust +1; fatigue -1`) is the only place a stat changes**
  (2026-08-14). It's committed once the moment that path is taken — the moment of crossing between episodes.
  Stats don't change during dialogue, so returning by save/load is always consistent, and the reachability proof
  transitions with exactly this value (no approximation). It's also written in the tool — in the path panel, and in the **episode
  node's choice form** as a `[stat ▾][＋/－][value]` row (2026-08-17). Whichever hand writes it,
  the value lives in this one field, and the syntax is exactly this text.
  A choice involving numbers must be a path fork — options inside an episode only switch staging.
- **Flags (bool stats) are switched on and off, not increased or decreased** (2026-08-19): `met_willow true` ·
  `met_willow false`. They can be mixed in the same field — `trust +2; met_willow true`.
  These are the same words as writing a flag as `true`/`false` in the `조건` sheet.
  - Writing `+1` on a flag is an error. It would read as "increase the number of meetings by one", and writing `+2`
    would have the same result even though it means nothing.
  - Conversely, writing `true` on an integer stat (`trust`) is an error too — integers are values that accumulate.
  - **One path setting the same flag twice is an error** (`met true; met false`). For changes, several
    times simply sum up, but for setting, nobody knows which one wins.

  > ⚠ **Chapters that use flags don't get a `progression.json` for the time being.** The game-side contract
  > can't carry "setting" yet. Exporting without them would run the game with the flag never turned on,
  > so instead of quietly dropping them, it states the reason and stops. That line appears in the validation report.
- ⛔ **The dialogue entry column was removed on 2026-11-18** — `DialogueEntryId` (= the Yarn node name) is made from the EpisodeId (before that, the tool filled it
  with the EpisodeId on creation, and if Excel said otherwise, that value won).
- **X·Y are the position on screen** (v4, 2026-09-18) — dragging a card changes these cells, and editing these cells
  in Excel makes the board follow. The export's Position is the same value.
  ~~(v3: the screen was drawn with a depth layout and these cells were only used for export)~~

### Episode workbook — `episodes/{chapterId}/{episodeId}.xlsx` (6 columns, v14)

**The left two are structure, the right four are dialogue** (2026-08-24, v14):

```
type  condition label │ index  LineId  speaker  content
└─ structure ────────┘ └─ dialogue ─────────────────────┘
```

Scanning only the first column shows the grammar `dialogue·dialogue·IF·dialogue·ENDIF·dialogue` as is, and **the index
means the order of dialogue delivered to the player**. If rows that draw structure had numbers,
those numbers would become row numbers rather than dialogue order.

| Col | Name | Who writes it |
|---|---|---|
| A | `유형` (type) | `대사` (dialogue; empty means the same) · `IF` · `ELSEIF` · `ENDIF` — a drop-down. **This column decides what kind of row it is, so it comes first** |
| B | `조건라벨` (condition label) | A label from the chapter's `조건` sheet — a **drop-down**. Only on `IF` · `ELSEIF` rows |
| C | `인덱스` (index) | A person — **10·20·30 style** (not row numbers). **This value is the dialogue line's identity** — changing the index of a row already written makes it a new line, cutting off that line's staging. To insert between, use a free number like 15. ⚠ **Only dialogue rows have it** |
| D | `LineId` | **Nobody writes it** (a relic — leave it empty. The line's identity is the index in column C) |
| E · F | `화자` (speaker) · `내용` (content) | The dialogue body. The speaker is a **drop-down** too. ⚠ **The only two columns the tool could write back** — when [🔒 dialogue locked] was released in the staging graph (2026-08-24) |

> **⛔ Don't write index · LineId · speaker · content on `IF` · `ELSEIF` · `ENDIF` rows.**
> Those rows draw only **the flow** of the block — dialogue written there isn't carried into the output and is quietly
> discarded. Excel **blocks input itself** in the `인덱스` · `LineId` · `내용` columns, and any value that still gets in
> (by pasting, etc.) is pointed out by row · column in the validation report and **raised as an error.**
>
> ⚠ Only the `화자` column can't be blocked by Excel — a cell has only one validation, and that slot is used by the speaker
> drop-down. If one of the two had to win, the drop-down wins (it's used on every dialogue line, while
> this bar matters once in a while). The validation report catches that side — a stronger bar
> (it can't be escaped by pasting either).

> **Only the index is cleaned up by the tool.** The template pre-fills numbers in rows 2–500, so if you change row 42
> to `IF`, 410 is already written in that cell — **it isn't a person's mistake, so it isn't raised as an error.**
> Sync clears that cell and notes it in the validation report. (If Excel holds the file, it's deferred to the
> next sync. Even if the number stays, the tool already ignores it, so the output is correct.)

**Conditional branches are blocks** (v10, 2026-08-17). Open with an `IF` row and close with an `ENDIF` row —
everything in between is inside the condition. Add branches in the middle with `ELSEIF`, and **nesting works too.**

```
type   | cond. label | index | LineId | speaker | content
대사   |             |  10   |        | 윌로    | The hallway was quiet.
IF     | 신뢰높음     |       |        |         |
대사   |             |  20   |        | 윌로    | I trust you.
ELSEIF | 분노누적     |       |        |         |
대사   |             |  30   |        | 라루    | I'm still angry.
ENDIF  |             |       |        |         |
대사   |             |  40   |        | 윌로    | The door opened.
```

> **Old versions (9 columns) are migrated automatically when the app opens them** (leaving a `.bak`). The `IN` of an `IF` used to point to a span lower in the table
> (`INPUT`…`OUT`); migration pulls those rows up right under the `IF` and closes them with `ENDIF`. **The indexes of dialogue rows are kept** — that number is the line's identity, so renumbering
> would cut off the staging attached to it. So numbers may be jumbled after migration, but
> **the reading order is the sheet's row order**, so behavior isn't affected.

> **Choices were abolished from scripts.** If `CHOICE`/`OPTION` remain, the reader points at that row
> and says where to move it (the chapter's `선택지` · `간선` sheets). Stat changes (old column J) were abolished too — the only place stats
> change is the chapter's `간선` sheet.

---

## 7. Condition expression syntax — precisely

```
trust >= 3           stat comparison
anger <= 0
trust == 5
trust > 3            (opened 2026-08-16)
anger < 2            (opened 2026-08-16)
A ; B                AND (semicolon)
```

⛔ **`cleared:` was abolished on 2026-08-25.** The progression core no longer remembers clear history.
Write the same thing with a **Bool stat (flag)** — make a flag in the `스탯` sheet,
write `flag true` in the `스탯변화` of **every path leaving** that episode, and read it at the gate with
`flag == true`. `복도완료` in the sample (`tests/fixtures/chapter-samples/ch05.chapter.json`) has that shape.

⚠ **If you miss even one way out, the gate quietly changes.** The tool used to count it automatically,
so the migration isn't done by the tool for you — a person has to check it.

**The operators you can use are the five `>=` · `<=` · `==` · `>` · `<`.** `!=` isn't accepted —
letting a comparison the runtime doesn't have pass at authoring time would break in the game.

- **Values are integers only.** Decimals are refused (cutting off boundary bugs like `2.5 >= 3` at the source).
- **A key not in the `스탯` sheet is an error.** The tool doesn't correct typos to similar names.

---

## 8. Conditional branches are blocks (v10)

~~Spans (INPUT / OUT)~~ **were abolished** (2026-08-17). Conditions used to be written as "where it goes in and
where it comes out" with three columns, `태그` · `IN` · `OUT`, and there were six rules. Now it's
one **block opened by `IF` and closed by `ENDIF`**, and there's one rule: **pairs must match**.

```
type   | cond. label | index | speaker | content
대사   |             |  10   | 라루    | Stood in front of the door.
IF     | 신뢰높음     |       |         |
대사   |             |  20   | 윌로    | I trust you.
ELSEIF | 분노누적     |       |         |
대사   |             |  30   | 라루    | I'm still angry.
ENDIF  |             |       |         |
대사   |             |  40   | 윌로    | The door opened.
```

- **They nest.** You can open another `IF` inside an `IF` — something the old version forbade.
- **`ELSEIF` can be added.** Everything up to `ENDIF` is one block.
- **Lines don't need to be moved.** The old version kept the condition body in a span lower in the table and pointed to it by number,
  but now you write it **right where it's read**.

The validator only enforces **pairs**: what's opened must be closed, and nothing can be closed that wasn't opened.

### Condition blocks without dialogue (2026-08-24)

The space between `IF` and `ENDIF` **can be empty.** Owner: *"After setting a condition I have to attach dialogue,
but could you make it so I don't have to? In the staging graph I can make it
detour at that condition with a custom node."*

> ⚠ Those were the words of the time, so it says **"custom node"**. That kind disappeared on 2026-09-17, so
> read it now as **just an episode** — the meaning of a place to visit and return from is the same.

```
type   | cond. label | index | speaker | content
대사   |             |  10   | 윌로    | The hallway was quiet.
IF     | 신뢰높음     |       |         |          ← nothing in between
ENDIF  |             |       |         |
```

⛔ The **IF branch port** that empty branch used to get (a card attached there was **detoured** to and returned from when the condition held — the "branch exit")
was removed on 2026-11-19 (usability UX28 — it didn't check its target and could stop the game). Empty blocks are still read and written, but
there's nothing to hang on them. Visiting only when a condition holds is done by **a branch point + the condition at the start of the branch card**.

- **A block can be at the very end of the script.** It used to be refused (there was no following dialogue line
  to carry the transition). Now the transition lives in the node's **tail**.
- ⚠ **Opening without closing is still an error** — that isn't a block without dialogue, but a transition with genuinely
  nowhere to attach.

> **There are no choices in the script.** If `CHOICE`/`OPTION` remain, the reader points at that row
> and says where to move it (the chapter's `선택지` · `간선` sheets) — see §3 · §4.

---

## 9. What the tool refuses (and why)

| Situation | The tool's response |
|---|---|
| Exporting with validation errors | Doesn't create the file and reports every reason |
| A stat key that doesn't exist · decimals · unsupported operators | An error. Not corrected automatically |
| Empty choice text | An error unless it's an auto path — the only path without text is an auto path (`AUTO` — the episode's only path · no conditions · no stat changes). If it's a path connected without text alongside choices, write text or remove the other path. ⛔ v12's "paths that move on without text are abolished — error" later became the explicit auto path |
| A line with empty dialogue | A warning — exported filled with one invisible character. Normal for a line that only runs staging |

Path endpoints and the fixed choices of test starts **follow automatically** on renames (otherwise they'd become ghosts).

⛔ Refusals · migrations tied to Excel workbooks (not writing while Excel has the file open, removing the episode `종류` column · `엔딩키` column on open and leaving a `.bak`) were
removed in D2 together with the workbooks. Now the tool **refuses to open** a project in a shape it doesn't make (2026-11-18 — [`two-graphs.md`](design/two-graphs.md) section 7 AF, Korean).

---

## 10. Not possible yet (awaiting the owner's decision)

| Item | Status |
|---|---|
| **Tier 2 stat persistence** | The runtime has no place to store it — stats don't carry across chapters. **The most important pending item** |
| Publishing an episode that ends with choices | A gap in the tool's model. Two options are documented, awaiting a decision |
| The `!=` operator | On hold, to be opened together in the parser · docs · samples after checking the runtime |
| Runtime importer (Gate D) | There's nothing yet that reads the exported JSON |
| ~~Stat sheet editing UI~~ | ⛔ Resolved — stats belong to the chapter and are edited in the [Structure] tab (2026-11-17 · `variables` of `game.definition.json` were removed) |
