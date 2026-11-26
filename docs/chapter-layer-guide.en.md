# Chapter layer guide

How to use the layer where chapters (episode structure · branches · conditions · stats) are built. It's a different layer from the
[Writer's guide](writer-guide.en.md), where you write dialogue line by line — **the Writer's guide covers the [Script] tab (dialogue · choices · branches);
this document covers the [Structure] tab (episode structure · conditions · stats)**. The exact rules of the screens and data are in
[`design/two-graphs.md`](design/two-graphs.md) (Korean). This is the English version of [`chapter-layer-guide.md`](chapter-layer-guide.md).

> **There are three kinds of names — this is where people get confused most.**
> The **episode ID** is edited in the `Episode ID` box of the "Inspector" on the right after selecting a card in [Structure] (edit and press Enter —
> paths · cards follow). The **title** (shown name) is edited in the [Script] tab (the Inspector's name box · double-click in the Hierarchy).
> The **choice text** ("Take the left path") is the button text the player sees, and it's edited in the [Script] tab — the choice slot under a card,
> or click the line and use the "Text" box in the Inspector. [Structure]'s path panel only shows the text as a header ("Choice · text").
> Double-click the line to go to that choice in the [Script] tab.

---

## 0. One thing to know first

**The source of truth is the project folder.** Edits in the tool are kept in the project (`project.vnproject.json` and the board files in `story/`),
and the progression JSON · Yarn dialogue in the work folder `exported/` are **outputs written automatically** from it (§5 ⑤).

So these properties follow:

- **Undo with Ctrl+Z.** Moving or deleting a card steps back in one go. Anything deleted comes back as it was with undo.
- **File → Save (Ctrl+S)** writes to the files, and if anything changed, autosave also runs every 10 minutes.
- Dialogue is written in **the card writing boxes of the [Script] tab** (§5 ④).

---

## 1. Reference material — what to read when

| Document | What's in it | When to read it |
|---|---|---|
| **This document** | Screen operations · workflow of the [Structure] tab · condition expression syntax | When actually using the tool |
| [`writer-guide.en.md`](writer-guide.en.md) | For writers, **the [Script] tab** (dialogue layer) | When writing dialogue · choices · branches |
| [`design/two-graphs.md`](design/two-graphs.md) (Korean) | The authoritative screens · data · rules of both tabs | When you need the exact rule for a behavior |
| [`runtime-contract.md`](runtime-contract.md) (Korean) | The contract between the progression JSON · Yarn dialogue and the runtime | When putting exported files into the game |
| [`ch01.chapter.json`](../tests/fixtures/chapter-samples/ch01.chapter.json) | The contract sample (project format) — scenes · auto paths · conditions · stats · event keys | When looking at a new chapter and the shape of runtime output |
| [`ch05.chapter.json`](../tests/fixtures/chapter-samples/ch05.chapter.json) | A sample for screen · episode regression tests (project format) | When looking at existing cases |

> The `ch05` sample has **one intended error** (`branch05.02A` is unreachable — no path changes `trust`, so the "신뢰높음" (high trust) gate
> never opens). It's not a bug — it's a demonstration of what the reachability proof catches.

---

## 2. Where the files are

```
{project folder}/                  ← what you choose with File → [New project…] · [Open…] (a folder)
├─ project.vnproject.json          ← the project — chapters (episodes · paths · conditions · stats) · speakers
├─ story/*.vnstory.json            ← boards — one per chapter, the cards and dialogue of the [Script] tab
├─ runtime.target.json             ← where to send (when set in [⚙ Settings] "Export")
├─ release-baselines/              ← (if present) release baselines — §5 ⑤
└─ exported/                       ← the work folder ([📂 Work folder]) — written automatically (only chapters that pass validation)
   ├─ {chapterId}.progression.json
   └─ yarn/{chapter}_{scene}.yarn   ← dialogue-only Yarn (one per scene)
```

Except for `release-baselines/`, the tool builds and edits all of them — you never need to make them by hand.

---

## 3. The screen

**The [Structure] tab** — the window opens on the [Script] tab, and [Structure] is the second tab.

- **Left** — the **Hierarchy**. Expand and collapse project → chapter → scene → episode (branch cards are "⑂ branch card ID" rows under the calling
  episode — "⑂ title" with "Show IDs" off). One chapter = one board (1:1).
  - Click a row and the board selects it, and the tab on the right follows (see **Right** below). Clicking a chapter row switches to that chapter.
    ↑↓ select · ← collapse · → expand. Collapsing is shared with the [Script] tab. "‹" at its head folds the whole panel.
  - **The [Structure] tab's Hierarchy only selects and collapses** — adding, deleting and renaming chapters · scenes · episodes (right-click ·
    double-click to rename · drag to move) is in the **[Script] tab**. IDs of new items are made automatically by the tool (`ch01` · `scene01` · `ch01_scene01_new01` …).
  - With "Show IDs" in the toolbar (on at first), rows show IDs with the shown names beside them. Turn it off and they show names, like the [Script] tab (remembered on this PC).
  - **Changing an ID** is done in the box in the "Inspector" tab after selecting the row (or card) — chapter → `Chapter ID` (the board · progression JSON file name follow ·
    selecting a chapter opens the "Conditions & stats" tab, so open the "Inspector" tab by hand), scene → `Scene ID` (every episode of that scene and the scene name · note
    follow — it won't change to an ID another scene uses), episode → `Episode ID` (paths · cards follow). It's applied with Enter or by clicking elsewhere; Esc returns to the
    value before applying. `exported/{oldId}.progression.json`, exported under the old chapter ID, is removed by the next export (§5 ⑤).
    Shown names (chapter name · scene name · episode title) are edited in the [Script] tab.
  - **Deleting a chapter** is done in the [Script] tab — select the chapter and press **[Delete]** in the toolbar ("Delete chapter") → click once more in the confirmation box beside the button.
    It removes the chapter with its scenes · episodes · the cards on the board, and undo (Ctrl+Z) brings it back. Don't delete files from the folder by hand — the board and
    nodes stay in the project and blow up with "no calling node".
- **Center** — the graph board. Card = episode, line = path (edge).
  - Chips on the first row of a card's header: blue `Scene start` = where the scene is entered from outside (the chapter's starting episode — the first in the chapter's episode
    list — gets this chip too) · purple `{scene ID}` (the scene name with "Show IDs" off) · `🔒` = an incoming path has a show/unlock condition · `⚠` = an error or unreachable
    (there's a path, but a condition stops it from being reached). The second row is the episode ID (the title with it off). **A faded card** = not yet connected from the start —
    a warning because it's work in progress, and it doesn't block export. The event key is a value the tool doesn't interpret, so cards have no badge for it (§6).
  - On a line: a text box — the choice text in the middle (`AUTO` for an auto path, `⚠ No text` if empty), "Show: …" · "Unlock: …" above, stat changes below.
  - **Ways out are below the card, ways in are above it** — on the bottom edge **three points stand in a row**, and those paths go into **one point on the top edge**
    of the target card. The same handling as the [Script] tab.
    - Three points is **the minimum** — with four paths, all four are drawn. Empty slots are **hollow points**.
    - Point colors: **orange** = connected choice · **blue** = auto-advance · **red** = no text (to be written — until then it goes to the game with placeholder text) · **gray hollow** = a slot not used yet.
    - **Branches** don't appear here — they leave as purple **dotted lines** from the branch point on the card's side edge (the side visited) and go into a small chip of the
      branch card (§4 branches).
- **Right** — three tabs: **"Inspector" · "Speakers" · "Conditions & stats"**. "›" at the end of the tab row folds the whole panel.
  - Selecting something goes to the tab that shows it — the project row in the Hierarchy → "Speakers", a chapter row → "Conditions & stats", anything else (scene · episode rows ·
    cards · lines · chips on the board) → "Inspector". Clicking empty board to clear the selection or switching chapters with the combo leaves the tab as is, and you can open a
    tab by hand at any time.
  - **"Inspector"** — the one thing selected now. Episode → `Episode ID` box · list of choices (read-only) · dialogue preview; choice line → path panel (show condition ·
    unlock condition · locked hint · stat change); branch chip → branch panel (§4 branches); scene → `Scene ID` box · that scene's episodes; chapter → `Chapter ID` box.
  - **"Speakers"** — project-wide. Link a character key to each speaker (names are added and edited in the [Script] tab). Adding a speaker gives it a **temporary key**
    — `temp_` + the romanized name (하린 → `temp_harin` · standard romanization, so 카스미 is `temp_kaseumi` · on a clash `temp_harin2` · with nothing to romanize
    `temp_char01`). It doesn't follow renames. Speakers with a temporary key get a ⚠ in this tab (they don't appear in the validation report).
    Speakers who appear in that chapter's dialogue but have **no** key show as a 🟡 warning in the validation report below and get a ⚠ in this tab too (it doesn't block the
    progression JSON). Entering a key that matches the art clears it.
  - **"Conditions & stats"** — per chapter (the small chapter drop-down at the top right shows the current chapter). A condition table (name box → expression box) and a stat
    table (key box · type · values) — names · keys are edited in their boxes. Under each table there's always a name box + [＋ Add] (§5 ③).
