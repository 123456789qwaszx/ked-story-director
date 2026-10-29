using Vn.Authoring.Model;
using Vn.Authoring.Results;

namespace Vn.Authoring.Rendering;

/// <summary>
/// 펼친 <see cref="DialogueResult"/>를 출력 옵션에 맞는 평평한 Segment 목록으로 합성한다.
///
/// <b>대사 미리보기의 유일한 입구다.</b> Runtime Full · 대본 · 녹음 대본 · 번역표가 모두 같은
/// Segment 목록에서 나오고, 카드의 글 칸도 여기를 지난다(ScenarioOnly). 형식마다 합성기를 따로 두면
/// 같은 원고에서 나온 문서들이 조금씩 다른 이야기를 하게 된다.
///
/// 이 클래스는 조건 의미를 다시 계산하지 않는다. 결과가 이미 펼친 구조를 들고 있다.
/// 문자열 문법도 모른다. 실제 표기는 Formatter가 맡는다.
/// ⛔ 연출 명령 조각(연출 결과와의 합성)은 2026-10-29에 연출과 함께 <c>ked-story-compositor</c>로 갔다.
/// </summary>
public static class ResultDocumentComposer
{
    public static RenderedDocument Compose(
        DialogueResult dialogue,
        StoryProject? project = null,
        DocumentOutputOptions? options = null,
        OutputPresetId? presetId = null,
        ILocalizedLineProvider? localization = null)
    {
        ArgumentNullException.ThrowIfNull(dialogue);

        options ??= OutputPresetCatalog.RuntimeFull.Options;

        var segments = new List<RenderedSegment>();
        var nodeSource = new RenderSourceReference(NodeId: dialogue.SourceNodeId);

        if (options.IncludeStructure)
        {
            segments.Add(new RenderedSegment(
                Id: $"node:{dialogue.SourceNodeId}:header",
                Kind: RenderedSegmentKind.NodeHeader,
                Layer: DocumentLayer.Structure,
                Source: nodeSource,
                Text: dialogue.SourceNodeName));
        }

        // ⛔ <b>설정노드의 할당은 노드 머리의 `set`으로 내지 않는다</b> (2026-08-24,
        // `docs/work-orders/chapter-scope-variables-orders.md` §4). 그 할당은 챕터의 <b>선언</b>이라
        // 노드마다 되밟으면 초기화의 수명이 에피소드가 된다. 작가가 대본 <b>줄에</b> 단 set은
        // 그대로 낸다(아래 line.Sets) — 그건 이야기 도중의 변화다.

        int depth = 0;
        int choiceBlockOrdinal = -1;
        int choiceOptionIndex = -1;
        bool showDialogue = options.IncludeDialogueText ||
            options.IncludeLocalizedDialogue ||
            options.IncludeSpeaker ||
            options.IncludeLineId;

        // 갈래 출구는 여는 줄이 소유하지만(§4.2) 실행은 갈래의 끝에서 일어난다.
        // 여는 줄 바로 뒤에 jump를 두면 갈래 본문이 그 아래 묻혀 실행되지 않는다.
        // 그래서 자기 체인의 다음 전환이 갈래를 닫는 순간에 내보낸다. 조건 갈래의 출구는
        // 안에 열린 선택 블록(W54)의 전환에는 흘러나오지 않는다 — 소유 체인이 다르다.
        // 조건 갈래의 출구는 jump가 아니라 detour다 — 커스텀 씬을 재생하고 갈래로
        // 돌아와 나머지 대본을 계속한다. 선택지 출구는 그대로 jump(이동)다.
        (RenderSourceReference Source, string Target, bool IsChoice)? pendingBranchJump = null;

        // W54: 조건 갈래 안 선택 블록 — 들여쓰기는 (조건 열림 ? 1 : 0) + (옵션 본문 ? 1 : 0).
        // W55: 조건 종료는 열린 선택지도 함께 닫는다 — Pres 합성 조건을 잃지 않으려면
        // 암묵 ChoiceEnd를 조건 종료보다 먼저 내야 한다.
        // 지금 몇 겹의 조건 안인가 (2026-08-17에 불리언에서 늘렸다).
        int conditionDepth = 0;
        bool choiceOpen = false;

        // ⚠ 줄들을 돈 <b>뒤에 한 번 더</b> 돈다 — 마지막이 <b>꼬리</b>다(줄 없는 자리).
        // 대사 없는 조건 블록이 대본의 끝이면 전환을 실을 줄이 없어서, 그 전환들은 결과의
        // `TrailingTransitions`에 산다. 여기서 <b>베끼지 않고 같은 루프로</b> 지나야 한다:
        // 전환→세그먼트 규칙(깊이·들여쓰기·대기 중 출구·암묵 ChoiceEnd)이 한 벌이어야
        // 두 자리가 갈리지 않는다. 흐름 해석도 같은 수를 쓴다.
        for (int lineIndex = 0; lineIndex <= dialogue.Lines.Count; lineIndex++)
        {
            bool tail = lineIndex == dialogue.Lines.Count;
            DialogueResultLine? line = tail ? null : dialogue.Lines[lineIndex];

            IReadOnlyList<DialogueResultTransition> transitions =
                tail ? dialogue.TrailingTransitions : line!.Transitions;

            if (tail && transitions.Count == 0)
            {
                break; // 꼬리가 없으면 한 바퀴도 돌 것이 없다.
            }

            var lineSource = new RenderSourceReference(
                NodeId: dialogue.SourceNodeId,
                LineId: line?.LineId,
                ConditionId: (tail ? transitions.FirstOrDefault() : line!.Transition)?.ConditionId);

            // 세그먼트 Id의 뿌리 — 줄이 없으면 노드의 꼬리다.
            string source = line?.LineId ?? $"{dialogue.SourceNodeId}:trailing";

            bool isOptionLabel = false;

            // 한 줄 앞에 전환이 여럿 몰릴 수 있다 (2026-08-17) — Yarn에 전환만 있는 줄이
            // 없어서 겹쳐 닫기·연달아 열기가 전부 다음 대사 줄 앞에 쌓인다. 순서대로 재생한다.
            foreach (DialogueResultTransition transition in transitions)
            {
                bool isChoiceTransition = transition.Kind is ConditionTransitionKind.BeginChoice
                    or ConditionTransitionKind.BeginNextOption
                    or ConditionTransitionKind.EndChoice;

                // 자기 체인의 전환만 대기 중인 출구를 흘려보낸다 (W54) — 조건 갈래의 출구가
                // 안의 선택 전환에서 새어 나오면 선택지가 제시되기 전에 점프해 버린다.
                if (options.IncludeExecutionJumps && pendingBranchJump is { } pending &&
                    pending.IsChoice == isChoiceTransition)
                {
                    AddBranchExit(segments, project, pending.Source, pending.Target, pending.IsChoice);
                    pendingBranchJump = null;
                }
                else if (!isChoiceTransition)
                {
                    // 조건 전환은 어떤 대기든 닫는다(깨진 구조의 안전망 — 잃지 않고 낸다).
                    if (options.IncludeExecutionJumps && pendingBranchJump is { } stale)
                    {
                        AddBranchExit(segments, project, stale.Source, stale.Target, stale.IsChoice);
                    }

                    pendingBranchJump = null;
                }

                switch (transition.Kind)
                {
                    case ConditionTransitionKind.BeginChoice:
                    case ConditionTransitionKind.BeginNextOption:
                        isOptionLabel = true;
                        choiceOpen = true;
                        depth = conditionDepth + 1; // 옵션 본문은 감싼 조건보다 한 겹 안이다

                        if (transition.Kind == ConditionTransitionKind.BeginChoice)
                        {
                            choiceBlockOrdinal++;
                            choiceOptionIndex = 0;
                        }
                        else
                        {
                            choiceOptionIndex++;
                        }

                        // ⚠ 옵션 라벨은 <b>그 줄 자체가 버튼</b>이라 꼬리에는 있을 수 없다.
                        if (showDialogue && line is { } optionLine)
                        {
                            // 라벨 라인은 일반 대사가 아니라 버튼이다. 본문 줄 수를 세는
                            // 규칙(계약서 B)에서 빠지도록 별도 종류로 낸다.
                            segments.Add(new RenderedSegment(
                                Id: $"choice:{source}",
                                Kind: RenderedSegmentKind.ChoiceOption,
                                Layer: DocumentLayer.Dialogue,
                                Source: lineSource,
                                IndentLevel: conditionDepth, // 라벨은 감싼 조건 깊이 (W54)
                                Text: options.IncludeDialogueText ? optionLine.Text : null,
                                LocalizedText: options.IncludeLocalizedDialogue
                                    ? localization?.GetLocalizedText(optionLine.LineId)
                                    : null,
                                Speaker: options.IncludeSpeaker ? optionLine.CharacterName : null,
                                OptionId: transition.OptionId,
                                ChoiceBlockOrdinal: choiceBlockOrdinal,
                                ChoiceOptionIndex: choiceOptionIndex));
                        }

                        break;

                    case ConditionTransitionKind.EndChoice:
                        choiceOpen = false;
                        depth = conditionDepth; // 조건 안이었다면 그 갈래로 돌아간다 (W54)

                        if (showDialogue)
                        {
                            segments.Add(new RenderedSegment(
                                Id: $"choice:{source}:end",
                                Kind: RenderedSegmentKind.ChoiceEnd,
                                Layer: DocumentLayer.Dialogue,
                                Source: lineSource,
                                ChoiceBlockOrdinal: choiceBlockOrdinal));
                        }

                        break;

                    default:
                        // 조건 종료가 선택지를 함께 닫는다 (W55) — Pres의 합성 조건($__ch)이
                        // 조건 사본보다 먼저 닫히도록 암묵 ChoiceEnd를 낸다.
                        if (choiceOpen && showDialogue)
                        {
                            segments.Add(new RenderedSegment(
                                Id: $"choice:{source}:implicit-end",
                                Kind: RenderedSegmentKind.ChoiceEnd,
                                Layer: DocumentLayer.Dialogue,
                                Source: lineSource,
                                ChoiceBlockOrdinal: choiceBlockOrdinal));
                        }

                        choiceOpen = false;

                        // 표지 자신은 <b>바깥</b> 깊이에 선다 — 감싸는 줄이 아니라 경계다.
                        // `<<if>>`는 열기 전 깊이, `<<endif>>`는 닫은 뒤 깊이, `<<elseif>>`는
                        // 자기 체인의 바깥(= 지금 깊이 한 겹 밖)이다.
                        int markerIndent = transition.Kind switch
                        {
                            ConditionTransitionKind.BeginIf => conditionDepth,
                            _ => Math.Max(0, conditionDepth - 1)
                        };

                        // 깊이는 이제 세어서 안다 — 불리언이던 시절에는 겹친 블록을 표현할
                        // 수 없었다. EndIf는 한 겹 벗고, BeginIf는 한 겹 쓰고, elseif는
                        // 제자리다(같은 체인의 다른 갈래라 깊이가 안 는다).
                        conditionDepth = transition.Kind switch
                        {
                            ConditionTransitionKind.EndIf => Math.Max(0, conditionDepth - 1),
                            ConditionTransitionKind.BeginIf => conditionDepth + 1,
                            _ => conditionDepth
                        };

                        depth = conditionDepth;

                        if (options.IncludeConditions)
                        {
                            AddTransition(segments, source, transition, lineSource, markerIndent);
                        }

                        break;
                }

                // 줄 없는 갈래의 출구 (2026-08-24) — 실을 줄이 없어 <b>전환에</b> 실려 온다.
                // 줄에 매달린 출구는 아래(줄 단위)에서 대기시키므로 여기서 겹치지 않는다:
                // 이 칸은 꼬리 전환에만 채워진다.
                //
                // ⚠ 여는 전환에서 대기시켜야 <b>닫는 전환이 흘려보낸다</b> — 그래야
                // `<<if>>` → `<<detour>>` → `<<endif>>` 순서가 나온다. 여는 자리에 바로
                // 내면 갈래 본문보다 앞서 뛴다(같은 이유로 줄 쪽도 대기시킨다).
                if (transition.ExitTargetNodeId is { } trailingTarget)
                {
                    pendingBranchJump = (
                        lineSource,
                        trailingTarget,
                        transition.Kind is ConditionTransitionKind.BeginChoice
                            or ConditionTransitionKind.BeginNextOption);
                }
            }

            int indent = options.IncludeConditions || isOptionLabel ? depth : 0;

            // 꼬리에는 대사가 없다 — 갈래를 열고 닫기만 한다. 출구는 전환에
            // 실려 왔으므로 위 루프가 이미 대기시켰다.
            // (`tail`이 아니라 `line`으로 묻는다 — 그래야 아래가 널 아님으로 좁혀진다.)
            if (line is null)
            {
                continue;
            }

            // 「조건 분기」 표식 (R7 P-5) — 이 줄 <b>앞에서</b> 다녀온다. 전환과 같은 규약이다.
            //
            // ⚠ 조건이 없다: `<<if>>`로 감싸지 않고 `<<detour>>` 한 줄만 나간다. 성립하든
            //   말든 다녀오고, 다녀온 자유 씬이 제 첫머리에서 보고 아니면 곧바로 돌아온다.
            if (line.DetourTargetNodeId is { Length: > 0 } marked)
            {
                segments.Add(new RenderedSegment(
                    Id: $"branch:{line.LineId}:mark",
                    Kind: RenderedSegmentKind.BranchDetour,
                    Layer: DocumentLayer.ExecutionJumps,
                    Source: lineSource,
                    IndentLevel: indent,
                    TargetNodeId: marked,
                    TargetNodeName: project?.FindNode(marked)?.Name));
            }

            if (showDialogue && !isOptionLabel)
            {
                segments.Add(new RenderedSegment(
                    Id: $"line:{line.LineId}",
                    Kind: RenderedSegmentKind.DialogueLine,
                    Layer: DocumentLayer.Dialogue,
                    Source: lineSource,
                    IndentLevel: indent,
                    Text: options.IncludeDialogueText ? line.Text : null,
                    LocalizedText: options.IncludeLocalizedDialogue
                        ? localization?.GetLocalizedText(line.LineId)
                        : null,
                    Speaker: options.IncludeSpeaker ? line.CharacterName : null));
            }

            if (line.BranchExitTargetNodeId is { } branchTarget)
            {
                // 갈래 출구의 소유자는 화면에 보이는 마지막 줄이 아니라 갈래를 여는 LineId다.
                // 결과에서도 같은 규칙을 지켜야 Preview에서 원본으로 돌아갈 수 있다.
                pendingBranchJump = (
                    lineSource,
                    branchTarget,
                    line.Transition?.Kind is ConditionTransitionKind.BeginChoice
                        or ConditionTransitionKind.BeginNextOption);
            }
        }

        if (options.IncludeExecutionJumps && pendingBranchJump is { } lastPending)
        {
            // 선택지로 끝나는 노드의 마지막 옵션 출구가 여기로 온다 — 닫는 전환이 없어도
            // 출구를 조용히 버리지 않는다(순서: 옵션 본문 안에서 점프가 먼저, 닫힘은 그 뒤).
            AddBranchExit(segments, project, lastPending.Source, lastPending.Target, lastPending.IsChoice);
        }

        if (choiceOpen && showDialogue)
        {
            // 문서 끝까지 열린 선택 블록 — 옵션들이 곧 노드의 끝이다(포트 규칙: 이어진
            // 옵션은 점프, 안 이은 옵션은 에피소드 종료). Pres 사본의 합성 조건($__ch)과
            // 이미터의 들여쓰기 짝을 위해 여기서 닫는다.
            segments.Add(new RenderedSegment(
                Id: $"node:{dialogue.SourceNodeId}:trailing-choice-end",
                Kind: RenderedSegmentKind.ChoiceEnd,
                Layer: DocumentLayer.Dialogue,
                Source: nodeSource,
                ChoiceBlockOrdinal: choiceBlockOrdinal));
        }

        // 문서 끝까지 열린 조건 블록도 같은 이유로 닫는다 (2026-08-17 소유자 보고 —
        // "<<endif>>가 붙을 곳이 없습니다"). 닫힘은 <b>다음 줄</b>이 실어 나르는데,
        // 조건 블록이 에피소드의 마지막이면 그 다음 줄이 없다. 안 닫으면 `<<if>>`만
        // 남은 Yarn이 나가 컴파일이 깨진다 — 선택 블록 쪽에는 이미 있던 마무리다.
        if (conditionDepth > 0 && options.IncludeConditions)
        {
            segments.Add(new RenderedSegment(
                Id: $"node:{dialogue.SourceNodeId}:trailing-condition-end",
                Kind: RenderedSegmentKind.ConditionEnd,
                Layer: DocumentLayer.Conditions,
                Source: nodeSource));
        }

        if (options.IncludeExecutionJumps && dialogue.DefaultExitTargetNodeId is { } defaultTarget)
        {
            segments.Add(new RenderedSegment(
                Id: $"node:{dialogue.SourceNodeId}:default-jump",
                Kind: RenderedSegmentKind.DefaultJump,
                Layer: DocumentLayer.ExecutionJumps,
                Source: nodeSource,
                TargetNodeId: defaultTarget,
                TargetNodeName: project?.FindNode(defaultTarget)?.Name));
        }

        if (options.IncludeStructure)
        {
            segments.Add(new RenderedSegment(
                Id: $"node:{dialogue.SourceNodeId}:footer",
                Kind: RenderedSegmentKind.NodeFooter,
                Layer: DocumentLayer.Structure,
                Source: nodeSource));
        }

        return new RenderedDocument(dialogue.SourceNodeId, segments, options, presetId);
    }

