using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>우측 기둥 위의 하이어아키</b> (2026-10-30 소유자: *"유니티 하이어아키처럼 챕터를 부모로 하는 장면이 있고, 이 장면에
/// 포함된 에피소드들을 모으는 방식으로"* — <c>docs/plans/hierarchy.md</c> HY1).
///
/// 프로젝트 → 챕터 → 장면 → 에피소드를 접었다 편다. 어느 줄이든 고르면 챕터 그래프가 그것을 고르고 아래 칸이 따라
/// 바뀐다(프로젝트 → 화자, 챕터 → 챕터, 장면 · 에피소드 → 편집). 판에서 고른 것은 트리에 비친다.
/// </summary>
public sealed class StoryHierarchyTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 프로젝트_아래에_챕터가_서고_판에서_고른_에피소드의_챕터와_장면이_펴진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();

        // 처음에는 챕터들이 접혀 있다 — 챕터 수십 개가 다 펴져 있으면 하이어아키가 아니라 목록이다.
        Assert.Equal([HierarchyKind.Project, HierarchyKind.Chapter, HierarchyKind.Chapter], tree.Rows.Select(row => row.Kind));
        Assert.Equal(["ch01", "ch02"], tree.Rows.Skip(1).Select(row => row.ChapterId));

        // 판에서 고르면 가는 길이 펴지고 그 줄이 골라진다. 다른 장면은 접힌 채다.
        graph.SelectChapter("ch01");
        graph.SelectEpisode("b");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "b"), tree.Selection?.Key);
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.SceneKey("ch01", "hall"));
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.SceneKey("ch01", "opening"));
        Assert.DoesNotContain(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "root"));
        HierarchyRow b = Assert.Single(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "b"));
        Assert.Equal(3, b.Depth);   // 프로젝트 0 · 챕터 1 · 장면 2 · 에피소드 3

        window.Close();
    });

    [Fact]
    public void 장면ID를_하나도_안_적은_챕터는_장면_단_없이_에피소드가_챕터_바로_아래에_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, _, StoryHierarchy tree) = Show();

        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);

        List<HierarchyRow> under = tree.Rows.SkipWhile(row => row.ChapterId != "ch02").Skip(1).ToList();
        Assert.Equal([HierarchyKind.Episode, HierarchyKind.Episode], under.Select(row => row.Kind));
        Assert.All(under, row => Assert.Equal(2, row.Depth));
        Assert.All(under, row => Assert.False(row.IsSceneRoot));   // 모두가 제 장면의 루트라 표식이 뜻이 없다
        Assert.Contains("장면 미지정", tree.Rows.Single(row => row.Key == StoryHierarchy.ChapterKey("ch02")).Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(tree.Rows, row => row.Text.StartsWith("__scene_", StringComparison.Ordinal));

        window.Close();
    });

    [Fact]
    public void 에피소드_줄을_누르면_챕터_그래프가_그것을_고르고_편집_칸이_선다_줄은_다시_세우지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        graph.FindControl<TabControl>("RightTabs")!.SelectedItem = graph.FindControl<TabItem>("SpeakerTab");

        string key = StoryHierarchy.EpisodeKey("ch02", "y");
        Button row = tree.RowButton(key)!;
        row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(key, tree.Selection?.Key);
        Assert.Equal("ch02", graph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem);
        Assert.Equal("y", graph.FindControl<TextBox>("IdBox")!.Text);
        Assert.Same(graph.FindControl<TabItem>("EditTab"), graph.FindControl<TabControl>("RightTabs")!.SelectedItem);

        // 고르는 것으로는 줄을 다시 세우지 않는다 — 다시 세우면 더블클릭의 둘째 누름이 새 컨트롤에 떨어진다.
        Assert.Same(row, tree.RowButton(key));

        window.Close();
    });

    [Fact]
    public void 챕터_줄은_그_챕터와_챕터_칸을_프로젝트_줄은_화자_칸을_연다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();
        var tabs = graph.FindControl<TabControl>("RightTabs")!;

        tree.RowButton(StoryHierarchy.ChapterKey("ch02"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("ch02", graph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem);
        Assert.Equal("ch02", window.SessionProbe.ActiveFile?.Name);
        Assert.Same(graph.FindControl<TabItem>("ConditionTab"), tabs.SelectedItem);
        Assert.Equal(StoryHierarchy.ChapterKey("ch02"), tree.Selection?.Key);

        tree.RowButton(StoryHierarchy.ProjectKey)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Same(graph.FindControl<TabItem>("SpeakerTab"), tabs.SelectedItem);
        Assert.Equal(StoryHierarchy.ProjectKey, tree.Selection?.Key);

        window.Close();
    });

    [Fact]
    public void 사람이_접은_마디는_다시_그려도_접혀_있고_판에서_그_안을_고르면_다시_펴진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();
        string chapter = StoryHierarchy.ChapterKey("ch02");
        tree.Fold(chapter, collapse: false);
        tree.Fold(chapter, collapse: true);

        // 편집 한 번이 판과 하이어아키를 다시 세운다 — 그래도 접힌 채다.
        window.SessionProbe.Editor.MoveEpisode("ch02", "x", 300, 300);
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x"));

        // 판에서 그 안의 에피소드를 고르면 드러난다 — 고른 것이 안 보이면 하이어아키가 거짓말을 한다.
        graph.SelectChapter("ch02");
        graph.SelectEpisode("x");
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x"));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 챕터 둘 — ch01은 장면 둘(opening: root · a, hall: b), ch02는 장면ID 없이 x · y.
    /// </summary>
    private (MainWindow Window, ChapterGraphView Graph, StoryHierarchy Tree) Show()
    {
        Directory.CreateDirectory(_directory);

        var editor = new ProjectEditor(new StoryProject { Title = "하이어아키" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "a", title: "a", 220, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "b", title: "b", 440, 0, sceneId: "hall");
        editor.EnsureChapter("ch02");
        editor.EnsureChapterBoard("ch02");
        editor.AddEpisode("ch02", "x", title: "x", 0, 0);
        editor.AddEpisode("ch02", "y", title: "y", 220, 0);

        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView graph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        return (window, graph, graph.HierarchyView);
    }
}
