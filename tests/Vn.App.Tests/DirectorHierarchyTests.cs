using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>연출 그래프의 우측도 하이어아키 + 인스펙터이고, 두 탭의 선택은 함께 움직인다</b> (2026-10-30 HY3 —
/// <c>docs/plans/hierarchy.md</c>).
///
/// 에피소드를 고르면 그 챕터의 판이 열리고 그 카드의 대사 편집기가 선다(탭마다 제 일 — HY-D2). 프로젝트 · 챕터 · 장면 줄은
/// 요약과 「챕터 그래프에서 열기」를 보인다. 어느 탭에서 에피소드를 고르든 세션의 선택(그 카드)이 되고, 두 하이어아키와
/// 챕터 그래프가 그것을 비춘다.
/// </summary>
public sealed class DirectorHierarchyTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-hierarchy", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 연출_그래프의_하이어아키에서_에피소드를_고르면_그_판이_열리고_카드의_대사_편집기가_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Show();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        StoryHierarchy tree = graph.HierarchyView;

        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        tree.RowButton(StoryHierarchy.EpisodeKey("ch02", "y"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        DialogueNode card = Card(session, "ch02", "y");
        Assert.Equal(card.Id, session.SelectedNodeId);
        Assert.Equal("ch02", session.ActiveFile?.Name);
        Assert.Equal(card.Id, Editor(window).NodeId);
        Assert.True(Editor(window).IsVisible);
        Assert.Equal("에피소드 · y", graph.FindControl<TextBlock>("InspectorTitle")!.Text);

        window.Close();
    });

    [Fact]
    public void 한_탭에서_고른_에피소드를_다른_탭과_두_하이어아키가_비춘다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Show();
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;

        // 챕터 그래프에서 고르면 — 세션의 선택(그 카드)이 되고 연출 그래프의 하이어아키가 비춘다.
        chapterGraph.SelectChapter("ch01");
        chapterGraph.SelectEpisode("b");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Card(session, "ch01", "b").Id, session.SelectedNodeId);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "b"), graph.HierarchyView.Selection?.Key);

        // 연출 그래프에서 카드를 고르면 — 챕터 그래프가 그 챕터 · 에피소드를 고르고 그 하이어아키가 비춘다.
        session.Select(Card(session, "ch02", "x").Id);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("ch02", chapterGraph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem);
        Assert.Equal("x", chapterGraph.FindControl<TextBox>("IdBox")!.Text);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch02", "x"), chapterGraph.HierarchyView.Selection?.Key);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch02", "x"), graph.HierarchyView.Selection?.Key);

        window.Close();
    });

    [Fact]
    public void 챕터_장면_줄은_요약을_보이고_챕터_그래프에서_열기로_넘어간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Show();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        TextBlock title = graph.FindControl<TextBlock>("InspectorTitle")!;

        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(session.SelectedNodeId);
        Assert.False(Editor(window).IsVisible);
        Assert.Equal("챕터 · ch01", title.Text);

        Summary(graph, "summary-scene:hall").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("장면 · hall", title.Text);
        Assert.Equal(StoryHierarchy.SceneKey("ch01", "hall"), graph.HierarchyView.Selection?.Key);

        // 장면 이름은 챕터 그래프에서 고친다 — 넘어가면 그 인스펙터에 같은 장면이 선다.
        Summary(graph, "summary-open").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal(0, window.FindControl<TabControl>("MainTabs")!.SelectedIndex);
        Assert.Equal("장면 · hall", chapterGraph.FindControl<TextBlock>("InspectorTitle")!.Text);

        // 장면의 에피소드를 누르면 그 카드의 대사를 쓴다.
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();
        Summary(graph, "summary-episode:b").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Card(session, "ch01", "b").Id, session.SelectedNodeId);
        Assert.True(Editor(window).IsVisible);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!;

    private static DialogueNodeEditor Editor(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();

    /// <summary>연출 그래프 인스펙터의 요약 칸에서 그 단추.</summary>
    private static Button Summary(GraphEditorView graph, string tag) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && (string?)button.Tag == tag);

    /// <summary>챕터 둘 — ch01은 장면 둘(opening: root · a, hall: b), ch02는 장면ID 없이 x · y. 연출 그래프 탭을 연다.</summary>
    private (MainWindow Window, AuthoringSession Session) Show()
    {
        Directory.CreateDirectory(_directory);

        var editor = new ProjectEditor(new StoryProject { Title = "연출 하이어아키" });
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
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = GraphTab;
        Dispatcher.UIThread.RunJobs();

        return (window, session);
    }
}
