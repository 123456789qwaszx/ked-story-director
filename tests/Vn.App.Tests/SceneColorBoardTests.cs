using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 장면은 장면마다 색이 다르다</b>(design/scene-colors.md · 2026-11-26 소유자 — 「권고대로」). 장면 면은 그 장면 색의 <b>비치는</b> 바탕이라 장면이
/// 겹친 자리는 한 겹 더 진하고(소유자 — 「같은 색의 장면끼리 겹칠 때, 이게 아예 구분이 안 가는데」), 장면 이름표 앞에는 같은 색의 점이 선다. 접은 장면 상자도
/// 그 장면의 색이다. 색은 고른 칸이 먼저, 그 밖은 자동(<see cref="SceneColors"/>). 미니맵에도 장면 자리가 그 장면 색으로 깔린다(2026-11-27).
/// </summary>
public sealed class SceneColorBoardTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-scene-color", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 장면마다_그_장면_색의_비치는_바탕이고_이름표_앞에_같은_색의_점이_선다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, _) = Show();
        IReadOnlyDictionary<string, int> colors = Colors(session);

        Assert.NotEqual(colors["opening"], colors["hall"]);   // 이야기 차례로 이웃한 장면은 다르다

        foreach (string sceneId in new[] { "opening", "hall" })
        {
            Color fill = ((ISolidColorBrush)Frame(graph, sceneId).Background!).Color;
            Assert.Equal(Skin.SceneTintOf(colors[sceneId]), fill);
            Assert.InRange(fill.A, 1, 254);   // 비친다 — 겹친 자리는 한 겹 더 진하다

            Avalonia.Controls.Shapes.Ellipse dot = Dot(graph, sceneId);
            Assert.Equal(Skin.SceneMarkOf(colors[sceneId]), ((ISolidColorBrush)dot.Fill!).Color);
        }
    });

    [Fact]
    public void 색을_고르면_판이_따라가고_되돌리면_돌아온다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, _) = Show();
        int auto = Colors(session)["hall"];
        int picked = (auto % SceneColors.Count) + 1;

        session.Editor.SetSceneColor("ch01", "hall", picked);   // 창의 셸이 판을 다시 짓는다(NodeMetadata) — 여기서는 손으로
        graph.Rebuild();
        Settle(graph);

        Assert.Equal(Skin.SceneTintOf(picked), ((ISolidColorBrush)Frame(graph, "hall").Background!).Color);
        Assert.Equal(Skin.SceneMarkOf(picked), ((ISolidColorBrush)Dot(graph, "hall").Fill!).Color);

        session.Editor.Undo();
        graph.Rebuild();
        Settle(graph);

        Assert.Equal(Skin.SceneTintOf(auto), ((ISolidColorBrush)Frame(graph, "hall").Background!).Color);
    });

    [Fact]
    public void 접은_장면_상자도_그_장면의_색이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        int color = Colors(session)["hall"];

        session.SetSceneCollapsed(board, "hall", collapsed: true);
        graph.Rebuild();
        Settle(graph);

        Border proxy = graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneProxyTag + board + "/hall");
        Color mark = Skin.SceneMarkOf(color);
        Color border = ((ISolidColorBrush)proxy.BorderBrush!).Color;
        Color background = ((ISolidColorBrush)proxy.Background!).Color;

        Assert.Equal((mark.R, mark.G, mark.B), (border.R, border.G, border.B));
        Assert.Equal(255, background.A);   // 상자는 비치지 않는다 — 아래 선이 보이지 않게
        Assert.NotEqual(Skin.ChapterFrameFill, background);
    });

    [Fact]
    public void 미니맵에_장면_자리가_그_장면_색으로_카드_밑에_깔린다() => HeadlessUi.Run(() =>
    {
        // design/scene-colors.md ⑤ — 미니맵에서도 장면 덩어리가 보인다. ⛔ 그 전에는 카드만 그렸다.
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        IReadOnlyDictionary<string, int> colors = Colors(session);
        Canvas map = graph.FindControl<Canvas>("MinimapCanvas")!;

        foreach (string sceneId in new[] { "opening", "hall" })
        {
            Avalonia.Controls.Shapes.Rectangle area = MinimapScene(map, board, sceneId);
            Assert.Equal(Skin.SceneTintOf(colors[sceneId]), ((ISolidColorBrush)area.Fill!).Color);   // 판과 같은 비치는 색
            Assert.True(area.Width > 2 && area.Height > 2, "장면 자리는 카드 여럿을 품는 크기다");
        }

        // 판의 자리대로 — opening(위)이 hall(아래)보다 위다.
        Assert.True(Canvas.GetTop(MinimapScene(map, board, "opening")) < Canvas.GetTop(MinimapScene(map, board, "hall")));

        // 카드 밑에 깔린다 — 장면 자리를 모두 먼저 그리고 카드(꼬리표 없는 사각형)가 그 뒤에 선다.
        List<Control> children = map.Children.ToList();
        int lastScene = children.FindLastIndex(child => (child.Tag as string)?.StartsWith(GraphEditorView.MinimapSceneTag, StringComparison.Ordinal) == true);
        int firstCard = children.FindIndex(child => child is Avalonia.Controls.Shapes.Rectangle && child.Tag is null);
        Assert.True(lastScene >= 0 && firstCard > lastScene, $"장면 {lastScene} · 카드 {firstCard}");
    });

    [Fact]
    public void 미니맵의_접은_장면_상자도_그_장면의_색이다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        int color = Colors(session)["hall"];

        session.SetSceneCollapsed(board, "hall", collapsed: true);
        graph.Rebuild();
        Settle(graph);

        // 접으면 장면 자리 대신 상자 하나 — 그 장면의 점 색 덩어리다(판의 상자 테두리 · 점과 한 색).
        Color fill = ((ISolidColorBrush)MinimapScene(graph.FindControl<Canvas>("MinimapCanvas")!, board, "hall").Fill!).Color;
        Color mark = Skin.SceneMarkOf(color);
        Assert.Equal((mark.R, mark.G, mark.B), (fill.R, fill.G, fill.B));
    });

    [Fact]
    public void RGB로_고른_색도_판의_면은_비치고_이름표_점은_그_색이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 「RGB 표에서 선택도」. 면은 고른 색을 비치게(짙으면 더 옅게), 점은 고른 색(옅으면 짙게)이다.
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        int blue = SceneColors.Custom(0x3366CC);

        session.Editor.SetSceneColor("ch01", "hall", blue);
        graph.Rebuild();
        Settle(graph);

        Color fill = ((ISolidColorBrush)Frame(graph, "hall").Background!).Color;
        Assert.Equal(Skin.SceneTintOf(blue), fill);
        Assert.InRange(fill.A, 1, Skin.SceneTint1.A);
        Assert.Equal((0x33, 0x66, 0xCC), ((int)fill.R, (int)fill.G, (int)fill.B));
        Assert.Equal(Color.Parse("#3366CC"), ((ISolidColorBrush)Dot(graph, "hall").Fill!).Color);

        // 미니맵 · 접은 상자도 같은 색이다.
        Assert.Equal(Skin.SceneTintOf(blue), ((ISolidColorBrush)MinimapScene(graph.FindControl<Canvas>("MinimapCanvas")!, board, "hall").Fill!).Color);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>미니맵에서 그 장면의 자리(펼친 장면의 면 · 접은 장면의 상자).</summary>
    private static Avalonia.Controls.Shapes.Rectangle MinimapScene(Canvas map, string board, string sceneId) =>
        map.Children.OfType<Avalonia.Controls.Shapes.Rectangle>()
            .Single(rect => rect.Tag as string == GraphEditorView.MinimapSceneTag + board + "/" + sceneId);

    private static IReadOnlyDictionary<string, int> Colors(AuthoringSession session)
    {
        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        return SceneColors.Of(ChapterSceneGrouping.Of(chapter.ToGraphModel("ch01")), chapter.SceneNotes);
    }

    /// <summary>그 장면의 테두리 면 — 눌리지 않는 빈 Border 가운데 장면의 판 자리에 선 것.</summary>
    private static Border Frame(GraphEditorView graph, string sceneId)
    {
        Rect area = graph.SceneAreaOf(sceneId)!.Value;
        return graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(border => border.Child is null && !border.IsHitTestVisible &&
                              Canvas.GetLeft(border) == area.X && Canvas.GetTop(border) == area.Y &&
                              border.Width == area.Width && border.Height == area.Height);
    }

    /// <summary>그 장면 이름표 앞의 점.</summary>
    private static Avalonia.Controls.Shapes.Ellipse Dot(GraphEditorView graph, string sceneId)
    {
        Border label = graph.FindControl<Canvas>("FrameLabelLayer")!.Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + sceneId);
        return ((Panel)label.Child!).Children.OfType<Avalonia.Controls.Shapes.Ellipse>().Single(ellipse => Equals(ellipse.Tag, GraphEditorView.SceneDotTag));
    }

    private static void Settle(GraphEditorView graph)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
        }
    }

    /// <summary>ch01 — opening(root · a) 아래에 hall(b). 판의 맨 위에서 떨어뜨린다.</summary>
    private (GraphEditorView Graph, AuthoringSession Session, string Board) Show()
    {
        var session = new AuthoringSession();
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "장면 색" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");
        string board = session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "root", "root", 400, 400, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "a", "a", 400, 800, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "b", "b", 400, 1300, sceneId: "hall");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "다음");

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();
        Settle(graph);

        return (graph, session, board);
    }
}
