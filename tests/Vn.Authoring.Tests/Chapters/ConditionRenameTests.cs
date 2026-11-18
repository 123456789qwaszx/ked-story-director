using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>조건 이름 바꾸기</b> (2026-11-16 · <c>docs/work-orders/writer-fields-orders.md</c> W4) — [대본] 탭에서 작가가 조건을 뜻으로 부르는 이름을
/// 고친다. 라벨은 참조 열쇠라 간선의 표시조건 · 해금조건이 따라가고, 판에 공급된 사본은 Id를 지킨 채 이름만 간다(대사 줄의 갈래가
/// 그 Id를 붙든다). 식은 그대로다. 비울 수 없고, 이미 있는 이름으로는 바꾸지 않는다. 되돌리기 한 칸이다.
/// </summary>
public sealed class ConditionRenameTests
{
    [Fact]
    public void 간선의_표시조건과_해금조건이_따라가고_식은_그대로다()
    {
        ProjectEditor editor = World();
        editor.UpdateEdge("ch01", "root", "a", conditionLabel: "신뢰높음", visibleConditionLabel: "신뢰높음", matchOptionLabel: "믿는다");
        editor.UpdateEdge("ch01", "root", "b", visibleConditionLabel: "신뢰높음", matchOptionLabel: "따진다");

        ChapterConditionRenameOutcome outcome = editor.RenameChapterCondition("ch01", "신뢰높음", "  하린과 친해졌을 때 ");

        Assert.True(outcome.Applied);
        Assert.Equal(2, outcome.Edges);

        ChapterCondition condition = Assert.Single(Chapter(editor).Conditions);
        Assert.Equal(("하린과 친해졌을 때", "trust >= 3"), (condition.Label, condition.Expression));

        ChapterEdge toA = Edge(editor, "a");
        Assert.Equal(("하린과 친해졌을 때", "하린과 친해졌을 때"), (toA.ConditionLabel, toA.VisibleConditionLabel));
        ChapterEdge toB = Edge(editor, "b");
        Assert.Equal((null, "하린과 친해졌을 때"), (toB.ConditionLabel, toB.VisibleConditionLabel));
        Assert.Empty(editor.FindConditionUses("ch01", "신뢰높음"));
    }

    [Fact]
    public void 되돌리기_한_칸이다()
    {
        ProjectEditor editor = World();
        editor.UpdateEdge("ch01", "root", "a", conditionLabel: "신뢰높음", matchOptionLabel: "믿는다");

        Assert.True(editor.RenameChapterCondition("ch01", "신뢰높음", "친해짐").Applied);
        editor.Undo();

        Assert.Equal("신뢰높음", Assert.Single(Chapter(editor).Conditions).Label);
        Assert.Equal("신뢰높음", Edge(editor, "a").ConditionLabel);
    }

    [Fact]
    public void 판에_공급된_사본은_Id를_지킨_채_이름만_간다()
    {
        ProjectEditor editor = World();
        editor.EnsureChapterBoard("ch01");
        ConditionDefinition before = Supplied(editor).Single();
        Assert.Equal("신뢰높음", before.Name);

        Assert.True(editor.RenameChapterCondition("ch01", "신뢰높음", "친해짐").Applied);

        ConditionDefinition after = Supplied(editor).Single();
        Assert.Equal((before.Id, "친해짐"), (after.Id, after.Name));
    }

    [Fact]
    public void 식이_빈_조건도_이름을_바꾼다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "미정", string.Empty);

        Assert.True(editor.RenameChapterCondition("ch01", "미정", "축제에 가기로 했을 때").Applied);
        Assert.Contains(Chapter(editor).Conditions, item => item.Label == "축제에 가기로 했을 때" && item.Expression.Length == 0);
    }

    [Theory]
    [InlineData("  ", "비울 수 없습니다")]
    [InlineData("신뢰높음", "같은 이름")]
    [InlineData("요령", "이미 있습니다")]
    public void 빈_이름_같은_이름_있는_이름으로는_바꾸지_않는다(string to, string refusal)
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "요령", "trust < 3");

        ChapterConditionRenameOutcome outcome = editor.RenameChapterCondition("ch01", "신뢰높음", to);

        Assert.False(outcome.Applied);
        Assert.Contains(refusal, outcome.Refusal, StringComparison.Ordinal);
        Assert.Equal(["신뢰높음", "요령"], Chapter(editor).Conditions.Select(item => item.Label));
    }

    [Fact]
    public void 없는_조건은_거절한다()
    {
        ChapterConditionRenameOutcome outcome = World().RenameChapterCondition("ch01", "없는것", "새것");

        Assert.False(outcome.Applied);
        Assert.Contains("없습니다", outcome.Refusal, StringComparison.Ordinal);
    }

    private static ProjectEditor World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "a");
        editor.AddEpisode("ch01", "b", title: "b", 0, 0, sceneId: "b");
        editor.AddEdge("ch01", "root", "a", optionLabel: "믿는다");
        editor.AddEdge("ch01", "root", "b", optionLabel: "따진다");
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        return editor;
    }

    private static ChapterDocument Chapter(ProjectEditor editor) => editor.FindChapter("ch01")!;

    private static ChapterEdge Edge(ProjectEditor editor, string to) =>
        Chapter(editor).Edges.Single(edge => edge.FromEpisodeId == "root" && edge.ToEpisodeId == to);

    private static IEnumerable<ConditionDefinition> Supplied(ProjectEditor editor) =>
        editor.Project.EnumerateNodes().OfType<SetNode>()
            .Where(node => node.Name == ChapterBoardSupply.ConditionSupplyNodeName("ch01"))
            .SelectMany(node => node.Conditions);
}
