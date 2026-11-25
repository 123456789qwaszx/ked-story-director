using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>[대본] 판 크기</b> (2026-11-17 · settings C1) — 프로젝트에 싣고(기본이면 안 쓴다), 늘리면 크기만,
/// 줄여서 판 밖으로 나가는 카드가 있으면 카드 전체의 자리와 선 꺾임을 같은 비율로 다시 적는다. 크기 · 자리 · 꺾임이 되돌리기 한 칸이다.
/// </summary>
public sealed class BoardSizeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-board-size", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>챕터 하나 — 카드 넷을 가로 0 · 3000 · 6000 · 9000에 한 줄로(세로 100), 다섯째는 세로 6000.</summary>
    private static ProjectEditor Board()
    {
        var editor = new ProjectEditor(new StoryProject { Title = "판 크기" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");

        string[] ids = ["a", "b", "c", "d", "e"];
        (double X, double Y)[] at = [(0, 100), (3000, 100), (6000, 100), (9000, 100), (3000, 6000)];

        for (int index = 0; index < ids.Length; index++)
        {
            editor.AddEpisode("ch01", ids[index], title: ids[index], 0, 0, sceneId: ids[index]);
            editor.MoveNode(Card(editor, ids[index]).Id, at[index].X, at[index].Y);
        }

        return editor;
    }

    private static DialogueNode Card(ProjectEditor editor, string episodeId) =>
        EpisodeNaming.CardFor(editor.Project, "ch01", episodeId)!;

    private static (double X, double Y) At(ProjectEditor editor, string episodeId) =>
        (Card(editor, episodeId).Layout.X, Card(editor, episodeId).Layout.Y);

    private static (double X, double Y)[] AllAt(ProjectEditor editor) =>
        new[] { "a", "b", "c", "d", "e" }.Select(id => At(editor, id)).ToArray();

    private static bool Overlap(ProjectEditor editor, string a, string b)
    {
        StoryNode first = Card(editor, a);
        StoryNode second = Card(editor, b);

        return first.Layout.X < second.Layout.X + BoardSize.CardWidthOf(second) &&
               second.Layout.X < first.Layout.X + BoardSize.CardWidthOf(first) &&
               first.Layout.Y < second.Layout.Y + BoardSize.CardHeightOf(second) &&
               second.Layout.Y < first.Layout.Y + BoardSize.CardHeightOf(first);
    }

    [Fact]
    public void 기본_판은_12000x8000이고_파일에_쓰지_않으며_바꾼_크기는_저장_왕복한다()
    {
        ProjectEditor editor = Board();
        Assert.Equal(new BoardSize(12000, 8000), editor.Project.Board);

        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        Directory.CreateDirectory(_directory);
        ProjectStore.Save(manifest, editor.Project);
        Assert.DoesNotContain("\"board\"", File.ReadAllText(manifest), StringComparison.Ordinal);

        editor.SetBoardSize(20000, 15000);
        ProjectStore.Save(manifest, editor.Project);

        Assert.Equal(new BoardSize(20000, 15000), ProjectStore.Load(manifest).Project.Board);
    }

    [Fact]
    public void 범위_밖의_값은_고를_때_최소_최대로_들이고_파일에_있으면_받지_않는다()
    {
        ProjectEditor editor = Board();

        editor.SetBoardSize(100000, 80000);
        Assert.Equal(new BoardSize(BoardSize.Maximum, BoardSize.Maximum), editor.Project.Board);

        // 손으로 고친 파일은 들이지 않는다(2026-11-18 · old-shapes O8 — ⛔ 그 전에는 범위 안으로 들이고 숫자가 아니면 기본으로 읽었다).
        Assert.Throws<InvalidDataException>(() =>
            ProjectManifestJson.ReadBoard(System.Text.Json.Nodes.JsonNode.Parse("{\"width\": 10, \"height\": 9000}")));
        Assert.Throws<InvalidDataException>(() =>
            ProjectManifestJson.ReadBoard(System.Text.Json.Nodes.JsonNode.Parse("{\"width\": \"넓게\", \"height\": 9000}")));
        Assert.Throws<InvalidDataException>(() =>
            ProjectManifestJson.ReadBoard(System.Text.Json.Nodes.JsonNode.Parse("{\"width\": 20000}")));
        Assert.Equal(new BoardSize(20000, 9000),
            ProjectManifestJson.ReadBoard(System.Text.Json.Nodes.JsonNode.Parse("{\"width\": 20000, \"height\": 9000}")));
    }

    [Fact]
    public void 늘리면_카드_자리는_그대로고_크기만_바뀐다_되돌리기_한_칸()
    {
        ProjectEditor editor = Board();
        int revisions = 0;
        editor.Changed += (_, _) => revisions++;
        (double X, double Y)[] before = AllAt(editor);

        editor.SetBoardSize(30000, 20000);

        Assert.Equal(new BoardSize(30000, 20000), editor.Project.Board);
        Assert.Equal(before, AllAt(editor));
        Assert.Equal(1, revisions);

        // 같은 크기는 적지 않는다.
        editor.SetBoardSize(30000, 20000);
        Assert.Equal(1, revisions);

        editor.Undo();
        Assert.Equal(BoardSize.Default, editor.Project.Board);
    }

    [Fact]
    public void 줄여도_카드가_다_들어가면_아무것도_옮기지_않는다()
    {
        ProjectEditor editor = Board();
        (double X, double Y)[] before = AllAt(editor);

        // 가장 오른쪽 카드는 9000 + 210, 가장 아래는 6000 + 240 — 10000 × 7000 안에 든다.
        BoardResizePlan plan = BoardResize.Plan(editor.Project, 10000, 7000);
        Assert.False(plan.Compresses);
        Assert.Equal(0, plan.MovedCards);

        editor.SetBoardSize(10000, 7000);

        Assert.Equal(new BoardSize(10000, 7000), editor.Project.Board);
        Assert.Equal(before, AllAt(editor));
    }

    [Fact]
    public void 줄여서_넘치면_카드_전체를_같은_비율로_줄여_새_판에_들인다()
    {
        ProjectEditor editor = Board();

        // 가로만 넘친다 — 가로 비율 (6000 − 210) / 9000, 세로는 그대로.
        BoardResizePlan plan = BoardResize.Plan(editor.Project, 6000, 8000);
        Assert.True(plan.Compresses);
        Assert.Equal(1, plan.RatioY);
        Assert.Equal((6000 - BoardSize.CardWidth) / 9000, plan.RatioX, precision: 9);
        Assert.Equal(4, plan.MovedCards);   // a는 0이라 그대로, 나머지 넷(e도 가로 3000)이 움직인다

        editor.SetBoardSize(6000, 8000);

        // 줄이는 축은 정수로 내린다(끝자리 오차로 판을 넘지 않게).
        double r = plan.RatioX;
        Assert.Equal((0, 100), At(editor, "a"));
        Assert.Equal((Math.Floor(3000 * r), 100), At(editor, "b"));
        Assert.Equal((Math.Floor(6000 * r), 100), At(editor, "c"));
        Assert.Equal((Math.Floor(9000 * r), 100), At(editor, "d"));     // 나간 카드만이 아니라 전체가 같은 비율이다
        Assert.Equal((Math.Floor(3000 * r), 6000), At(editor, "e"));
        Assert.True(At(editor, "d").X + BoardSize.CardWidth <= 6000);
        Assert.Equal(new BoardSize(6000, 8000), editor.Project.Board);
    }

    [Fact]
    public void 줄여서_새로_겹친_카드는_비키고_비키다_넘치면_판을_넓힌다()
    {
        // 한 줄에 카드 열둘을 오른쪽 멀리(9000부터) 300 간격으로 — 가로 4000으로 줄이면 비율이 0.3쯤이라 간격(90쯤)이 카드 폭(210)보다
        // 좁아져 겹친다. 비켜 서면 열둘이 4000에 안 든다.
        var editor = new ProjectEditor(new StoryProject { Title = "촘촘" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        string[] ids = Enumerable.Range(1, 12).Select(index => $"e{index:00}").ToArray();

        for (int index = 0; index < ids.Length; index++)
        {
            editor.AddEpisode("ch01", ids[index], title: ids[index], 0, 0, sceneId: ids[index]);
            editor.MoveNode(EpisodeNaming.CardFor(editor.Project, "ch01", ids[index])!.Id, 9000 + (index * 300), 100);
        }

        editor.SetBoardSize(4000, 8000);

        for (int first = 0; first < ids.Length; first++)
        {
            for (int second = first + 1; second < ids.Length; second++)
            {
                Assert.False(Overlap(editor, ids[first], ids[second]), $"{ids[first]} · {ids[second]}");
            }
        }

        // 열둘이 비켜 서면 4000에 안 든다 — 판이 카드 끝까지 넓어진다(카드가 판 밖에 남지 않는다).
        double right = ids.Max(id => At(editor, id).X + BoardSize.CardWidth);
        Assert.True(editor.Project.Board.Width >= right);
        Assert.True(editor.Project.Board.Width > 4000);

        // 차례는 그대로다.
        Assert.Equal(ids, ids.OrderBy(id => At(editor, id).X).ToArray());
    }

    [Fact]
    public void 원래_겹쳐_있던_카드는_비키지_않는다()
    {
        ProjectEditor editor = Board();
        editor.MoveNode(Card(editor, "c").Id, 9050, 120);   // d(9000, 100)와 원래 겹친다
        Assert.True(Overlap(editor, "c", "d"));

        editor.SetBoardSize(6000, 8000);

        Assert.True(Overlap(editor, "c", "d"));   // 사람이 겹쳐 둔 것은 사람의 것이다
    }

    [Fact]
    public void 선_꺾임도_같은_비율로_줄인다()
    {
        ProjectEditor editor = Board();
        editor.AddEdge("ch01", "a", "d", optionLabel: "멀리");
        editor.SetEdgeShape("ch01", "a", "d", "멀리", new EdgeShape(EdgeShapeKind.Curve, 400, -200));
        Card(editor, "b").EdgeShapes["default"] = new EdgeShape(EdgeShapeKind.Elbow, 300, 60);

        BoardResizePlan plan = BoardResize.Plan(editor.Project, 6000, 4000);
        editor.SetBoardSize(6000, 4000);

        EdgeShape rail = editor.FindChapter("ch01")!.Edges.Single(edge => edge.ToEpisodeId == "d").Shape!;
        Assert.Equal((EdgeShapeKind.Curve, 400 * plan.RatioX, -200 * plan.RatioY), (rail.Kind, rail.BendX, rail.BendY));

        EdgeShape exit = Card(editor, "b").EdgeShapes["default"];
        Assert.Equal((300 * plan.RatioX, 60 * plan.RatioY), (exit.BendX, exit.BendY));
    }

    [Fact]
    public void 크게_줄이고_되돌리기_한_번이면_크기_카드_자리_선_꺾임이_정확히_그대로다()
    {
        ProjectEditor editor = Board();
        editor.AddEdge("ch01", "a", "d", optionLabel: "멀리");
        editor.SetEdgeShape("ch01", "a", "d", "멀리", new EdgeShape(EdgeShapeKind.Curve, 400, -200));
        editor.SetBoardSize(30000, 20000);
        string before = ProjectSnapshotCodec.Encode(editor.Project);

        editor.SetBoardSize(BoardSize.Minimum, BoardSize.Minimum);   // 실수로 크게 줄였다
        Assert.NotEqual(before, ProjectSnapshotCodec.Encode(editor.Project));

        editor.Undo();
        Assert.Equal(before, ProjectSnapshotCodec.Encode(editor.Project));
        Assert.Equal(new BoardSize(30000, 20000), editor.Project.Board);

        // 다시 실행도 한 번이다.
        editor.Redo();
        Assert.Equal(BoardSize.Minimum, editor.Project.Board.Width);
    }
}
