using Vn.App.Strings;
namespace Vn.App.Views;

/// <summary>
/// <b>쓸 수 있는 단축키 표</b> — 설정 창 「단축키」 묶음이 자리별로 보인다(2026-11-18 소유자 — 「설정 안에 사용가능한 단축키들을 정리해두는
/// 것이 필요」 · side-fold-settings K2).
///
/// 원천은 이 표 한 벌이다. 키를 더하거나 바꾸면 여기와 <c>docs/writer-guide.md</c>를 함께 고친다. 실제로 듣는 것만 적는다 — 적어 두고 안
/// 들으면 표가 거짓말을 한다(그래서 이 표를 만들며 하이어아키 화살표가 실제 앱에서 듣지 않던 것을 고쳤다).
///
/// 키는 「Ctrl+S」 · 「Delete」 꼴로 한 벌만 적는다 — 맥에서는 설정 창이 보일 때 ⌘ · ⌫로 바꾼다(<see cref="PlatformKeys.Label"/> · writer-app WA1).
/// </summary>
internal static class Shortcuts
{
    /// <summary>한 줄 — 누르는 것과 하는 일.</summary>
    internal sealed record Entry(string Keys, string Does);

    /// <summary>한 자리 — 그 자리에서 듣는 줄들.</summary>
    internal sealed record Place(string Name, IReadOnlyList<Entry> Entries);

    internal static readonly IReadOnlyList<Place> Table =
    [
        new(Ui.Keys_AnywhereInWindow,
        [
            new("Ctrl+S", Ui.Main_Save),
            new("Ctrl+Z", Ui.Keys_UndoAlsoInFileMenu),
            new("Ctrl+Y", Ui.Keys_RedoAlsoInFileMenu),
            new("Esc", Ui.Keys_CloseDeleteConfirmationClickingOutside),
        ]),
        new(Ui.Keys_ScriptBoard,
        [
            new(Ui.Keys_MouseWheel, Ui.Keys_ZoomWithWheelPansBoard),
            new(Ui.Keys_CtrlMouseWheelTrackpadPinch, Ui.Keys_ZoomWhetherWheelPansBoard),
            new(Ui.Keys_MiddleButtonDrag, Ui.Keys_PanBoard),
            new(Ui.Keys_DragOnEmptySpace, Ui.Keys_SelectAreaDraggingAnySelected),
            new(Ui.Keys_RightClickEmptySpace, Ui.Keys_AddChapterSceneEpisodeDepending),
            new("Delete", Ui.Keys_DeleteSelectedCardEpisodeBranch),
            new("Ctrl+F", Ui.Keys_FindDialogueSpeakersCardTitles),
            new("F", Ui.Keys_FitSelectionToScreenEpisodes),
            new(Ui.Keys_DoubleClickCardSTitle, Ui.Keys_RenameTitleInPlaceEnter),
            new(Ui.Keys_RightClickCard, Ui.Keys_RenameShowInStructureTab),
            new(Ui.Keys_DoubleClickChoiceLine, Ui.Keys_GoToThatChoiceIn),
            new(Ui.Keys_ClickChapterSceneLabel, Ui.Keys_SelectsEveryCardInsideDrag),
            new(Ui.Keys_DoubleClickLabel, Ui.Keys_CollapseDoubleClickCollapsedLabel),
            new(Ui.Keys_DoubleClickSizeHandle, Ui.Keys_ResetThatSizeToDefault),
            new(Ui.Keys_ClickDragMinimap, Ui.Keys_MoveViewThere),
        ]),
        new(Ui.Keys_ScriptCardWritingBox,
        [
            new("Enter", Ui.Keys_NewLineOneLineIs),
            new("Esc", Ui.Keys_DiscardTextNotYetApplied),
            new("Ctrl+Z · Ctrl+Y", Ui.Keys_UndoRedoProjectTextNot),
            new(Ui.Keys_MouseWheelTrackpad, Ui.Keys_BoardSWheelWorksOver),
        ]),
        new(Ui.Keys_ScriptDialogueEditor,
        [
            new("Tab · Shift+Tab", Ui.Keys_NextFieldPreviousFieldSpeaker),
            new("Ctrl+Tab", Ui.Keys_ToPreviousFieldSameAs),
            new("＋ → Tab → Enter", Ui.Keys_AddLineBelowInLine),
        ]),
        new(Ui.Keys_StructureBoard,
        [
            new(Ui.Keys_MouseWheel, Ui.Keys_ZoomWithWheelPansBoard),
            new(Ui.Keys_CtrlMouseWheelTrackpadPinch, Ui.Keys_ZoomWhetherWheelPansBoard),
            new(Ui.Keys_MiddleButtonDrag, Ui.Keys_PanBoardEvenWhenStarting),
            new(Ui.Keys_ClickDragEmptySpace, Ui.Keys_ClearSelectionSelectArea),
            new("F", Ui.Keys_FitSelectionToScreenEpisode),
            new(Ui.Keys_DoubleClickEpisodeCard, Ui.Keys_GoToThatCardIn),
            new(Ui.Keys_DoubleClickBranchChip, Ui.Keys_GoToThatBranchCard),
            new(Ui.Keys_DoubleClickChoiceLineChoice, Ui.Keys_GoToThatChoiceIn2),
        ]),
        new(Ui.Keys_Hierarchy,
        [
            new("↑ · ↓", Ui.Keys_MoveBetweenVisibleRowsSide),
            new("←", Ui.Keys_CollapseIfAlreadyCollapsedOr),
            new("→", Ui.Keys_ExpandIfAlreadyExpandedGo),
            new(Ui.Keys_CtrlClick, Ui.Keys_AddOrRemoveThatRow),
            new(Ui.Keys_ShiftClick, Ui.Keys_SelectEveryRowFromOne),
            new(Ui.Keys_DoubleClickRow, Ui.Keys_RenameEnterAppliesEscClicking),
            new(Ui.Keys_RightClick, Ui.Keys_AddRenameDeleteScriptTab),
            new(Ui.Keys_DragEpisodeRow, Ui.Keys_DropItOnAnotherScene),
            new(Ui.Keys_FindBoxEnter, Ui.Keys_SelectFirstMatchThenMove),
            new(Ui.Keys_FindBoxEsc, Ui.Keys_ClearSearchTreeReturnsTo),
        ]),
        new(Ui.Keys_TextFields,
        [
            new("Enter", Ui.Keys_ApplyClickingElsewhereAlsoApplies),
            new("Esc", Ui.Keys_RevertToValueBeforeApplying),
            new("Ctrl+Enter", Ui.Keys_ApplySceneNoteEpisodeDirection),
            new(Ui.Keys_RightClick, Ui.Keys_LineTableOfDialogueEditor),
            new("Enter · Esc", Ui.Keys_SettingsWindowOKCancel),
        ]),
    ];
}