- **Top toolbar** — chapter combo (showing the chapter name) · [Auto layout] · [Fit to screen] · "Hide empty choices" · "Show IDs".
  Episodes are added and deleted in the [Script] tab ([＋ Episode] · [Delete] · the Delete key · right-click in the Hierarchy — §4).
  When the project changes, the screen redraws itself (edits report the change — there's no folder watching).
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
| Rollback / backlog jump | Replays from the scene start **within the same scene**. Nothing is committed |

In other words, **everything can be taken back within a scene, and it's committed when the scene ends.**

### Where it's set

**Which scene an episode belongs to is decided in the [Script] tab** — [Structure] doesn't change scenes.

- A new episode belongs to a scene from the start — right-click a scene row in the [Script] Hierarchy → "Add episode" · right-click empty space inside a scene's frame on the
  board → "Add episode" add it to that scene; the toolbar's [＋ Episode] decides in the order selected scene → selected card's scene → new scene.
- To move it to another scene, **drag** the episode row in the [Script] Hierarchy **onto another scene row**. Dropped on a scene row of another chapter, it moves to that chapter, and since chapters can't be connected, every path touching the episode is cut (conditions · stats the new chapter lacks are added to it).
- New scenes are created with right-click a chapter row → "Add scene" · right-click empty space inside a chapter's frame (outside scenes) on the board → "Add scene".
- A scene's ID (`Scene ID` — the name of the save · rollback boundary) is edited in the `Scene ID` box of the "Inspector" tab after selecting the scene row in [Structure] —
  every episode of that scene follows. The scene name is edited in the [Script] tab.
- **An episode always belongs to a scene.** Episodes with an empty scene ID are neither created nor read.

### Two rules

1. **A scene has only one entrance from outside** (= the scene start). Rollback returns there and Continue resumes there, so with two landing points,
   "where does this scene start" would differ by route.
   - The chapter start counts as that one.
   - Several paths coming into the same scene start is **normal**.
   - Leaving the scene and **coming back to the same scene start is normal too** (a hub structure).
   - The [Script] tab won't connect to another card of a scene that already has a start. If it's broken anyway, validation flags **the path that made the second entrance**
     as an error and blocks that chapter's export.
2. **Auto paths can cross scenes.** What happens at the boundary (clearing the stage · committing · saving) is the same as crossing with a choice that asks; only no box appears.

### On screen

- **Cards**: chips on the first row of the header — a blue `Scene start` on the card where a scene starts, and a purple `{scene ID}` — with the episode ID on the second row.
  Turn off **Show IDs** in the toolbar (on at first) and the Hierarchy and cards show names (scene names · episode titles), like the [Script] tab — remembered on this PC.
- **Folded board** ([Script] tab — double-click a chapter label): the inside of the board is shown **grouped by scene**. Branch cards no episode calls gather at the bottom
  under `Outside scenes`.
- **Hierarchy** (both tabs): expands and collapses as `project → chapter → scene → episode`.

---

## 4. Handling (the graph board)

| What you want | How |
|---|---|
| Select an episode | **Click** a card → the "Inspector" tab on the right |
| **Make an episode** | Done in the [Script] tab — the toolbar's **[＋ Episode]** (selected scene → selected card's scene → new scene) · **right-click** a scene row in the Hierarchy **→ "Add episode"** · right-click empty space inside a scene's frame on the board → "Add episode" → a new episode is created in that scene with no path. Connect by dragging a choice slot under a card on the [Script] board. It isn't added in the [Structure] tab |
| **Delete an episode** | Done in the [Script] tab — select the card and press **[Delete]** in the toolbar ("Delete episode" → the confirmation box beside the button) · the **Delete** key (doesn't ask) · right-click an episode row in the Hierarchy → "Delete episode". Its paths · card go with it, and one Ctrl+Z brings it back. A chapter's last episode isn't deleted |
| **Go write the script** | **Double-click** a card → goes to that card in the [Script] tab (switches tabs and selects that card). Write the dialogue in that card's writing box (§5 ④) |
| Edit a path | **Click a choice line or its text** → the path panel in the "Inspector" (show condition · unlock condition · locked hint · stat change) |
| Go edit the choice text | **Double-click** a choice line or its text → goes to that choice in the [Script] tab (the text is edited there) |
| Select an area | **Drag** on empty board → the cards caught become a group, and dragging any card of the group moves them together (one undo step when dropped) |
| Clear the selection | **Click empty** board |
| **Connect · disconnect** (in the [Script] tab only) | **Write text** in one of the slots under a card and **drag its point onto another card** to make a choice path. You can also connect first without text — the first slot of a card whose slots are all empty makes an auto path, any other slot makes a choice without text (write it later). To disconnect, drop it on empty space. What's made there shows in [Structure] as **the same path**. [Structure] doesn't connect or disconnect paths; it only edits their conditions · stat changes |

**People decide positions.** **Drag** cards to move them, and the position you drop them at is saved as is — the same handling as the [Script] tab.
If you moved one by mistake, one **Ctrl+Z** (one drag is one undo). Zoom with the wheel, drag with the middle button to move the board, and the
toolbar's **[Fit to screen]** adjusts only the zoom and scroll so all of this chapter's cards fit on one screen (card positions stay).

**The board (white) and outside the board (gray)** — the board is as big as this chapter's cards plus margins, and if it's smaller than the screen, the surroundings are the
gray outside. The outside accepts input like the board: click and drag to select an area, and cards and the selection rectangle dragged outside the board stay visible. Drop
a card outside the board — not only to the right · down, but also dragging the leftmost · topmost card further left · up — and the board widens that way while every card
stays where it is on screen (if the board exceeds the screen, scroll bars appear). The board is centered on the screen at first · when switching chapters · with
[Fit to screen] · when changing zoom.

If the board gets tangled, the toolbar's **[Auto layout]** re-lays it out modeled on how the cards are placed in the [Script] tab (positions moved by hand are lost —
for a chapter with no cards in [Script], in flow order, top to bottom). This also steps back in one go.

