# Story Director — Writer's Guide

A tool for writing a visual novel's dialogue, choices and branches. No programming knowledge is needed.
This one document takes you from getting started to handing over your work.

> **This document covers the [Script] tab — the layer where you write dialogue.**
> For the [Structure] tab, where condition expressions, stats, and the conditions and stat changes on each choice are set,
> see the [Chapter layer guide](chapter-layer-guide.en.md).
>
> The screen language follows your PC's language (Korean on a Korean PC, English otherwise). To change it: [⚙ Settings] →
> "Other" → "Language (언어)" — it takes effect the next time you start the app. New items get their starting names ("New chapter",
> "New scene", "New episode") in the screen language at the time they're made — names you've given are never changed by the language.

## 1. The first time only

1. Unzip the archive you received wherever you like. E.g. `Documents\StoryDirector\` (don't run it from inside the zip — unzip it first).
2. Double-click `StoryDirector.exe`.
3. **If a blue Windows warning appears**: click "More info", then "Run anyway".
   (It's the standard warning for a program still in development that isn't signed yet.)
   - **Mac**: move the `StoryDirector` app you unzipped into Applications and open it. The first time, a "can't be opened" window appears — close it,
     then **System Settings → Privacy & Security → [Open Anyway]** (only once). In this document, press `Ctrl+…` as `⌘` on a Mac (Ctrl works too · redo is `⌘⇧Z`),
     and `Delete` is the Mac's delete (⌫).
4. When the tool opens, "No project yet" and **[Open…] · [New project…]** appear in the middle of the board (the **File** menu at the far left of the
   top bar has the same). **A project is one folder.**
   - First time? **New project…** → choose an empty folder to put the project in. `project.vnproject.json` and `story\` are created inside it.
   - Have a project you received? **Open…** → choose that **project folder** (choose the folder, not a file).
5. From then on, starting the tool automatically reopens the project you had open last.

## 2. The order of writing (the basic loop)

> **Writing happens in the [Script] tab.** It's the first of the two tabs at the top, and it's the writer's place. The tool opens on [Script] (new projects too). [Structure], where conditions and stats are attached, is the second.

1. **Find where to write** — in the Hierarchy (explorer) on the left, go down `chapter → scene → episode`.
   **Click the ▶ ▼ to the left of a name** to expand and collapse (clicking the name doesn't collapse — that spot is
   for selecting, dragging and renaming).
   - **Find** — type a name or ID in the box at the top of the Hierarchy (for Korean names, initial consonants alone work — "ㅅㅌ") and only matching rows and their chapter · scene rows remain. It searches inside
     collapsed chapters too. **Enter** (or ↓) selects the first match and the board goes to that card. Clear it with **Esc** or the ✕ at the right of the box and the tree returns to how you folded it. The [Structure] tab works the same.
   - **You can select several rows at once** — hold Ctrl (⌘ on Mac) and click to add or remove a row; hold Shift and click to select everything from the row clicked before
     to this one (chapters · scenes · episodes can be mixed). The board and Inspector follow the last row clicked, and [Delete] is off while several rows are selected.
     **"Hide empty choices"** in the Inspector and the **formatting tools** at the top apply to every episode in the selected rows at once (a chapter row means every episode in that chapter ·
     if "Hide empty choices" is mixed, it shows half-checked, and clicking turns it on for all).
   - **Any row can be selected.** Selecting an episode selects its card, and its dialogue line table appears in the "Inspector" on the right.
     Selecting a scene opens the scene name box and scene note in the "Inspector"; selecting a chapter opens the "Conditions & stats" tab; selecting the project row at the top opens the "Speakers" tab.
     With a chapter selected, click the "Inspector" tab to see **how many characters you've written in that chapter** (with and without spaces) and how many lines — only dialogue and narration count, not speaker names.
     Clicking empty space on the board does the same — inside a scene's frame selects that scene, inside a chapter's frame that chapter, outside chapters the project row.
   - **⌂** = the episode where that scene starts (where the game returns and resumes).
   - **⑂** rows under an episode = the branch cards that episode calls (see "Branch cards" below).
   - **⚠** — on a scene row: a scene with two entrances from outside (a scene must start in one place).
     On a chapter row: that chapter has errors — see the "Validation report" at the bottom of the [Structure] tab.
   - You can move with the keyboard too (after clicking a row once): `↑↓` move between rows — the selection shows on the right right away; `←` collapse · go to parent; `→` expand · go to first child.
     All the keys are collected in [⚙ Settings] → "Shortcuts".
   - What you collapse **stays collapsed**. It's still collapsed after you restart the app.
1-1. **If there's no place yet** — you can make one here. **An episode always belongs to a chapter and a scene.**
   - If this is your first time and there are no chapters, press **[＋ Create first chapter]** in the middle of the board — "New chapter" and "New scene" are created together with the first card.
     The same button in the [Structure] tab takes you to the [Script] tab and creates it there. (While there are no chapters, [＋ Episode] is off.)
   - **[＋ Episode]** in the toolbar adds to the chapter shown in the toolbar's "Chapter" combo — in the selected scene, or else in the selected card's scene.
     If nothing is selected, it also creates a "New scene".
   - New chapter = **right-click the project row (or empty space) in the Hierarchy → `Add chapter`**, or **right-click empty space outside chapters on the board → `Add chapter`**.
     A "New chapter" is created right away with a "New scene" scene and one episode, and is selected (if you right-clicked on the board, the card goes where you clicked; from the Hierarchy, to the right of the board's
     other cards).
   - **Right-click a chapter row** → `Add scene` (with its first episode) · `Rename`
   - **Right-click a scene row** → `Add episode` (in that scene — no path is connected; connecting is step 3 below) · `Rename`
   - **Right-click an episode row** → `Rename` · `Delete episode`
   - **Right-clicking empty space on the board** works too — inside a scene's frame `Add episode`, outside scenes but inside a chapter's frame `Add scene`, outside chapters `Add chapter`.
     Episodes · scenes · chapters (their first episode) put the card where you clicked.
   - **Delete with [Delete] above the board** — with nothing selected it's a gray "Delete". Select a chapter and it becomes "Delete chapter", a scene "Delete scene", an episode
     "Delete episode", and clicking it shows a confirmation box beside it once more. To cancel, click elsewhere or press Esc.
     - Delete chapter — that chapter's scenes · episodes · cards go with it.
     - Delete scene — that scene's episodes · cards, the branch cards those episodes called, and the scene's name · note go with it. Branch cards also called from other scenes stay.
     - Select a branch card and it's "Delete branch card" — deletes just that card without asking (the branch points that called it stay, unconnected).
     - **A chapter always keeps one scene and one episode** — select the last episode or scene and [Delete] becomes a gray "Can't delete" (hover to see why) (right-click `Delete episode` ·
       the Delete key also don't delete and say why). To delete the whole chapter, select the chapter and use "Delete chapter".
   - Renaming opens a name box right in the row, for any row. Selecting a chapter opens the "Conditions & stats" tab on the right with the chapter name box at the top,
     and selecting a scene puts a name box at the top of the Inspector on the right, so you can edit there too
     (just like an episode card's name box — edit and press Enter, Esc cancels, empty shows the ID). Anything deleted comes back with one undo (Ctrl+Z).
   - New items are named "New chapter" · "New scene" · "New episode" — rename them by double-clicking, as below.
1-2. **Renaming · moving** — done right in the Hierarchy.
   - **Double-click** a row to rename it. `Enter` confirms; `Esc` or clicking elsewhere cancels.
     **Only the shown name changes** — the ID inside the game stays the same, so feel free to polish names.
     Two scenes with the same name aren't merged. If you clear a name, the ID (like `ch01`, `scene01`) shows.
   - Chapter IDs are assigned automatically — rename by double-clicking the row.
   - **Drag an episode onto another scene row in the same chapter** to move it into that scene. It doesn't move to other chapters.
     While you drag, a translucent tag with the episode's name follows the pointer — a blue border means you can drop there, red means you can't.
     **A move that would give a scene two entrances isn't accepted** — while you drag, that scene row gets a red border and a balloon (and the status bar below) says which scene
     would get which entrance and why. Episodes with connected paths usually hit this: disconnect the path the balloon names first, then reconnect after moving.
     There's no way to take an episode out of scenes — an episode always belongs to a scene.
2. **Write** — in an episode card's writing box, write `Speaker: dialogue` one line at a time. One line is one line of dialogue; leave out the speaker for narration.
   **There's no button to press** — it's applied when you pause for a moment or leave the box (deleted lines are applied when you leave the box).
   Discard text not yet applied with `Esc`.
   - To space out lines, leave blank lines — they stay as you spaced them. Blank lines are for reading only and don't go into the dialogue sent to the game.
   - There's always one blank line at the bottom of the writing box — even after going elsewhere, click that line and keep writing (no need to press Enter first).
   - If the text can't be applied, the box's border turns red, and hovering over the box shows why (it also appears in the status bar below). The text stays in the box, so just fix it.
   - Select a card and its lines appear as a table (`#` · speaker · dialogue) in the "Inspector" on the right. You can edit line by line here too —
     type **just the first letter or initial consonants** in the speaker field ("샤" · "ㅅ" · "ㅅㅌ") and candidates from the names registered in the "Speakers" tab appear right away. Click **▾** beside the field to choose
     from all names. To add a line, use [Add line] at the right of the table header; right-click a line for `Add line below` · `Delete`.
   - The **font · font size** in [⚙ Settings] → "Script writing box" are the font and size for **new cards** — they're written into a card when it's made (in the project file ·
     shown the same to people you work with), and changing the setting later doesn't change existing cards. To change the font or size of existing text, select the text and use the formatting
     tools at the top. **Bold · text color** are set in the same place and follow the same rule (they're for new cards) — a card that's bold from the start can't have just part of it made regular.
     **Line spacing** follows the same rule (for new cards); change an existing card's line spacing with the line spacing −/+ in the formatting tools. Line spacing stops at the narrowest value
     for that font and size (the height of one line of Hangul — it differs by font and PC) and the status bar shows "This is the minimum line spacing." — going lower would only lower the number
     while the screen stays the same. **Cards with smaller text can go tighter** — lines containing only the smaller text (wrapped dialogue · blank lines too) move down to fit it,
     while lines with text at the starting size (speaker names · text not made smaller) stop where that text doesn't overlap. If enlarged text makes the box too small, drag its bottom edge to enlarge it.
   - If the writing box is too small, drag the card's right · bottom edge to enlarge it (double-click the handle for the starting size).
   - **Text formatting (for writers)** — **It doesn't go into the game.** Yarn dialogue and progression JSON have no formatting; these are marks for writers to see (to enlarge text or
     change colors in the game, leave a note as a direction). They're saved in the project file, so **people you work with see them too.**
     Drag across text in a card's writing box to select it, then apply with the tools on the right of the top row of [Script] — font · size (−/+) · **B** (bold) · *I* (italic) · S (strikethrough) ·
     A (text color) · pen (background color) · alignment (left · center · right) · line spacing (−/+).
     **If you select only cards without clicking into a writing box** (in the Hierarchy · a card's header on the board · several too), it applies to all text in those cards, and size becomes the card's base size
     (text you partly enlarged keeps its difference). One undo brings it all back.
     - Bold · italic · strikethrough turn off if all the selected text already has that style, otherwise they apply to all of it. The buttons look pressed according to the selected text's style.
     - Choose a color from the ten in the menu, from "Colors used in this project" (colors used by people you work with show too), or in "Choose (RGB)" type a hex value or
       move the R · G · B sliders and apply with [Apply] (or Enter). "None" removes it.
     - Click the size box or line spacing box, **type a number and press Enter** to set that value (size for all selected text · line spacing for the card · Esc cancels).
     - Size is added on top of that card's starting size. "Default font" is that card's starting font. Enlarging text makes only the lines with that text taller; other lines'
       spacing stays.
     - Alignment and line spacing are one per card (not per line). You don't need to select text — having the cursor in that box is enough. Line spacing goes in 10% steps, from that card's narrowest value to 250%.
     - Speaker names (`하린:`) and condition lines aren't formatted — if the selection spans them, only the dialogue text is formatted.
     - Formatting follows the same characters when you edit the text. Text typed at the end of formatted text continues its style; text typed right before it doesn't (same as Word and Hangul).
     - Formatting also undoes one step per undo (Ctrl+Z).
     - Hangul fonts have no italic face, so pressing *I* draws Hangul letters slanted (the same way Word and Hangul do it · Latin text uses its italic face). On a PC without the chosen font,
       the default font is shown.
     - While you choose a font or color, the selected text stays lightly highlighted, and afterward those characters remain selected so you can apply another format right away.
     - Clicking outside the writing box (empty board · a card's header) turns the tools off — select text in a writing box again to turn them on.
3. **Connect choices** — choice slots appear under an episode card (a point and a text).
   - Click a gray **"Empty choice"** slot, write the choice text (Enter), then drag the point (now red) onto the next episode card to connect it. A connected point is green.
   - On a card with no choice text yet, the first slot is **"Auto"** — drag and drop its point without writing text to make a path that moves on without asking.
     Writing any choice text closes the auto slot.
   - Drop a connected point on empty space to disconnect it (dropping it on its own card does the same). Drop it on another episode card to reconnect it there.
   - Choices connect only to **episode cards in the same chapter** — not to cards in other chapters or to branch cards.
   - To go to another scene, connect to **that scene's start (⌂)**. You can't connect to another card of a scene that already has a start — a scene must
     start in one place (where rollback returns and Continue resumes). If you need to go to that card, split it into another scene first, then connect.
     A scene with no incoming path from outside yet can be connected at any card, and that card becomes its start.
   - **Paths that go back** to an earlier card (including the start of the same scene) can be connected too — while going around that loop, you see the same dialogue again. It's fine as long as there's
     at least one choice that leads out; a loop with no way out (including a loop made only of auto paths) shows as a 🟡 warning in the [Structure] tab's "Validation report".
   - **While you drag, you can see what dropping will do** — the tag beside the pointer, the color of the dragged line, the description in the bar below, and the border of the card under the pointer all say the same thing.
     Blue is connect · reconnect, red is disconnect · can't connect (the reason is in the bar), gray is no change.
     Even before dragging, hovering over a point enlarges it and a tag says what dragging can do.
   - Click a choice line and the Inspector shows that line's text · conditions · stat changes (you can edit the text there too).
     Conditions and stat changes are set in the [Structure] tab — double-click the line to go to that choice in the [Structure] tab.
4. **Branches — visiting and returning** — to briefly visit other dialogue in the middle of dialogue, in the Inspector's line table click `＋` at the right of that line → "＋ Add branch".
   For details, see "Branch cards" below.
5. **Save** — [Save] in the **File** menu at the far left of the top bar, or **Ctrl+S**. If anything has changed, it also saves automatically every 10 minutes.
   If you make a mistake, **Ctrl+Z** (undo); to redo, **Ctrl+Y** — both are in the **File** menu too (they're grayed out when there's nothing to undo).
   Inside a card's writing box, Ctrl+Z · Ctrl+Y are also project undo · redo (text not yet applied is discarded first).
   If the cursor is in another text box (a name box · the Inspector's line table, etc.), Ctrl+Z undoes that box's typing.

> **You can change names later.** Renaming a speaker in the "Speakers" tab updates the speaker of lines already written, and renaming a condition
> in the "Conditions & stats" tab updates the places that use it — **they follow along**. One undo (Ctrl+Z) undoes the rename and everything that followed.
> A name can't be emptied or changed to a name that already exists — to delete it, press [✕].

### Scene notes — writing down the scene's situation

Click a **scene row** in the [Script] tab's explorer and a note area appears on the right.

- **Scene note** — write freely about the scene's situation: what the scene shows, where each character's feelings go from and to
  (e.g. `하린: worn out → determined`), the line where the feeling turns, who's on stage · place · time. Faint text in the empty box suggests what to write.
- **Episode direction** — choose one of the scene's episodes in the note area's "＋ Episode" box to attach a card. Write what applies only to that episode.
  The card stays even if the direction is empty. To remove the card, press [Remove] (the direction written in it is deleted too).
- After writing, click elsewhere or press **Ctrl+Enter** to apply. Enter is a new line; Esc discards text not yet applied. Undo (Ctrl+Z) works.
- Scenes with a note get **"note"** on their explorer row and on their label on the board.
- ⚠ **Write the scene's situation, not staging.** What moves and how (entrances · expression names · camera) is decided by the staging side.
- Renaming the scene keeps its note. If you delete all of the scene's episodes or move them to other scenes, the note **isn't deleted** — it remains as an "Orphaned scene note";
  select the chapter and open the "Inspector" tab to see it, and delete it there with [Delete]. (Deleting the whole scene with [Delete] deletes its note too.)
- Notes don't go into the game. They're written into the dialogue Yarn in the `yarn/` folder of the work folder ([📂 Work folder] in the top bar — `exported/` in the project folder) as comment lines starting with `//`
  (scene notes at the top of the file, episode directions before their episode).

### Dialogue-only Yarn — check that the dialogue runs on its own

Once validation passes, **one `.yarn` file per scene** is written automatically to the work folder's `yarn/` (`{chapter}_{scene}.yarn`). Each episode is one
node, with dialogue · conditions · branches and no staging. It isn't copied to the game folder (you can choose another folder in [⚙ Settings] → "Export (choose mirror folders)"
— it's copied when you save).

- You can use `#` · `//` · `<<` · `\` in dialogue — the tool converts them so Yarn can read them. `{stat("key")}` reads a stat, so it goes through as is.
- Lines that start with `->` or `===` without a speaker, and unmatched `{`, can't be read by Yarn. If there's such a line, **that scene's file isn't written** and
  "chapter · scene: what's wrong" appears at the top of the "Validation report" at the bottom of the [Structure] tab. Once fixed, it's written on the next export.

### Branch cards — cards you visit and return from

To briefly visit other dialogue in the middle of dialogue and come back, select the card, then in the "Inspector" line table on the right click `＋` at the right of that line → **"＋ Add branch"**.
A branch point appears before that line, and a **branch card** (purple · header "⑂ branch") is created and connected along with it. Write the dialogue for the visit in that card.

- A branch card **isn't an episode** — it has no choice slots, because choices during a visit-and-return don't appear in the game.
  Forks in the story are made with the choice slots under episode cards (step 3 of section 2 above).
- Branch points connect **only to branch cards**. Dropping one on an episode is refused, and the status bar says why.
- **Branch cards don't branch** — a branch card's line `＋` has no "＋ Add branch". The visit-and-return goes one level deep
  — if you need to split further, add another branch to the calling episode.
- **To visit only when a condition holds**, set the branch card's "⑂ Condition" (below).
- If you delete a branch card ([Delete branch card]), its branch point remains unconnected — **drag that branch point onto empty space** to create a new branch
  card there.
- A branch card belongs to the calling episode's scene, and in the explorer it shows as a "⑂ title" row under that episode. Its title starts as "New branch";
  select the card and edit it in the name box at the top of the Inspector (empty shows the branch card ID). The ID is set in the [Structure] tab.
- The condition is chosen in the "⑂ Condition" box that appears **above the script in the Inspector when the branch card is selected** (clicking the "⑂ Condition" label on the card's header also selects that card).
  The branch is always visited, and the branch card checks the condition at its start — its dialogue plays only when it holds; otherwise it returns as soon as it's visited. The calling card's
  branch box and branch line only show the current condition.

### Speakers · condition names — added and edited in [Script]

The side panel on the right has three tabs — **"Inspector" · "Speakers" · "Conditions & stats"**. Speakers and conditions · stats always have their own tabs. Selecting a card · scene · line
opens the "Inspector", selecting a chapter opens "Conditions & stats", and clicking the project row at the top of the explorer (or empty space outside chapters on the board) opens "Speakers".

- **Speakers** — the **"Speakers" tab** (project-wide — every chapter uses the same list). Type a name in the box below and press [＋ Add] (or Enter). Editing a name box
  also updates the speaker of lines already written. [✕] removes it only from the list (names already written stay in the script). The **character key** that links portraits and expressions
  gets a **temporary key** automatically when you add a speaker — `temp_` + the romanized name (하린 → `temp_harin`). Renaming doesn't change the key.
  The developer sets keys that match the art in the [Structure] tab — speakers with a temporary key get a ⚠ in the [Structure] tab's "Speakers" tab (they don't appear in the validation report).
  Speakers with an empty key appear as a 🟡 warning in the validation report (their dialogue is written as is).
  Click the **color button** before a name box to choose that speaker's **name color** (ten preset colors · colors used in this project · Choose RGB · none) — in [Script] card writing boxes
  that speaker's name (`하린` in `하린:`) shows in that color. The **background color button** beside it (a square with "A" drawn in it — how the name will look) chooses the **name background** —
  that color goes behind the name, used together with the name color. They're marks for writers, so they don't go into the game, but people you work with can see them.
- **Names only in the script** — you can also write straight into a card, like `시구레: …`, without adding the speaker. Names not in the list gather under
  **"names only in the script"** at the bottom of the "Speakers" tab, with their line counts (in Korean alphabetical order — similar typos line up next to each other). [Register] puts one in the list (with a temporary key);
  with two or more, [Register all] puts them all in at once (one Ctrl+Z brings it back). Editing a name box updates that name's dialogue lines in every chapter, and
  **renaming to a name in the list merges them into that speaker** — renaming `윌러` to `윌로` makes the typo disappear. Names with a space (`늙은 상인`)
  must be in the list for the script to read them as speakers, so add them above first.
- **Conditions** — the condition list appears under the stat table in the **"Conditions & stats" tab**. This tab is **per chapter** — it belongs to the current chapter (the same chapter as the chapter combo above the board),
  and the small drop-down at the top right of the tab goes to another chapter. Name conditions **by meaning** (e.g. `when you've become close to Harin`).
  A condition made with [＋ Add] is **"Undecided"** — since what it checks isn't decided yet, it **always passes** (only a 🟡 warning in the validation report).
  The real expression (`trust >= 3`) is filled in by the developer in the [Structure] tab (conditions can also be added and renamed in the [Structure] tab). Renaming a condition updates the paths that use it, and [✕], if paths use it,
  says where and doesn't delete it.
- **Stats** are created by the developer in the [Structure] tab. A stat is called by one **key** — the text on the game screen is decided by the game.
  To put a stat's current value in dialogue, paste the `{stat("key")}` you [Copy] from the stat table in the "Conditions & stats" tab.
  If the developer changes a stat key, `{stat("key")}` in that chapter's dialogue changes to the new key too (translation fields too · the translation isn't treated as outdated). Deleting a stat
  doesn't remove `{stat("key")}` from dialogue — the validation report flags it with 🟡 as "dialogue reading a missing stat", so fix that line.

## 3. Working with the graph screen

| What you want | How |
|---|---|
| Zoom | Mouse wheel · trackpad — even over a card's writing box and while writing. On a Mac trackpad, pinch with two fingers too |
| Get around with a touchpad · trackpad | Turn on [⚙ Settings] → "Other" → **"Wheel pans the board"** (on from the start on Mac) — a two-finger swipe (= wheel) moves the board, and zoom is **Ctrl (⌘ on Mac)+wheel**. Shift+wheel moves sideways. Turn it off and the wheel zooms again. The [Structure] board follows too |
| Scroll text inside a card | Drag the scroll bar on the right of the writing box (the wheel zooms the board). To show all the text, turn on Fit height (↕) |
| When the board runs out of room | In the top bar, [⚙ Settings] → "[Script] board size", increase the width · height (4,000 ~ 60,000). The board grows to the right and down, and you can undo (Ctrl+Z). If shrinking pushes cards off the board, all cards are gathered in by the same ratio — it tells you how many before [OK] |
| Move the view | Drag with the mouse wheel (middle button) held down |
| Go far | Click the minimap at the bottom right |
| See everything | [Fit all] button under the minimap |
| Find | **Ctrl+F** (⌘F on Mac) — a find box appears at the top right of the board. As you type, it searches every card's dialogue · speakers · titles, goes to that card (unfolding folded scenes · chapters) and selects the found text. `Enter` next · `Shift+Enter` previous · `Esc` closes and lets you keep writing right at the found line. Press it with text selected in a writing box to search for that text |
| Fit the selection to the screen | **F** (outside a writing box) — episodes · branch cards are centered at a comfortable editing size (100%); scenes · chapters are fitted so their frame is on screen. Same whether you selected in the Hierarchy or on the board. **The [Structure] tab works the same** — episode cards · branch chips centered at 100% (even cards at the edge of the board), a selected path shows its two cards, a scene its cards, a chapter uses [Fit to screen] |
| Move several cards | Drag with the left button from empty space to group them, then drag — or click a chapter · scene label on the board once to select every card inside, then drag right away |
| Fold chapters · scenes | **Double-click** a chapter · scene label on the board (double-click the folded table · list to unfold it) |
| Resize a card | Drag the card's right · bottom edge (the bottom right corner does both · width and writing box height go up to 4,000). Double-click a handle for the starting size |
| Fit width · Fit height | The two buttons at the right end of the card's title row (**↔ width** · **↕ height**) — whichever is on fits automatically as you write (when you pause or leave the box). Width fits the card's width so lines don't wrap; height fits the writing box's height so every line shows (never smaller than the starting size). New cards have **only height on**. Click to turn them on and off (turning off keeps the size); dragging an edge to resize turns that side off. On a card with height on, dragging the width also refits the height to that width. Changing line spacing · text size · font · bold with the formatting tools refits right away too. On/off is saved per card in the project (Ctrl+Z brings it back) |
| Rename a card's title | **Double-click the title text** in the card's header and a title box appears right there. Edit and press `Enter`; to cancel, press `Esc` or click elsewhere (empty board · empty part of a card, etc.). Empty shows the ID (IDs are set in the [Structure] tab) |
| Show in the [Structure] tab | **Right-click** a card → "Show in [Structure] tab" goes to that episode in the [Structure] tab. Double-click a choice line to go to that choice |
| Widen the board | Click "‹" on the Hierarchy at the left, or "›" at the end of the tab row of the side panel on the right (Inspector · Speakers · Conditions & stats), to fold that panel into a strip. Click the strip to expand it again — both tabs fold together and stay folded after restarting the app |
| Screen language (한국어 · English) | Choose in [⚙ Settings] → "Other" → "Language (언어)" — at first it follows the PC's language (Korean on a Korean PC, English otherwise). A change takes effect **the next time you start the app**. It's separate from the language of the dialogue and project data |
| Shape of new lines | In [⚙ Settings] → "Other" → "New lines", choose elbow · curve · straight (elbow at first) — choice lines · branch lines you connect after that take that shape. Existing lines stay as they are. Change a single line by clicking it and using [Elbow] [Curve] [Straight] above it; [↺ Auto] returns it to the shape in Settings |
| Hide the minimap | Turn on [⚙ Settings] → "Other" → "Hide minimap" — it's hidden as soon as you click. The map and [Fit all · 50 · 100 · 150] disappear and only the current zoom (e.g. 28%) remains as faint text. Clicking there reaches the writing box underneath |
| Change the script's text | [⚙ Settings] → "Script writing box" — font · font size · line spacing (%) · weight · text color, with a preview. All are for new cards; existing cards stay as they are (selected text with the formatting tools at the top · a card's line spacing with the line spacing −/+ in the formatting tools). [Reset to default] is default font · 11 · 140% |
| Fewer hints | Turn on [⚙ Settings] → "Other" → "Fewer hints" — as soon as you click, the how-to explanations in the side panel disappear, and "No … yet — …" lines keep only their first part. Field names · titles · warnings stay. Turn it off to see everything again |
| Write lines with the keyboard in the dialogue editor | In the line table of the side panel, move between fields with Tab · Shift+Tab (speaker → dialogue → ＋ → next line). **Ctrl+Tab** also goes to the previous field — to go back to the previous line while writing. On a line, **[＋] → Tab → Enter** adds a new line below it and you write right away in its speaker field ([Add line] above also goes to the new line) |
| See all shortcuts | Expand [⚙ Settings] → "Shortcuts" — they're grouped by place (window · [Script] board · card writing box · dialogue editor · [Structure] board · Hierarchy · text fields) |
| Add right there | Right-click empty space — `Add episode` inside a scene · `Add scene` inside a chapter · `Add chapter` outside chapters |
| Delete an episode | Select an episode (or branch card) and press **Delete** (delete ⌫ on Mac) — deletes without asking (Ctrl+Z brings it back). If the cursor is in a writing box, it deletes text |

## 4. Don'ts (important)

- **Don't open or edit files in the project folder directly from File Explorer.**
  `project.vnproject.json` and `story\` are managed by the tool. Do all editing inside the tool.
- `exported\` (the work folder) is rewritten by the tool automatically — anything edited there by hand is overwritten on the next export.
- `runtime.target.json` (if present) is written by "Export (choose mirror folders)" in [⚙ Settings]. Don't edit it by hand.
- Don't delete or rename files in the folder. If you put a second project file (`….vnproject.json`) in the same folder, the tool can't tell which to open
  and won't open either. Moving the **whole** project folder is fine.

## 5. Exchanging · backups

For now, projects are exchanged with the developer as **zip (compressed) files**. How to receive and send them back is in
**"Read me first.txt" → "Sending projects back and forth"** in the app package. In short —

- **When you receive one**: unzip the zip from the developer, then File → [Open…] → that folder.
- **When you send it back**: save (Ctrl+S) → right-click the project folder → compress (zip) → add the date to the name (e.g. `ourstory-1120.zip`) → send. The zip you sent is your backup.
- Don't edit the same project at the same time as the developer — after sending, don't edit until you get a new version (changes the two of you make separately aren't merged).
  When you get a new version, keep the old folder with the date added to its name.
- **The yellow bar** — if the project files are changed elsewhere while the tool is open (a synced folder · another program), the tool notices when you return to the window. If you have no unsaved
  changes, it reloads automatically; if you do, a yellow bar appears at the top and asks you to choose [Reload (discard changes here)] · [Overwrite with mine]. Until you choose,
  saving and autosave stop — it never silently overwrites changes made elsewhere.
- Once you're working together in earnest, you'll exchange projects with Git (GitHub Desktop) instead of zips — the developer will explain then.

## 6. Ask the administrator for

These are things you can't do from the writer's screen, or that we decided writers won't do. Ask when you need them.

- **Linking character keys** (to attach portraits · expressions to a new speaker — speaker names you add yourself in the [Script] tab)
- **Filling in condition expressions · creating stats** (when you have "Undecided" conditions or need a new stat)
- **Tool updates** (the administrator sends new versions — don't get other versions on your own). The window title shows the version number (e.g. "Story Director 0.1.0").
  For a new version, unzip it and replace the old `StoryDirector.exe` with it — recent projects and settings stay.
  A new version may not open a project made with an earlier version (a message mentioning the "format version" appears) — don't edit that project; tell the administrator

## 7. When something goes wrong

- If the screen looks wrong or something doesn't work: send the administrator **a screenshot + one line about what you just did**, along with the version number in the window title.
- The status bar at the bottom tells you the result of what you just did — including why something didn't work.
- Even if the tool closes, everything up to the last save is safe (if anything has changed, it saves automatically every 10 minutes). Start it again to continue.
