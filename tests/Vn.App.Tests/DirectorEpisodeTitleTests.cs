using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 탭은 에피소드를 보이는 제목으로 보이고 고친다</b> — 카드 머리 · 인스펙터 이름 칸과 머리글 · 하이어아키가 제목(비면
/// EpisodeId)을 보이고, 이름 칸과 하이어아키의 이름 고치기는 제목만 고친다. EpisodeId는 [구성] 탭에서 정한다 — [구성] 탭의 칸과
/// 하이어아키는 그대로 EpisodeId다.
/// </summary>
public sealed class DirectorEpisodeTitleTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-episode-title", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 이름_칸에_제목을_적고_Enter를_누르면_제목만_바뀌고_대본_탭이_제목을_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode card = Card(session, "b");
        session.Select(card.Id);
        Dispatcher.UIThread.RunJobs();

        TextBox name = NameBox(window);
        Assert.Equal("b", name.Text);

        name.Text = "교무실";
        Enter(name);

        // 제목만 바뀐다 — EpisodeId · 카드 이름 · 선택은 그대로다.
        ChapterEpisode episode = Episode(session, "b");
        Assert.Equal(("b", "교무실"), (episode.EpisodeId, episode.Title));
        Assert.Equal(("b", "b"), (card.Name, card.MarkedEpisodeId));
        Assert.Equal(card.Id, session.SelectedNodeId);

        // [대본] — 이름 칸 · 머리글 · 카드 머리 · 하이어아키가 제목이다.
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Assert.Equal("교무실", NameBox(window).Text);
        Assert.Equal("ch01챕터 - hall", graph.FindControl<TextBlock>("InspectorTitle")!.Text);   // 「{챕터}챕터 - {장면}」 — 제목은 이름 칸
        Assert.False(graph.FindControl<TextBlock>("InspectorSubtitle")!.IsVisible);   // 부제(대사 줄 — 화자 · 대사 · 분기)는 없다
        Assert.Contains("교무실", CardTexts(graph, card.Id));
        Assert.Equal("교무실", Row(graph.HierarchyView, "b").Text);

        // [구성] — EpisodeId 그대로, 제목은 제목 칸과 곁글에.
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        Assert.Equal("b", chapterGraph.FindControl<TextBox>("IdBox")!.Text);
        Assert.Equal(("b", "교무실"), (Row(chapterGraph.HierarchyView, "b").Text, Row(chapterGraph.HierarchyView, "b").Detail));

        // 되돌리기 한 칸.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("b", Episode(session, "b").Title);

        window.Close();
    });

    [Fact]
    public void 제목을_비우면_EpisodeId가_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode card = Card(session, "b");
        session.Select(card.Id);
        Dispatcher.UIThread.RunJobs();

        TextBox name = NameBox(window);
        name.Text = string.Empty;
        Enter(name);

        Assert.Equal(string.Empty, Episode(session, "b").Title);
        Assert.Equal("b", NameBox(window).Text);
        Assert.Contains("b", CardTexts(window.FindControl<GraphEditorView>("Graph")!, card.Id));

        window.Close();
    });

    [Fact]
    public void 대본_탭_하이어아키의_이름_고치기는_제목을_고치고_구성_탭_하이어아키는_고치지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        StoryHierarchy director = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        StoryHierarchy chapter = window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;
        director.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        chapter.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);

        Rename(director, StoryHierarchy.EpisodeKey("ch02", "x"), "복도");
        ChapterEpisode x = session.Editor.FindChapter("ch02")!.Episodes.Single(episode => episode.EpisodeId == "x");
        Assert.Equal("복도", x.Title);

        // [구성] 탭 하이어아키는 이름 칸을 열지 않는다 — EpisodeId는 [구성] 탭 인스펙터의 에피소드ID 칸에서 고친다.
        chapter.BeginRename(StoryHierarchy.EpisodeKey("ch02", "y"));
        Assert.Null(chapter.RenameBox);
        Assert.Contains(session.Editor.FindChapter("ch02")!.Episodes, episode => episode.EpisodeId == "y");

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    private static ChapterEpisode Episode(AuthoringSession session, string episodeId) =>
        session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == episodeId);

    private static TextBox NameBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single().FindControl<TextBox>("NameBox")!;

    /// <summary>[대본] 판의 그 카드 안의 글들.</summary>
    private static List<string?> CardTexts(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>()
            .Single(card => card.Tag as string == nodeId)
            .GetVisualDescendants().OfType<TextBlock>()
            .Select(text => text.Text)
            .ToList();

    private static HierarchyRow Row(StoryHierarchy tree, string episodeId) =>
        tree.Rows.Single(row => row.Kind == HierarchyKind.Episode && row.EpisodeId == episodeId);

    private static void Rename(StoryHierarchy tree, string key, string text)
    {
        tree.BeginRename(key);
        TextBox box = tree.RenameBox!;
        box.Text = text;
        Enter(box);
    }

    private static void Enter(TextBox box)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();
    }
}
