using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>F — 고른 것에 화면을 맞춘다</b>(2026-11-18 소유자 — 「장면이나 특정 에피소드를 누른 상태에서 F를 누르면 그 장면에 맞게 화면이 맞춰지도록 …
/// 에피소드는 편집하기 적절한 크기로 화면 중앙에」 · <c>docs/work-orders/writer-comfort-orders.md</c> WC2).
///
/// 못 박는 것: ① 카드를 골랐으면 100%로 그 카드가 화면 가운데에 ② 장면을 골랐으면 그 장면의 카드가 화면 안에 ③ 글 칸에서 F는 글자다.
/// </summary>
public sealed class DirectorFitTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-fit", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 카드를_고르고_F를_누르면_100퍼센트로_그_카드가_화면_가운데에_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        string b = EpisodeNaming.CardFor(session.Project, "ch01", "b")!.Id;
        session.Editor.MoveNodes(new Dictionary<string, (double X, double Y)> { [b] = (5000, 3000) });   // 판 한가운데 — 가장자리에 막히지 않게
        session.Select(b);
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        PressF(window);

        Assert.Equal("100%", graph.FindControl<TextBlock>("ZoomText")!.Text);
        Border card = CardOf(graph, b);
        Point center = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), scroll)!.Value;
        Assert.Equal(scroll.Viewport.Width / 2, center.X, 2);
        Assert.Equal(scroll.Viewport.Height / 2, center.Y, 2);

        window.Close();
    });

    [Fact]
    public void 장면을_고르고_F를_누르면_그_장면이_화면에_들어오고_글_칸에서는_글자다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        string b = EpisodeNaming.CardFor(session.Project, "ch01", "b")!.Id;
        session.Editor.MoveNodes(new Dictionary<string, (double X, double Y)> { [b] = (9000, 6000) });   // hall 장면을 멀리
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        PressF(window);

        Border card = CardOf(graph, b);
        Point topLeft = card.TranslatePoint(default, scroll)!.Value;
        Point bottomRight = card.TranslatePoint(new Point(card.Bounds.Width, card.Bounds.Height), scroll)!.Value;
        Assert.InRange(topLeft.X, 0, scroll.Viewport.Width);
        Assert.InRange(topLeft.Y, 0, scroll.Viewport.Height);
        Assert.InRange(bottomRight.X, 0, scroll.Viewport.Width);
        Assert.InRange(bottomRight.Y, 0, scroll.Viewport.Height);

        // 글 칸에서는 F가 글자다 — 화면이 움직이지 않는다.
        session.Select(b);
        Dispatcher.UIThread.RunJobs();
        TextBox writing = graph.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(item => item.Tag as string == GraphEditorView.WritingTag + b);
        writing.Focus();
        Dispatcher.UIThread.RunJobs();
        string zoom = graph.FindControl<TextBlock>("ZoomText")!.Text!;
        Vector offset = scroll.Offset;
        Assert.False(PressF(window));
        Assert.Equal(zoom, graph.FindControl<TextBlock>("ZoomText")!.Text);
        Assert.Equal(offset, scroll.Offset);

        window.Close();
    });

    private static bool PressF(MainWindow window)
    {
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F, Source = window };
        window.RaiseEvent(args);
        Dispatcher.UIThread.RunJobs();
        return args.Handled;
    }

    private static Border CardOf(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(border => border.GetLogicalDescendants().OfType<TextBox>()
                .Any(box => box.Tag as string == GraphEditorView.WritingTag + nodeId));
}