### Branches — a detour that's always visited

**Branches** opened on lines in the [Script] tab are drawn on this board as **purple dotted lines**. They're a different kind from choices (solid lines):

| | Choice (solid) | Branch (dotted) |
|---|---|---|
| Who picks | **The player** | Nobody — **it's always visited once** |
| Text | Required (only auto paths are empty) | None |
| Stat change | Applied once when this path is taken | None |
| Condition | Show · unlock conditions on **this path** | **At the start of the branch card visited** |

⭐ **What's gated is the side visited.** The branch is always taken — when visited, the condition stops it, it plays nothing and comes straight back.

**The main place to choose the condition is the [Script] tab** — select a branch card and a **"⑂ Condition"** box appears above the script.
In [Structure], clicking the branch card's **chip** opens the same **single condition box** in the "Inspector" on the right. The choice is written at the start of that branch
card, and **its content plays only when the condition holds**. "(always)" means it always plays. One undo (Ctrl+Z) brings it back. The dotted line and the branch point are
**display only** — clicking does nothing, and hovering says where it goes.

**What's visited is a branch card.** A branch card isn't an episode, so it doesn't stand on the board by itself; it's attached to the right of the calling episode as a
**small chip** ("⑂ branch card ID" — "⑂ title" with "Show IDs" off · with a condition, the chip grows to two rows with "Condition: label" on top), and the dotted line goes
into it. Clicking the chip shows **branch card ID · called from · condition · script** (read-only) on the right — the script and title are edited in the [Script] tab.
The **branch card ID** (= the game's Yarn node name, automatically `ch01_scene01_branch01`) is edited in that box, then Enter — it won't change to a name another node in the
project uses. It also appears in the Hierarchy as a "⑂ branch card ID" row under the calling episode (title beside it · "⑂ title" with "Show IDs" off). Branch cards have
no choices and aren't in the progression JSON.

> ⚠ **Opening and removing branches is the [Script] tab's job.** This board only **reads and draws** branches and accepts only conditions — if the same thing had two
> owners, they would diverge.

> ⚠ If the start of that branch card was **written by hand in the dialogue editor**, this box is locked and the reason appears. Overwriting it here would make a script
> with mismatched pairs — open that card in the [Script] tab and edit it there.

---

## 5. Workflow

### ① Make a chapter

It's made in the [Script] tab — right-click the project row · empty space in the Hierarchy → **"Add chapter"**, or right-click empty space outside chapters on the board →
"Add chapter". If there are no chapters, **[＋ Create first chapter]** appears in the middle of the board (the same button in [Structure] takes you to the [Script] tab and
creates it there). A "New chapter" (ID automatic — `ch01` …) is created with a "New scene" scene and one episode, and selected — one undo step.
The chapter name is edited in the [Script] tab (the `Chapter name` box at the top of the "Conditions & stats" tab · double-click in the Hierarchy), and the chapter ID in the
`Chapter ID` box of the [Structure] "Inspector". A new chapter starts **without stats** — stats belong to the chapter and are added in the "Conditions & stats" tab.

> **⚠ Keep stats to 2–5, with narrow ranges around 0–5.**
>
> This isn't taste, it's a **computational limit**. To prove "no playthrough can reach this episode", the tool goes through **every possible state**, and that number
> multiplies with the number of stats and their ranges:
>
> | Stats | Range | States to search |
> |---|---|---|
> | 2 | 0–5 | 36 |
> | 3 | 0–10 | 1,331 |
> | 5 | 0–100 | **about 10 billion** — it never finishes |
>
> Beyond that, the proof stops partway and "unreachable" is downgraded **from a certainty to a warning** (it won't call something it doesn't know an error).
> And **if you start wide and narrow it later, every condition expression already written loses its meaning** — starting narrow is far cheaper.

### ② Build the branches — the heart of this layer

The hands that build branches are in **the [Script] tab**. [Structure] is the place to look at the shape and set conditions · stats.

1. In the [Script] tab, click the starting card (the first time, the starting episode with the `Scene start` chip).
2. The toolbar's **[＋ Episode]** (it goes in the selected card's scene) or **right-click** a scene row in the Hierarchy **→ "Add episode"** → it's created with no path, with a
   placeholder Id (`ch01_scene01_new01`… — containing the chapter · scene).
