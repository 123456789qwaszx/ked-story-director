using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>두 판이 얼마나 일하는가</b> (2026-11-18 소유자 — 「수십 수백개의 에피소드들을 한번에 잡고 옮길 때 렉은 없을지. 또 텍스트를 쓰고 그거
/// 인스펙터 창에 반영되거나 하는 지연을 조금 더 줄이는」 · <c>docs/work-orders/finishing-orders.md</c> FX3). 시간은 기계마다 달라 고정할 수
/// 없으므로 <see cref="ChapterGraphWorkAmountTests"/>처럼 <b>일의 횟수</b>를 건다.
///
/// 못 박는 것: ① 카드 글 칸의 글을 넣어도 [대본] 판의 철도를 다시 긋지 않는다(⛔ [구성]이 다시 읽을 때마다 한 번 더 그었다) ② 가려진
/// [구성] 판은 바뀔 때마다 그리지 않고 보일 때 한 번 그린다 ③ [대본] 무리를 끄는 동안 · 놓은 뒤 철도를 통째로 다시 긋지 않고(⛔ 움직일
/// 때마다 통째로 그었다) 무리 안팎을 잇는 길만 다시 긋는다 — 그래도 놓은 뒤의 모습은 통째로 그은 것과 같다 ④ 글을 되돌리고 다시 해도 판을
/// 다시 짓지 않고, 고른 카드가 아니어도 그 카드의 글 칸이 돌아온다(board-speed PF1 — ⛔ 되돌리기가 늘 구조로 알려 판을 통째로 다시 지었다)
/// ⑤ 인스펙터가 그 카드를 보이는 채 글이 짧은 사이에 두 번 바뀌어도 옛 글이 돌아오지 않는다(⛔ 줄 표의 낡은 칸이 옛 글을 도로 넣어 두 값 사이를
/// 끝없이 오갔다 — 창이 멈췄다) ⑥ 카드의 「빈 선택지 숨기기」를 켜고 꺼도 판을 다시 짓지 않고 그 카드의 철도만 다시 긋는다 — 모습은 통째로 그은 것과
/// 같다(board-speed PF2 — ⛔ 「펼친 판이 바뀌었다」로 알려 판을 통째로 다시 지었다) ⑦ [구성] 판을 끄는 동안 판을 다시 짓지 않고 한쪽만 움직인
/// 선택지 선만 다시 긋는다 — 마지막 걸음의 모습은 놓은 뒤 통째로 그린 모습과 같다(board-speed PF3 — ⛔ 움직일 때마다 통째로 다시 지었다).
/// </summary>
public sealed class BoardWorkAmountTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-board-work", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 글을_넣어도_대본_판의_철도는_다시_긋지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string cardId = EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id;

        TextBox Box()
        {
            TextBox box = graph.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
                .Single(item => item.Tag as string == GraphEditorView.WritingTag + cardId);
            box.Focus();
            Dispatcher.UIThread.RunJobs();
            return box;
        }

        // 첫 줄은 줄이 느는 것이라 판이 다시 선다 — 쓰는 중의 일은 있는 줄을 고치는 것이다.
        TextBox box = Box();
        box.Text = "오늘은 바다에 가자.";
        graph.CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();

        box = Box();
        int rails = graph.RailDrawCount;
        long revision = session.Editor.Revision;
        box.Text = "오늘은 산에 가자.";
        graph.CommitLiveWriting();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(revision + 1, session.Editor.Revision);   // 글은 들어갔다
        Assert.Equal(rails, graph.RailDrawCount);

        window.Close();
    });

    [Fact]
    public void 가려진_구성_판은_보일_때_한_번_그린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        TabControl tabs = window.FindControl<TabControl>("MainTabs")!;
        session.SetShowIds(false);   // [구성] 카드가 제목을 보인다 — 앱 설정이라 끝에 되돌린다(다른 시험으로 새지 않게)
        Dispatcher.UIThread.RunJobs();

        try
        {
            int draws = view.CanvasDrawCount;
            session.Editor.UpdateEpisode("ch01", "a", title: "첫째");
            Dispatcher.UIThread.RunJobs();
            session.Editor.UpdateEpisode("ch01", "a", title: "둘째");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(draws, view.CanvasDrawCount);   // 가려진 동안은 그리지 않는다

            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(draws + 1, view.CanvasDrawCount);   // 보이면 한 번 — 마지막 모습으로
            Assert.Contains(view.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBlock>(),
                block => block.Text == "둘째");

            // 바뀐 것이 없으면 탭을 오가도 다시 그리지 않는다.
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            tabs.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(draws + 1, view.CanvasDrawCount);
        }
        finally
        {
            session.SetShowIds(true);
        }

        window.Close();
    });
    [Fact]
    public void 무리를_끄는_동안_철도는_따라가기만_하고_놓은_뒤의_모습은_통째로_그은_것과_같다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "교실로 간다");   // opening 안
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "복도로 나간다");    // opening → hall (무리 안팎)
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        Border label = canvas.Children.OfType<Border>().Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + "opening");
        var top = (Visual)TopLevel.GetTopLevel(canvas)!;
        var pointer = new Pointer(0, PointerType.Mouse, true);
        Point at = label.TranslatePoint(new Point(4, 4), top)!.Value;

        int rails = graph.RailDrawCount;
        int redrawn = graph.RailFollowRedrawCount;
        label.RaiseEvent(new PointerPressedEventArgs(label, pointer, top, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

        for (int step = 1; step <= 4; step++)
        {
            canvas.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, canvas, pointer, top, at + new Point(step * 30, step * 15), 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
            Dispatcher.UIThread.RunJobs();
        }

        long revision = session.Editor.Revision;
        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, top, at + new Point(120, 60), 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(revision + 1, session.Editor.Revision);   // 함께 한 칸
        Assert.Equal(rails, graph.RailDrawCount);              // 통째로는 한 번도 긋지 않았다
        Assert.InRange(graph.RailFollowRedrawCount - redrawn, 1, 4 * 2 + 2);   // 무리 안팎을 잇는 a의 가지만 — 움직일 때마다 하나

        // 따라간 모습 = 같은 카드로 통째로 그은 모습(끌기 밖의 자리 갱신은 통째로 긋는다) — 한 픽셀 안에서. 새로 그은 그림은 새 자리를
        // 픽셀에 맞추고(반올림 한 번), 옮긴 그림은 옛 자리를 픽셀에 맞춘 데서 끈 만큼(배율 때문에 소수다) 그대로 간다(TranslateTransform) —
        // 반올림이 두 번 다른 자리에서 일어나 차이는 1 픽셀 아래까지 난다(옛 자리 x.4 · 끈 만큼 0.2 → 0.2 대 1). ⛔ 그 전에는 0.6으로 보아
        // 글꼴이 다른 맥에서 0.6이 났다(writer-app WA9).
        List<(string Kind, double[] At)> followed = Shapes(graph, canvas);
        graph.RefreshPositions();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(rails + 1, graph.RailDrawCount);
        List<(string Kind, double[] At)> drawn = Shapes(graph, canvas);

        Assert.Equal(drawn.Count, followed.Count);

        for (int index = 0; index < drawn.Count; index++)
        {
            Assert.Equal(drawn[index].Kind, followed[index].Kind);
            Assert.Equal(drawn[index].At.Length, followed[index].At.Length);
            Assert.True(drawn[index].At.Zip(followed[index].At).All(pair => Math.Abs(pair.First - pair.Second) < 1),
                $"{drawn[index].Kind} 통째로 [{string.Join(", ", drawn[index].At.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))}] · " +
                $"따라감 [{string.Join(", ", followed[index].At.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))}]");
        }

        window.Close();
    });

    [Fact]
    public void 글을_되돌리고_다시_해도_판을_다시_짓지_않고_그_카드의_글_칸이_따라온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string cardA = EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id;
        session.Select(cardA);   // 사람이 쓰는 그대로 — 고른 카드의 글이 바뀌면 그 칸이 따라온다
        Dispatcher.UIThread.RunJobs();
        session.Editor.ApplyScenarioText(cardA, "첫 줄.");
        session.Editor.ApplyScenarioText(cardA, "고친 첫 줄.");
        Dispatcher.UIThread.RunJobs();

        // 다른 카드를 고른 채 되돌린다 — 되돌린 글은 고른 카드의 것이 아니다.
        session.Select(EpisodeNaming.CardFor(session.Project, "ch01", "b")!.Id);
        Dispatcher.UIThread.RunJobs();
        TextBox WritingOf(string cardId) => graph.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(item => item.Tag as string == GraphEditorView.WritingTag + cardId);
        TextBox box = WritingOf(cardA);
        Assert.Contains("고친 첫 줄.", box.Text, StringComparison.Ordinal);

        int rails = graph.RailDrawCount;
        Click(window.FindControl<MenuItem>("UndoMenuItem")!);
        Assert.Same(box, WritingOf(cardA));   // 판을 다시 짓지 않았다 — 같은 칸이다
        Assert.DoesNotContain("고친", box.Text, StringComparison.Ordinal);
        Assert.Contains("첫 줄.", box.Text, StringComparison.Ordinal);

        Click(window.FindControl<MenuItem>("RedoMenuItem")!);
        Assert.Contains("고친 첫 줄.", box.Text, StringComparison.Ordinal);
        Assert.Equal(rails, graph.RailDrawCount);

        window.Close();
    });

    [Fact]
    public void 인스펙터가_보이는_카드의_글이_잇달아_바뀌어도_옛_글로_돌아가지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        string cardA = EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id;
        session.Select(cardA);   // 인스펙터의 줄 표가 이 카드를 보인다
        Dispatcher.UIThread.RunJobs();
        long before = session.Editor.Revision;

        // 사이에 화면이 일할 틈 없이 두 번 — 되돌리고 곧 다시 실행할 때와 같다.
        session.Editor.ApplyScenarioText(cardA, "첫 줄.");
        session.Editor.ApplyScenarioText(cardA, "고친 첫 줄.");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + 2, session.Editor.Revision);   // 줄 표가 아무것도 도로 넣지 않았다
        Assert.Equal("고친 첫 줄.", Vn.Authoring.Rendering.WorkingDialoguePreview.ScenarioWriting(session.Project, cardA).Text);

        window.Close();
    });

    [Fact]
    public void 빈_선택지를_숨기고_세워도_판을_다시_짓지_않고_그_카드의_철도만_다시_긋는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "교실로 간다");
        Dispatcher.UIThread.RunJobs();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        string root = EpisodeNaming.CardFor(session.Project, "ch01", "root")!.Id;

        int rails = graph.RailDrawCount;
        int redrawn = graph.RailFollowRedrawCount;
        int visuals = graph.RailVisuals.Count;
        List<(string Kind, double[] At)> shown = Shapes(graph, canvas);

        session.SetHideEmptyChoices(root, hide: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(rails, graph.RailDrawCount);                      // 통째로 긋지 않았다
        Assert.Equal(redrawn + 1, graph.RailFollowRedrawCount);        // root의 묶음 하나만
        Assert.True(graph.RailVisuals.Count < visuals);                // 빈 칸이 걷혔다
        AssertSameAsFullRedraw(graph, canvas, ref rails);

        session.SetHideEmptyChoices(root, hide: false);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Equal(visuals, graph.RailVisuals.Count);
        AssertSameShapes(shown, Shapes(graph, canvas));

        session.SetHideEmptyChoices(root, hide: false);   // 이 PC의 설정이다 — 처음대로 두고 끝낸다
        window.Close();
    });

    /// <summary>지금 모습이 같은 카드로 통째로 그은 모습과 같은가(끌기 밖의 자리 갱신은 통째로 긋는다).</summary>
    private static void AssertSameAsFullRedraw(GraphEditorView graph, Canvas canvas, ref int rails)
    {
        List<(string Kind, double[] At)> followed = Shapes(graph, canvas);
        graph.RefreshPositions();
        Dispatcher.UIThread.RunJobs();
        rails = graph.RailDrawCount;
        AssertSameShapes(Shapes(graph, canvas), followed);
    }

    private static void AssertSameShapes(List<(string Kind, double[] At)> drawn, List<(string Kind, double[] At)> other)
    {
        Assert.Equal(drawn.Count, other.Count);

        for (int index = 0; index < drawn.Count; index++)
        {
            Assert.Equal(drawn[index].Kind, other[index].Kind);
            Assert.True(drawn[index].At.Zip(other[index].At).All(pair => Math.Abs(pair.First - pair.Second) <= 0.6),
                $"{drawn[index].Kind} [{string.Join(", ", drawn[index].At)}] · [{string.Join(", ", other[index].At)}]");
        }
    }

    [Fact]
    public void 구성_판을_끄는_동안_다시_짓지_않고_마지막_모습은_놓은_뒤와_같다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "교실로 간다");
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "복도로 나간다");
        Dispatcher.UIThread.RunJobs();
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Canvas canvas = view.FindControl<Canvas>("GraphCanvas")!;
        var top = (Visual)TopLevel.GetTopLevel(canvas)!;
        var pointer = new Pointer(0, PointerType.Mouse, true);
        Border card = canvas.Children.OfType<Border>().Single(border => border.Tag as string == "a");
        Point grab = canvas.TranslatePoint(new Point(Canvas.GetLeft(card) + 30, Canvas.GetTop(card) + 12), top)!.Value;

        int builds = view.CanvasBuildCount;
        int redrawn = view.PartRedrawCount;
        card.RaiseEvent(new PointerPressedEventArgs(card, pointer, top, grab, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        Point to = grab;
        for (int step = 1; step <= 4; step++)
        {
            to = grab + new Point(step * 20, step * 40);   // 아래로 — 판의 왼위는 그대로다
            canvas.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, canvas, pointer, top, to, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(builds, view.CanvasBuildCount);                // 끄는 동안 판을 다시 짓지 않았다
        Assert.Equal(redrawn + (4 * 2), view.PartRedrawCount);      // a에 닿은 선 둘(root→a · a→b)만 걸음마다
        List<(string Kind, double[] At)> dragging = CanvasShapes(canvas);

        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, top, to, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.CanvasBuildCount > builds);                // 놓으면 한 번 적고 다시 그린다
        AssertSameShapes(CanvasShapes(canvas), dragging);

        window.Close();
    });

    /// <summary>[구성] 판의 그림마다 실제 모양 — 종류와 자리(옮김 포함) · 기하. 보이는 것만, 차례 없이 견주게 정렬한다.</summary>
    private static List<(string Kind, double[] At)> CanvasShapes(Canvas canvas) =>
        canvas.Children
            .Where(visual => visual.IsVisible)
            .Select(visual =>
            {
                Point origin = visual.TranslatePoint(default, canvas)!.Value;
                var by = new Vector(origin.X, origin.Y);

                IEnumerable<Point> points = visual switch
                {
                    Line line => [line.StartPoint + by, line.EndPoint + by],
                    Polygon polygon => polygon.Points.Select(point => point + by),
                    _ => [origin, origin + new Vector(visual.Bounds.Width, visual.Bounds.Height)]
                };

                return (Kind: visual.GetType().Name, At: points.SelectMany(point => new[] { point.X, point.Y }).ToArray());
            })
            .OrderBy(shape => shape.Kind, StringComparer.Ordinal)
            .ThenBy(shape => string.Join(",", shape.At.Select(value => Math.Round(value).ToString(CultureInfo.InvariantCulture))), StringComparer.Ordinal)
            .ToList();

    private static void Click(MenuItem item)
    {
        item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>철도 그림마다 판 위의 실제 모양 — 종류와 자리(옮김 포함) · 기하의 수들. 차례 없이 견주게 반올림한 글로 정렬한다.</summary>
    private static List<(string Kind, double[] At)> Shapes(GraphEditorView graph, Canvas canvas) =>
        graph.RailVisuals
            .Select(visual =>
            {
                Point origin = visual.TranslatePoint(default, canvas)!.Value;
                var by = new Vector(origin.X, origin.Y);

                IEnumerable<Point> points = visual switch
                {
                    Line line => [line.StartPoint + by, line.EndPoint + by],
                    Polygon polygon => polygon.Points.Select(point => point + by),
                    Avalonia.Controls.Shapes.Path path => [path.Data!.Bounds.TopLeft + by, path.Data.Bounds.BottomRight + by],
                    _ => [origin, origin + new Vector(visual.Bounds.Width, visual.Bounds.Height)]
                };

                return (Kind: visual.GetType().Name, At: points.SelectMany(point => new[] { point.X, point.Y }).ToArray());
            })
            .OrderBy(shape => shape.Kind, StringComparer.Ordinal)
            .ThenBy(shape => string.Join(",", shape.At.Select(value => Math.Round(value).ToString(CultureInfo.InvariantCulture))), StringComparer.Ordinal)
            .ToList();
}
