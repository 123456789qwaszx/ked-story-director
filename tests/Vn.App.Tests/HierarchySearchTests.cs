using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
/// <b>하이어아키 찾기</b> (2026-11-23 소유자 — 「구성과 대본의 하이어아키에 검색기능만 추가합시다. 이게 생각보다 에피소드가 많을 때, 특정 에피소드 찾는게
/// 힘들더라고요」 · <c>docs/plans/hierarchy.md</c> §6). 위의 찾기 칸에 치는 동안 맞는 줄과 그 위 줄만 남고(접어 둔 챕터 안도), ID와 이름 둘 다로 · 자음만으로도
/// 찾는다. Enter는 첫 맞는 줄을 고르고(판이 따른다) Esc는 지운다 — 접힘은 그대로 돌아오고 고른 줄이 보인다. 두 탭 모두.
/// </summary>
public sealed class HierarchySearchTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-search", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 치는_동안_맞는_줄과_그_위_줄만_남고_접어_둔_챕터_안도_찾으며_지우면_접힘이_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, StoryHierarchy tree) = Open(MainTab.Script);
        string ch02 = StoryHierarchy.ChapterKey("ch02");
        tree.Fold(ch02, collapse: true);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(tree.Rows, row => row.ChapterId == "ch02" && row.Kind == HierarchyKind.Episode);
        List<string> before = tree.Rows.Select(row => row.Key).ToList();

        Search(tree, "숲");

        Assert.Equal(
            [
                StoryHierarchy.ProjectKey,
                StoryHierarchy.ChapterKey("ch01"), StoryHierarchy.SceneKey("ch01", "opening"), StoryHierarchy.EpisodeKey("ch01", "a"),
                ch02, StoryHierarchy.SceneKey("ch02", "street"), StoryHierarchy.EpisodeKey("ch02", "y")
            ],
            tree.Rows.Select(row => row.Key));
        Assert.Equal(2, tree.SearchHits);
        Assert.Contains(ch02, tree.Folds.Collapsed);   // 찾는 동안 펴 보여도 접힘은 그대로다

        // 지우면 접어 둔 대로 돌아온다.
        Search(tree, string.Empty);
        Assert.Equal(before, tree.Rows.Select(row => row.Key));
        Assert.False(tree.Searching);
        window.Close();
    });

    [Fact]
    public void ID와_이름_둘_다로_자음만으로도_찾고_맞는_것이_없으면_말한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, _, StoryHierarchy tree) = Open(MainTab.Script);

        // 자음 — 「ㅅㅈ」는 시작 · 시장.
        Search(tree, "ㅅㅈ");
        Assert.Equal(["root", "x"], Episodes(tree));

        // [대본] 탭은 이름으로 서 있어도 ID로 찾는다 — 대소문자는 가리지 않는다.
        Search(tree, "B");
        Assert.Equal(["b"], Episodes(tree));
        Assert.Equal("성문 앞", tree.Rows.Single(row => row.EpisodeId == "b").Text);

        // 장면 · 챕터도 — 장면 이름이 맞으면 그 장면 줄까지(그 안의 에피소드는 맞을 때만).
        Search(tree, "street");
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.SceneKey("ch02", "street"));
        Assert.Empty(Episodes(tree));

        Search(tree, "없는말");
        Assert.Equal([StoryHierarchy.ProjectKey], tree.Rows.Select(row => row.Key));
        Assert.Equal(0, tree.SearchHits);
        Assert.Contains(tree.FindControl<StackPanel>("RowHost")!.Children.OfType<TextBlock>(),
            block => block.Text == "「없는말」에 맞는 것이 없습니다.");
        window.Close();
    });

    [Fact]
    public void Enter는_첫_맞는_줄을_고르고_판이_따르며_Esc는_지우고_고른_줄이_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, StoryHierarchy tree) = Open(MainTab.Script);
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: true);
        Dispatcher.UIThread.RunJobs();

        TextBox box = tree.SearchBox;
        Click(window, box);
        box.Text = "오두막";
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(StoryHierarchy.EpisodeKey("ch02", "y"), tree.Selection?.Key);
        Assert.Equal(EpisodeNaming.CardFor(session.Project, "ch02", "y")!.Id, session.SelectedNodeId);
        Assert.Equal("오두막", box.Text);   // 고른 뒤에도 찾은 그대로다

        // ↓ 키 — 하이어아키가 받는다(첫 줄로 간 뒤라 초점이 거기 있다). 찾은 줄 사이로 내려간다 — 남은 줄이 그것뿐이라 그대로다.
        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(StoryHierarchy.EpisodeKey("ch02", "y"), tree.Selection?.Key);

        // Esc — 찾기 칸에서 지운다. 접힘은 돌아오되 고른 줄은 보인다(그 길만 펴진다).
        Click(window, box);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, box.Text ?? string.Empty);
        Assert.False(tree.Searching);
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "y"));
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x"));
        Assert.Equal(StoryHierarchy.EpisodeKey("ch02", "y"), tree.Selection?.Key);
        window.Close();
    });

    [Fact]
    public void 구성_탭의_하이어아키도_ID로_서_있어도_이름으로_찾는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, _, StoryHierarchy tree) = Open(MainTab.Structure);
        Assert.True(tree.ReadOnly);

        Search(tree, "숲의");
        HierarchyRow found = Assert.Single(tree.Rows, row => row.Kind == HierarchyKind.Episode);
        Assert.Equal(("a", "a"), (found.EpisodeId, found.Text));   // [구성] 탭은 ID로 선다 — 이름(「숲의 입구」)으로 찾았다
        Assert.Equal(1, tree.SearchHits);

        // 찾는 동안 삼각형은 접지 않는다.
        Border arrow = tree.GetVisualDescendants().OfType<Border>()
            .First(border => border.Tag is string tag && tag == "fold:" + StoryHierarchy.ChapterKey("ch01"));
        Assert.Contains("찾는 동안", (string)ToolTip.GetTip(arrow)!, StringComparison.Ordinal);
        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 챕터 둘 — ch01: 장면 opening(root 「시작」 · a 「숲의 입구」) · hall(b 「성문 앞」), ch02: 장면 street(x 「시장」 · y 「숲속 오두막」).
    /// </summary>
    private (MainWindow Window, AuthoringSession Session, StoryHierarchy Tree) Open(int tab)
    {
        Directory.CreateDirectory(_directory);

        var editor = new ProjectEditor(new StoryProject { Title = "찾기" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "시작", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "a", title: "숲의 입구", 220, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "b", title: "성문 앞", 440, 0, sceneId: "hall");
        editor.EnsureChapter("ch02");
        editor.EnsureChapterBoard("ch02");
        editor.AddEpisode("ch02", "x", title: "시장", 0, 0, sceneId: "street");
        editor.AddEpisode("ch02", "y", title: "숲속 오두막", 220, 0, sceneId: "street");

        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = tab;
        Dispatcher.UIThread.RunJobs();

        StoryHierarchy tree = tab == MainTab.Script
            ? window.FindControl<GraphEditorView>("Graph")!.HierarchyView
            : window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;

        return (window, session, tree);
    }

    private static void Search(StoryHierarchy tree, string text)
    {
        tree.SearchBox.Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string?> Episodes(StoryHierarchy tree) =>
        tree.Rows.Where(row => row.Kind == HierarchyKind.Episode).Select(row => row.EpisodeId).ToList();

    private static void Click(MainWindow window, Control control)
    {
        Point center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