3. Write text (e.g. "Follow Laru") in a choice slot under the starting card, and **drag** its point onto the new card to create a choice path. Connecting without text makes
   an auto path (the first slot of a card whose slots are all empty) or a choice without text (any other slot — write the text later).
4. Names: the title is set in the [Script] tab (the Inspector's name box · double-click in the Hierarchy), and the episode ID in the `Episode ID` box of the [Structure] "Inspector".

Connecting to an existing episode (merging · looping) works the same — drag the choice point onto that card.

### ③ Set conditions

A condition is registered once in a **name ↔ expression** table, and called **only by its name** everywhere.

1. In the box under the condition table of the **"Conditions & stats" tab** on the right (per chapter — the small chapter drop-down at the top right is the current chapter),
   type a name (e.g. `HighTrust`) and press **[＋ Add]** (or Enter) — it starts as "Undecided". Write something like `trust >= 3` in that row's **expression** box (Enter · click elsewhere).
   - An expression is "stat key · comparison · value" — the syntax is in §7. If an expression doesn't hold, the [Validation report] below says "the expression doesn't hold" and
     why (`=>` → `>=`, `=` → `==`, `&&` · `,` → `;`, `$key` → `key`, and for a key not in the stat table, the list of existing keys), and the box below shows **examples using
     this chapter's stats**.
   - As soon as the expression is filled in, the condition line (`<<if …>>`) in the writing boxes of cards using that condition changes too. An expression that names a stat
     not yet created goes onto the board the moment that stat is added.
   - A condition's **name** is edited in the row's name box — choices · branch cards using that name follow (Esc cancels). Deleting is done in the [Script] tab's
     "Conditions & stats" tab. A condition with an empty expression is "Undecided" — it always passes and only warns.
   - Stats too: type a key in the box under the stat table in the same tab and press [＋ Add] (it starts as a number (integer) 0, 0–100) — the type ("Number (integer)" ·
     "On/Off") and values are edited on its row, and the **key** in the row's key box.
   - **Write stat keys in English** — start with a letter and use only letters · digits · `_` (e.g. `courage` · `met_harin`). The key goes into Yarn as is, so Hangul ·
     spaces · symbols aren't accepted, and the status bar says why. A project that still has Hangul keys is flagged with warnings by the [Validation report] — fix them to
     English in the key box and paths · conditions follow. Entering the key box shows a warning on that row — the key is the name stored in saves, so changing it after
     release returns that stat to its initial value in playthroughs already saved.
2. **Click a choice line** on the board → choose that name in one of the two combos of the path panel (the condition's expression box appears below the combo, and
   [＋ New condition] beside it creates the name and expression and sets it right away).
   - **Show condition** = if not met, **the choice itself isn't shown**
   - **Unlock condition** = if not met, **it's shown locked** with the `Locked hint` (e.g. "Not enough trust")

   To hide, use the show condition; to show it locked, use the unlock condition. There's no "hide when locked" box — unlock condition + hide has the same result as
   writing that expression in the show condition.
3. That's it — **there's no [Apply].** It applies the moment you choose (one undo step). Only text boxes (locked hint · stat values) apply when you click elsewhere or press
   Enter. File → Save (Ctrl+S) · autosave write it to the file.

**Show / unlock conditions belong to the path, not the episode.** The two combos in the path panel are **drop-downs** of the chapter's condition names, so typos can't
happen — a newly added condition appears in the list automatically.

They're on paths rather than episodes because when two routes come into the same room, each route's conditions can differ. Put on the episode, there'd be no place to tell
the two apart.

**Stat changes belong to the path too** — it's **the only place** a stat changes (§6).

### ④ Write the script

**Double-click** a card in [Structure] to go to that card in the [Script] tab. Dialogue is written in that card's **writing box** — one line is one line of dialogue
(`Speaker: dialogue`), applied when you pause or leave the box (one undo step). You can also edit it in the dialogue editor (line table) of the "Inspector" on the right.
The tool keeps each line's identity (LineId), so what's attached to a line follows it even when you edit the dialogue. [Structure]'s "Inspector" shows a read-only preview
of the dialogue. Condition blocks are in §8; for details, see the [Writer's guide](writer-guide.en.md) and [`two-graphs.md`](design/two-graphs.md) §2.4 (Korean).

### ⑤ Validate and export

The [Validation report] below runs automatically. Besides structural errors, a **reachability proof** runs and says "no route can reach this episode",
**pointing to the condition that causes it**.

- An episode not yet connected by any path (work in progress · unfinished) is **a warning, not an error** (`not connected`), and doesn't block export.
- An episode that has paths but can't be reached **because of conditions** is a design bug, so it's an error — fix the condition or stat change the proof points to.
- **Loops with no way out are warned about too.** Going around a loop is fine in itself — if there's a choice that leads out even after going back, as with "ask again",
  or a path that opens once stats build up, nothing is said. Only when, once inside, every choice keeps you circling so the chapter can't end, it attaches "This loop has no
  way out" (or says so when you're trapped only depending on stats), and for a loop of auto paths only, "This loop is made of auto paths only — it goes around A → B → A
  forever without asking …" to the episode closest to the start. A place with no path to choose ends the chapter in the game, so it isn't a loop. It doesn't block export.

**Export happens automatically.** Every time the project changes, **every** chapter's `exported/{chapterId}.progression.json` is updated (it isn't written if the bytes
are the same). A chapter whose progression JSON is written also gets its dialogue-only Yarn (`exported/yarn/{chapter}_{scene}.yarn` — one per scene) at the same time.
**[📂 Work folder]** in the top strip opens `exported/`.

- **If there are validation errors, that chapter's file isn't touched.** "The progression JSON wasn't written" appears at the top of the validation report, and
  `· progression JSON not written` is added to the header (visible even when collapsed). Below it are the blocked chapters' errors — even if it isn't the selected chapter,
  even if the reason is the progression core's refusal (the chapter ID at the front of the line tells you which chapter). Leaving a stale file is better than overwriting it
  with garbage — what the runtime reads is always a version that "was correct once". Test starts are test data and aren't mixed in.
- **A chapter with no episodes isn't blocked.** There's simply nothing to write, so it's skipped (the file isn't touched) and `· progression JSON not written` doesn't appear.
  That chapter's validation report says "There are no episodes yet".
