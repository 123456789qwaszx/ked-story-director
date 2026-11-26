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
/// <b>[대본] 판의 챕터 · 장면 이름표는 판 위의 화면 겹에 서서, 줄여도 서로 · 카드와 겹치지 않는다</b>(design/board-zoom.md 4절 · 2026-11-26 소유자 —
/// 「챕터와 장면 이름표가 축소되면서 겹치는데 … 챕터는 경계 줄 대신, 경계 바깥 아웃라인 위쪽에 얹고, 장면의 경우 최소 축소로 축소하더라도, 장면 자체의
/// 영역을 벗어나지는 않도록」). 챕터는 테두리 바깥 위, 장면은 늘 왼위 — 첫 카드 위에서 위로 자라다가 자리가 모자라면 글자가 줄고, 그래도 모자라면 같은 자리의
/// 작은 표가 된다(같은 날 소유자 — 숨거나 가운데로 옮겨 가는 것은 어색하다). 아주 멀리서는 장면 한가운데에 장면 이름이 옅게 깔린다.
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
    public void 배율을_훑어도_장면_이름표는_제자리에_서고_숨지_않으며_겹치지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「장면 이름표가 숨는 타이밍이 있다는 것 자체가 어색하고 … 갑자기 중앙에 장면 이름이 생기니 저게 도대체 뭔지 헷갈린다」.
        // 장면 이름표는 늘 왼위다 — 자리가 모자라면 글자가 줄고(화면 10px까지), 그래도 모자라면 같은 자리의 작은 표다. 챕터 테두리 위로는 나가지 않는다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        Rect? cornerAt100 = null;

        foreach (double zoom in Zooms)
        {
            graph.ZoomTo(zoom);
            Settle(graph);

            var places = graph.FrameLabelPlaces.ToList();

            for (int i = 0; i < places.Count; i++)
            {
                for (int j = i + 1; j < places.Count; j++)
                {
                    Assert.False(places[i].Screen.Intersects(places[j].Screen), $"{zoom:P0}: {places[i].Key} ↔ {places[j].Key}");
                }
            }

            Rect chapter = ScreenOf(graph, graph.ChapterAreaOf("ch01")!.Value);
            List<Rect> cards = CardRects(graph, session);

            foreach (string sceneId in new[] { "opening", "hall" })
            {
                (string key, Rect screen, GraphEditorView.FrameLabelPlace place) = Place(graph, GraphEditorView.SceneLabelTag + sceneId);
                Assert.True(place is GraphEditorView.FrameLabelPlace.Corner or GraphEditorView.FrameLabelPlace.Chip, $"{zoom:P0}: {key} {place}");
                Assert.True(screen.Y >= chapter.Y - 0.5, $"{zoom:P0}: {key}가 챕터 테두리 위로 나갔다");

                Rect scene = ScreenOf(graph, graph.SceneAreaOf(sceneId)!.Value);
                Assert.True(screen.X >= scene.X - 0.5, $"{zoom:P0}: {key}가 장면의 왼쪽 밖이다");   // 늘 그 장면의 왼위

                if (place == GraphEditorView.FrameLabelPlace.Corner)
                {
                    Assert.DoesNotContain(cards, card => card.Intersects(screen));
                    Assert.True(screen.Width <= scene.Width + 0.5, $"{zoom:P0}: {key} 폭 {screen.Width} > 장면 {scene.Width}");
                    Border label = Layer(graph).Children.OfType<Border>().Single(border => border.Tag as string == key);
                    double shown = ((TextBlock)label.Child!).FontSize * (label.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1);
                    Assert.True(shown >= GraphEditorView.SceneLabelShrinkPx - 0.01, $"{zoom:P0}: {key} 글자 {shown:0.0}px");
                }
            }

            if (Math.Abs(zoom - 1) < 0.001)
            {
                cornerAt100 = Place(graph, GraphEditorView.SceneLabelTag + "opening").Screen;
            }
        }

        // 100%에서는 두 장면 모두 제 첫 카드 위의 이름표다(위 테두리 선에 걸터앉는다). 35%에서도 첫 장면의 이름표는 숨지 않는다(⛔ 그 전에는 사라졌다).
        Assert.NotNull(cornerAt100);
        graph.ZoomTo(1);
        Settle(graph);
        Assert.Equal(GraphEditorView.FrameLabelPlace.Corner, Place(graph, GraphEditorView.SceneLabelTag + "opening").Place);
        Assert.Equal(GraphEditorView.FrameLabelPlace.Corner, Place(graph, GraphEditorView.SceneLabelTag + "hall").Place);
        graph.ZoomTo(0.35);
        Settle(graph);
        Assert.Equal(GraphEditorView.FrameLabelPlace.Corner, Place(graph, GraphEditorView.SceneLabelTag + "opening").Place);
    });

    [Fact]
    public void 아주_멀리서는_장면_한가운데에_장면_이름이_옅게_깔린다() => HeadlessUi.Run(() =>
    {
        // 장면의 카드가 모두 덩어리일 때만 — 이름표와 다른 모양(바탕 · 테두리 없는 옅은 글)으로 카드 뒤(판 위)에. 구석의 이름표 · 작은 표는 그대로다.
        (GraphEditorView graph, _) = Show();
        TextBlock Mark() => graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<TextBlock>()
            .Single(text => text.Tag as string == GraphEditorView.SceneWatermarkTag + "opening");

        Assert.False(Mark().IsVisible);
        Assert.Equal(string.Empty, Mark().Text);   // 글 그대로의 판에는 글자가 남지 않는다

        graph.ZoomTo(0.1);
        Settle(graph);
        Assert.True(Mark().IsVisible);
        Assert.Equal("opening", Mark().Text);
        Assert.False(Mark().IsHitTestVisible);
        Assert.NotNull(graph.FrameLabelPlaces.SingleOrDefault(item => item.Key == GraphEditorView.SceneLabelTag + "opening").Key);   // 구석의 표도 그대로

        graph.ZoomTo(1);
        Settle(graph);
        Assert.False(Mark().IsVisible);
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
    /// 하이어아키 픽스처를 [대본] 탭으로 열고 ch01의 카드를 판의 맨 위에서 떨어뜨린다 — opening(root · a) 아래에 hall(b). 맨 위에 붙은 장면은 이름표가
    /// 판 밖으로 못 나가 제 카드를 덮을 수 있다(그 경우는 여기서 다루지 않는다).
    /// </summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        session.Editor.MoveNodes(new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal)
        {
            [CardId(session, "root")] = (2400, 1200),
            [CardId(session, "a")] = (2700, 1200),
            [CardId(session, "b")] = (2400, 1560)
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
