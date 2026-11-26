using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 챕터 · 장면 이름표는 판 위의 화면 겹에 선다</b>(design/board-zoom.md 4절 · 2026-11-26 소유자 — 「챕터와 장면 이름표가 축소되면서 겹치는데 …
/// 챕터는 경계 줄 대신, 경계 바깥 아웃라인 위쪽에 얹고」). 챕터는 테두리 바깥 위. 장면은 <b>늘 왼위에 이름 그대로</b> — 줄이거나 작은 표로 바꾸거나 남을
/// 비켜 가지 않고, 챕터 이름표와만 겹치지 않는다(같은 날 소유자 — 「그냥 항상 장면 이름이 고정된채로 표시되고 있게 … 예외적으로 챕터이름과만 겹치지
/// 않도록」). 챕터를 끌어서는 그 이름표가 판 맨 위 밖으로 나가게 할 수 없다(「애초에 그렇게 화면 바깥으로는 안 끌리게」).
/// </summary>
public sealed class FrameLabelLayerTests : IDisposable
{
    private static readonly double[] Zooms = [2.0, 1.5, 1.0, 0.75, 0.5, 0.4, 0.3, 0.2, 0.15, 0.1];

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-frame-label-layer", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 챕터_이름표는_테두리_바깥_위에_얹힌다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, _) = Show();

        foreach (double zoom in new[] { 1.0, 0.2 })
        {
            graph.ZoomTo(zoom);
            Settle(graph);

            (_, Rect screen, GraphEditorView.FrameLabelPlace place) = Place(graph, GraphEditorView.ChapterLabelTag + "ch01");
            Assert.Equal(GraphEditorView.FrameLabelPlace.Above, place);
            Assert.Equal(ScreenOf(graph, graph.ChapterAreaOf("ch01")!.Value).Top, screen.Bottom, 0);   // 아래 끝이 위 테두리 선에 닿는다
        }
    });

    [Fact]
    public void 배율을_훑어도_장면_이름표는_늘_제자리에_이름_그대로_서고_챕터_이름표와만_비킨다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「장면 이름이 사실상 점으로 줄어들게 되는데 … 그냥 과감하게 표시해주셔도 … 그냥 항상 장면 이름이 고정된채로 표시되고 있게
        // 하는 게 더 낫겠습니다. 예외적으로 챕터이름과만 겹치지 않도록」. ⛔ 그 전에는 위쪽 장면의 카드 · 경계에 닿으면 글자가 줄다가 점(작은 표)이 됐다.
        (GraphEditorView graph, _) = Show();

        foreach (double zoom in Zooms)
        {
            graph.ZoomTo(zoom);
            Settle(graph);

            Rect chapter = ScreenOf(graph, graph.ChapterAreaOf("ch01")!.Value);
            Rect chapterLabel = Place(graph, GraphEditorView.ChapterLabelTag + "ch01").Screen;

            foreach (string sceneId in new[] { "opening", "hall" })
            {
                (string key, Rect screen, GraphEditorView.FrameLabelPlace place) = Place(graph, GraphEditorView.SceneLabelTag + sceneId);
                Border label = Layer(graph).Children.OfType<Border>().Single(border => border.Tag as string == key);

                Assert.Equal(GraphEditorView.FrameLabelPlace.Corner, place);
                Assert.True(label.IsVisible, $"{zoom:P0}: {key}가 숨었다");
                Assert.True(double.IsPositiveInfinity(label.MaxWidth), $"{zoom:P0}: {key}를 말줄임했다");   // 이름 그대로
                double shown = NameOf(label).FontSize * (label.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1);
                Assert.True(shown >= GraphEditorView.SceneLabelMinPx - 0.01, $"{zoom:P0}: {key} 글자 {shown:0.0}px");

                // 챕터 이름표와는 겹치지 않고, 챕터 테두리 위로 나가지 않는다.
                Assert.False(screen.Intersects(chapterLabel), $"{zoom:P0}: {key} ↔ 챕터 이름표");
                Assert.True(screen.Y >= chapter.Y - 0.5, $"{zoom:P0}: {key}가 챕터 테두리 위로 나갔다");

                // 늘 그 장면의 왼위 — 아래 끝이 장면 위 여백의 아래 끝(장면 위 선 + 22)에 붙는다. 챕터 테두리에 막힌 때만 그 아래로 내려선다.
                Rect area = graph.SceneAreaOf(sceneId)!.Value;
                Rect scene = ScreenOf(graph, area);
                double anchor = ScreenOf(graph, new Rect(area.X, area.Y + 22, 1, 1)).Y;
                Assert.True(screen.X >= scene.X - 0.5, $"{zoom:P0}: {key}가 장면의 왼쪽 밖이다");
                Assert.True(Math.Abs(screen.Bottom - anchor) < 1 || Math.Abs(screen.Y - (chapter.Y + 2)) < 1,
                    $"{zoom:P0}: {key}가 제자리({anchor:0.0})를 떠났다 — {screen}");
            }

            // 아래 장면(hall)은 챕터 테두리에 닿지 않는다 — 어느 배율에서나 제자리다(위 장면의 카드 · 선택지 칸에 닿아도 비키지 않는다).
            Rect hallArea = graph.SceneAreaOf("hall")!.Value;
            double hallAnchor = ScreenOf(graph, new Rect(hallArea.X, hallArea.Y + 22, 1, 1)).Y;
            double hallBottom = Place(graph, GraphEditorView.SceneLabelTag + "hall").Screen.Bottom;
            Assert.True(Math.Abs(hallAnchor - hallBottom) < 0.5, $"{zoom:P0}: hall {hallBottom:0.0} ≠ {hallAnchor:0.0}");
        }
    });

    [Fact]
    public void 챕터가_판_맨_위에_붙어_이름표가_안에_서도_장면_이름표는_그_아래로_비킨다() => HeadlessUi.Run(() =>
    {
        // 손으로 고친 파일 · 줄인 판처럼 챕터 이름표가 테두리 안에 서는 때 — 장면 이름표가 챕터 이름표와 겹치지 않는 유일한 예외다.
        (GraphEditorView graph, _) = Show(top: 10);

        foreach (double zoom in new[] { 1.0, 0.5, 0.2, 0.1 })
        {
            graph.ZoomTo(zoom);
            Settle(graph);

            (_, Rect chapterLabel, GraphEditorView.FrameLabelPlace chapterPlace) = Place(graph, GraphEditorView.ChapterLabelTag + "ch01");
            Assert.Equal(GraphEditorView.FrameLabelPlace.Inside, chapterPlace);

            (string key, Rect opening, GraphEditorView.FrameLabelPlace place) = Place(graph, GraphEditorView.SceneLabelTag + "opening");
            Assert.Equal(GraphEditorView.FrameLabelPlace.Corner, place);
            Assert.False(opening.Intersects(chapterLabel), $"{zoom:P0}: {key} ↔ 챕터 이름표 {chapterLabel}");
        }
    });

    [Fact]
    public void 챕터를_위로_끌어도_그_이름표는_판_맨_위_밖으로_나가지_않고_무리의_모양은_그대로다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「챕터이름이 장면 안으로 침범한다는 것은, 챕터를 화면 바깥으로 끌었다는 것인데, 애초에 그렇게 화면 바깥으로는 안 끌리게」.
        (GraphEditorView graph, AuthoringSession session) = Show();

        // 줄인 판에서 먼저 — 이름표가 화면 최소 크기라 판 단위로 높아 끝이 더 아래다. 그다음 100%에서는 더 위까지 간다.
        foreach (double zoom in new[] { 0.3, 1.0 })
        {
            graph.ZoomTo(zoom);
            Settle(graph);
            Dictionary<string, (double X, double Y)> before = Positions(session);

            DragLabel(graph, GraphEditorView.ChapterLabelTag + "ch01", new Point(-40, -5000));

            Dictionary<string, (double X, double Y)> after = Positions(session);
            (_, Rect chapterLabel, GraphEditorView.FrameLabelPlace place) = Place(graph, GraphEditorView.ChapterLabelTag + "ch01");
            Rect board = ScreenOf(graph, new Rect(0, 0, 1, 1));

            Assert.Equal(GraphEditorView.FrameLabelPlace.Above, place);   // 이름표는 늘 테두리 바깥 위에 선다
            Assert.True(chapterLabel.Y >= board.Y - 1, $"{zoom:P0}: 챕터 이름표가 판 맨 위 밖이다 — {chapterLabel.Y} < {board.Y}");
            Assert.True(after["root"].Y < before["root"].Y, $"{zoom:P0}: 위로 끌리지 않았다");

            // 무리째 멈춘다 — 카드마다 따로 막혀 찌그러지지 않는다.
            Assert.Equal(before["a"].X - before["root"].X, after["a"].X - after["root"].X, 3);
            Assert.Equal(before["b"].Y - before["root"].Y, after["b"].Y - after["root"].Y, 3);
        }
    });

    [Fact]
    public void 이미_판_맨_위에_붙은_챕터는_끌어도_아래로_끌려_내려가지_않는다() => HeadlessUi.Run(() =>
    {
        // 끝을 넘어 있는 카드(옛 파일 · 줄인 판에서 끝이 바뀐 것)는 끌기를 시작했다고 끝으로 튀지 않는다 — 더 바깥으로만 못 간다.
        (GraphEditorView graph, AuthoringSession session) = Show(top: 10);
        graph.ZoomTo(1);
        Settle(graph);

        DragLabel(graph, GraphEditorView.ChapterLabelTag + "ch01", new Point(80, -30));

        Dictionary<string, (double X, double Y)> after = Positions(session);
        Assert.Equal(10, after["root"].Y, 3);           // 위로는 못 가고 내려앉지도 않는다
        Assert.Equal(2400 + 80, after["root"].X, 3);    // 옆으로는 간다
    });

    [Fact]
    public void 이름표_위에서_휠을_굴려도_판의_배율이_바뀐다() => HeadlessUi.Run(() =>
    {
        // 이름표의 겹은 판(스크롤) 밖이다 — 휠이 겹에서 멈추면 이름표 위에서는 확대 · 축소가 안 된다.
        (GraphEditorView graph, _) = Show();
        graph.ZoomTo(1);
        Settle(graph);

        Border label = Layer(graph).Children.OfType<Border>().Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + "opening");
        TextBlock zoomText = graph.FindControl<TextBlock>("ZoomText")!;
        string before = zoomText.Text ?? string.Empty;
        var top = (Visual)TopLevel.GetTopLevel(label)!;
        Point at = label.TranslatePoint(new Point(4, 4), top)!.Value;

        label.RaiseEvent(new PointerWheelEventArgs(label, new Pointer(0, PointerType.Mouse, true), top, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None, new Vector(0, -1))
        {
            RoutedEvent = InputElement.PointerWheelChangedEvent
        });
        Settle(graph);

        Assert.NotEqual(before, zoomText.Text);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 하이어아키 픽스처를 [대본] 탭으로 열고 ch01의 카드를 <paramref name="top"/>에 둔다(기본은 판의 맨 위에서 떨어뜨린다) — opening(root · a) 아래에
    /// hall(b). 편집기로 옮기므로 끌기의 끝(판 맨 위)에 막히지 않는다 — 옛 파일처럼 맨 위에 붙일 수 있다.
    /// </summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show(double top = 1200)
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        session.Editor.MoveNodes(new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal)
        {
            [CardId(session, "root")] = (2400, top),
            [CardId(session, "a")] = (2700, top),
            [CardId(session, "b")] = (2400, top + 360)
        });

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Settle(graph);
        return (graph, session);
    }

    private static void Settle(GraphEditorView graph)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
        }
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Canvas Layer(GraphEditorView graph) => graph.FindControl<Canvas>("FrameLabelLayer")!;

    /// <summary>장면 이름표의 이름 — 점 · 이름 줄의 글.</summary>
    private static TextBlock NameOf(Border label) => ((Panel)label.Child!).Children.OfType<TextBlock>().Single();

    private static Dictionary<string, (double X, double Y)> Positions(AuthoringSession session) =>
        new[] { "root", "a", "b" }.ToDictionary(
            episode => episode,
            episode => (EpisodeNaming.CardFor(session.Project, "ch01", episode)!.Layout.X, EpisodeNaming.CardFor(session.Project, "ch01", episode)!.Layout.Y));

    /// <summary>이름표를 눌러 화면에서 <paramref name="by"/>만큼 끌고 놓는다 — 이름표는 그 챕터 · 장면의 카드를 모두 골라 함께 끈다.</summary>
    private static void DragLabel(GraphEditorView graph, string key, Point by)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Border label = Layer(graph).Children.OfType<Border>().Single(border => border.Tag as string == key);
        var top = (Visual)TopLevel.GetTopLevel(canvas)!;
        var pointer = new Pointer(0, PointerType.Mouse, true);
        Point at = label.TranslatePoint(new Point(4, 4), top)!.Value;

        label.RaiseEvent(new PointerPressedEventArgs(label, pointer, top, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        for (int step = 1; step <= 4; step++)
        {
            canvas.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, canvas, pointer, top, at + (by * (step / 4d)), 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
            Dispatcher.UIThread.RunJobs();
        }

        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, top, at + by, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        Settle(graph);
    }

    private static (string Key, Rect Screen, GraphEditorView.FrameLabelPlace Place) Place(GraphEditorView graph, string key) =>
        graph.FrameLabelPlaces.Single(item => item.Key == key);

    /// <summary>판의 네모가 화면 겹에서 선 자리 — 코드의 셈과 따로, 실제 배치(배율 변환 · 스크롤)를 거쳐 잰다.</summary>
    private static Rect ScreenOf(GraphEditorView graph, Rect board)
    {
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        return new Rect(canvas.TranslatePoint(board.TopLeft, Layer(graph))!.Value, canvas.TranslatePoint(board.BottomRight, Layer(graph))!.Value);
    }

    private static List<Rect> CardRects(GraphEditorView graph, AuthoringSession session) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Where(border => border.Tag is string tag && session.Project.FindNode(tag) is not null)
            .Select(card => ScreenOf(graph, new Rect(Canvas.GetLeft(card), Canvas.GetTop(card), card.Bounds.Width, card.Bounds.Height)))
            .ToList();
}
