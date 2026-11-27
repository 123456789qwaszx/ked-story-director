using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>여러 카드를 함께 고르고 함께 바꾼다</b>(2026-11-27 소유자 — 「여러개의 에피소드 노드를 드래그해서 동시에 선택한 상태에서, '기본 크기로 고정', '가로 글 맞춤',
/// '세로 글 맞춤' 기능을 쓸 경우 선택한 것들 모두에게 적용」 · 「하나의 노드를 선택한 상태에서 Alt+ 클릭으로 다른 노드도 같이 선택」 · 「Shift+클릭으로 선택한
/// 노드 중 원하는 노드만을 선택 해제」). 카드는 위에서 아래로 root(0,0) · a(0,400) · b(0,800)이다.
/// </summary>
public sealed class CardPickManyTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-pick-many", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 범위로_고른_무리에서_머리의_단추는_모두에게_입고_되돌리기_한_번에_모두_돌아온다() => HeadlessUi.Run(() =>
    {
        (AuthoringSession session, GraphEditorView graph) = Show();
        string a = CardId(session, "a");
        string b = CardId(session, "b");
        string root = CardId(session, "root");

        Band(graph, new Point(230, 810), new Point(100, 520));   // a · b
        Assert.Equal(new[] { a, b }.Order(StringComparer.Ordinal), Sorted(graph));

        // 기본 크기로 고정 — 누른 카드가 풀려 있으니 모두 고정한다. 무리 밖의 root는 그대로다.
        Button(graph, GraphEditorView.BasePinToggleTag, a).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle(graph);
        Assert.True(Layout(session, a).PinnedToBase);
        Assert.True(Layout(session, b).PinnedToBase);
        Assert.False(Layout(session, root).PinnedToBase);
        Assert.StartsWith("고른 카드 2개에 함께", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(2, graph.GroupedNodeIds.Count);   // 단추를 눌러도 무리는 그대로다

        // 되돌리기 한 번에 둘 다 돌아온다.
        session.Editor.Undo();
        Settle(graph);
        Assert.False(Layout(session, a).PinnedToBase);
        Assert.False(Layout(session, b).PinnedToBase);

        // 글 맞춤 — 누른 카드(b)가 바뀌는 쪽으로 모두 맞춘다. 이미 그쪽인 카드는 그대로다.
        bool widthBefore = Layout(session, b).FitWidth;
        session.Editor.SetNodeFit(a, !widthBefore, Layout(session, a).FitHeight);   // a는 이미 바뀔 쪽
        Settle(graph);
        Band(graph, new Point(230, 810), new Point(100, 520));
        Button(graph, GraphEditorView.FitWidthToggleTag, b).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle(graph);
        Assert.Equal(!widthBefore, Layout(session, a).FitWidth);
        Assert.Equal(!widthBefore, Layout(session, b).FitWidth);

        bool heightBefore = Layout(session, a).FitHeight;
        Button(graph, GraphEditorView.FitHeightToggleTag, a).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle(graph);
        Assert.Equal(!heightBefore, Layout(session, a).FitHeight);
        Assert.Equal(!heightBefore, Layout(session, b).FitHeight);
        Assert.Equal(heightBefore, Layout(session, root).FitHeight);

        // 무리 밖 카드의 단추는 그 카드 하나다.
        Button(graph, GraphEditorView.BasePinToggleTag, root).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle(graph);
        Assert.True(Layout(session, root).PinnedToBase);
        Assert.False(Layout(session, a).PinnedToBase);
    });

    [Fact]
    public void Alt로_누르면_고른_것에_더하고_Shift로_누르면_그_카드만_뺀다() => HeadlessUi.Run(() =>
    {
        (AuthoringSession session, GraphEditorView graph) = Show();
        string a = CardId(session, "a");
        string b = CardId(session, "b");
        string root = CardId(session, "root");

        session.Select(root);
        Settle(graph);

        // Alt+누름 — 지금 고른 root와 함께 무리가 된다. 인스펙터는 마지막에 누른 카드다. 끌기는 시작하지 않는다.
        PressCard(graph, a, KeyModifiers.Alt);
        Assert.Equal(new[] { a, root }.Order(StringComparer.Ordinal), Sorted(graph));
        Assert.Equal(a, session.SelectedNodeId);
        PressCard(graph, b, KeyModifiers.Alt);
        Assert.Equal(new[] { a, b, root }.Order(StringComparer.Ordinal), Sorted(graph));
        Assert.All([a, b, root], id => Assert.Equal(2, Card(graph, id).BorderThickness.Left));

        // 무리는 범위로 고른 것과 같다 — 머리의 단추가 모두에게 입는다.
        Button(graph, GraphEditorView.BasePinToggleTag, root).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Settle(graph);
        Assert.All([a, b, root], id => Assert.True(Layout(session, id).PinnedToBase));

        // Shift+누름 — 그 카드만 뺀다. 인스펙터가 보이던 카드(b)를 빼면 남은 것 가운데 마지막으로 옮긴다.
        PressCard(graph, b, KeyModifiers.Shift);
        Assert.Equal(new[] { a, root }.Order(StringComparer.Ordinal), Sorted(graph));
        Assert.NotEqual(b, session.SelectedNodeId);
        Assert.Equal(1, Card(graph, b).BorderThickness.Left);

        // 하나만 남으면 무리를 풀고 그 카드 하나를 고른 것이 된다.
        PressCard(graph, a, KeyModifiers.Shift);
        Assert.Empty(graph.GroupedNodeIds);
        Assert.Equal(root, session.SelectedNodeId);
        Assert.Equal(1, Card(graph, a).BorderThickness.Left);

        // 고르지 않은 카드를 Shift로 눌러도 그대로다. 고른 카드 하나를 빼면 아무것도 안 고른 것이 된다.
        PressCard(graph, b, KeyModifiers.Shift);
        Assert.Equal(root, session.SelectedNodeId);
        PressCard(graph, root, KeyModifiers.Shift);
        Assert.Null(session.SelectedNodeId);

        // 아무것도 안 고른 채 Alt로 누르면 그 카드 하나를 고른다.
        PressCard(graph, a, KeyModifiers.Alt);
        Assert.Equal(a, session.SelectedNodeId);
        Assert.Empty(graph.GroupedNodeIds);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>[대본] 탭 — ch01 판만 편다(ch02 판이 같은 자리에 펼쳐지면 카드가 겹친다).</summary>
    private (AuthoringSession Session, GraphEditorView Graph) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        session.SetFileExpanded(EpisodeNaming.BoardOf(session.Project, "ch02")!.Id, expanded: false);
        Dispatcher.UIThread.RunJobs();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Settle(graph);
        return (session, graph);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static NodeLayout Layout(AuthoringSession session, string nodeId) => session.Project.FindNode(nodeId)!.Layout;

    private static string[] Sorted(GraphEditorView graph) => graph.GroupedNodeIds.Order(StringComparer.Ordinal).ToArray();

    private static void Settle(GraphEditorView graph)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
        }
    }

    private static Canvas Canvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static Border Card(GraphEditorView graph, string nodeId) =>
        Canvas(graph).Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static Button Button(GraphEditorView graph, string tag, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Button>().Single(button => button.Tag as string == tag + nodeId);

    /// <summary>카드 머리의 제목 글자를 눌렀다 뗀다 — 그 키를 누른 채로.</summary>
    private static void PressCard(GraphEditorView graph, string nodeId, KeyModifiers keys)
    {
        TextBlock title = Card(graph, nodeId).GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Tag as string == GraphEditorView.CardTitleTag);
        var root = (Visual)TopLevel.GetTopLevel(title)!;
        Point at = title.TranslatePoint(new Point(4, 6), root) ?? default;
        var pointer = new Pointer(0, PointerType.Mouse, true);
        title.RaiseEvent(new PointerPressedEventArgs(
            title, pointer, root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            keys, 1));
        Dispatcher.UIThread.RunJobs();
        Canvas(graph).RaiseEvent(new PointerReleasedEventArgs(
            Canvas(graph), pointer, root, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            keys, MouseButton.Left));
        Settle(graph);
    }

    /// <summary>빈 판을 눌러 끌고 놓는다 — 범위 긋기.</summary>
    private static void Band(GraphEditorView graph, Point from, Point to)
    {
        var root = (Visual)TopLevel.GetTopLevel(graph)!;
        Point At(Point onCanvas) => Canvas(graph).TranslatePoint(onCanvas, root)!.Value;
        var pointer = new Pointer(0, PointerType.Mouse, true);
        Canvas canvas = Canvas(graph);
        canvas.RaiseEvent(new PointerPressedEventArgs(canvas, pointer, root, At(from), 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
        canvas.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, canvas, pointer, root, At(to), 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
        canvas.RaiseEvent(new PointerReleasedEventArgs(canvas, pointer, root, At(to), 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        Settle(graph);
    }
}
