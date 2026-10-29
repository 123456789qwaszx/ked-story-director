using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Results;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// 선택지는 조건 전환과 동형의 "선택 전환 체인"이다.
/// 순서 안정성은 세이브 계약이고(계약서 C3), 라벨은 접두 없이 출력되며(D6),
/// 미리보기 태그는 표시 전용이고 실제 효과는 본문의 set이다(D5).
/// </summary>
public class ChoiceTests
{
    // ── 흐름 해석 ───────────────────────────────────────────────────────────

    [Fact]
    public void 선택_체인은_조건과_같은_기계로_갈래를_만든다()
    {
        var sample = new Sample();
        string label1 = sample.Line("안전한 길", LineConditionTransition.BeginChoice());
        string body1 = sample.Line("본문 1");
        string label2 = sample.Line("깊은 숲", LineConditionTransition.BeginNextOption());
        string body2 = sample.Line("본문 2");
        string after = sample.Line("블록 뒤", LineConditionTransition.EndChoice());

        DialogueFlow flow = ConditionFlowResolver.Resolve(sample.Dialogue, sample.Project);

        Assert.Equal(2, flow.Branches.Count);
        Assert.All(flow.Branches, branch => Assert.True(branch.IsChoice));
        Assert.Equal(new[] { label1, label2 }, flow.Branches.Select(branch => branch.OpenLineId));
        Assert.Equal(new[] { 0, 1 }, flow.Branches.Select(branch => branch.BranchIndexInChain));
        Assert.All(flow.Branches, branch => Assert.StartsWith("op_", branch.OptionId!, StringComparison.Ordinal));

        // 본문 줄은 자기 옵션 갈래 안에 있고, 블록 뒤 줄은 바깥이다.
        Assert.Equal(label1, flow.Lines.Single(line => line.Line.LineId == body1).Branch!.OpenLineId);
        Assert.Equal(label2, flow.Lines.Single(line => line.Line.LineId == body2).Branch!.OpenLineId);
        Assert.Null(flow.Lines.Single(line => line.Line.LineId == after).Branch);
        Assert.Empty(flow.Problems);
    }

    [Fact]
    public void 조건_안_선택지는_정식_구성이다()
    {
        // W54 (소유자 결정): 조건 갈래 본문 안에서 닫히는 선택 블록은 지원한다.
        var sample = new Sample();
        string opener = sample.Line("조건 시작", LineConditionTransition.BeginIf(sample.ConditionA.Id));
        string label1 = sample.Line("사과", LineConditionTransition.BeginChoice());
        string body1 = sample.Line("사과 본문");
        string label2 = sample.Line("포도", LineConditionTransition.BeginNextOption());
        string body2 = sample.Line("포도 본문");
        string joined = sample.Line("합류(조건 안)", LineConditionTransition.EndChoice());
        sample.Line("끝", LineConditionTransition.EndIf());

        DialogueFlow flow = ConditionFlowResolver.Resolve(sample.Dialogue, sample.Project);

        Assert.Empty(flow.Problems);
        Assert.Equal(3, flow.Branches.Count); // 조건 1 + 옵션 2

        // 깊이: 옵션 라벨·본문은 2(조건+선택), 선택이 닫힌 합류 줄은 다시 조건 안 1.
        Assert.Equal(2, flow.Lines.Single(line => line.Line.LineId == body1).Depth);
        Assert.Equal(2, flow.Lines.Single(line => line.Line.LineId == label2).Depth);
        Assert.Equal(1, flow.Lines.Single(line => line.Line.LineId == joined).Depth);

        // 안쪽 갈래 표시는 옵션이고, 바깥 조건 갈래는 선택지 줄까지 덮는다.
        Assert.Equal(label1, flow.Lines.Single(line => line.Line.LineId == body1).Branch!.OpenLineId);
        ConditionBranch condition = flow.Branches.Single(branch => !branch.IsChoice);
        Assert.Equal(opener, condition.OpenLineId);
        Assert.True(condition.LastLineIndex >=
            flow.Lines.Single(line => line.Line.LineId == body2).Index);

        // 펼쳐도 막는 문제가 없다.
        Assert.Empty(sample.BlockingProblems());
    }

    [Fact]
    public void 선택이_닫히기_전의_조건_전환은_여전히_MixedChain이다()
    {
        // 조건 종료(EndIf)는 W55로 선택지를 함께 닫지만, elseif 같은 갈래 전환은 여전히 위반이다.
        var sample = new Sample();
        sample.Line("조건 시작", LineConditionTransition.BeginIf(sample.ConditionA.Id));
        sample.Line("선택 시작", LineConditionTransition.BeginChoice());
        sample.Line("끝", LineConditionTransition.BeginElseIf(sample.ConditionB.Id)); // 선택지 끝 없이 갈래 전환

        DialogueFlow flow = ConditionFlowResolver.Resolve(sample.Dialogue, sample.Project);
        Assert.Contains(flow.Problems, problem => problem.Kind == FlowProblemKind.MixedChain);

        Assert.NotEmpty(sample.BlockingProblems());
    }

