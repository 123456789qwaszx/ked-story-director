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
/// 때마다 통째로 그었다) 무리 안팎을 잇는 길만 다시 긋는다 — 그래도 놓은 뒤의 모습은 통째로 그은 것과 같다.
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

        // 따라간 모습 = 같은 카드로 통째로 그은 모습(끌기 밖의 자리 갱신은 통째로 긋는다) — 반 픽셀 안에서(새로 그은 그림은 배치가
        // 픽셀에 맞추고, 옮긴 그림은 끈 만큼 그대로 간다).
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
            Assert.True(drawn[index].At.Zip(followed[index].At).All(pair => Math.Abs(pair.First - pair.Second) <= 0.6),
                $"{drawn[index].Kind} 통째로 [{string.Join(", ", drawn[index].At.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))}] · " +
                $"따라감 [{string.Join(", ", followed[index].At.Select(value => value.ToString("F1", CultureInfo.InvariantCulture)))}]");
        }

        window.Close();
    });

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
