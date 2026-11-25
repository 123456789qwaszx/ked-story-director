using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>연출 그래프의 우측도 하이어아키 + 인스펙터이고, 두 탭의 선택은 함께 움직인다</b>.
///
/// 에피소드를 고르면 그 챕터의 판이 열리고 그 카드의 대사 편집기가 선다(탭마다 제 일). 프로젝트 · 챕터 · 장면 줄은
/// 요약과 「챕터 그래프에서 열기」를 보인다. 어느 탭에서 에피소드를 고르든 세션의 선택(그 카드)이 되고, 두 하이어아키와
/// 챕터 그래프가 그것을 비춘다.
/// </summary>
public sealed class DirectorHierarchyTests : IDisposable
{
    private const int GraphTab = MainTab.Script;

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

        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);
        tree.RowButton(StoryHierarchy.EpisodeKey("ch02", "y"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        DialogueNode card = Card(session, "ch02", "y");
        Assert.Equal(card.Id, session.SelectedNodeId);
        Assert.Equal("ch02", session.ActiveFile?.Name);
        Assert.Equal(card.Id, Editor(window).NodeId);
        Assert.True(Editor(window).IsVisible);
        Assert.Equal("ch02 - street", graph.InspectorHeaderText);   // 「{챕터} - {장면}」

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

        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(session.SelectedNodeId);
        Assert.False(Editor(window).IsVisible);
        Assert.Equal("ch01", graph.InspectorHeaderText);

        // 요약 칸은 장면 목록을 세우지 않는다 — 장면은 하이어아키에서 고른다.
        Assert.DoesNotContain(SummaryTags(graph), tag => tag.StartsWith("summary-scene:", StringComparison.Ordinal));
        graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("hall", graph.InspectorHeaderText);
        Assert.Equal(StoryHierarchy.SceneKey("ch01", "hall"), graph.HierarchyView.Selection?.Key);

        // 장면 요약은 메모 공간이다 — 에피소드 목록도 「[구성] 탭에서 장면 열기」도 없다. 에피소드는 하이어아키에서 고르고,
        // 고르면 그 카드의 대사를 쓴다.
        Assert.DoesNotContain(SummaryTags(graph), tag => tag.StartsWith("summary-episode:", StringComparison.Ordinal));
        Assert.DoesNotContain("summary-open", SummaryTags(graph));
        graph.HierarchyView.Fold(StoryHierarchy.SceneKey("ch01", "hall"), collapse: false);
        graph.HierarchyView.RowButton(StoryHierarchy.EpisodeKey("ch01", "b"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Card(session, "ch01", "b").Id, session.SelectedNodeId);
        Assert.True(Editor(window).IsVisible);

        window.Close();
    });

    [Fact]
    public void 챕터_콤보는_하이어아키와_판에서_고른_챕터를_따라간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「그래프쪽이나 하이어아키에서 특정 챕터나 챕터내 장면, 에피소드를 눌렀을 때, 누른 챕터로 드롭다운의 챕터역시 바뀌도록」.
        (MainWindow window, AuthoringSession session) = Show();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ComboBox combo = graph.FindControl<ComboBox>("ChapterCombo")!;
        StoryHierarchy tree = graph.HierarchyView;

        void Pick(string key)
        {
            tree.RowButton(key)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        Pick(StoryHierarchy.ChapterKey("ch02"));
        Assert.Equal("ch02", combo.SelectedItem);

        Pick(StoryHierarchy.SceneKey("ch01", "opening"));
        Assert.Equal("ch01", combo.SelectedItem);

        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);
        Pick(StoryHierarchy.EpisodeKey("ch02", "x"));
        Assert.Equal("ch02", combo.SelectedItem);

        // 판에서 다른 챕터의 카드를 누른 것 — 고른 카드의 챕터가 활성 판이 된다.
        session.Select(Card(session, "ch01", "b").Id);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ch01", session.ActiveFile?.Name);
        Assert.Equal("ch01", combo.SelectedItem);

        window.Close();
    });

    [Fact]
    public void 다른_챕터가_막혀도_검증_보고가_그_까닭을_보인다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「진행 JSON 미출력이라고 에러를 띄우는데 보고할 것이 없다고 나오는 경우」. 결론은 전 챕터의 내보내기에서 오고 보고는
        // 고른 챕터 하나였다 — 막힌 챕터의 오류가 보고에 같이 서야 「아래 오류를 고치면」이 참이다.
        (MainWindow window, AuthoringSession session) = Show();
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        chapterGraph.SelectChapter("ch01");
        // 문구를 단 자동 길 — ch02가 막힌다(AutoEdgeHasChoiceLabel). ⛔ 2026-11-26까지는 문구 없는 길로 막았다(이제 경고 · 임시 문구로 나간다).
        session.Editor.AddEdge("ch02", "x", "y", optionLabel: "간다", auto: true);
        Dispatcher.UIThread.RunJobs();

        // 보고는 [구성] 탭에 선다 — 가려진 동안의 그리기는 탭이 보일 때 한다(finishing FX3).
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Structure;
        Dispatcher.UIThread.RunJobs();

        string header = (string)chapterGraph.FindControl<Expander>("DiagnosticsExpander")!.Header!;
        List<string> lines = chapterGraph.FindControl<StackPanel>("DiagnosticsPanel")!.GetLogicalDescendants()
            .OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();

        Assert.Contains("진행 JSON 미출력", header, StringComparison.Ordinal);
        Assert.Contains("오류 1", header, StringComparison.Ordinal);
        Assert.Contains(lines, line => line.StartsWith("검증 오류로 진행 JSON이 나가지 않았습니다: ch02", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("ch02 · 간선", StringComparison.Ordinal));
        Assert.DoesNotContain("보고할 것이 없습니다.", lines);

        // 고치면 결론과 그 오류가 함께 걷힌다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain("미출력", (string)chapterGraph.FindControl<Expander>("DiagnosticsExpander")!.Header!, StringComparison.Ordinal);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!;

    private static DialogueNodeEditor Editor(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();

    private static List<string> SummaryTags(GraphEditorView graph) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible).Select(control => control.Tag as string ?? string.Empty).ToList();

    /// <summary>연출 그래프 인스펙터의 요약 칸에서 그 단추.</summary>
    private static Button Summary(GraphEditorView graph, string tag) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && (string?)button.Tag == tag);

    /// <summary>하이어아키 시험의 프로젝트(<see cref="HierarchyFixture"/>)를 연출 그래프 탭으로 연다.</summary>
    private (MainWindow Window, AuthoringSession Session) Show() => HierarchyFixture.Open(_directory, GraphTab);
}
