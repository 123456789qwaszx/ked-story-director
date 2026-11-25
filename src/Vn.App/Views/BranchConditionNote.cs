using Vn.Authoring.Chapters;
using Vn.App.Strings;

namespace Vn.App.Views;

/// <summary>
/// 분기 조건을 사람에게 말하는 글 — 연출 그래프 카드의 표와 인스펙터의 분기 카드가 같은 말을 쓴다.
///
/// 조건은 분기 카드의 첫머리에 선다(<see cref="BranchEntryCondition"/>). 고르는 곳은 분기 카드 한 곳이다 — [대본] 탭은 그 카드의
/// 대본 위쪽 칸(<see cref="BranchConditionPicker"/>), [구성] 탭은 그 칩의 인스펙터(2026-11-17). 지금 걸린 조건은 늘 보인다 — 안 보이면
/// 조건 없는 분기와 조건 걸린 분기가 판에서 똑같아 보인다.
/// </summary>
internal static class BranchConditionNote
{
    /// <summary>어디서 정하는가.</summary>
    internal static string Where => Ui.Branch_ConditionsAreChosenOnBranch;

    /// <summary>그 분기(다녀갈 카드)에 걸린 조건이 <b>식 미정</b>인가 — 그 챕터의 그 이름 조건이 식이 비었다.</summary>
    internal static bool IsUndecided(Vn.Authoring.Model.StoryProject project, string targetNodeId, string? label) =>
        label is not null &&
        project.FindNode(targetNodeId) is { } node &&
        EpisodeNaming.ChapterOf(project, node).Conditions.FirstOrDefault(condition =>
            string.Equals(condition.Label, label, StringComparison.Ordinal)) is { } found &&
        string.IsNullOrWhiteSpace(found.Expression);

    /// <summary>카드 머리에 다는 짧은 표.</summary>
    internal static string Chip(BranchEntryCondition.State state, bool undecided = false) =>
        state.Label is { } label
            ? undecided ? string.Format(Ui.Branch_ConditionUndecided, label) : string.Format(Ui.Branch_Condition, label)
            : state.Blocked is not null ? Ui.Branch_ConditionWrittenByHand : Ui.Branch_NoCondition;

    /// <summary>지금 이 분기가 어떻게 도는가 — 한 문장.</summary>
    internal static string Sentence(BranchEntryCondition.State state, bool undecided = false) =>
        state.Label is { } label
            ? undecided
                ? string.Format(Ui.Branch_ConditionItSUndecidedSo, label)
                : string.Format(Ui.Branch_PlaysOnlyWhenConditionHolds, label)
            : state.Blocked is not null
                ? Ui.Branch_ThereSConditionWrittenBy
                : Ui.Branch_ForNowItSAlways;
}