- **Old files of deleted or renamed chapters are removed.** On export, `{chapterId}.progression.json` of chapters no longer in the project is deleted from `exported/` —
  left there, the game would keep loading a chapter that doesn't exist. `exported/` is a place the tool builds from the model, so nothing is lost by removing them (undo the
  deletion · rename and the next export writes it again). Removed files appear on the "Progression JSON →" line of the validation report; files that couldn't be removed
  (held by another program) appear at the top of the report.
- **Release baselines** — copy the version imported to the server to `release-baselines/{chapterId}.progression.json` (there's no button in the tool — a person copies that
  file from `exported/`), and from then on, a chapter with edits that **change the order numbers** of existing choices (inserting before · deleting · swapping) has its
  progression JSON update blocked, and the validation report points to that episode. Adding at the end and editing the text · conditions · stat changes in the same place
  aren't blocked. This is because choice history piles up on the server by choice order number (`OptionIndex`) ([`runtime-contract.md`](runtime-contract.md) §3, Korean).

**Files are sent to their destinations only on save.** In [⚙ Settings] "Export (choose mirror folders)", set a folder outside `exported/` for each format — the progression
JSON's destination is the game folder (the runtime's `ChapterProgression`), and the Yarn dialogue can have its own. File → Save (Ctrl+S) mirrors the freshly written
`exported/` there — intermediate states while editing don't go out. Old files are removed **only if they're exactly as the tool sent them (same bytes)**; files a person
edited or that came from elsewhere · old destinations from before a change aren't touched. Missing folders aren't created. The Yarn dialogue isn't sent to the same place
as the game folder (its node names would clash with the staged Yarn). Where things were sent appears in the validation report right after saving ("Sent to →").