    /// <summary>
    /// 갈래 출구 하나. 선택지 옵션은 jump(그 씬으로 이동), 조건 갈래(IF)는 detour —
    /// 커스텀 씬을 재생하고 갈래로 돌아와 나머지 대본을 계속한다.
    /// </summary>
    private static void AddBranchExit(
        List<RenderedSegment> segments,
        StoryProject? project,
        RenderSourceReference source,
        string targetNodeId,
        bool isChoice)
    {
        segments.Add(new RenderedSegment(
            Id: $"branch:{source.LineId}:jump",
            Kind: isChoice ? RenderedSegmentKind.BranchJump : RenderedSegmentKind.BranchDetour,
            Layer: DocumentLayer.ExecutionJumps,
            Source: source,
            IndentLevel: 1,
            TargetNodeId: targetNodeId,
            TargetNodeName: project?.FindNode(targetNodeId)?.Name));
    }

    private static void AddTransition(
        List<RenderedSegment> segments,
        string idRoot,
        DialogueResultTransition transition,
        RenderSourceReference source,
        int indentLevel = 0)
    {
        if (transition.Kind == ConditionTransitionKind.EndIf)
        {
            segments.Add(new RenderedSegment(
                Id: $"condition:{idRoot}:end",
                Kind: RenderedSegmentKind.ConditionEnd,
                Layer: DocumentLayer.Conditions,
                Source: source,
                IndentLevel: indentLevel));
            return;
        }

        string expression = transition.Expression is { Length: > 0 } value
            ? value
            : transition.ConditionId ?? string.Empty;

        segments.Add(new RenderedSegment(
            Id: $"condition:{idRoot}:open",
            Kind: transition.Kind == ConditionTransitionKind.BeginIf
                ? RenderedSegmentKind.ConditionBegin
                : RenderedSegmentKind.ConditionElseIf,
            Layer: DocumentLayer.Conditions,
            Source: source,
            IndentLevel: indentLevel,
            Text: transition.ConditionName,
            Expression: expression));
    }
}
