using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판 위의 휠</b> (2026-11-19 소유자 — 「트랙패드 수정을 합시다」 · <c>docs/work-orders/writer-app-orders.md</c> WA2).
///
/// 배율은 신호의 크기에 비례하고 가로 값은 판을 가로로 옮긴다 — [구성] 판과 한 벌이다(<see cref="BoardWheel"/> · 그쪽 시험은
/// <see cref="ChapterGraphNavigationTests"/>). ⛔ 신호 하나마다 ×1.15 · ÷1.15여서 트랙패드에서 배율이 튀었고, 가로로 쓸면 축소됐다.
/// </summary>
public sealed class BoardWheelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-board-wheel", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 휠_한_칸은_그대로이고_잔_휠은_그_크기만큼만_확대한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, GraphEditorView graph, ScrollViewer scroll) = Open();
        double start = Scale(graph);   // 판은 열 때 화면에 맞춘 배율로 선다 — 거기서부터 잰다

        Wheel(scroll, new Vector(0, 1));
        Assert.Equal(start * 1.15, Scale(graph), 3);

        Wheel(scroll, new Vector(0, -1));
        Assert.Equal(start, Scale(graph), 3);

        Wheel(scroll, new Vector(0, 0.25));
        Assert.Equal(start * Math.Pow(1.15, 0.25), Scale(graph), 3);

        for (int index = 0; index < 3; index++)
        {
            Wheel(scroll, new Vector(0, 0.25));
        }

        Assert.Equal(start * 1.15, Scale(graph), 3);   // 잔 신호 넷 = 휠 한 칸

        window.Close();
    });

    [Fact]
    public void 가로_휠은_배율이_아니고_판을_가로로_옮긴다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, GraphEditorView graph, ScrollViewer scroll) = Open();
        Assert.True(scroll.Extent.Width > scroll.Viewport.Width, "판이 뷰포트보다 넓어야 한다");
        double start = Scale(graph);
        scroll.Offset = default;

        Wheel(scroll, new Vector(-1, 0));

        Assert.Equal(start, Scale(graph), 3);
        Assert.Equal(new Vector(BoardWheel.PanStep, 0), scroll.Offset);

        Wheel(scroll, new Vector(1, 0));

        Assert.Equal(default, scroll.Offset);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, GraphEditorView Graph, ScrollViewer Scroll) Open()
    {
        (MainWindow window, _) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        return (window, graph, graph.FindControl<ScrollViewer>("GraphScroll")!);
    }

    private static double Scale(GraphEditorView graph) =>
        graph.FindControl<LayoutTransformControl>("ZoomHost")!.LayoutTransform is ScaleTransform scale ? scale.ScaleX : 1;

    /// <summary>판의 빈 곳 위에서 굴린 휠.</summary>
    private static void Wheel(ScrollViewer scroll, Vector delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        var top = (Visual)TopLevel.GetTopLevel(scroll)!;
        Point at = scroll.TranslatePoint(new Point(scroll.Bounds.Width * 0.6, scroll.Bounds.Height * 0.6), top)!.Value;

        scroll.RaiseEvent(new PointerWheelEventArgs(scroll, new Pointer(0, PointerType.Mouse, true), top, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), modifiers, delta)
        {
            RoutedEvent = InputElement.PointerWheelChangedEvent
        });
        Dispatcher.UIThread.RunJobs();
    }
}
