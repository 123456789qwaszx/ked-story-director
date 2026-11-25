using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Results;

public enum PublishProblemKind
{
    /// <summary>카드의 대본에 살아 있는 줄이 없다.</summary>
    MissingScript,

    /// <summary>같은 LineId가 결과 안에서 두 번 나온다.</summary>
    DuplicateLineId,

    /// <summary>조건 정의를 찾을 수 없다.</summary>
    UnknownCondition,

    /// <summary>조건 구조가 성립하지 않는다(짝 없는 elseif·endif 등).</summary>
    InvalidConditionStructure,

    /// <summary>대본에서 사라진 줄에 대사 논리가 남아 있다. 막지 않는다.</summary>
    OrphanData
}

/// <param name="IsBlocking">true면 이 노드가 성립하지 않는다 — 미리보기와 글 칸이 그대로 보여 주되 문제로 짚는다.</param>
public sealed record PublishProblem(
    PublishProblemKind Kind,
    string? LineId,
    string Message,
    bool IsBlocking);

/// <summary>
/// 대사 노드 하나를 펼친 본문과 그 검증 결과.
///
/// 이름의 "발행"은 남은 이름이다 — 발행(고정본 <c>vN</c>)은 2026-10-29에 연출과 함께
/// <c>ked-story-compositor</c>로 갔다. 여기서는 미리보기 · 카드 글 칸 · 흐름 해석이 같은 펼침을 읽는다.
/// </summary>
public sealed class DialogueDraft
{
    public DialogueDraft(
        string sourceNodeId,
        string sourceNodeName,
        int sourceScriptRevision,
        string locale,
        IReadOnlyList<DialogueResultLine> lines,
        IReadOnlyList<PublishProblem> problems,
        IReadOnlyList<DialogueResultTransition>? trailingTransitions = null)
    {
        TrailingTransitions = trailingTransitions ?? Array.Empty<DialogueResultTransition>();
        SourceNodeId = sourceNodeId;
        SourceNodeName = sourceNodeName;
        SourceScriptRevision = sourceScriptRevision;
        Locale = locale;
        Lines = lines;
        Problems = problems;
    }

    public string SourceNodeId { get; }

    public string SourceNodeName { get; }

    public int SourceScriptRevision { get; }

    public string Locale { get; }

    public IReadOnlyList<DialogueResultLine> Lines { get; }

    /// <summary>마지막 줄 뒤의 전환들 — 대사 없는 조건 블록이 대본의 끝일 때 (2026-08-24).</summary>
    public IReadOnlyList<DialogueResultTransition> TrailingTransitions { get; }

    public IReadOnlyList<PublishProblem> Problems { get; }

}

/// <summary>
/// DialogueNode의 작업 상태를 <see cref="DialogueResult"/> 모양으로 펼친다 — 대본의 화자·대사,
/// 조건 전환(조건 Id가 아니라 이름과 식), 갈래 출구, 꼬리 전환까지.
/// </summary>
public static class DialoguePublisher
{
    public static DialogueDraft Draft(
        StoryProject project,
        string dialogueNodeId,
        string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        DialogueNode node = project.FindDialogue(dialogueNodeId)
            ?? throw new InvalidOperationException(string.Format(Msg.Edit_IsnTDialogueNode, dialogueNodeId));

        DialogueScript script = DialogueScriptResolver.Resolve(project, node, locale);
        DialogueFlow flow = ConditionFlowResolver.Resolve(node, script, project);
        AvailableConditionCatalog available = AvailableConditionResolver.Resolve(
            project,
            node.Id);

        var problems = new List<PublishProblem>();
        var lines = new List<DialogueResultLine>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (ResolvedLine resolved in flow.Lines)
        {
            DialogueLine line = resolved.Line;

            if (!seen.Add(line.LineId))
            {
                problems.Add(new PublishProblem(
                    PublishProblemKind.DuplicateLineId,
                    line.LineId,
                    string.Format(Msg.Diag_LineIdAppearsTwiceInResult, line.LineId),
                    IsBlocking: true));
            }

            lines.Add(new DialogueResultLine(
                resolved.Index,
                line.LineId,
                line.Revision,
                line.Speaker,
                line.Text,
                Freeze(line.Transition, project, available),
                // 둘째 전환부터 — 겹쳐 닫기·연달아 열기가 여기 실린다 (2026-08-17).
                line.Transitions.Count <= 1
                    ? null
                    : line.Transitions
                        .Skip(1)
                        .Select(transition => Freeze(transition, project, available))
                        .OfType<DialogueResultTransition>()
                        .ToArray(),
                line.Detours));
        }

        AddFlowProblems(flow, problems);
        AddScriptProblems(node, problems);

        // 설정노드의 변수 배정은 2026-09-17에 폐지됐다 (작가 변수 폐지). 결과의 배정 칸(`Assignments`)은 L5(2026-10-29)에 걷혔고,
        //    설정노드 자체(챕터 조건 공급)도 2026-11-25에 걷혔다(condition-supply C1).
        return new DialogueDraft(
            node.Id,
            node.Name,
            node.Script.SourceRevision,
            script.Locale,
            lines,
            problems,
            // 마지막 줄 뒤의 전환 (2026-08-24) — 대사 없는 조건 블록이 대본의 끝일 때.
            // 줄에 실린 것과 <b>같은 얼리기</b>를 지난다: 조건 Id가 아니라 <b>식</b>이
            // 결과에 굳어야 산출물이 자기 완결이 된다.
            FreezeTrailing(node, project, available));
    }

