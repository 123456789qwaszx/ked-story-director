using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Results;

namespace Vn.Authoring.Tests;

/// <summary>
/// 펼친 결과를 평평한 문서로 합성해도 조건 의미와 원본 식별자가 사라지지 않는지 검증한다.
/// Preview 문자열보다 Segment 목록이 먼저이며, Formatter는 그 목록을 읽기만 해야 한다.
/// (⛔ 연출 명령 조각의 합성은 2026-10-29에 연출과 함께 ked-story-compositor로 갔다.)
/// </summary>
public class ResultDocumentComposerTests
{
    [Fact]
    public void 조건_전환을_실행_순서대로_평평하게_합성한다()
    {
        var sample = new Sample();
        var (_, l1, l2, l3, l4, l5, _) = sample.BuildSpecExample();

        DialogueResult result = sample.Unfold();
        RenderedDocument document = ResultDocumentComposer.Compose(result, project: sample.Project);

        RenderedSegment[] meaningful = document.Segments.ToArray();

        // ⛔ 갈래 끝마다 서던 갈래 출구의 detour는 2026-11-19에 걷혔다(usability UX28).
        Assert.Equal(
            new[]
            {
                RenderedSegmentKind.NodeHeader,
                RenderedSegmentKind.DialogueLine,
                RenderedSegmentKind.ConditionBegin,
                RenderedSegmentKind.DialogueLine,
                RenderedSegmentKind.DialogueLine,
                RenderedSegmentKind.ConditionElseIf,
                RenderedSegmentKind.DialogueLine,
                RenderedSegmentKind.DialogueLine,
                RenderedSegmentKind.ConditionEnd,
                RenderedSegmentKind.DialogueLine,
                RenderedSegmentKind.DialogueLine,
                RenderedSegmentKind.NodeFooter
            },
            meaningful.Select(segment => segment.Kind));

        RenderedSegment begin = meaningful.Single(segment => segment.Kind == RenderedSegmentKind.ConditionBegin);
        RenderedSegment elseIf = meaningful.Single(segment => segment.Kind == RenderedSegmentKind.ConditionElseIf);
        Assert.Equal(l1, begin.Source.LineId);
        Assert.Equal(l3, elseIf.Source.LineId);
        Assert.Equal(sample.Dialogue.Id, begin.Source.NodeId);

        RenderedSegment end = meaningful.Single(segment =>
            segment.Kind == RenderedSegmentKind.ConditionEnd);
        Assert.Equal(l5, end.Source.LineId);

        Assert.Contains(meaningful, segment => segment.Source.LineId == l2);
        Assert.Contains(meaningful, segment => segment.Source.LineId == l4);
    }

    /// <summary>
    /// 펼친 순간의 조건 이름과 식을 결과가 함께 싣는다 — 합성기는 조건을 다시 찾지 않는다.
    /// </summary>
    [Fact]
    public void 결과는_조건이_삭제된_뒤에도_같은_식을_재현한다()
    {
        var sample = new Sample();
        sample.Line("조건 대사", LineConditionTransition.BeginIf(sample.ConditionA.Id));
        DialogueResult result = sample.Unfold();
        string id = sample.ConditionA.Id;
        string expression = sample.ConditionA.Expression;

        sample.DropCondition(id);

        RenderedDocument document = ResultDocumentComposer.Compose(result, project: sample.Project);
        RenderedSegment condition = Assert.Single(document.Segments, segment =>
            segment.Kind == RenderedSegmentKind.ConditionBegin);

        Assert.Equal(expression, condition.Expression);
        Assert.Equal(id, condition.Source.ConditionId);
    }

    [Fact]
    public void 작업_중_미리보기는_사용할_수_없는_조건을_경고로_알린다()
    {
        // 범위가 판(챕터)이 된 뒤로 "못 쓴다"는 <b>다른 챕터에 있다</b>는 뜻이다 (2026-08-17).
        // 조건이 다른 챕터에만 있으면 이 판의 대사는 그 조건을 잃는다.
        var sample = new Sample();
        string id = sample.ConditionA.Id;
        string opening = sample.Line("조건 대사", LineConditionTransition.BeginIf(id));

        var other = new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "다른 챕터" };
        other.Conditions.Add(sample.Project.Chapters.Single(chapter => chapter.ChapterId == "test").Conditions.Single(item => item.Label == id));
        sample.Project.Chapters.Add(other);
        sample.Project.Files.Add(new StoryFile("sf_other", "다른 챕터"));
        sample.DropCondition(id);

        RenderedDocument document = WorkingDialoguePreview.Compose(sample.Project, sample.Dialogue.Id);

        RenderedSegment warning = Assert.Single(document.Segments, segment =>
            segment.Kind == RenderedSegmentKind.Warning);
        Assert.Equal(opening, warning.Source.LineId);
        Assert.Contains("다른 챕터의 조건", warning.Text ?? string.Empty, StringComparison.Ordinal);

        // 작가가 고른 조건은 그대로 남는다.
        Assert.Equal(
            id,
            sample.Dialogue.FindExtension(opening)!.Transition!.ConditionId);

        // 그리고 그 상태는 막는 문제다.
        Assert.NotEmpty(sample.BlockingProblems());
    }

    [Fact]
    public void 대사_Segment는_노드와_LineId_원본을_유지한다()
    {
        var sample = new Sample();
        string line = sample.Line("원본 위치");

        DialogueResult result = sample.Unfold();
        RenderedDocument document = ResultDocumentComposer.Compose(result, project: sample.Project);
        RenderedSegment segment = Assert.Single(document.Segments, item =>
            item.Kind == RenderedSegmentKind.DialogueLine);

        Assert.Equal(sample.Dialogue.Id, segment.Source.NodeId);
        Assert.Equal(line, segment.Source.LineId);
    }
}
