using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>연출 그래프에서 장면 하나만 접는다</b> (2026-10-30 — 하이어아키의 눈, hierarchy SF).
///
/// 소유자: *"챕터가 접히는 건 현재를 유지하되, 챕터 내의 일부 장면만 접었을 경우, 접은 장면을 간소화 시키도록 해줘"* ·
/// *"접힌 상태로 간선의 연결을 드러내줘야 해"*. 접은 장면은 판 안의 줄 목록 상자가 되고, 선택지 길은 그 줄에서 나가고
/// 그 줄로 든다. 장면 이름표를 누르면 접히고, 상자를 두 번 누르면 펴진다.
/// </summary>
public sealed class SceneFoldTests
{
    // 줄 상자의 치수 — GraphEditorView의 ProxyHeaderHeight · ProxyRowHeight와 같다.
    private const double HeaderHeight = 50;
    private const double RowHeight = 28;

    [Fact]
    public void 장면을_접으면_그_장면만_줄_상자가_되고_다른_장면의_카드와_챕터_박스는_선다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string board) = Show();

        session.SetSceneCollapsed(board, "hall", collapsed: true);
        graph.Rebuild();

        Canvas canvas = BoardCanvas(graph);
        Border proxy = SceneProxy(canvas, board, "hall");

        Assert.Contains("장면 hall", Texts(proxy));
        Assert.Null(CardOf(canvas, Card(session, "b").Id));
        Assert.NotNull(CardOf(canvas, Card(session, "root").Id));
        Assert.NotNull(CardOf(canvas, Card(session, "a").Id));

        // 챕터 박스는 그대로다 — 판째 접힌 것이 아니다.
        Assert.Contains(canvas.Children.OfType<Border>(), border =>
            (border.Child as TextBlock)?.Text?.StartsWith("챕터 ch01", StringComparison.Ordinal) == true);
    });

    [Fact]
    public void 접은_장면으로_드는_선택지_길은_그_줄에_붙는다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string board) = Show();

        session.SetSceneCollapsed(board, "hall", collapsed: true);
        graph.Rebuild();

        Canvas canvas = BoardCanvas(graph);

        // a(펼친 opening) → b(접은 hall) — 「카드 없음」에서 멈추지 않고 상자의 b 줄 높이까지 간다.
        ShapePath lane = Lane(canvas, "a", "b");
        double rowY = Card(session, "b").Layout.Y + HeaderHeight + (RowHeight / 2);

        Assert.Equal(rowY, lane.Data!.Bounds.Bottom, precision: 1);
        Assert.DoesNotContain(canvas.Children.OfType<TextBlock>(), text =>
            text.Text?.Contains("카드 없음", StringComparison.Ordinal) == true);
    });

    [Fact]
    public void 접은_장면에서_나가는_길은_그_줄에서_나가고_장면_안끼리의_길은_상자_머리가_센다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string board) = Show();

        session.SetSceneCollapsed(board, "opening", collapsed: true);
        graph.Rebuild();

        Canvas canvas = BoardCanvas(graph);
        Border proxy = SceneProxy(canvas, board, "opening");

        // a는 상자의 둘째 줄이다(root · a) — a → b는 그 줄 높이에서 나가 b 카드의 접점으로 든다.
        ShapePath lane = Lane(canvas, "a", "b");
        double rowY = Canvas.GetTop(proxy) + HeaderHeight + RowHeight + (RowHeight / 2);
        Assert.Equal(rowY, lane.Data!.Bounds.Top, precision: 1);

        // root → a는 장면 안이라 선이 없고, 상자 머리가 센다.
        Assert.Null(LaneOrNull(canvas, "root", "a"));
        Assert.Contains(Texts(proxy), text => text.Contains("안쪽 길 1개", StringComparison.Ordinal));
    });

    [Fact]
    public void 장면_이름표를_두_번_누르면_접히고_상자를_두_번_누르면_펴진다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「한번 클릭했을 때 접히는 대신, 더블 클릭을 해야 접히도록」. 한 번은 그 장면의 카드를 고른다(아래 시험).
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        Canvas canvas = BoardCanvas(graph);

        Border label = canvas.Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + "hall");
        label.RaiseEvent(Press(label));
        Assert.Empty(session.CollapsedScenes);   // 한 번은 접지 않는다
        label.RaiseEvent(Press(label, clickCount: 2));

        Assert.Contains(new Vn.Authoring.Graph.GraphSceneKey(board, "hall"), session.CollapsedScenes);

        graph.Rebuild();
        Border proxy = SceneProxy(canvas, board, "hall");
        proxy.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, Press(proxy)));

        Assert.Empty(session.CollapsedScenes);

        graph.Rebuild();
        Assert.NotNull(CardOf(canvas, Card(session, "b").Id));
    });

    [Fact]
    public void 장면_이름표를_한_번_누르면_그_장면의_카드를_모두_골라_끌면_함께_움직인다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「한번 좌클릭을 했을 때는 … 그 장면 내에 포함된 모든 에피소드가 클릭되도록 해서 이동시키는 용도로」.
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        Canvas canvas = BoardCanvas(graph);
        DialogueNode root = Card(session, "root");
        DialogueNode a = Card(session, "a");
        DialogueNode b = Card(session, "b");
        // 무리 끌기는 판에 선 자리에서 옮긴다(범위 선택의 무리와 같다).
        (double X, double Y) Shown(DialogueNode card) => (Canvas.GetLeft(CardOf(canvas, card.Id)!), Canvas.GetTop(CardOf(canvas, card.Id)!));
        (double X, double Y) rootAt = Shown(root);
        (double X, double Y) aAt = Shown(a);
        (double X, double Y) bAt = (b.Layout.X, b.Layout.Y);

        Border label = canvas.Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + "opening");
        var top = (Visual)TopLevel.GetTopLevel(canvas)!;
        var pointer = new Pointer(0, PointerType.Mouse, true);
        Point at = label.TranslatePoint(new Point(4, 4), top)!.Value;
        Point to = at + new Point(120, 60);
        label.RaiseEvent(new PointerPressedEventArgs(
            label, pointer, top, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

        // 그 장면의 카드가 무리다(굵은 테두리) — 다른 장면의 카드는 아니다.
        Assert.Equal(2, CardOf(canvas, root.Id)!.BorderThickness.Left);
        Assert.Equal(2, CardOf(canvas, a.Id)!.BorderThickness.Left);
        Assert.Equal(1, CardOf(canvas, b.Id)!.BorderThickness.Left);

        // 그대로 끈다 — 판의 배율을 지나 판 위에서 얼마나 움직였는지로 잰다.
        Point delta = top.TranslatePoint(to, canvas)!.Value - top.TranslatePoint(at, canvas)!.Value;
        long before = session.Editor.Revision;
        canvas.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, canvas, pointer, top, to, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, pointer, top, to, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(rootAt.X + delta.X, root.Layout.X, 3);
        Assert.Equal(rootAt.Y + delta.Y, root.Layout.Y, 3);
        Assert.Equal(aAt.X + delta.X, a.Layout.X, 3);
        Assert.Equal(aAt.Y + delta.Y, a.Layout.Y, 3);
        Assert.Equal(bAt, (b.Layout.X, b.Layout.Y));   // 다른 장면은 그대로
        Assert.Equal(before + 1, session.Editor.Revision);   // 함께 한 칸
        Assert.Empty(session.CollapsedScenes);
    });

    [Fact]
    public void 이름표로_고른_무리에서_카드_하나를_눌렀다_놓으면_그_카드만_고른다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「챕터나 장면을 눌러서 그것에 포함된 여러 에피소드들이 선택된 상태에서, 다시 개별 에피소드를 누를 경우 … 다른것들까지
        // 모두 선택강조가 유지되는 버그」. 누르고 끌면 무리가 함께 움직이고(위 시험), 끌지 않고 놓으면 그 카드 하나만이다 — 윈도우의 목록과 같다.
        (GraphEditorView graph, AuthoringSession session, string _) = Show();
        Canvas canvas = BoardCanvas(graph);
        DialogueNode root = Card(session, "root");
        DialogueNode a = Card(session, "a");
        var top = (Visual)TopLevel.GetTopLevel(canvas)!;
        var pointer = new Pointer(0, PointerType.Mouse, true);

        void Release(Point at) => canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, pointer, top, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));

        // 장면 이름표를 눌렀다 놓는다 — 그 장면의 카드가 무리다.
        Border label = canvas.Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + "opening");
        Point onLabel = label.TranslatePoint(new Point(4, 4), top)!.Value;
        label.RaiseEvent(new PointerPressedEventArgs(
            label, pointer, top, onLabel, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Release(onLabel);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, CardOf(canvas, root.Id)!.BorderThickness.Left);
        Assert.Equal(2, CardOf(canvas, a.Id)!.BorderThickness.Left);

        // 무리 안의 a를 눌렀다 그 자리에서 놓는다 — a만 고른다.
        Border cardA = CardOf(canvas, a.Id)!;
        Point onA = cardA.TranslatePoint(new Point(20, 10), top)!.Value;
        long before = session.Editor.Revision;
        cardA.RaiseEvent(new PointerPressedEventArgs(
            cardA, pointer, top, onA, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        Release(onA);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(a.Id, session.SelectedNodeId);
        Assert.Equal(2, CardOf(canvas, a.Id)!.BorderThickness.Left);
        Assert.Equal(1, CardOf(canvas, root.Id)!.BorderThickness.Left);   // 무리가 풀렸다
        Assert.Equal(before, session.Editor.Revision);                     // 움직이지 않았으니 적은 것이 없다
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>ch01: opening(root · a), hall(b) — 위에서 아래로. 선택지 길 root→a · a→b.</summary>
    private static (GraphEditorView Graph, AuthoringSession Session, string Board) Show()
    {
        var session = new AuthoringSession();
        string directory = Path.Combine(Path.GetTempPath(), "vn-scene-fold", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string manifest = Path.Combine(directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "장면 접기" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");
        string board = session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "root", "root", 0, 0, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "a", "a", 0, 400, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "b", "b", 0, 900, sceneId: "hall");
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "다음");

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        return (graph, session, board);
    }

    private static Canvas BoardCanvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    private static Border? CardOf(Canvas canvas, string nodeId) =>
        canvas.Children.OfType<Border>().FirstOrDefault(border => border.Tag as string == nodeId);

    private static Border SceneProxy(Canvas canvas, string board, string sceneId) =>
        canvas.Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneProxyTag + board + "/" + sceneId);

    private static ShapePath? LaneOrNull(Canvas canvas, string from, string to) =>
        canvas.Children.OfType<ShapePath>().FirstOrDefault(path => path.Tag as string == $"레인:{from}>{to}");

    private static ShapePath Lane(Canvas canvas, string from, string to) =>
        LaneOrNull(canvas, from, to) ?? throw new Xunit.Sdk.XunitException($"레인 {from}>{to}가 없습니다");

    private static List<string> Texts(Control root) =>
        root.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();

    private static PointerPressedEventArgs Press(Control target, int clickCount = 1) =>
        new(target, new Pointer(0, PointerType.Mouse, true), target, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount);
}