    /// <summary>초안을 결과 모양으로 감싼다 — 미리보기 합성기와 흐름 해석이 이 모양을 읽는다.</summary>
    public static DialogueResult AsResult(DialogueDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new DialogueResult(
            draft.SourceNodeId,
            draft.SourceNodeName,
            draft.SourceScriptRevision,
            draft.Locale,
            draft.Lines,
            draft.TrailingTransitions);
    }

    /// <summary>대본 끝의 줄 없는 갈래들의 전환을 얼린다 (2026-08-24). (그 갈래의 출구를 함께 싣던 것은 2026-11-19에 걷혔다.)</summary>
    private static IReadOnlyList<DialogueResultTransition> FreezeTrailing(
        DialogueNode node,
        StoryProject project,
        AvailableConditionCatalog available) =>
        node.TrailingTransitions
            .Select(transition => Freeze(transition, project, available))
            .OfType<DialogueResultTransition>()
            .ToList();

    private static DialogueResultTransition? Freeze(
        LineConditionTransition? transition,
        StoryProject project,
        AvailableConditionCatalog available)
    {
        if (transition is null)
        {
            return null;
        }

        if (transition.Kind == ConditionTransitionKind.EndIf)
        {
            return new DialogueResultTransition(transition.Kind, null, null, null);
        }

        AvailableCondition? condition = available.Find(transition.ConditionId)
            ?? AvailableConditionResolver.FindKnown(project, transition.ConditionId);

        return new DialogueResultTransition(
            transition.Kind,
            transition.ConditionId,
            condition?.DisplayName,
            condition?.Expression);
    }

    private static void AddFlowProblems(DialogueFlow flow, List<PublishProblem> problems)
    {
        foreach (FlowProblem problem in flow.Problems)
        {
            (PublishProblemKind kind, bool blocking) = problem.Kind switch
            {
                // 여기서는 <b>막는 문제</b>로 짚는다 — 미리보기가 [주의]로 보여 준다. 게임으로
                //    내보낼 때 빈 씬을 완화하는 것은 compositor의 몫이다(지금 상태를 그대로 보낸다).
                FlowProblemKind.UnknownCondition => (PublishProblemKind.UnknownCondition, true),
                FlowProblemKind.UnavailableCondition => (PublishProblemKind.UnknownCondition, true),
                FlowProblemKind.ElseIfWithoutIf => (PublishProblemKind.InvalidConditionStructure, true),
                FlowProblemKind.EndIfWithoutIf => (PublishProblemKind.InvalidConditionStructure, true),
                FlowProblemKind.NestedCondition => (PublishProblemKind.InvalidConditionStructure, true),
                FlowProblemKind.OrphanedLineExtension => (PublishProblemKind.OrphanData, false),
                _ => (PublishProblemKind.InvalidConditionStructure, true)
            };

            problems.Add(new PublishProblem(kind, problem.LineId, problem.Message, blocking));
        }
    }

    private static void AddScriptProblems(
        DialogueNode node,
        List<PublishProblem> problems)
    {
        Script.ScriptDocument document = node.Script;

        if (document.Lines.Count > 0 && document.ActiveLines.Any())
        {
            return;
        }

        // 막는 문제로 짚는다 — 위 `AddFlowProblems`와 같은 이유다.
        problems.Add(new PublishProblem(
            PublishProblemKind.MissingScript,
            null,
            Msg.Diag_ThisCardHasNoLive,
            IsBlocking: true));
    }
}
