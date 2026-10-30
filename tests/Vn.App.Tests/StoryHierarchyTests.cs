using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>우측 기둥 위의 하이어아키</b> (2026-10-30 소유자: *"유니티 하이어아키처럼 챕터를 부모로 하는 장면이 있고, 이 장면에
/// 포함된 에피소드들을 모으는 방식으로"* — <c>docs/plans/hierarchy.md</c> HY1).
///
/// 프로젝트 → 챕터 → 장면 → 에피소드를 접었다 편다. 어느 줄이든 고르면 챕터 그래프가 그것을 고르고 인스펙터가 따라
/// 바뀐다(프로젝트 → 화자, 챕터 → 조건 · 스탯, 장면 → 장면, 에피소드 → 구조 — HY2). 판에서 고른 것은 트리에 비친다.
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

        // 처음에는 고른 것(프로젝트를 열면 세션이 고르는 첫 카드 — 두 탭이 같은 것을 본다, HY3)이 든 챕터만 펴진다 —
        // 챕터 수십 개가 다 펴져 있으면 하이어아키가 아니라 목록이다.
        Assert.Equal(HierarchyKind.Project, tree.Rows[0].Kind);
        Assert.Equal(["ch01", "ch02"], tree.Rows.Where(row => row.Kind == HierarchyKind.Chapter).Select(row => row.ChapterId));
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);
        Assert.Equal(StoryHierarchy.ChapterKey("ch02"), tree.Rows[^1].Key);   // ch02는 접힌 채 맨 끝

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
    public void 에피소드_줄을_누르면_챕터_그래프가_그것을_고르고_인스펙터가_그것을_보인다_줄은_다시_세우지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        graph.Inspect(HierarchyPick.Project);

        string key = StoryHierarchy.EpisodeKey("ch02", "y");
        Button row = tree.RowButton(key)!;
        row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(key, tree.Selection?.Key);
        Assert.Equal("ch02", graph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem);
        Assert.Equal("y", graph.FindControl<TextBox>("IdBox")!.Text);
        Assert.Equal("Selection", Shown(graph));

        // 고르는 것으로는 줄을 다시 세우지 않는다 — 다시 세우면 더블클릭의 둘째 누름이 새 컨트롤에 떨어진다.
        Assert.Same(row, tree.RowButton(key));

        window.Close();
    });

    [Fact]
    public void 챕터_줄은_그_챕터와_조건_스탯을_프로젝트_줄은_화자를_인스펙터에_세운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();

        tree.RowButton(StoryHierarchy.ChapterKey("ch02"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("ch02", graph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem);
        Assert.Equal("ch02", window.SessionProbe.ActiveFile?.Name);
        Assert.Equal("Chapter", Shown(graph));
        Assert.Equal(StoryHierarchy.ChapterKey("ch02"), tree.Selection?.Key);

        tree.RowButton(StoryHierarchy.ProjectKey)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Speaker", Shown(graph));
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

    [Fact]
    public void 장면_줄을_고르면_장면_인스펙터가_서고_이름을_고치면_그_장면의_에피소드가_함께_바뀐다() => HeadlessUi.Run(() =>
    {
        // HY2 — 장면은 엔티티가 아니다: 이 장면ID를 든 에피소드들의 묶음이다. 이름을 고치면 그 에피소드들의 장면ID가
        // 한 번에 바뀌고(되돌리기 한 칸) 다른 장면은 그대로다.
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);

        tree.RowButton(StoryHierarchy.SceneKey("ch01", "opening"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Selection", Shown(graph));
        Assert.True(graph.FindControl<StackPanel>("ScenePanel")!.IsVisible);
        Assert.False(graph.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.False(graph.FindControl<TextBlock>("NoSelectionText")!.IsVisible);
        Assert.Equal("장면 · opening", graph.FindControl<TextBlock>("InspectorTitle")!.Text);
        Assert.Equal("opening", graph.FindControl<TextBox>("SceneNameBox")!.Text);
        Assert.Equal(
            ["scene-episode:root", "scene-episode:a"],
            graph.FindControl<StackPanel>("SceneEpisodesPanel")!.Children.Select(child => (string?)child.Tag));

        graph.FindControl<TextBox>("SceneNameBox")!.Text = "prologue";
        graph.RenameInspectedScene();
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        Assert.Equal(["prologue", "prologue", "hall"], SceneIds(session));
        Assert.Equal(StoryHierarchy.SceneKey("ch01", "prologue"), tree.Selection?.Key);
        Assert.Equal("장면 · prologue", graph.FindControl<TextBlock>("InspectorTitle")!.Text);

        session.Editor.Undo();
        Assert.Equal(["opening", "opening", "hall"], SceneIds(session));

        window.Close();
    });

    [Fact]
    public void 장면_인스펙터의_에피소드를_누르면_그_에피소드를_본다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, ChapterGraphView graph, StoryHierarchy tree) = Show();
        graph.Inspect(new HierarchyPick(HierarchyKind.Scene, "ch01", "opening"));
        Dispatcher.UIThread.RunJobs();

        graph.FindControl<StackPanel>("SceneEpisodesPanel")!.Children.OfType<Button>()
            .Single(button => (string?)button.Tag == "scene-episode:a")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("에피소드 · a", graph.FindControl<TextBlock>("InspectorTitle")!.Text);
        Assert.Equal("a", graph.FindControl<TextBox>("IdBox")!.Text);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "a"), tree.Selection?.Key);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static string[] SceneIds(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.Episodes.Select(episode => episode.SceneId ?? string.Empty).ToArray();

    /// <summary>인스펙터에 선 칸 — 하나만 선다.</summary>
    private static string Shown(ChapterGraphView graph) =>
        Assert.Single(new[] { "Selection", "Speaker", "Chapter" },
            name => graph.FindControl<ScrollViewer>(name + "Section")!.IsVisible);

    /// <summary>하이어아키 시험의 프로젝트(<see cref="HierarchyFixture"/>)를 챕터 그래프 탭으로 연다.</summary>
    private (MainWindow Window, ChapterGraphView Graph, StoryHierarchy Tree) Show()
    {
        (MainWindow window, _) = HierarchyFixture.Open(_directory);
        ChapterGraphView graph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        return (window, graph, graph.HierarchyView);
    }
}