    [Fact]
    public void 조건_종료_한_줄이_선택지도_함께_닫는다()
    {
        // W55 (소유자 지시): 선택지 끝과 조건 끝을 한 줄로 겹칠 수 있다.
        var sample = new Sample();
        sample.Line("조건 시작", LineConditionTransition.BeginIf(sample.ConditionA.Id));
        sample.Line("사과", LineConditionTransition.BeginChoice());
        string body = sample.Line("사과 본문");
        string combined = sample.Line("모두 끝", LineConditionTransition.EndIf());
        sample.Line("바깥");

        DialogueFlow flow = ConditionFlowResolver.Resolve(sample.Dialogue, sample.Project);

        Assert.Empty(flow.Problems);
        Assert.Equal(0, flow.Lines.Single(line => line.Line.LineId == combined).Depth); // 둘 다 닫혔다

        // 중첩 선택지 안의 드롭다운에는 결합 종료 항목이 제시된다.
        ResolvedLine inside = flow.Lines.Single(line => line.Line.LineId == combined);
        Assert.True(inside.PrecedingBranch is { IsChoice: true } && inside.PrecedingDepth == 2);
        IReadOnlyList<ConditionChoice> choices = ConditionChoices.For(
            inside.PrecedingBranch,
            sample.Dialogue,
            sample.Project,
            choiceInsideCondition: true);
        Assert.Contains(choices, choice =>
            choice.Kind == ConditionChoiceKind.EndIf &&
            choice.Label == ConditionChoices.EndChoiceAndIfLabel);

        Assert.Empty(sample.BlockingProblems()); // 막는 문제도 없다
        _ = body;
    }

    [Fact]
    public void 끝까지_열린_선택_블록은_막히지_않고_알림만_남는다()
    {
        // 규칙 개정 (2026-08-14 소유자 승인, 2단계 포트 규칙) — 선택지로 끝나는 노드는
        // 정상이다. 옵션들이 곧 노드의 끝이고, 이어진 옵션은 출구로 점프하며 안 이은
        // 옵션은 에피소드 종료다. 예전에는 발행을 막았다(Gate B 반칸의 정체).
        var sample = new Sample();
        sample.Line("라벨", LineConditionTransition.BeginChoice());
        sample.Line("본문");

        DialogueDraft draft = DialoguePublisher.Draft(sample.Project, sample.Dialogue.Id);

        Assert.DoesNotContain(draft.Problems, problem => problem.IsBlocking);
        Assert.Contains(draft.Problems, problem =>
            !problem.IsBlocking && problem.Message.Contains("문서 끝까지 열려"));
    }

    // ── OptionId 정체성 ─────────────────────────────────────────────────────

    [Fact]
    public void 편집기는_OptionId를_발급하고_전환을_다시_지정해도_잇는다()
    {
        var sample = new Sample();
        string label = sample.Line("라벨", LineConditionTransition.BeginChoice());

        string firstId = sample.Dialogue.FindExtension(label)!.Transition!.OptionId!;
        Assert.StartsWith("op_", firstId, StringComparison.Ordinal);

        // 같은 줄의 전환 종류를 바꿔도 옵션의 정체성은 그대로다.
        sample.Editor.SetLineTransition(
            sample.Dialogue.Id,
            label,
            LineConditionTransition.BeginNextOption());

        Assert.Equal(firstId, sample.Dialogue.FindExtension(label)!.Transition!.OptionId);
    }

    [Fact]
    public void 선택_전환은_OptionId까지_저장_왕복된다()
    {
        var sample = new Sample();
        string label = sample.Line("라벨", LineConditionTransition.BeginChoice());
        sample.Line("끝", LineConditionTransition.EndChoice());
        string optionId = sample.Dialogue.FindExtension(label)!.Transition!.OptionId!;

        StoryProject reloaded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(sample.Project));
        LineConditionTransition transition =
            reloaded.FindDialogue(sample.Dialogue.Id)!.FindExtension(label)!.Transition!;

