using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 분기 카드</b> (2026-11-17 · branch-card B3) — 에피소드와 모양이 다르고(보라 · 「⑂ 분기」),
/// 분기점은 분기 카드에만 잇는다(에피소드에 놓으면 받지 않고 까닭을 말한다). 이어지지 않은 분기점을 빈 곳에 놓으면 그 자리에 새 분기 카드가 선다.
/// 분기 카드의 장면은 부르는 에피소드의 것이다. 분기 카드를 지우면 카드만 간다.
/// </summary>
public sealed class BranchCardBoardTests : IDisposable
{
    private const int GraphTab = MainTab.Script;

    private readonly string _directory = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "vn-branch-card-board", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 분기_카드는_머리에_분기_표가_서고_에피소드와_색이_다르다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode root, DialogueNode branch) = Stage();

        Border card = Card(window, branch.Id);
        Border episode = Card(window, root.Id);

        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "⑂ 분기");
        Assert.DoesNotContain(episode.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "⑂ 분기");
        Assert.NotEqual(((Avalonia.Media.ISolidColorBrush)card.Background!).Color,
            ((Avalonia.Media.ISolidColorBrush)episode.Background!).Color);

        window.Close();
    });

    [Fact]
    public void 분기_선은_판을_새로_지어도_장면_테두리_면_위에_깔려_곧바로_보인다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「분기 연결선의 경우 안 보이는 버그가 있네요. 저 상태에서 약간 움직이면 그제서야 보입니다」. 선을 맨 앞(0)에 끼워 테두리
        // 밑에 깔렸고, 불투명한 장면 면에 가려 이름표만 떴다.
        (MainWindow window, AuthoringSession session, _, _) = Stage();
        GraphEditorView graph = Graph(window);

        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        Assert.True(graph.EdgeCount > 0);
        Assert.Equal(0, graph.EdgesUnderFrames);

        window.Close();
    });

    [Fact]
    public void 분기점을_에피소드에_놓으면_받지_않고_까닭을_말한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode root, DialogueNode branch) = Stage();
        string lineId = root.Script.ActiveLines.First().Id;
        long before = session.Editor.Revision;

        DragKnob(window, root.Id, Card(window, EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id));

        Assert.Equal([branch.Id], session.Project.FindDialogue(root.Id)!.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Equal(before, session.Editor.Revision);
        Assert.Contains("분기 카드에만", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 이어지지_않은_분기점을_빈_곳에_놓으면_그_자리에_분기_카드가_선다_되돌리기_한_칸() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode root, DialogueNode branch) = Stage();
        string lineId = root.Script.ActiveLines.First().Id;

        // 분기 카드를 지운다 — 분기점은 이어지지 않은 채 남는다.
        session.Select(branch.Id);
        Dispatcher.UIThread.RunJobs();
        Button delete = Graph(window).FindControl<Button>("DeleteButton")!;
        Assert.Equal("분기 카드 삭제", delete.Content);
        delete.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(session.Project.FindNode(branch.Id));
        Assert.Contains("a", session.Editor.FindChapter("ch01")!.Episodes.Select(episode => episode.EpisodeId));   // 에피소드는 그대로
        Assert.Equal(1, session.Project.FindDialogue(root.Id)!.FindExtension(lineId)!.OpenDetours);

        // 이어지지 않은 분기점을 빈 곳(판 600, 500)에 놓는다.
        long before = session.Editor.Revision;
        DragKnob(window, root.Id, new Point(600, 500));

        DialogueNode caller = session.Project.FindDialogue(root.Id)!;
        string made = Assert.Single(caller.FindExtension(lineId)!.DetourTargetNodeIds);
        DialogueNode card = session.Project.FindDialogue(made)!;
        Assert.True(card.IsBranchCard);
        Assert.Equal(0, caller.FindExtension(lineId)!.OpenDetours);
        Assert.Equal(600, card.Layout.X, 1);
        Assert.Equal(before + 1, session.Editor.Revision);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(session.Project.FindNode(made));
        Assert.Equal(1, session.Project.FindDialogue(root.Id)!.FindExtension(lineId)!.OpenDetours);

        window.Close();
    });

    [Fact]
    public void 분기_카드는_부르는_에피소드의_장면_테두리_안에_든다() => HeadlessUi.Run(() =>
    {
        // root는 장면 opening — 분기 카드를 멀리(1500, 900) 옮겨도 opening 테두리가 그 카드까지 감싼다. 장면을 저장하지 않는다.
        (MainWindow window, AuthoringSession session, _, DialogueNode branch) = Stage();
        session.Editor.MoveNode(branch.Id, 1500, 900);
        Dispatcher.UIThread.RunJobs();

        Border card = Card(window, branch.Id);
        Rect cardRect = new(Avalonia.Controls.Canvas.GetLeft(card), Avalonia.Controls.Canvas.GetTop(card), card.Bounds.Width, card.Bounds.Height);

        Assert.Contains(SceneFrames(window), frame => frame.Contains(cardRect));

        window.Close();
    });

    [Fact]
    public void 하이어아키는_부르는_에피소드_밑에_분기_카드_줄을_세우고_누르면_두_탭이_그_카드로_간다() => HeadlessUi.Run(() =>
    {
        // 소유자(2026-11-17) — 분기 카드는 부르는 에피소드 밑에 「⑂ 이름」 줄로. [대본]은 그 카드로, [구성]은 그 칩(인스펙터)으로.
        (MainWindow window, AuthoringSession session, _, DialogueNode branch) = Stage();
        StoryHierarchy director = Graph(window).HierarchyView;
        StoryHierarchy chapter = window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;
        string key = StoryHierarchy.BranchKey("ch01", branch.Id);

        // [대본]은 제목, [구성]은 「ID로 보기」(처음 켜짐)라 ID(= Yarn 노드 이름)로 선다(2026-11-23 — 에피소드 줄과 같다 · ChapterIdViewTests).
        foreach ((StoryHierarchy tree, string text) in new[] { (director, $"⑂ {EpisodeNaming.BranchTitleOf(branch)}"), (chapter, $"⑂ {branch.Name}") })
        {
            tree.Show(new HierarchyPick(HierarchyKind.Episode, "ch01", EpisodeId: "root"));   // 그 길을 편다
            Dispatcher.UIThread.RunJobs();

            List<HierarchyRow> rows = tree.Rows.ToList();
            int root = rows.FindIndex(row => row.Kind == HierarchyKind.Episode && row.EpisodeId == "root");
            HierarchyRow row = rows[root + 1];
            Assert.Equal((HierarchyKind.Branch, key, text), (row.Kind, row.Key, row.Text));
            Assert.Equal(rows[root].Depth + 1, row.Depth);
        }

        // [대본] — 누르면 그 카드를 고른다.
        director.RowButton(key)!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(branch.Id, session.SelectedNodeId);

        // [구성] — 누르면 그 칩을 고르고 인스펙터에 분기 카드가 선다.
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Structure;
        Dispatcher.UIThread.RunJobs();
        chapter.RowButton(key)!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.True(chapterGraph.FindControl<StackPanel>("BranchPanel")!.IsVisible);
        Assert.Contains(EpisodeNaming.BranchTitleOf(branch), chapterGraph.FindControl<TextBlock>("InspectorTitle")!.Text, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 판에서_분기_카드를_고르면_하이어아키의_그_줄이_칠해진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, _, DialogueNode branch) = Stage();

        session.Select(branch.Id);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(StoryHierarchy.BranchKey("ch01", branch.Id), Graph(window).HierarchyView.Selection?.Key);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// [대본] 탭의 ch01(root · a — opening, b — hall) — root 첫 줄에 「분기 추가」로 분기 카드 하나. 판에서 root를 (60, 60)에 둔다.
    /// </summary>
    private (MainWindow Window, AuthoringSession Session, DialogueNode Root, DialogueNode Branch) Stage()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode root = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        session.Editor.MoveNode(root.Id, 60, 60);
        DialogueNode branch = session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        session.Editor.MoveNode(branch.Id, 60, 400);
        session.SelectFile(session.EnsureChapterBoard("ch01"), reveal: false);
        Dispatcher.UIThread.RunJobs();
        Graph(window).Rebuild();
        Dispatcher.UIThread.RunJobs();

        return (window, session, root, branch);
    }

    private static GraphEditorView Graph(MainWindow window) => window.FindControl<GraphEditorView>("Graph")!;

    private static Canvas Canvas(MainWindow window) => Graph(window).FindControl<Canvas>("GraphCanvas")!;

    private static Border Card(MainWindow window, string nodeId) =>
        Canvas(window).Children.OfType<Border>().Single(border => border.Tag as string == nodeId);

    private static Ellipse Knob(MainWindow window, string nodeId) =>
        Canvas(window).Children.OfType<Ellipse>().Single(knob => (knob.Tag as string)?.StartsWith(
            $"{GraphEditorView.LineKnobTag}execution:{nodeId}:detour:", StringComparison.Ordinal) == true);

    /// <summary>장면 테두리들 — 눌리지 않는 둥근 Border(장면 프레임)의 자리.</summary>
    private static List<Rect> SceneFrames(MainWindow window) =>
        Canvas(window).Children.OfType<Border>()
            .Where(border => !border.IsHitTestVisible && border.Child is null && border.CornerRadius.TopLeft == 8)
            .Select(border => new Rect(Avalonia.Controls.Canvas.GetLeft(border), Avalonia.Controls.Canvas.GetTop(border), border.Width, border.Height))
            .ToList();

    private static void DragKnob(MainWindow window, string nodeId, Border target) =>
        DragKnob(window, nodeId, target.TranslatePoint(new Point(target.Bounds.Width / 2, 12), Canvas(window))!.Value);

    /// <summary>그 카드의 분기점을 끌어 판의 그 자리에 놓는다.</summary>
    private static void DragKnob(MainWindow window, string nodeId, Point onCanvas)
    {
        Ellipse knob = Knob(window, nodeId);
        Point from = Canvas(window).TranslatePoint(
            new Point(Avalonia.Controls.Canvas.GetLeft(knob) + (knob.Width / 2), Avalonia.Controls.Canvas.GetTop(knob) + (knob.Height / 2)), window)!.Value;
        Point to = Canvas(window).TranslatePoint(onCanvas, window)!.Value;

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
