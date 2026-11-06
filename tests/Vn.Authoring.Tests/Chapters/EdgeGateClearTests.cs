using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>간선의 조건을 「없음」으로 돌리면 정말로 없다</b> — [구성] 탭 간선 패널은 「(없음)」을 빈 글자로 보낸다. 빈 글자로 남으면
/// 화면이 조건이 있는 것으로 읽어 판의 선택지 문구 위에 「해금: 」이 남았다. 해금조건 · 표시조건 · 잠금 안내문 모두 빈 글자는
/// null로 적고, 파일에서 빈 글자를 읽어도 null이다.
/// </summary>
public sealed class EdgeGateClearTests
{
    [Fact]
    public void 빈_글자로_지우면_조건과_잠금_안내문이_null이_된다()
    {
        ProjectEditor editor = World();
        editor.AddEdge("ch01", "시작", "끝", conditionLabel: "신뢰높음", optionLabel: "계속");
        editor.UpdateEdge("ch01", "시작", "끝", matchOptionLabel: "계속",
            visibleConditionLabel: "신뢰높음", lockedMessage: "아직 이르다");
        Assert.True(Edge(editor).HasGate);

        editor.UpdateEdge("ch01", "시작", "끝", matchOptionLabel: "계속",
            conditionLabel: string.Empty, visibleConditionLabel: " ", lockedMessage: string.Empty);

        ChapterEdge edge = Edge(editor);
        Assert.Null(edge.ConditionLabel);
        Assert.Null(edge.VisibleConditionLabel);
        Assert.Null(edge.LockedMessage);
        Assert.False(edge.HasGate);
    }

    [Fact]
    public void 새_간선의_빈_해금조건도_null이다()
    {
        ProjectEditor editor = World();
        editor.AddEdge("ch01", "시작", "끝", conditionLabel: string.Empty, optionLabel: "계속");

        Assert.Null(Edge(editor).ConditionLabel);
    }

    [Fact]
    public void 파일의_빈_조건도_null로_읽는다()
    {
        ProjectEditor editor = World();
        editor.AddEdge("ch01", "시작", "끝", conditionLabel: "신뢰높음", optionLabel: "계속");

        string encoded = ProjectSnapshotCodec.Encode(editor.Project)
            .Replace("\"conditionLabel\": \"신뢰높음\"", "\"conditionLabel\": \"\"", StringComparison.Ordinal);
        Assert.Contains("\"conditionLabel\": \"\"", encoded, StringComparison.Ordinal);

        StoryProject reloaded = ProjectSnapshotCodec.Decode(encoded);
        Assert.Null(reloaded.Chapters.Single().Edges.Single().ConditionLabel);
    }

    private static ChapterEdge Edge(ProjectEditor editor) => editor.FindChapter("ch01")!.Edges.Single();

    private static ProjectEditor World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "시작", "첫 화", 0, 0);
        editor.AddEpisode("ch01", "끝", "끝 화", 200, 0);
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        return editor;
    }
}
