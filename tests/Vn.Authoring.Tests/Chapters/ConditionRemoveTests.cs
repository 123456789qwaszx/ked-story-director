using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>쓰는 조건도 지운다 — 길에서만 쓰면 떼고 지운다</b> (2026-11-26 소유자 — 「조건을 추가는 할 수 있는데, 지울 수 없다는 게 조금 아쉽고」).
///
/// [✕]는 쓰는 곳을 보이고 한 번 더 묻는다. 「모두 떼고 지우기」는 그 길들의 표시조건 · 해금조건만 「없음」으로 하고 조건을 지운다 — 한 변경이라
/// 되돌리기 한 칸에 함께 돌아온다. 길과 잠금 안내문은 남는다. 대사 줄의 갈래가 쓰면 그래도 거절한다 — 툴이 대사의 조건 블록을 걷지 않는다.
/// </summary>
public sealed class ConditionRemoveTests
{
    [Fact]
    public void 길에서_쓰는_조건은_그냥은_거절하고_떼고_지우기는_한_칸이다()
    {
        ProjectEditor editor = Chapter();
        editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        editor.UpdateEdge("ch01", "root", "a", visibleConditionLabel: "신뢰높음", matchOptionLabel: "믿는다");
        editor.AddEdge("ch01", "root", "b", optionLabel: "기다린다");
        editor.UpdateEdge("ch01", "root", "b", conditionLabel: "신뢰높음", lockedMessage: "아직 믿지 않는다", matchOptionLabel: "기다린다");

        ChapterConditionRemoveOutcome refused = editor.RemoveChapterCondition("ch01", "신뢰높음");
        Assert.False(refused.Applied);
        Assert.Equal(2, refused.Uses.Count);
        Assert.All(refused.Uses, use => Assert.Equal(ChapterUseKind.Edge, use.Kind));

        ChapterConditionRemoveOutcome done = editor.RemoveChapterCondition("ch01", "신뢰높음", clearEdges: true);

        Assert.True(done.Applied);
        Assert.Equal(2, done.EdgesCleared);
        Assert.DoesNotContain(editor.FindChapter("ch01")!.Conditions, item => item.Label == "신뢰높음");
        Assert.Null(Edge(editor, "a").VisibleConditionLabel);
        Assert.Null(Edge(editor, "b").ConditionLabel);

        // 길과 잠금 안내문은 남는다 — 해금조건을 다시 걸면 안내문이 그대로 선다.
        Assert.Equal("믿는다", Edge(editor, "a").OptionLabel);
        Assert.Equal("아직 믿지 않는다", Edge(editor, "b").LockedMessage);

        editor.Undo();

        Assert.Contains(editor.FindChapter("ch01")!.Conditions, item => item.Label == "신뢰높음");
        Assert.Equal("신뢰높음", Edge(editor, "a").VisibleConditionLabel);
        Assert.Equal("신뢰높음", Edge(editor, "b").ConditionLabel);
    }

    [Fact]
    public void 떼기는_그_조건만_떼고_같은_길의_다른_조건은_남긴다()
    {
        ProjectEditor editor = Chapter();
        editor.AddChapterCondition("ch01", "다른것", "trust < 3");
        editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        editor.UpdateEdge("ch01", "root", "a", conditionLabel: "다른것", visibleConditionLabel: "신뢰높음", matchOptionLabel: "믿는다");

        Assert.True(editor.RemoveChapterCondition("ch01", "신뢰높음", clearEdges: true).Applied);

        Assert.Null(Edge(editor, "a").VisibleConditionLabel);
        Assert.Equal("다른것", Edge(editor, "a").ConditionLabel);
    }

    [Fact]
    public void 대사_갈래가_쓰면_떼고_지우기도_거절하고_아무것도_안_바꾼다()
    {
        ProjectEditor editor = Chapter();
        editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        editor.UpdateEdge("ch01", "root", "a", visibleConditionLabel: "신뢰높음", matchOptionLabel: "믿는다");
        DialogueNode node = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        editor.SetLineTransition(node.Id, node.Script.ActiveLines.First().Id, LineConditionTransition.BeginIf("신뢰높음"));
        long before = editor.Revision;

        ChapterConditionRemoveOutcome refused = editor.RemoveChapterCondition("ch01", "신뢰높음", clearEdges: true);

        Assert.False(refused.Applied);
        Assert.Contains(refused.Uses, use => use.Kind == ChapterUseKind.DialogueBranch);
        Assert.Equal(before, editor.Revision);
        Assert.Equal("신뢰높음", Edge(editor, "a").VisibleConditionLabel);
        Assert.Contains(editor.FindChapter("ch01")!.Conditions, item => item.Label == "신뢰높음");
    }

    [Fact]
    public void 안_쓰는_조건은_떼고_지우기로도_그냥_지워진다()
    {
        ProjectEditor editor = Chapter();

        ChapterConditionRemoveOutcome done = editor.RemoveChapterCondition("ch01", "신뢰높음", clearEdges: true);

        Assert.True(done.Applied);
        Assert.Equal(0, done.EdgesCleared);
        Assert.Empty(editor.FindChapter("ch01")!.Conditions);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static ChapterEdge Edge(ProjectEditor editor, string to) =>
        editor.FindChapter("ch01")!.Edges.Single(edge => edge.FromEpisodeId == "root" && edge.ToEpisodeId == to);

    /// <summary>ch01 — 스탯 <c>trust</c>, 조건 「신뢰높음」(trust &gt;= 3), 에피소드 root · a · b, 판과 카드.</summary>
    private static ProjectEditor Chapter()
    {
        var editor = new ProjectEditor(new StoryProject());
        ChapterDocument chapter = editor.EnsureChapter("ch01");
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));

        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "root");
        editor.AddEpisode("ch01", "b", title: "b", 0, 0, sceneId: "root");
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        editor.EnsureChapterBoard("ch01");
        return editor;
    }
}
