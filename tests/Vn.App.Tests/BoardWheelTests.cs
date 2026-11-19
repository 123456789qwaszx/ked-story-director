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
///
/// 설정 「휠로 판 옮기기」(WA3 — 맥만 처음부터 켜짐)를 켜면 휠이 판을 옮기고 배율은 Ctrl(맥은 ⌘도)+휠이다. 트랙패드 집기는 늘 배율이다.
/// 스위치는 이 시험 프로세스가 함께 쓴다 — 켠 시험은 끝에 「고른 적 없음」으로 돌린다.
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

    [Fact]
    public void 휠로_판_옮기기의_처음_값은_맥만_켜짐이다() => HeadlessUi.Run(() =>
    {
        BoardWheel.Set(null);   // 고른 적이 없다
        bool was = PlatformKeys.IsMac;

        try
        {
            PlatformKeys.IsMac = false;
            Assert.False(BoardWheel.Pans);

            PlatformKeys.IsMac = true;
            Assert.True(BoardWheel.Pans);

            // 고르면 OS와 무관하게 고른 대로다.
            BoardWheel.Set(false);
            Assert.False(BoardWheel.Pans);
            Assert.False(AppSettingsService.LoadWheelPansBoard());
        }
        finally
        {
            PlatformKeys.IsMac = was;
            BoardWheel.Set(null);
        }

        Assert.Null(AppSettingsService.LoadWheelPansBoard());
    });

    [Fact]
    public void 휠로_판_옮기기를_켜면_휠은_판을_옮기고_Ctrl_휠이_배율이다() => Panning(() =>
    {
        (MainWindow window, GraphEditorView graph, ScrollViewer scroll) = Open();
        Assert.True(scroll.Extent.Width > scroll.Viewport.Width && scroll.Extent.Height > scroll.Viewport.Height, "판이 뷰포트보다 커야 한다");
        double start = Scale(graph);
        scroll.Offset = default;

        // 내리면 아래로 — 배율은 그대로다.
        Wheel(scroll, new Vector(0, -1));
        Assert.Equal(new Vector(0, BoardWheel.PanStep), scroll.Offset);
        Assert.Equal(start, Scale(graph), 3);

        // 두 손가락 쓸기는 가로 · 세로를 함께 보낸다.
        Wheel(scroll, new Vector(-0.5, -0.5));
        Assert.Equal(new Vector(BoardWheel.PanStep / 2, BoardWheel.PanStep * 1.5), scroll.Offset);

        // Shift+휠은 가로다(세로 휠뿐인 마우스).
        Wheel(scroll, new Vector(0, -1), KeyModifiers.Shift);
        Assert.Equal(new Vector(BoardWheel.PanStep * 1.5, BoardWheel.PanStep * 1.5), scroll.Offset);
        Assert.Equal(start, Scale(graph), 3);

        // Ctrl+휠이 배율이다.
        Wheel(scroll, new Vector(0, 1), KeyModifiers.Control);
        Assert.Equal(start * 1.15, Scale(graph), 3);

        // 끄면 다음 휠부터 다시 배율이다.
        BoardWheel.Set(false);
        Wheel(scroll, new Vector(0, -1));
        Assert.Equal(start, Scale(graph), 3);

        window.Close();
    });

    [Fact]
    public void 맥에서는_커맨드_휠도_배율이다() => Panning(() =>
    {
        bool was = PlatformKeys.IsMac;
        PlatformKeys.IsMac = true;

        try
        {
            (MainWindow window, GraphEditorView graph, ScrollViewer scroll) = Open();
            double start = Scale(graph);

            Wheel(scroll, new Vector(0, 1), KeyModifiers.Meta);
            Assert.Equal(start * 1.15, Scale(graph), 3);

            window.Close();
        }
        finally
        {
            PlatformKeys.IsMac = was;
        }
    });

    [Fact]
    public void 트랙패드_집기는_스위치와_무관하게_배율이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, GraphEditorView graph, ScrollViewer scroll) = Open();
        double start = Scale(graph);

        try
        {
            BoardWheel.Set(false);
            Magnify(scroll, 0.1);
            Assert.Equal(start * 1.1, Scale(graph), 3);

            BoardWheel.Set(true);
            Magnify(scroll, -0.1);
            Assert.Equal(start * 1.1 * 0.9, Scale(graph), 3);
        }
        finally
        {
            BoardWheel.Set(null);
        }

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>「휠로 판 옮기기」를 켠 채 본문을 돌리고 「고른 적 없음」으로 돌린다.</summary>
    private static void Panning(Action body) => HeadlessUi.Run(() =>
    {
        BoardWheel.Set(true);

        try
        {
            body();
        }
        finally
        {
            BoardWheel.Set(null);
        }
    });

    private (MainWindow Window, GraphEditorView Graph, ScrollViewer Scroll) Open()
    {
        (MainWindow window, _) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
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

    /// <summary>판 위에서 두 손가락을 벌리고(+) 오므린(−) 집기 — 맥 트랙패드가 보내는 신호.</summary>
    private static void Magnify(ScrollViewer scroll, double amount)
    {
        var top = (Visual)TopLevel.GetTopLevel(scroll)!;
        Point at = scroll.TranslatePoint(new Point(scroll.Bounds.Width * 0.6, scroll.Bounds.Height * 0.6), top)!.Value;

        scroll.RaiseEvent(new PointerDeltaEventArgs(InputElement.PointerTouchPadGestureMagnifyEvent, scroll,
            new Pointer(0, PointerType.Mouse, true), top, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None, new Vector(amount, amount)));
        Dispatcher.UIThread.RunJobs();
    }
}
