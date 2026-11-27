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
    // 줄 상자의 치수 — GraphEditorView의 ProxyHeaderHeight · ProxyRowHeight · ProxyWidth와 같다.
    private const double HeaderHeight = 50;
    private const double RowHeight = 28;
    private const double ProxyWidth = 250;

    [Fact]
    public void 장면을_접으면_그_장면만_줄_상자가_되고_다른_장면의_카드와_챕터_박스는_선다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session, string board) = Show();

        session.SetSceneCollapsed(board, "hall", collapsed: true);
        graph.Rebuild();

        Canvas canvas = BoardCanvas(graph);
        Border proxy = SceneProxy(canvas, board, "hall");

        // 이름은 상자 위 바깥의 장면 이름표가 말한다 — 상자 머리에는 접힘 · 수만(2026-11-27).
        Assert.Contains(Texts(SceneLabel(graph, "hall")), text => text.StartsWith("장면 hall", StringComparison.Ordinal) && text.EndsWith("›", StringComparison.Ordinal));
        Assert.DoesNotContain(Texts(proxy), text => text.Contains("hall", StringComparison.Ordinal));
        Assert.Null(CardOf(canvas, Card(session, "b").Id));
        Assert.NotNull(CardOf(canvas, Card(session, "root").Id));
        Assert.NotNull(CardOf(canvas, Card(session, "a").Id));

        // 챕터 박스는 그대로다 — 판째 접힌 것이 아니다(그 이름표는 판 위의 화면 겹에 선다).
        Assert.Contains(Labels(graph).Children.OfType<Border>(), border =>
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
    public void 상자가_출발보다_왼쪽이어도_길은_줄의_입구_왼쪽으로_든다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 「다른 장면에서 Root로 연결되는 것 역시, 선택지보다 왼쪽에 있더라도 … 입구로 연결되는 게 맞는 것 같은데, 왼쪽에 있을 경우에는
        // 출구로 연결되어버리는 현상」. 줄의 왼쪽 점이 입구, 오른쪽 점이 출구다.
        (GraphEditorView graph, AuthoringSession session, string board) = Show(openingX: 1200, hallX: 0);

        session.SetSceneCollapsed(board, "hall", collapsed: true);
        graph.Rebuild();

        Canvas canvas = BoardCanvas(graph);
        Border proxy = SceneProxy(canvas, board, "hall");
        double left = Canvas.GetLeft(proxy);
        double rowY = Canvas.GetTop(proxy) + HeaderHeight + (RowHeight / 2);

        // a(오른쪽 위) → b(왼쪽 아래 상자): 길은 상자의 왼쪽 바깥으로 돌아 줄 높이에서 끝난다 — 상자 위로 건너 상자를 가로지르지 않는다.
        ShapePath lane = Lane(canvas, "a", "b");
        Assert.True(lane.Data!.Bounds.Left < left, $"길이 상자 왼쪽 바깥으로 돌아야 한다 — {lane.Data.Bounds.Left} ≥ {left}");
        Assert.Equal(rowY, lane.Data.Bounds.Bottom, precision: 1);

        // 화살촉은 줄의 왼쪽 점(입구)에 닿는다 — 오른쪽 점(출구)에는 없다.
        List<Avalonia.Controls.Shapes.Polygon> arrows = canvas.Children.OfType<Avalonia.Controls.Shapes.Polygon>()
            .Where(arrow => Math.Abs(Canvas.GetTop(arrow) - rowY) < 0.5)
            .ToList();
        Assert.Contains(arrows, arrow => Math.Abs(Canvas.GetLeft(arrow) - left) < 0.5);
        Assert.DoesNotContain(arrows, arrow => Math.Abs(Canvas.GetLeft(arrow) - (left + ProxyWidth)) < 0.5);
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

        Border label = Labels(graph).Children.OfType<Border>()
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
    public void 접어도_장면_이름표는_펼쳤을_때와_같은_자리에_같은_모양으로_서고_두_번_누르면_펴진다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 접으면 크고 또렷하던 이름이 사라지던 것(「당신이 권하는 방향대로」). 접어도 같은 이름표가 상자의 왼위 바깥 — 펼쳤을 때와 같은
        // 자리에 선다. 끝의 표만 「⌄」(펼침) → 「›」(접힘)이다.
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        Settle(graph);
        Rect before = Place(graph, GraphEditorView.SceneLabelTag + "hall");

        session.SetSceneCollapsed(board, "hall", collapsed: true);
        graph.Rebuild();
        Settle(graph);

        Rect after = Place(graph, GraphEditorView.SceneLabelTag + "hall");
        Assert.Equal(before.X, after.X, precision: 1);
        Assert.Equal(before.Bottom, after.Bottom, precision: 1);
        Assert.Equal(before.Height, after.Height, precision: 1);   // 같은 크기 규칙

        // 이름 앞의 점도 그대로 — 그 장면의 색이다.
        Border label = SceneLabel(graph, "hall");
        Assert.Contains(((Panel)label.Child!).Children, child => Equals(child.Tag, GraphEditorView.SceneDotTag));

        // 두 번 누르면 편다(펼친 이름표를 두 번 누르면 접는 것의 짝).
        label.RaiseEvent(Press(label));
        Assert.NotEmpty(session.CollapsedScenes);   // 한 번은 펴지 않는다 — 그 장면을 고르고 끌 채비만
        label.RaiseEvent(Press(label, clickCount: 2));
        Assert.Empty(session.CollapsedScenes);
    });

    [Fact]
    public void 이름표를_쉬지_않고_누르면_두_번마다_한_번_접고_편다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 「한번 더블클릭을 해서 상태를 바꾼 다음에는, 특정 시간 내에 한번만 클릭해도 바로 더블클릭을 한 듯 상태가 바뀝니다」. 접은 뒤
        // 같은 자리에 선 접힌 이름표를 셋째로 누르면(운영체제의 셈 3) 한 번 누름이다 — 펴지 않는다. 넷째가 두 번 누름이라 편다. ⛔ 그 전에는 2 이상이 모두
        // 두 번 누름이라 셋째에 곧바로 다시 펴졌다.
        (GraphEditorView graph, AuthoringSession session, string board) = Show();
        var states = new List<bool>();

        for (int clicks = 1; clicks <= 6; clicks++)
        {
            graph.Rebuild();
            Settle(graph);
            Border label = SceneLabel(graph, "hall");   // 접으면 이름표가 접힌 상자의 것으로 갈린다 — 그 자리의 이름표를 누른다
            label.RaiseEvent(Press(label, clickCount: clicks));
            Dispatcher.UIThread.RunJobs();
            states.Add(session.CollapsedScenes.Contains(new Vn.Authoring.Graph.GraphSceneKey(board, "hall")));
        }

        Assert.Equal([false, true, true, false, false, true], states);
    });

    [Fact]
    public void 접힌_판의_표에도_챕터_이름표가_표_위에_서고_두_번_누르면_펴진다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 접힌 챕터도 펼쳤을 때와 같은 챕터 이름표(먹색 면 · 흰 글)가 표의 위 바깥에 선다. 표의 폭으로 말줄임하지 않는다.
        (GraphEditorView graph, AuthoringSession session, string board) = Show();

        session.SetFileExpanded(board, expanded: false);
        graph.Rebuild();
        Settle(graph);

        Border label = Labels(graph).Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.ChapterLabelTag + "ch01");
        Assert.Equal("챕터 ch01 ›", ((TextBlock)label.Child!).Text);

        // 표의 위 바깥 — 이름표의 아래 끝이 표의 위 끝에 붙는다(화면 겹의 좌표로 잰다).
        Border table = BoardCanvas(graph).Children.OfType<Border>().Single(border => border.Tag as string == board);
        Point top = table.TranslatePoint(new Point(0, 0), Labels(graph))!.Value;
        var placed = graph.FrameLabelPlaces.Single(item => item.Key == GraphEditorView.ChapterLabelTag + "ch01");
        Assert.Equal(GraphEditorView.FrameLabelPlace.Above, placed.Place);
        Assert.Equal(top.Y, placed.Screen.Bottom, precision: 0);
        // 머리에는 이름이 없다 — 접힘 · 노드 수와 파일 자리만(파일 자리에는 판 이름이 든다).
        Assert.DoesNotContain(Texts(table), text => text == "ch01");
        Assert.Contains(Texts(table), text => text.StartsWith("접힘", StringComparison.Ordinal));

        label.RaiseEvent(Press(label, clickCount: 2));
        Assert.Contains(board, session.ExpandedFileIds);
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

        Border label = Labels(graph).Children.OfType<Border>()
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
        Border label = Labels(graph).Children.OfType<Border>()
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

    /// <summary>ch01: opening(root · a), hall(b) — 위에서 아래로. 선택지 길 root→a · a→b. 장면마다 가로 자리를 줄 수 있다.</summary>
    private static (GraphEditorView Graph, AuthoringSession Session, string Board) Show(double openingX = 0, double hallX = 0)
    {
        var session = new AuthoringSession();
        string directory = Path.Combine(Path.GetTempPath(), "vn-scene-fold", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        string manifest = Path.Combine(directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "장면 접기" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");
        string board = session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "root", "root", openingX, 0, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "a", "a", openingX, 400, sceneId: "opening");
        session.Editor.AddEpisode("ch01", "b", "b", hallX, 900, sceneId: "hall");
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

    /// <summary>챕터 · 장면 이름표가 서는 화면 겹(design/board-zoom.md 4절).</summary>
    private static Canvas Labels(GraphEditorView graph) => graph.FindControl<Canvas>("FrameLabelLayer")!;

    private static Border SceneLabel(GraphEditorView graph, string sceneId) =>
        Labels(graph).Children.OfType<Border>().Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + sceneId);

    /// <summary>배치를 마치고 이름표를 지금 배율 · 스크롤에 다시 세운다 — 화면 겹의 자리를 잴 수 있게.</summary>
    private static void Settle(GraphEditorView graph)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
        }

        graph.PlaceFrameLabels();
    }

    /// <summary>그 이름표가 선 자리(화면 겹의 좌표).</summary>
    private static Rect Place(GraphEditorView graph, string key) => graph.FrameLabelPlaces.Single(item => item.Key == key).Screen;

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
