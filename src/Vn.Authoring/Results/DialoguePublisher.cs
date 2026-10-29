using Vn.Authoring.Definition;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Results;

public enum PublishProblemKind
{
    /// <summary>읽을 대본이 없다.</summary>
    MissingScript,

    /// <summary>같은 LineId가 결과 안에서 두 번 나온다.</summary>
    DuplicateLineId,

    /// <summary>조건 정의를 찾을 수 없다.</summary>
    UnknownCondition,

    /// <summary>조건 구조가 성립하지 않는다(짝 없는 elseif·endif 등).</summary>
    InvalidConditionStructure,

    /// <summary>출구가 없는 노드를 가리킨다.</summary>
    MissingExitTarget,

    /// <summary>변수 이름이 비어 있는 등 성립하지 않는 변수 변경.</summary>
    InvalidSetOperation,

    /// <summary>옵션 라벨에 안정 Id가 없거나 선택 구조가 성립하지 않는다.</summary>
    InvalidChoiceOption,

    /// <summary>미리보기 태그 생성에 관한 알림(대문자 변수 소문자화 등). 막지 않는다.</summary>
    ChoicePreviewNotice,

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
/// ⛔ 이름의 "발행"은 남은 이름이다 — 발행(고정본 <c>vN</c>)은 2026-10-29에 연출과 함께
/// <c>ked-story-compositor</c>로 갔다. 여기서는 미리보기 · 카드 글 칸 · 흐름 해석이 같은 펼침을 읽는다.
/// </summary>
public sealed class DialogueDraft
{
    public DialogueDraft(
        string sourceNodeId,
        string sourceNodeName,
        string? sourceScriptId,
        int sourceScriptRevision,
        string locale,
        IReadOnlyList<DialogueResultLine> lines,
        IReadOnlyList<DialogueResultAssignment> assignments,
        string? defaultExitTargetNodeId,
        IReadOnlyList<PublishProblem> problems,
        IReadOnlyList<DialogueResultTransition>? trailingTransitions = null)
    {
        TrailingTransitions = trailingTransitions ?? Array.Empty<DialogueResultTransition>();
        SourceNodeId = sourceNodeId;
        SourceNodeName = sourceNodeName;
        SourceScriptId = sourceScriptId;
        SourceScriptRevision = sourceScriptRevision;
        Locale = locale;
        Lines = lines;
        Assignments = assignments;
        DefaultExitTargetNodeId = defaultExitTargetNodeId;
        Problems = problems;
    }

    public string SourceNodeId { get; }

    public string SourceNodeName { get; }

    public string? SourceScriptId { get; }

    public int SourceScriptRevision { get; }

    public string Locale { get; }

    public IReadOnlyList<DialogueResultLine> Lines { get; }

    /// <summary>마지막 줄 뒤의 전환들 — 대사 없는 조건 블록이 대본의 끝일 때 (2026-08-24).</summary>
    public IReadOnlyList<DialogueResultTransition> TrailingTransitions { get; }

    public IReadOnlyList<DialogueResultAssignment> Assignments { get; }

    public string? DefaultExitTargetNodeId { get; }

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
        GameDefinition? definition = null,
        string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        DialogueNode node = project.FindDialogue(dialogueNodeId)
            ?? throw new InvalidOperationException($"'{dialogueNodeId}'는 대사 노드가 아닙니다.");