        Assert.Equal(ConditionTransitionKind.BeginChoice, transition.Kind);
        Assert.Equal(optionId, transition.OptionId);
    }

    // ── 펼침 ────────────────────────────────────────────────────────────────

    [Fact]
    public void 펼친_결과에_OptionId가_실린다()
    {
        var sample = new Sample();
        string label = sample.Line("라벨", LineConditionTransition.BeginChoice());
        sample.Line("본문");
        sample.Line("끝", LineConditionTransition.EndChoice());
        string optionId = sample.Dialogue.FindExtension(label)!.Transition!.OptionId!;

        DialogueResult result = sample.Unfold();

        Assert.Equal(optionId, result.FindLine(label)!.Transition!.OptionId);
    }

    // ── 합성(Segment) ───────────────────────────────────────────────────────

    [Fact]
    public void 라벨은_ChoiceOption으로_본문은_들여쓴_일반_라인으로_펼쳐진다()
    {
        var sample = new Sample();
        string label = sample.Line("안전한 길", LineConditionTransition.BeginChoice());
        string body = sample.Line("본문");
        sample.Line("끝", LineConditionTransition.EndChoice());

        RenderedDocument document = ResultDocumentComposer.Compose(sample.Unfold(), project: sample.Project);

        RenderedSegment option = document.Segments.Single(segment =>
            segment.Kind == RenderedSegmentKind.ChoiceOption);
        Assert.Equal("안전한 길", option.Text);
        Assert.Equal(0, option.ChoiceBlockOrdinal);
        Assert.Equal(0, option.ChoiceOptionIndex);

        // D5 — 정수 증감만 태그가 되고 키는 소문자다. 대입과 비정수는 태그를 만들지 않는다.
        // ⛔ <b>미리보기 태그가 사라졌다</b> (2026-09-17 — 작가 변수 폐지). 태그는 줄의
        //    `<<set>>`에서 <b>파생</b>되던 표시용 글자였다. 값이 변하는 자리가 챕터 간선의
        //    `스탯변화`로 옮겨졌으므로, 선택지에 비용을 보여 주려면 그쪽에서 다시 내야 한다.
        Assert.Null(option.Tags);

        // 라벨은 일반 대사 라인으로 나오지 않는다.
        Assert.DoesNotContain(document.Segments, segment =>
            segment.Kind == RenderedSegmentKind.DialogueLine && segment.Source.LineId == label);

        RenderedSegment bodyLine = document.Segments.Single(segment =>
            segment.Kind == RenderedSegmentKind.DialogueLine && segment.Source.LineId == body);
        Assert.Equal(1, bodyLine.IndentLevel);

        // ⛔ 「대문자 변수 소문자화」 알림은 2026-09-17에 사라졌다 — 그 알림은 옵션의
        //    <b>미리보기 태그</b>가 소문자 키로 조회된다는 사정이었고, 태그가 작가 변수
        //    set에서 파생되던 것이라 함께 없어졌다.
        DialogueDraft draft = DialoguePublisher.Draft(sample.Project, sample.Dialogue.Id);
        Assert.DoesNotContain(draft.Problems, problem =>
            problem.Kind == PublishProblemKind.ChoicePreviewNotice);
    }

    // ── 조건 안 선택지 (W54) ────────────────────────────────────────────────

    [Fact]
    public void 조건_안_선택지는_들여쓴_옵션으로_나오고_조건_출구는_선택_뒤에_detour한다()
    {
        // W54: 조건 갈래 안에서 닫히는 선택 블록 + 그 조건 갈래의 출구.
        var sample = new Sample();

        sample.Line("가게 앞이다.");
        string opener = sample.Line("주인이 있다", LineConditionTransition.BeginIf(sample.ConditionA.Id));
        string label1 = sample.Line("치킨을 산다", LineConditionTransition.BeginChoice());
        string body1 = sample.Line("치킨을 샀다.");
        sample.Line("그냥 나온다", LineConditionTransition.BeginNextOption());
        string lastBody = sample.Line("빈손으로 나왔다.");
        sample.Line("주인이 인사했다.", LineConditionTransition.EndChoice());
        sample.Line("가게를 나섰다.", LineConditionTransition.EndIf());
        sample.Line("집으로 간다.");

        // 조건 갈래의 출구 — 안의 선택지를 다 지나고 나서 점프해야 한다.
        sample.Editor.SetExitTarget(sample.Dialogue.Id, ExitPortKind.Branch, opener, sample.TargetA.Id);
        sample.Editor.SetExitTarget(sample.Dialogue.Id, ExitPortKind.Default, null, sample.TargetDefault.Id);

        IReadOnlyList<RenderedSegment> segments =
            ResultDocumentComposer.Compose(sample.Unfold(), project: sample.Project).Segments;
        int Index(Func<RenderedSegment, bool> match) => segments.ToList().FindIndex(segment => match(segment));

        Assert.Equal(1, segments.Single(segment => segment.Source.LineId == label1 &&
            segment.Kind == RenderedSegmentKind.ChoiceOption).IndentLevel);   // 라벨이 조건 깊이만큼 들여쓰였다
        Assert.Equal(2, segments.Single(segment => segment.Source.LineId == body1 &&
            segment.Kind == RenderedSegmentKind.DialogueLine).IndentLevel);   // 옵션 본문은 라벨보다 한 단 더

        // 조건 갈래 출구(detour)는 선택 블록의 마지막 본문 뒤, endif 앞에서 나온다 —
        // 선택 전환에 새어 나오면 선택지가 제시되기 전에 떠나 버린다.
        int lastOptionBody = Index(segment => segment.Source.LineId == lastBody);
        int detour = Index(segment => segment.Kind == RenderedSegmentKind.BranchDetour);
        int endif = Index(segment => segment.Kind == RenderedSegmentKind.ConditionEnd);
        Assert.True(lastOptionBody >= 0 && lastOptionBody < detour && detour < endif);
    }
}
