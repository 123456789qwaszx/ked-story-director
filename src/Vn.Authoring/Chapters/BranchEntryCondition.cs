using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>분기 에피소드의 첫머리 조건</b> — 읽고 쓰는 유일한 자리.
///
/// <b>왜 여기 사는가</b>: 부르는 쪽 대본에는
/// <c>&lt;&lt;detour&gt;&gt;</c> 한 줄뿐이고, <b>조건과 내용이 다녀올 곳에 함께</b> 있다.
/// 짝을 맞춰야 하는 <c>If</c>/<c>EndIf</c>가 남의 대본에 흩어지지 않는다.
///
/// <b>성립하면 돈다</b>(허가). 분기 자체는 <b>뚫어뒀다면 반드시 한 번 가는 통로</b>이고
/// (<see cref="ChapterBranchMarkers"/>), 막는 것은 다녀온 쪽이다 — 통로로 갔더니 조건으로
/// 막혀 있는 편이 논리상 맞고 이해하기 쉽다.
///
/// <b>어휘는 챕터의 것</b>이다. 기획자가 고르는 것은 <c>조건</c> 시트의 라벨이고, 줄 전환에도 그 라벨이 그대로 적힌다
/// (<see cref="AvailableConditionResolver"/>). ⛔ 2026-11-25까지는 판의 공급 설정노드가 든 사본의 Id가 적혔다(condition-supply C1).
/// </summary>
public static class BranchEntryCondition
{
    /// <param name="Label">걸린 챕터 조건의 라벨. 없으면 <c>null</c>(무조건 돈다).</param>
    /// <param name="Editable">우측 패널이 고칠 수 있는 모양인가.</param>
    /// <param name="Blocked">못 고치는 사유 — 사람에게 그대로 보여 준다.</param>
    public sealed record State(string? Label, bool Editable, string? Blocked)
    {
        public static State Refuse(string reason) => new(null, false, reason);
    }

    /// <summary>
    /// 지금 걸려 있는 조건과, 여기서 고쳐도 되는지.
    ///
    /// <b>손으로 지은 구조는 안 건드린다.</b> 툴이 아는 모양은 <i>첫 줄에 <c>BeginIf</c>
    /// 하나 + 꼬리에 <c>EndIf</c> 하나</i>뿐이다. 작가가 대사 편집기에서 갈래를 더 얹었다면
    /// 그 구조를 여기서 덮어쓰는 순간 <b>짝이 어긋난 대본</b>이 된다 — 그래서 읽기만 하고
    /// 사유를 말한다.
    /// </summary>
    public static State Read(StoryProject project, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.FindDialogue(nodeId) is not { } node)
        {
            return State.Refuse(Msg.Edit_CouldnTFindThatBranch);
        }

        if (node.Script.ActiveLines.FirstOrDefault() is not { } first)
        {
            return State.Refuse(Msg.Edit_ScriptIsEmptySoThere);
        }

        IReadOnlyList<LineConditionTransition> head =
            node.FindExtension(first.Id)?.Transitions ?? [];

        // 아무것도 없다 = 무조건 도는 분기. 가장 흔한 모양이고, 여기서 처음 걸 수 있다.
        if (head.Count == 0 && node.TrailingTransitions.Count == 0)
        {
            return new State(null, true, null);
        }

        if (!HasEntryShape(node, first) ||
            head is not [{ ConditionId: { } conditionId }])
        {
            return State.Refuse(
                Msg.Edit_ThereAreLinesWrittenOutside);
        }

        // 줄 전환에 적힌 것이 곧 챕터 조건의 라벨이다.
        return new State(
            AvailableConditionResolver.Resolve(project, nodeId).Find(conditionId)?.Name
                ?? conditionId,
            true,
            null);
    }

    /// <summary>
    /// 첫머리 조건을 지켜야 하는 카드인가 — 분기로 다녀오는 카드이고 툴이 아는 모양의 조건이 걸려 있다. 이런 카드의 첫 줄을
    /// 지우거나 옮기면 조건 구조가 깨지므로(첫 줄의 <c>BeginIf</c>가 함께 사라지거나 따라 내려간다) 화면이 그 손짓을 막는다.
    /// </summary>
    public static bool IsGuarded(StoryProject project, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.EnumerateNodes().OfType<DialogueNode>().Any(caller => caller.LineExtensions.Any(extension =>
                   extension.DetourTargetNodeIds.Contains(nodeId, StringComparer.Ordinal))) &&
               Read(project, nodeId) is { Label: not null, Editable: true };
    }

    /// <summary>
    /// 첫머리 조건을 지키는 분기 카드(<see cref="IsGuarded"/>)의 분기 색 번호 — 부르는 카드의 그 분기 포트 번호다. 연출 그래프의
    /// 분기 선 · 카드 표가 이 번호로 칠하므로, 대사 편집기의 첫머리 조건 묶음도 이 번호로 칠하면 같은 색이 된다. 아니면 <c>null</c>.
    /// </summary>
    public static int? EntryPalette(StoryProject project, string nodeId)
    {
        if (!IsGuarded(project, nodeId))
        {
            return null;
        }

        foreach (DialogueNode caller in project.EnumerateNodes().OfType<DialogueNode>())
        {
            if (NodeConnections.PortsOf(caller, project).FirstOrDefault(port =>
                    port.Kind == ExitPortKind.Detour && string.Equals(port.TargetNodeId, nodeId, StringComparison.Ordinal)) is { } detour)
            {
                return detour.PaletteIndex;
            }
        }

        return null;
    }

    /// <summary>툴이 아는 조건 모양 — 첫 줄에 <c>BeginIf</c> 하나, 꼬리에 <c>EndIf</c> 하나.</summary>
    private static bool HasEntryShape(DialogueNode node, ScriptLine first) =>
        node.FindExtension(first.Id)?.Transitions is [{ Kind: ConditionTransitionKind.BeginIf, ConditionId: not null }] &&
        node.TrailingTransitions is [{ Kind: ConditionTransitionKind.EndIf }];

    /// <summary>
    /// 조건을 건다(<c>null</c>이면 걷는다). 못 걸면 <b>사유</b>를 돌려주고 아무것도 안 바꾼다.
    ///
    /// 라벨은 <b>이 챕터의 조건 시트</b>에 있어야 한다. 비슷한 것을 추측해 잇지 않는다 —
    /// 조건은 기획자가 먼저 만드는 것이고, 툴이 만들어 주면 오타가 조용히 새 조건이 된다.
    /// </summary>
    public static string? Set(
        ProjectEditor editor, string nodeId, string? label)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (Read(editor.Project, nodeId) is { Editable: false } refused)
        {
            return refused.Blocked;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            editor.SetEntryCondition(nodeId, null);
            return null;
        }

        AvailableCondition? condition = AvailableConditionResolver
            .Resolve(editor.Project, nodeId).Conditions
            .FirstOrDefault(item => string.Equals(item.Name, label, StringComparison.Ordinal));

        if (condition is null)
        {
            return string.Format(Msg.Edit_ConditionIsnTInThis, label);
        }

        editor.SetEntryCondition(nodeId, condition.Id);

        return null;
    }
}
