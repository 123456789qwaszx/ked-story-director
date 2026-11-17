using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 화면은 사람이 그 챕터로 가자고 할 때만 옮긴다</b> (2026-11-17 소유자 보고).
///
/// 모든 챕터가 한 캔버스에 서서, 활성 판은 다른 챕터의 카드를 누르기만 해도 바뀐다. 전에는 「활성 판이 바뀌면 그 판으로」가 판을
/// 다시 그릴 때만 돌아서 <b>선택지 문구를 처음 열 때</b>(문구 칸을 세우려고 판을 다시 그린다) 화면이 아까 활성이 된 챕터로 튀었다.
/// 지키는 것: 카드 누르기 · 판의 빈 곳 · [구성] 탭의 챕터 고르기로는 화면이 그대로다. 챕터 콤보 · 하이어아키의 챕터 줄은 바로
/// 그 챕터로 간다(접힌 챕터면 그 표로). 프로젝트를 연 첫 화면은 활성 챕터에 맞추고, [구성] 탭에서 열었으면 [대본] 탭이 보일 때 맞춘다.
/// </summary>
public sealed class DirectorBoardViewTests : IDisposable
{
    private const int ChapterTab = 0;
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-board-view", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 다른_챕터_카드를_누른_뒤_선택지_문구를_처음_열어도_화면이_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open(GraphTab);
        ScrollViewer scroll = Scroll(graph);

        // 둘째 챕터 카드를 누른다 — 활성 판이 그리로 바뀐다(선택을 [구성] 탭에 맞추는 길이 판을 고른다).
        CenterOn(graph, Text(graph, "먼 시작"));
        Click(window, Text(graph, "먼 시작"));
        Assert.Equal(Board(session, "ch02"), session.ActiveFileId);

        // 첫 챕터로 돌아와 선택지 문구를 처음 연다 — 판을 다시 그린다.
        CenterOn(graph, Text(graph, "가자"));
        (Vector offset, string zoom) before = (scroll.Offset, ZoomText(graph));
        Click(window, Text(graph, "가자"));

        Assert.NotNull(graph.SlotLabelBox);
        Assert.Equal(before, (scroll.Offset, ZoomText(graph)));

