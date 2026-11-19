using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>끄는 도중 창을 떠나면(Windows 키 · Alt+Tab) 하던 끌기를 정리한다</b> (2026-11-19 소유자 보고 · usability UX20). 놓음이 이 창으로 오지 않아
/// 끌기가 「끄는 중」으로 남았다 — 돌아와 판을 누르면 범위 사각형이 판에 남아 쌓였고, [대본] 판에서 선택지 점을 끌던 중이면 돌아와 누른 빈 곳이
/// 그 놓음이 되어 선택지가 끊겼다.
/// </summary>
public sealed class WindowLeaveGestureTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-window-leave", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 구성_판에서_범위를_끌다_창을_떠나면_사각형을_거두고_다시_눌러도_쌓이지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        Canvas canvas = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<Canvas>("GraphCanvas")!;
        Dispatcher.UIThread.RunJobs();

        Press(canvas, canvas, new Point(4, 4));
        Move(canvas, new Point(60, 60));
        Assert.Single(Bands(canvas));

        Leave(window);
        Assert.Empty(Bands(canvas));

        // 돌아와 다시 끌고 놓아도 사각형이 남지 않는다 — 몇 번을 해도.
        for (int round = 0; round < 3; round++)
        {
            Press(canvas, canvas, new Point(4, 4));
            Move(canvas, new Point(40, 40));
            Leave(window);
            Press(canvas, canvas, new Point(4, 4));
            Release(canvas, new Point(8, 8));
        }

        Assert.Empty(Bands(canvas));
        window.Close();
    });

    [Fact]
    public void 놓음을_못_본_범위_사각형은_다음_누름이_걷는다() => HeadlessUi.Run(() =>
    {
        // 창을 떠난 줄 모르는 경우에도(놓음이 다른 데로 갔다) 새 사각형을 열 때 옛것을 걷는다 — 두 판 모두.
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        Canvas chapter = window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<Canvas>("GraphCanvas")!;
        Press(chapter, chapter, new Point(4, 4));
        Move(chapter, new Point(40, 40));
        Press(chapter, chapter, new Point(4, 4));
        Release(chapter, new Point(8, 8));
        Assert.Empty(Bands(chapter));

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Canvas script = window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!;
        Press(script, script, new Point(4, 4));
        Move(script, new Point(40, 40));
        Press(script, script, new Point(4, 4));
        Release(script, new Point(8, 8));
        Assert.Empty(Bands(script));

        window.Close();
    });

    [Fact]
    public void 대본_판에서_선택지_점을_끌다_창을_떠나면_돌아와_누른_곳이_놓음이_되지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "간다");
        Dispatcher.UIThread.RunJobs();

        Ellipse knob = canvas.Children.OfType<StackPanel>().Single(row => row.Tag as string == "root#0").Children.OfType<Ellipse>().Single();
        Press(knob, canvas, default);
        Assert.Contains(canvas.Children.OfType<Line>(), line => line.StrokeDashArray is { Count: > 0 });   // 잇는 점선

        Leave(window);
        Assert.DoesNotContain(canvas.Children.OfType<Line>(), line => line.StrokeDashArray is { Count: > 0 });

        // 돌아와 판의 먼 빈 곳을 눌렀다 놓는다 — ⛔ 끌던 점의 놓음이 되어 선택지가 끊겼다.
        Press(canvas, canvas, new Point(5000, 5000));
        Release(canvas, new Point(5000, 5000));
        ChapterEdge edge = Assert.Single(session.Editor.FindChapter("ch01")!.ToGraphModel("ch01").Edges);
        Assert.Equal("a", edge.ToEpisodeId);

        window.Close();
    });

    [Fact]
    public void 대본_판에서_카드를_끌다_창을_떠나면_보이던_자리에서_놓는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;
        string a = EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id;
        Border card = canvas.Children.OfType<Border>().Single(border => border.Tag as string == a);
        var grab = new Point(Canvas.GetLeft(card) + 30, Canvas.GetTop(card) + 8);
        double startX = session.Project.FindNode(a)!.Layout.X;

        Press(card, canvas, grab);
        Move(canvas, grab + new Point(80, 0));
        long revision = session.Editor.Revision;

        Leave(window);
        Assert.Equal(revision + 1, session.Editor.Revision);                       // 놓은 것으로 적었다(되돌리기 한 칸)
        Assert.Equal(startX + 80, session.Project.FindNode(a)!.Layout.X, 1.0);

        // 돌아와 단추 없이 움직여도 카드가 따라오지 않는다.
        Move(canvas, grab + new Point(200, 100), pressed: false);
        Assert.Equal(startX + 80, session.Project.FindNode(a)!.Layout.X, 1.0);
        Assert.Equal(revision + 1, session.Editor.Revision);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 창을 떠난다 — 플랫폼 창이 「비활성」을 알리는 길 그대로(Windows 키 · Alt+Tab이 하는 그것). 그 알림 자리는 Avalonia 안쪽이라 리플렉션으로
    /// 부르고, 창의 <see cref="WindowBase.Deactivated"/>가 실제로 났는지 본다.
    /// </summary>
    private static void Leave(Window window)
    {
        int fired = 0;
        void Count(object? sender, EventArgs args) => fired++;
        window.Deactivated += Count;

        object impl = window.PlatformImpl!;
        System.Reflection.PropertyInfo deactivated = impl.GetType()
            .GetInterfaces().Prepend(impl.GetType())
            .SelectMany(type => type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            .First(property => property.Name.EndsWith("Deactivated", StringComparison.Ordinal) && property.PropertyType == typeof(Action));
        ((Action?)deactivated.GetValue(impl))!.Invoke();
        Dispatcher.UIThread.RunJobs();

        window.Deactivated -= Count;
        Assert.Equal(1, fired);
    }

    private static IEnumerable<Rectangle> Bands(Canvas canvas) =>
        canvas.Children.OfType<Rectangle>().Where(rectangle => rectangle.StrokeDashArray is { Count: > 0 });

    private static Point At(Control control, Point point) =>
        control.TranslatePoint(point, (Visual)TopLevel.GetTopLevel(control)!)!.Value;

    /// <summary>그것을 판의 그 자리(판 좌표)에서 누른다.</summary>
    private static void Press(Control control, Canvas board, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, At(board, at), 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Move(Canvas canvas, Point at, bool pressed = true)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, canvas, new Pointer(0, PointerType.Mouse, true), root, At(canvas, at), 0,
            new PointerPointProperties(pressed ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Canvas canvas, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, At(canvas, at), 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }
}
