using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판을 끝까지 줄여도 판은 왼위에 붙어 보인다</b>(2026-11-18 소유자 — 「배율이 12%가 되면 … 위쪽 텅 빈 부분이 보이고, 10%가 되면 아예
/// 화면에 아무것도 안 보입니다」 · writer-comfort WC1). ⛔ 판이 화면보다 작아지면 배율 칸이 화면만큼 늘어나 판을
/// 가운데로 밀었다 — 미니맵은 판이 왼위에 있다고 셈해 어긋났고, 더 줄이면 판이 화면 밖으로 나갔다.
/// </summary>
public sealed class BoardZoomOutTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-board-zoom-out", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 판이_화면보다_작아질_만큼_줄여도_판은_화면_왼위에_붙어_있다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        session.Editor.SetBoardSize(4000, 4000);   // 가장 작은 판 — 10%면 400 × 400으로 화면보다 작다
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        var top = (Visual)TopLevel.GetTopLevel(scroll)!;
        Point middle = scroll.TranslatePoint(new Point(scroll.Bounds.Width / 2, scroll.Bounds.Height / 2), top)!.Value;

        for (int step = 0; step < 30; step++)
        {
            scroll.RaiseEvent(new PointerWheelEventArgs(scroll, new Pointer(0, PointerType.Mouse, true), top, middle, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None, new Vector(0, -1))
            {
                RoutedEvent = InputElement.PointerWheelChangedEvent
            });
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("10%", graph.FindControl<TextBlock>("ZoomText")!.Text);
        Assert.True(canvas.Bounds.Height * 0.1 < scroll.Viewport.Height);   // 판이 화면보다 작다

        Point origin = canvas.TranslatePoint(default, scroll)!.Value;
        Assert.Equal(0, origin.X, 1);
        Assert.Equal(0, origin.Y, 1);

        window.Close();
    });
}