        DialogueScript script = DialogueScriptResolver.Resolve(project, node, locale);
        DialogueFlow flow = ConditionFlowResolver.Resolve(node, script, project, definition);
        AvailableConditionCatalog available = AvailableConditionResolver.Resolve(
            project,
            node.Id,
            definition);

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
                    $"LineId '{line.LineId}'가 결과 안에서 두 번 나옵니다.",
                    IsBlocking: true));
            }

            node.BranchExits.TryGetValue(line.LineId, out string? branchExit);

            if (line.Transition is { OpensOption: true } optionTransition)
            {
                if (string.IsNullOrWhiteSpace(optionTransition.OptionId))
                {
                    problems.Add(new PublishProblem(
                        PublishProblemKind.InvalidChoiceOption,
                        line.LineId,
                        $"옵션 라벨 라인 '{line.LineId}'에 안정 OptionId가 없습니다. " +
                        "전환을 다시 지정해 Id를 발급받으세요.",
                        IsBlocking: true));
                }

            }

            lines.Add(new DialogueResultLine(
                resolved.Index,
                line.LineId,
                line.Revision,
                line.Speaker,
                line.Text,
                Freeze(line.Transition, project, definition, available),
                line.Transition?.OpensBranch == true ? branchExit : null,
                null,
                // 둘째 전환부터 — 겹쳐 닫기·연달아 열기가 여기 실린다 (2026-08-17).
                line.Transitions.Count <= 1
                    ? null
                    : line.Transitions
                        .Skip(1)
                        .Select(transition => Freeze(transition, project, definition, available))
                        .OfType<DialogueResultTransition>()
                        .ToArray(),
                line.DetourTargetNodeId));
        }

        AddFlowProblems(flow, problems);
        AddScriptProblems(project, node, problems);

        // 선택지로 끝나는 노드는 정상이다 (2단계 포트 규칙, 2026-08-14 소유자 승인) —
        // 옵션들이 곧 노드의 끝이고, 이어진 옵션은 출구로 점프, 안 이은 옵션은 에피소드
        // 종료다. 합성기가 문서 끝에서 블록을 닫는다. 다만 "잊고 안 닫은" 경우와 화면상
        // 구분이 안 되므로, 막지 않고 알리기만 한다.
        bool choiceOpen = false;

        foreach (DialogueResultLine line in lines)
        {
            choiceOpen = line.Transition?.Kind switch
            {
                ConditionTransitionKind.BeginChoice or ConditionTransitionKind.BeginNextOption => true,
                // 조건 종료는 열린 선택지도 함께 닫는다 (W55) — 합성기가 암묵 ChoiceEnd를 낸다.
                ConditionTransitionKind.EndChoice or ConditionTransitionKind.EndIf => false,
                _ => choiceOpen
            };
        }

        if (choiceOpen)
        {
            problems.Add(new PublishProblem(
                PublishProblemKind.InvalidChoiceOption,
                null,
                "선택 블록이 문서 끝까지 열려 있습니다 — 이 옵션들이 노드의 끝입니다. " +
                "이어지는 줄을 두려면 블록 뒤 첫 일반 줄에 '선택지 끝'을 지정하세요.",
                IsBlocking: false));
        }

        // ⛔ 설정노드의 변수 배정은 2026-09-17에 폐지됐다 (작가 변수 폐지) — 설정노드는
        //    이제 <b>조건만</b> 공급한다. 결과에 실을 배정이 없다.
        var assignments = new List<DialogueResultAssignment>();

        return new DialogueDraft(
            node.Id,
            node.Name,
            script.ScriptId,
            project.FindScript(script.ScriptId)?.SourceRevision ?? 0,
            script.Locale,
            lines,
            assignments,
            // 커스텀 노드는 기본 출구가 없다 — detour의 복귀가 그 자리를 맡는다.
            node.DefaultExitTargetNodeId,
            problems,
            // 마지막 줄 뒤의 전환 (2026-08-24) — 대사 없는 조건 블록이 대본의 끝일 때.
            // 줄에 실린 것과 <b>같은 얼리기</b>를 지난다: 조건 Id가 아니라 <b>식</b>이
            // 결과에 굳어야 산출물이 자기 완결이 된다.
            FreezeTrailing(node, project, definition, available));
    }

    /// <summary>초안을 결과 모양으로 감싼다 — 미리보기 합성기와 흐름 해석이 이 모양을 읽는다.</summary>
    public static DialogueResult AsResult(DialogueDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new DialogueResult(
            draft.SourceNodeId,
            draft.SourceNodeName,
            draft.SourceScriptId,
            draft.SourceScriptRevision,
            draft.Locale,
            draft.Lines,
            draft.Assignments,
            draft.DefaultExitTargetNodeId,
            draft.TrailingTransitions);
    }

    /// <summary>
    /// 대본 끝의 줄 없는 갈래들을 얼린다 (2026-08-24) — 전환과 <b>그 갈래의 출구</b>를 함께.
    ///
    /// 출구가 전환에 붙는 이유: 실을 줄이 없다. 앵커는 흐름 해석과 같은 규칙으로 짓는다
    /// (<see cref="Flow.BranchAnchor"/>) — 두 곳이 다르게 세면 매단 detour가 사라진다.
    /// </summary>
    private static IReadOnlyList<DialogueResultTransition> FreezeTrailing(
        DialogueNode node,
        StoryProject project,
        GameDefinition? definition,
        AvailableConditionCatalog available)
    {
        var frozen = new List<DialogueResultTransition>(node.TrailingTransitions.Count);
        int opened = 0;

        foreach (LineConditionTransition transition in node.TrailingTransitions)
        {
            if (Freeze(transition, project, definition, available) is not { } item)
            {
                continue;
            }

            if (!transition.OpensBranch)
            {
                frozen.Add(item);
                continue;
            }

            node.BranchExits.TryGetValue(
                Flow.BranchAnchor.ForTrailing(opened++), out string? exit);

            frozen.Add(item with { ExitTargetNodeId = exit });
        }

        return frozen;
    }

    private static DialogueResultTransition? Freeze(
        LineConditionTransition? transition,
        StoryProject project,
        GameDefinition? definition,
        AvailableConditionCatalog available)
    {
        if (transition is null)
        {
            return null;
        }

        if (transition.IsChoiceKind)
        {
            // 선택 전환은 조건 카탈로그와 무관하다. 옵션의 정체성만 얼린다.
            return new DialogueResultTransition(transition.Kind, null, null, null, transition.OptionId);
        }

        if (transition.Kind == ConditionTransitionKind.EndIf)
        {
            return new DialogueResultTransition(transition.Kind, null, null, null);
        }

        AvailableCondition? condition = available.Find(transition.ConditionId)
            ?? AvailableConditionResolver.FindKnown(project, definition, transition.ConditionId);

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
                // ⚠ 여기서는 <b>막는 문제</b>로 짚는다 — 미리보기가 [주의]로 보여 준다. 게임으로
                //    내보낼 때 빈 씬을 완화하는 것은 compositor의 몫이다(지금 상태를 그대로 보낸다).
                FlowProblemKind.MissingScript => (PublishProblemKind.MissingScript, true),
                FlowProblemKind.UnknownCondition => (PublishProblemKind.UnknownCondition, true),
                FlowProblemKind.UnavailableCondition => (PublishProblemKind.UnknownCondition, true),
                FlowProblemKind.ElseIfWithoutIf => (PublishProblemKind.InvalidConditionStructure, true),
                FlowProblemKind.EndIfWithoutIf => (PublishProblemKind.InvalidConditionStructure, true),
                FlowProblemKind.NestedCondition => (PublishProblemKind.InvalidConditionStructure, true),
                FlowProblemKind.MissingExitTarget => (PublishProblemKind.MissingExitTarget, true),
                FlowProblemKind.MixedChain => (PublishProblemKind.InvalidChoiceOption, true),
                FlowProblemKind.OptionWithoutChoice => (PublishProblemKind.InvalidChoiceOption, true),
                FlowProblemKind.OrphanedBranchExit => (PublishProblemKind.OrphanData, false),
                FlowProblemKind.OrphanedLineExtension => (PublishProblemKind.OrphanData, false),
                _ => (PublishProblemKind.InvalidConditionStructure, true)
            };

            problems.Add(new PublishProblem(kind, problem.LineId, problem.Message, blocking));
        }
    }

    private static void AddScriptProblems(
        StoryProject project,
        DialogueNode node,
        List<PublishProblem> problems)
    {
        if (project.FindScript(node.ScriptId) is not { } document)
        {
            return;
        }

        if (document.Lines.Count > 0 && document.ActiveLines.Any())
        {
            return;
        }

        // 막는 문제로 짚는다 — 위 `AddFlowProblems`와 같은 이유다.
        problems.Add(new PublishProblem(
            PublishProblemKind.MissingScript,
            null,
            $"대본 '{document.Name}'에 살아 있는 줄이 없습니다. " +
            "연출만 매다는 씬이면 정상이고, 아니라면 대사를 적어 주세요.",
            IsBlocking: true));
    }
}