---

## 6. What episodes and paths hold

| Where | Fields |
|---|---|
| Episode | episode ID · title · **scene ID** · **event key** · position on the board (X · Y) · dialogue (the card on the board) |
| Path (choice) | from · to · choice text · auto or not · show condition · unlock condition · locked hint · **stat changes** |
| Chapter | chapter ID · name · conditions (name → expression) · stats (key · type · initial · min · max) |
| Project | speakers (name · character key · writer-only name color · name background) — shared by all chapters |

- **The starting episode = the first in the chapter's episode list** (there's no separate box for it).
- **The position on the board is the export's `Position`** — dragging a card changes it.
- An episode's Yarn node name (`DialogueEntryId`) is made from the episode ID — characters other than letters · digits · underscores become `_` (`장면 1` → `장면_1`).
  Don't change an episode ID once released — the game finds the node by this name and saves remember it.

### Event key — an index handed to Unity

An index that lets Unity raise its own events or rewards **when that episode has been fully watched**. **The tool doesn't interpret this value at all** — no badge on the
graph, no validation, no effect on progression. It's simply carried out as the node's `EventKey` in the progression JSON. Left empty, nothing happens. Which ending a
chapter is · what the next chapter is isn't decided by this tool — the end of a chapter is simply the fact that it has no outgoing paths.

