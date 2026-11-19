using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 판의 둘레도 판처럼 받고, 판 안과 밖이 보인다</b> (2026-11-19 소유자 · usability UX22). ⛔ 판(카드 범위 + 여백)이 화면보다 작으면 둘레가
/// 판이 아니어서 거기서는 끌기가 막혔고, 배율 칸이 판 밖을 잘라 범위 사각형 · 판 밖으로 끈 카드가 사라져 보였으며, 놓으면 넓어진 판이 가운데로
/// 다시 서 카드가 놓은 자리에서 밀렸다.
/// </summary>
public sealed class ChapterBoardDeskTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-board-desk", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 판_바깥이_보이는_칸을_채우고_판과_다른_바탕이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view) = Open();
        ScrollViewer scroll = view.FindControl<ScrollViewer>("GraphScroll")!;
        Panel desk = view.FindControl<Panel>("BoardDesk")!;
        Canvas canvas = Board(view);

        Assert.True(desk.Bounds.Width >= scroll.Viewport.Width - 0.5 && desk.Bounds.Height >= scroll.Viewport.Height - 0.5,
            $"바깥 {desk.Bounds.Size} · 화면 {scroll.Viewport}");
        Assert.True(canvas.Bounds.Width < scroll.Viewport.Width && canvas.Bounds.Height < scroll.Viewport.Height);   // 이 견본은 판이 화면보다 작다
        Assert.NotEqual(((ISolidColorBrush)desk.Background!).Color, ((ISolidColorBrush)canvas.Background!).Color);
        Assert.False(view.FindControl<LayoutTransformControl>("ZoomHost")!.ClipToBounds);   // 판 밖을 자르지 않는다

        // 처음 열면 판이 화면 가운데에 선다(⛔ 판을 그리기 전의 빈 판을 가운데로 놓아 버려 그린 판이 반 화면 비켜 섰다).
        Point origin = canvas.TranslatePoint(default, scroll)!.Value;
        Point end = canvas.TranslatePoint(new Point(canvas.Width, canvas.Height), scroll)!.Value;
        Assert.Equal(origin.X, scroll.Viewport.Width - end.X, 1.0);
        Assert.Equal(origin.Y, scroll.Viewport.Height - end.Y, 1.0);

        window.Close();
    });

    [Fact]
    public void 판_둘레를_눌러_끌어도_범위를_고른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view) = Open();
        Canvas canvas = Board(view);

        // 판 위쪽 바깥에서 눌러 카드 둘(root · a)을 덮도록 끈다.
        var from = new Point(20, -24);
        Assert.IsType<Panel>(window.InputHitTest(OnScreen(canvas, from)));   // 판 밖 — 바깥이 받는다
        window.MouseDown(OnScreen(canvas, from), MouseButton.Left);
        window.MouseMove(OnScreen(canvas, new Point(470, 140)), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Rectangle band = Assert.Single(canvas.Children.OfType<Rectangle>(), rectangle => rectangle.StrokeDashArray is { Count: > 0 });
        Assert.True(Canvas.GetTop(band) < 0);   // 판 밖에서 시작한 사각형이다(잘리지 않고 선다)

        window.MouseUp(OnScreen(canvas, new Point(470, 140)), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(canvas.Children.OfType<Rectangle>(), rectangle => rectangle.StrokeDashArray is { Count: > 0 });
        Assert.Equal("root", view.FindControl<TextBox>("IdBox")!.Text);   // 끌기 시작한 자리에서 가장 가까운 카드를 골랐다

        window.Close();
    });

    [Fact]
    public void 카드를_판_밖으로_끌어도_보이고_놓으면_그_자리에_남는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view) = Open();
        Canvas canvas = Board(view);
        double width = canvas.Width;
        double height = canvas.Height;
        Point sheet = OnScreen(canvas, default);

        // 가장 왼쪽 카드(root)를 판 아래 바깥으로 끈다.
        Border root = Card(canvas, "root");
        Point grab = OnScreen(root, new Point(30, 10));
        Point drop = grab + new Point(150, 170);
        window.MouseDown(grab, MouseButton.Left);
        window.MouseMove(grab + new Point(40, 0), RawInputModifiers.LeftMouseButton);
        window.MouseMove(drop, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Point dragged = OnScreen(root, default);
        Assert.Equal((width, height), (canvas.Width, canvas.Height));                  // 끄는 동안 판은 그대로다
        Assert.True(dragged.Y > OnScreen(canvas, new Point(0, height)).Y, "판 아래 바깥에 섰다");
        Assert.Equal(sheet, OnScreen(canvas, default));                                // 판은 비켜 서지 않는다

        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // 놓으면 판이 넓어져 카드를 감싸고, 카드는 화면에서 놓은 자리 그대로다 — 판의 기준(가장 왼쪽 카드)이 옮겨져도.
        Point placed = OnScreen(Card(canvas, "root"), default);
        Assert.Equal(dragged.X, placed.X, 1.0);
        Assert.Equal(dragged.Y, placed.Y, 1.0);
        Assert.True(canvas.Height > height);
        Assert.Equal(OnScreen(Card(canvas, "b"), default).Y, placed.Y - 170 * Scale(view), 2.0);   // 안 끈 카드도 제자리

        window.Close();
    });

    [Fact]
    public void 오른쪽_밖에_놓아_판이_넓어져도_판은_비켜_서지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view) = Open();
        Canvas canvas = Board(view);
        Point sheet = OnScreen(canvas, default);
        double width = canvas.Width;

        // 가장 오른쪽 카드(b)를 오른쪽으로 — 판의 오른쪽 여백(60)을 넘어 판이 넓어진다. 화면 안에서.
        Border b = Card(canvas, "b");
        Point grab = OnScreen(b, new Point(100, 30));
        Point drop = grab + new Point(40, 0);
        window.MouseDown(grab, MouseButton.Left);
        window.MouseMove(grab + new Point(10, 0), RawInputModifiers.LeftMouseButton);
        window.MouseMove(drop, RawInputModifiers.LeftMouseButton);
        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(canvas.Width > width);
        Assert.Equal(sheet, OnScreen(canvas, default));   // ⛔ 가운데로 다시 서느라 넓어진 반만큼 왼쪽으로 비켜 섰다

        window.Close();
    });

    [Fact]
    public void 창을_줄이면_판이_화면에_들어가게_띄움을_줄인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView view) = Open();
        ScrollViewer scroll = view.FindControl<ScrollViewer>("GraphScroll")!;
        Canvas canvas = Board(view);
        Assert.True(canvas.TranslatePoint(default, scroll)!.Value.X > 1);   // 처음에는 판이 화면보다 작아 가운데에 띄워 섰다

        window.Width = 1300;   // 판이 화면보다 넓어진다
        Dispatcher.UIThread.RunJobs();

        Assert.True(canvas.Width > scroll.Viewport.Width);
        Assert.Equal(0, canvas.TranslatePoint(default, scroll)!.Value.X, 0.5);   // 띄움이 줄어 판이 화면 왼쪽부터 선다(넘치는 만큼만 스크롤한다)

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>[구성] 탭 — ch01(root · a · hall의 b)이 한 줄로 선 작은 판. 판이 화면보다 작다.</summary>
    private (MainWindow Window, ChapterGraphView View) Open()
    {
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static Canvas Board(ChapterGraphView view) => view.FindControl<Canvas>("GraphCanvas")!;

    private static Border Card(Canvas canvas, string episodeId) =>
        canvas.Children.OfType<Border>().Single(border => border.Tag as string == episodeId);

    private static double Scale(ChapterGraphView view) =>
        (view.FindControl<LayoutTransformControl>("ZoomHost")!.LayoutTransform as ScaleTransform)?.ScaleX ?? 1;

    /// <summary>그것의 그 자리가 창에서 어디인가 — 포인터는 창 기준으로 준다.</summary>
    private static Point OnScreen(Visual visual, Point at) =>
        visual.TranslatePoint(at, (Visual)TopLevel.GetTopLevel(visual)!)!.Value;
}
