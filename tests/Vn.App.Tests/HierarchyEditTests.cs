using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>하이어아키에서 고치기</b> (2026-10-30 HY4a — <c>docs/plans/hierarchy.md</c>) — 우클릭 차림표와 두 번 눌러 이름 바꾸기.
///
/// 「챕터 · 장면 · 에피소드 추가」는 어느 줄에서나 같은 자리에 서고(어디서 눌러야 무엇이 나오는지 외울 것이 없게) 어디에
/// 붙일지는 누른 줄이 정한다. 그 아래에 그 줄만의 일이 선다. 새 장면은 첫 에피소드와 함께 선다 — 장면은 에피소드가
/// 장면ID를 들어야 있는 것이다.
/// </summary>
public sealed class HierarchyEditTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-edit", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 추가_셋은_어느_줄에서나_같고_그_아래에_그_줄의_일이_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);

        string[] adds = ["챕터 추가", "장면 추가", "에피소드 추가"];

        Assert.Equal(["챕터 추가"], Headers(tree, StoryHierarchy.ProjectKey));
        Assert.Equal([.. adds, "이름 바꾸기 · 제거…"], Headers(tree, StoryHierarchy.ChapterKey("ch01")));
        Assert.Equal([.. adds, "이름 바꾸기", "장면 풀기 (에피소드는 남는다)"], Headers(tree, StoryHierarchy.SceneKey("ch01", "opening")));
        Assert.Equal([.. adds, "이름 바꾸기", "에피소드 지우기"], Headers(tree, StoryHierarchy.EpisodeKey("ch01", "root")));

        window.Close();
    });

    [Fact]
    public void 에피소드_줄의_에피소드_추가는_그_뒤에_같은_장면으로_간선을_달고_서고_두_탭이_그것을_고른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);

        Click(tree, StoryHierarchy.EpisodeKey("ch01", "root"), "에피소드 추가");

        ChapterEpisode added = Assert.Single(Chapter(session).Episodes, episode => episode.EpisodeId == "new01");
        Assert.Equal("opening", added.SceneId);
        Assert.Contains(Chapter(session).Edges, edge => edge.FromEpisodeId == "root" && edge.ToEpisodeId == "new01");
        Assert.Equal(EpisodeNaming.CardFor(session.Project, "ch01", "new01")!.Id, session.SelectedNodeId);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "new01"), tree.Selection?.Key);

        // 되돌리기 한 칸 — 에피소드 · 간선 · 장면이 함께 물러난다.
        session.Editor.Undo();
        Assert.DoesNotContain(Chapter(session).Episodes, episode => episode.EpisodeId == "new01");

        window.Close();
    });

    [Fact]
    public void 장면_추가는_첫_에피소드와_함께_새_장면을_세운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;

        Click(tree, StoryHierarchy.ChapterKey("ch01"), "장면 추가");
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode added = Assert.Single(Chapter(session).Episodes, episode => episode.EpisodeId == "new01");
        Assert.Equal("scene01", added.SceneId);
        Assert.Contains(Chapter(session).Edges, edge => edge.ToEpisodeId == "new01");   // 떨어진 섬을 만들지 않는다
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.SceneKey("ch01", "scene01"));

        window.Close();
    });

    [Fact]
    public void 장면을_풀면_에피소드는_남고_에피소드를_지우면_사라진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);

        Click(tree, StoryHierarchy.SceneKey("ch01", "hall"), "장면 풀기 (에피소드는 남는다)");
        Assert.True(string.IsNullOrEmpty(Chapter(session).Episodes.Single(episode => episode.EpisodeId == "b").SceneId));

        Click(tree, StoryHierarchy.EpisodeKey("ch02", "y"), "에피소드 지우기");
        Assert.DoesNotContain(Chapter(session, "ch02").Episodes, episode => episode.EpisodeId == "y");

        window.Close();
    });

    [Fact]
    public void 두_번_눌러_이름을_바꾸고_Esc는_없던_일로_한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);

        // 에피소드 — 간선 · 카드가 함께 따라가는 개명(EpisodeRenamer).
        Rename(tree, StoryHierarchy.EpisodeKey("ch02", "x"), "x2", Key.Enter);
        Assert.NotNull(Chapter(session, "ch02").FindEpisode("x2"));
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x2"));

        // 장면 — 그 장면의 에피소드 모두의 장면ID.
        Rename(tree, StoryHierarchy.SceneKey("ch01", "hall"), "lobby", Key.Enter);
        Assert.Equal("lobby", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "b").SceneId);

        // Esc — 없던 일.
        Rename(tree, StoryHierarchy.EpisodeKey("ch02", "y"), "zzz", Key.Escape);
        Assert.NotNull(Chapter(session, "ch02").FindEpisode("y"));
        Assert.Null(tree.RenameBox);

        window.Close();
    });

    [Fact]
    public void 첫_누름은_줄을_다시_세우지_않아_둘째_누름이_같은_줄에_떨어진다() => HeadlessUi.Run(() =>
    {
        // 두 번 눌러 이름 고치기가 서는 조건 — 첫 누름(고르기)이 판 전환 · 탭 맞추기 · 하이어아키 다시 그리기를 부르는데,
        // 그때 줄이 새 컨트롤로 갈리면 둘째 누름이 새 단추에 떨어져 두 번 누름이 성립하지 않는다(옛 탐색기가 실제로 겪었다).
        // ⚠ 헤드리스는 두 번 누름의 시간 창이 없어 합성 더블클릭을 못 만든다 — 그래서 같은 단추에 떨어지는지를 잰다.
        (MainWindow window, StoryHierarchy tree) = Open();
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        string key = StoryHierarchy.EpisodeKey("ch02", "x");

        // 하이어아키 칸은 높이 200이다 — ch01을 접어 x 줄이 칸 안에 보이게 한다.
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: true);
        Dispatcher.UIThread.RunJobs();
        Button row = tree.RowButton(key)!;
        Point spot = row.TranslatePoint(new Point(20, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(key, tree.Selection?.Key);                 // 정말 골랐다 — 챕터가 바뀌고 카드가 골라졌다
        Assert.Equal("ch02", window.SessionProbe.ActiveFile?.Name);
        Assert.Same(row, tree.RowButton(key));                   // 그래도 줄은 그대로다

        // 이름 칸은 그 줄의 이름(Id)을 들고 열린다.
        tree.BeginRename(key);
        Assert.Equal("x", tree.RenameBox!.Text);

        window.Close();
    });

    [Fact]
    public void 에피소드를_다른_장면_줄로_끌면_그_장면으로_옮겨진다() => HeadlessUi.Run(() =>
    {
        // HY4b — 장면은 장면ID로 묶이는 것이라, 에피소드를 다른 장면에 놓는 것이 곧 그 에피소드의 장면ID를 바꾸는 일이다.
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        Dispatcher.UIThread.RunJobs();   // 다시 세운 줄들이 자리를 잡는다

        // 실제 손으로 — 누르고, 장면 줄 위로 옮기고, 놓는다(단추가 포인터를 잡아도 놓은 자리는 좌표로 찾는다).
        Button source = tree.RowButton(StoryHierarchy.EpisodeKey("ch01", "a"))!;
        Button target = tree.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!;
        Point from = source.TranslatePoint(new Point(30, source.Bounds.Height / 2), window)!.Value;
        Point to = target.TranslatePoint(new Point(30, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("hall", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "a").SceneId);

        // 되돌리기 한 칸.
        session.Editor.Undo();
        Assert.Equal("opening", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "a").SceneId);

        // 받지 않는 자리 — 같은 장면 · 에피소드 줄 · 다른 챕터.
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "a"), StoryHierarchy.SceneKey("ch01", "opening"));
        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "a"), StoryHierarchy.EpisodeKey("ch01", "root"));
        tree.Drag(StoryHierarchy.EpisodeKey("ch02", "x"), StoryHierarchy.SceneKey("ch01", "hall"));
        Assert.Equal("opening", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "a").SceneId);
        Assert.True(string.IsNullOrEmpty(Chapter(session, "ch02").Episodes.Single(episode => episode.EpisodeId == "x").SceneId));

        window.Close();
    });

    [Fact]
    public void 접힘은_두_탭이_함께_쓰고_프로젝트를_다시_열어도_남는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        StoryHierarchy director = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;

        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(director.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x"));   // 연출 쪽도 펴졌다

        director.Fold(StoryHierarchy.SceneKey("ch01", "hall"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "b"));      // 챕터 쪽도 펴졌다
        window.Close();

        // 같은 프로젝트를 다시 연다 — 사람이 편 것이 그대로다(사용자 설정에 프로젝트별로).
        (MainWindow again, StoryHierarchy reopened) = Open();
        Assert.Contains(reopened.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x"));
        Assert.Contains(reopened.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "b"));
        again.Close();
    });

    [Fact]
    public void 화살표로_고르고_접고_편다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();

        // 시작하면 세션이 고른 첫 카드(root)가 골라져 있다.
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);

        Press(tree, Key.Down);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "a"), tree.Selection?.Key);
        Assert.Equal("a", window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<TextBox>("IdBox")!.Text);

        Press(tree, Key.Up);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);

        // ← 에피소드에서 = 부모(장면)로, 다시 ← = 그 장면을 접는다, → = 편다, 다시 → = 첫 자식으로.
        Press(tree, Key.Left);
        string opening = StoryHierarchy.SceneKey("ch01", "opening");
        Assert.Equal(opening, tree.Selection?.Key);

        Press(tree, Key.Left);
        Assert.DoesNotContain(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "root"));

        Press(tree, Key.Right);
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "root"));

        Press(tree, Key.Right);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static void Press(StoryHierarchy tree, Key key)
    {
        tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Dispatcher.UIThread.RunJobs();
    }

    private (MainWindow Window, StoryHierarchy Tree) Open()
    {
        (MainWindow window, _) = HierarchyFixture.Open(_directory);
        return (window, window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView);
    }

    private static ChapterGraphModel Chapter(AuthoringSession session, string chapterId = "ch01") =>
        session.Editor.FindChapter(chapterId)!.ToGraphModel(chapterId, session.Definition);

    private static IEnumerable<MenuItem> Items(StoryHierarchy tree, string key) =>
        ((IEnumerable<object>)tree.RowButton(key)!.ContextMenu!.ItemsSource!).OfType<MenuItem>();

    private static string[] Headers(StoryHierarchy tree, string key) =>
        Items(tree, key).Select(item => (string)item.Header!).ToArray();

    /// <summary>그 줄의 차림표에서 그 항목을 누른다.</summary>
    private static void Click(StoryHierarchy tree, string key, string header)
    {
        Items(tree, key).Single(item => (string?)item.Header == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>그 줄의 이름 칸을 열고 적은 뒤 그 키를 누른다.</summary>
    private static void Rename(StoryHierarchy tree, string key, string text, Key press)
    {
        tree.BeginRename(key);
        TextBox box = tree.RenameBox!;
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = press });
        Dispatcher.UIThread.RunJobs();
    }
}