### Staging in the middle of a path — kept as **one episode slot**

A scene to play first between one episode and another (pure staging) is kept by **putting one more episode in that place**:

```text
A —"text"→ staging —auto→ B
```

When the player chooses, the staging plays, then it goes on to B without asking. You make it like any other episode, and leave the later slot's text empty as an
**auto path**.

⚠ **Don't use the same text twice in one episode.** The player sees two identical buttons, and since a path's identity is (from, to, text), the tool refuses it.

### Auto paths — going on without asking

A path connected without text from the first slot of a card whose slots are all empty is an **auto path** (`AUTO`). Three rules — breaking them is an error:

- It must be the **only** path of that episode — if there's something to choose, it's a choice that asks.
- **It can't have conditions** — auto-advance must always give the same result.
- **It can't have stat changes** — stat effects happen only on choices the player can see.

Writing text on an auto path makes it a choice. Auto paths can cross scenes (§3-1).

### Branches — a fork you visit and return from

A side branch that **splits from a line and comes back** is made in the [Script] tab: select the card, then in the line table of the "Inspector" on the right use `＋` at
the right of that line → **"＋ Add branch"**.

| | |
|---|---|
| In the script | A branch point appears **before** that line |
| On the board | **The branch card to visit (purple · "⑂ branch") is created with it and already connected** — at first it's always visited, with no condition |
| That branch card | Isn't an episode — **it has no choice slots** and isn't in the progression JSON. You visit it and return to the same spot |

> **The condition isn't written on the calling side.** The calling line only marks "where it splits" — whether to go and what the condition is are decided **at the start of
> the branch card visited** ("⑂ Condition" above the script when that branch card is selected — §4 branches). If the condition doesn't hold, it returns right away.
> Splitting **after** an episode ends is the job of the choice slots under the episode card.
>
> Branch points connect **only to branch cards** — dropping one on an episode is refused (while visiting and returning, that episode's choices wouldn't appear in the game).
> Branch cards don't branch (one level). **Drop** an unconnected branch point **on empty space** to create a new branch card there.

⚠ **It can't be placed after the last line** — a branch point stands *before* a line. Add one more line and place it before that.

> **A pure staging scene can have empty dialogue.** A line that turns the dialogue box off and only runs commands is exactly that — the tool fills it with one invisible
> character on export, so Yarn reads it normally. But if the box is left on while empty, an empty speech balloon appears, so empty lines are **flagged as a warning**.

### Stat changes — the only place a stat changes

**A path's stat changes are the only place a stat changes.** They're applied once the moment that path is taken — the moment you cross between episodes. Stats don't
change during dialogue, so returning by save / load is always consistent, and the reachability proof steps through exactly these values (no approximation). A choice that
involves numbers must be a path — options within an episode only vary the staging.

Add a row with **[＋ Stat change]** in the path panel, choose the stat and set the value. Only stats declared in the "Conditions & stats" tab can be chosen.

- **Number (integer) stats are added to and subtracted from** (`trust +1` · `fatigue -1`). The initial value and range are those of the stat table, and the value doesn't
  go outside the range.