        window.Close();
    });

    [Fact]
    public void 판의_빈_곳으로_챕터를_골라도_화면은_그대로다() => HeadlessUi.Run(() =>
    {
        // 빈 곳 누르기(7절 R)는 하이어아키 고르기와 같은 길을 지나지만, 이미 그 자리를 보고 있다 — 옮기지 않는다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open(GraphTab);
        ScrollViewer scroll = Scroll(graph);
        CenterOn(graph, Text(graph, "먼 시작"));
        Vector before = scroll.Offset;

        // 둘째 챕터의 두 카드 사이 — 챕터 테두리 안의 빈 곳.
        Control first = CardOf(Text(graph, "먼 시작"));
        Point gap = first.TranslatePoint(new Point(first.Bounds.Width + 40, 20), window)!.Value;
        window.MouseDown(gap, MouseButton.Left);
        window.MouseUp(gap, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Board(session, "ch02"), session.ActiveFileId);
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(before, scroll.Offset);

        window.Close();
    });

    [Fact]
    public void 구성_탭에서_챕터를_골라도_대본_화면은_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open(GraphTab);
        ScrollViewer scroll = Scroll(graph);
        Vector before = scroll.Offset;

        window.FindControl<ChapterGraphView>("ChapterGraph")!.SelectChapter("ch02");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Board(session, "ch02"), session.ActiveFileId);

        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(before, scroll.Offset);

        window.Close();
    });

    [Fact]
    public void 챕터_콤보로_고르면_바로_그_챕터로_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open(GraphTab);
        Assert.False(Visible(graph, Text(graph, "먼 시작")));

        graph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem = "ch02";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Board(session, "ch02"), session.ActiveFileId);
        Assert.True(Visible(graph, Text(graph, "먼 시작")));
        Assert.True(Visible(graph, Text(graph, "먼 끝")));

        window.Close();
    });

    [Fact]
    public void 하이어아키의_챕터_줄을_고르면_그_챕터로_가고_접힌_챕터면_그_표로_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open(GraphTab);
        Assert.False(Visible(graph, Text(graph, "먼 시작")));

        PickChapterRow(graph, "ch02");
        Assert.True(Visible(graph, Text(graph, "먼 시작")));

        // 첫 챕터로 돌아갔다가, 둘째 챕터를 접고 다시 고른다 — 카드 대신 표가 선다.
        PickChapterRow(graph, "ch01");
        Assert.True(Visible(graph, Text(graph, "가자")));
        session.SetFileExpanded(Board(session, "ch02"), expanded: false);
        Dispatcher.UIThread.RunJobs();

        PickChapterRow(graph, "ch02");
        Border proxy = Canvas(graph).Children.OfType<Border>()
            .Single(border => border.Tag as string == Board(session, "ch02"));   // 접힌 판의 표는 판 Id를 진다
        Assert.True(Visible(graph, proxy));

        window.Close();
    });

    [Fact]
    public void 대본_탭에서_연_프로젝트의_첫_화면은_활성_챕터에_맞춘다() => HeadlessUi.Run(() =>
    {
        // 활성 챕터(첫 판)가 원점에서 먼 프로젝트 — 맞추지 않으면 (0, 0)에 선 화면에는 아무것도 없다.
        (MainWindow window, _, GraphEditorView graph) = Open(GraphTab, farFirst: true);

        Assert.True(Visible(graph, Text(graph, "먼 시작")));

        window.Close();
    });

    [Fact]
    public void 구성_탭에서_연_프로젝트는_대본_탭이_처음_보일_때_맞춘다() => HeadlessUi.Run(() =>
    {
        // 앱은 [구성] 탭에서 열린다 — 그때 [대본] 판은 크기가 0이라 맞출 수 없다. 보일 때 맞춘다.
        (MainWindow window, _, GraphEditorView graph) = Open(ChapterTab, farFirst: true);

        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Visible(graph, Text(graph, "먼 시작")));

        // 한 번만 — 화면을 옮겨 두고 탭을 오가도 다시 맞추지 않는다.
        ScrollViewer scroll = Scroll(graph);
        scroll.Offset = new Vector(0, 0);
        Dispatcher.UIThread.RunJobs();
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = ChapterTab;
        Dispatcher.UIThread.RunJobs();
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Vector(0, 0), scroll.Offset);

        window.Close();
    });

    // ── 준비 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 챕터 둘 — ch01은 원점 가까이(root → a, 선택지 「가자」), ch02는 멀리(4000, 3000). <paramref name="farFirst"/>면 먼 챕터 하나만
    /// 있는 프로젝트다(그것이 활성 판이다).
    /// </summary>
    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Open(int tab, bool farFirst = false)
    {
        Directory.CreateDirectory(_directory);

        var editor = new ProjectEditor(new StoryProject { Title = "판 화면" });

        if (!farFirst)
        {
            editor.EnsureChapter("ch01");
            editor.EnsureChapterBoard("ch01");
            editor.AddEpisode("ch01", "root", title: "가까운 시작", 0, 0);
            editor.AddEpisode("ch01", "a", title: "가까운 끝", 220, 0);
            editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
            Place(editor, "ch01", "root", 100, 100);
            Place(editor, "ch01", "a", 600, 100);
        }

        editor.EnsureChapter("ch02");
        editor.EnsureChapterBoard("ch02");
        editor.AddEpisode("ch02", "x", title: "먼 시작", 0, 0);
        editor.AddEpisode("ch02", "y", title: "먼 끝", 220, 0);
        editor.AddEdge("ch02", "x", "y", optionLabel: "멀리");
        Place(editor, "ch02", "x", 4000, 3000);
        Place(editor, "ch02", "y", 4500, 3000);

        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 탭을 먼저 고른다 — 「어느 탭에서 프로젝트를 열었나」가 첫 화면 맞추기의 갈림길이다.
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = tab;
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        Dispatcher.UIThread.RunJobs();

        foreach (StoryFile board in session.Project.Files)
        {
            session.SetFileExpanded(board.Id, expanded: true);
        }

        Dispatcher.UIThread.RunJobs();

        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    private static void Place(ProjectEditor editor, string chapterId, string episodeId, double x, double y) =>
        editor.MoveNode(EpisodeNaming.CardFor(editor.Project, chapterId, episodeId)!.Id, x, y);

    private static string Board(AuthoringSession session, string chapterId) => session.EnsureChapterBoard(chapterId);

    private static ScrollViewer Scroll(GraphEditorView graph) => graph.FindControl<ScrollViewer>("GraphScroll")!;

    private static Canvas Canvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static string ZoomText(GraphEditorView graph) => graph.FindControl<TextBlock>("ZoomText")!.Text ?? string.Empty;

    /// <summary>판 위에서 그 글자를 든 글 조각(카드 제목 · 선택지 문구).</summary>
    private static TextBlock Text(GraphEditorView graph, string text) =>
        Canvas(graph).GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);

    /// <summary>그 글 조각을 든 카드 — 캔버스에 바로 선 조상.</summary>
    private static Control CardOf(Control inner) =>
        inner.GetVisualAncestors().OfType<Control>().First(control => control.Parent is Canvas);

    /// <summary>그 컨트롤이 판의 화면 안에 다 들어오는가.</summary>
    private static bool Visible(GraphEditorView graph, Control control)
    {
        ScrollViewer scroll = Scroll(graph);
        Point topLeft = control.TranslatePoint(new Point(0, 0), scroll)!.Value;
        Point bottomRight = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), scroll)!.Value;

        return topLeft.X >= 0 && topLeft.Y >= 0 &&
               bottomRight.X <= scroll.Viewport.Width && bottomRight.Y <= scroll.Viewport.Height;
    }

    /// <summary>판의 화면을 그 컨트롤이 가운데 오게 옮긴다(배율은 그대로).</summary>
    private static void CenterOn(GraphEditorView graph, Control control)
    {
        ScrollViewer scroll = Scroll(graph);
        Point scaled = control.TranslatePoint(new Point(0, 0), graph.FindControl<LayoutTransformControl>("ZoomHost")!)!.Value;
        scroll.Offset = new Vector(
            Math.Max(0, scaled.X - (scroll.Viewport.Width / 2)),
            Math.Max(0, scaled.Y - (scroll.Viewport.Height / 2)));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Control control)
    {
        Point at = control.TranslatePoint(new Point(5, 5), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void PickChapterRow(GraphEditorView graph, string chapterId)
    {
        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey(chapterId))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