- **On/off stats (flags) are turned on and off** (`met_willow true` · `met_willow false`). They can be mixed on one path — `trust +2; met_willow true`. These are the same
  words used for flags in condition expressions (`true` / `false`).
  - Writing `+1` on a flag is an error — it reads as "add one to the number of meetings", and writing `+2` would mean nothing yet give the same result.
  - Conversely, writing `true` on an integer stat is an error too — integers are values that accumulate.
  - **Setting the same flag twice on one path is an error** (`met true; met false`). Several changes simply add up, but with setting, nobody knows which one wins.

---

## 7. Condition expression syntax — exactly

```
trust >= 3           stat comparison
anger <= 0
trust == 5
trust > 3
anger < 2
met_willow == true   flag (on/off stat)
A ; B                AND (semicolon) — all must hold to pass
```

**The comparisons you can use are the five `>=` · `<=` · `==` · `>` · `<`.** `!=` and "or" (`||` · `or`) aren't accepted — letting a comparison the runtime doesn't have pass
at authoring time would break in the game.

- **Values are integers only.** Decimals are refused (cutting off boundary bugs like `2.5 >= 3` at the source). Flags use `true` · `false`.
- **A key not in the stat table is an error.** The tool doesn't fix typos into similar names.
- Flags use only `==` — size comparisons are errors.

**"Has that episode been passed" is written with a flag.** The progression core doesn't remember clear history. Make an on/off stat in the stat table, write `flag true` in
the stat changes of **every path leaving** that episode, and read it at the gate as `flag == true`. `복도완료` (`passed_corridor`) in the sample
(`tests/fixtures/chapter-samples/ch05.chapter.json`) has that shape.

⚠ **Miss even one way out and the gate quietly changes.** The tool doesn't count them for you, so a person has to check.

---

## 8. Conditions inside dialogue — they're blocks

In a card's writing box, a condition is a **block** in Yarn syntax — `<<if expression>>` opens it and `<<endif>>` closes it. In between is inside the condition, and
`<<elseif expression>>` adds another arm. The expression is the chapter condition's expression translated to Yarn, so stats are read with `stat("key")`.

```
윌로: 복도는 조용했다.
<<if stat("trust") >= 3>>
윌로: 너를 믿어.
<<elseif stat("anger") >= 2>>
라루: 아직도 화가 나.
<<endif>>
윌로: 문이 열렸다.
```

- **The expression must be one from that chapter's condition table.** The writing box holds a condition by its **name**, and only for display translates that name's
  expression to Yarn (`true` if undecided). Write an expression that isn't in the table and the box turns red and nothing is applied — first make the condition in the
  "Conditions & stats" tab. If several conditions have the same expression (undecided ones are all `true`), the condition currently set in that place is chosen.
- **Pairs must match** — what's opened must be closed, and nothing can be closed without being opened.
- **Nesting isn't supported** — opening a new condition inside a condition is reported as a problem and handled as another arm at the same depth.
- **A block may be empty inside.** But to visit other dialogue only when a condition holds, use **a branch point + the condition at the start of the branch card** (§4 branches).
- **A block may be at the very end of the script.**
- Dialogue can **read** a stat (`{stat("key")}`) but **can't change** it — don't use `<<set>>` (§6).
- **There are no choices inside dialogue** — they exist only in the choice slots (paths) under episode cards.

---

## 9. What the tool refuses (and why)

| Situation | The tool's response |
|---|---|
| A chapter with validation errors | Doesn't write that chapter's file, and reports every reason |
| Missing stat key · decimals · comparisons not accepted | Error. No automatic correction |
| Empty choice text | A 🟡 warning unless it's an auto path — it isn't blocked; it's written with the placeholder `[TODO] Missing choice text -> destination ID` (2026-11-26 · an error before that). The runtime doesn't accept empty text as is, and dropping the choice would shift the OptionIndex of the choices after it. It goes away once you write the text in the [Script] tab |
| Two entrances into a scene from outside | Error — points to the path that made the second entrance |
| A line with empty dialogue | Warning — it's exported filled with one invisible character. Normal for a line that only runs staging |
| A project in a shape the tool doesn't make · a project in a format before 5 | **Refused when opening**, with the reason — it isn't patched on the way in |

Path endpoints and fixed choices of test starts **follow renames automatically** (otherwise they'd become ghosts).

---

## 10. What doesn't work yet

| Item | Status |
|---|---|
| `!=` | On hold until confirmed in the runtime, then the parser · docs · samples open it together |
| Values that live beyond a chapter | Progression state lives as long as a chapter — stats don't carry over between chapters (progression between chapters is outside this tool) |
| Showing costs on choices | There's no way yet to show a path's stat changes on the choice button |
