using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 탭의 오른쪽 기둥은 탭 셋이다</b> — 인스펙터 · 화자 · 조건 및 스탯(2026-11-18 소유자 · <c>docs/work-orders/side-tabs-orders.md</c> V3).
/// 「현재 화자와 조건은 하이어아키에 숨겨져있다보니, 처음쓰는 사람입장에서는 알 방법이 없는데, 일종의 탭으로 상시 노출」.
///
/// 못 박는 것: ① 탭 셋이 늘 서 있고 화자 · 조건 탭은 줄을 고르지 않아도 내용이 있다 ② 고르면 그것을 보이는 탭으로 간다(프로젝트 → 화자,
/// 그 밖 → 인스펙터) ③ 조건 탭은 챕터 단위임을 말하고 지금 챕터(활성 판)를 보인다 — 작은 드롭다운으로 고르면 그 챕터로 가고 탭은 그대로다.
/// </summary>
public sealed class DirectorSideTabsTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-side-tabs", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 탭_셋이_늘_서고_화자와_조건은_줄을_고르지_않아도_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        session.Editor.AddChapterStat("ch01", "성실", initial: 1, minimum: 0, maximum: 5);
        Dispatcher.UIThread.RunJobs();

        TabControl tabs = graph.FindControl<TabControl>("SideTabs")!;
        Assert.Equal(["인스펙터", "화자", "조건 및 스탯"], tabs.Items.OfType<TabItem>().Select(item => (string?)item.Header));

        // 화자 탭 — 프로젝트 줄을 고르지 않아도 더하기 칸이 있다.
        graph.ShowSideTab(SideTab.Speakers);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Shown(graph), control => (string?)control.Tag == "speaker-add-name");

        // 조건 및 스탯 탭 — 챕터 단위임을 머리가 말하고, 지금 챕터(활성 판)의 스탯 표 · 조건 칸이 선다.
        graph.ShowSideTab(SideTab.Conditions);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(
            graph.FindControl<TabItem>("ConditionTabItem")!.GetLogicalDescendants().OfType<TextBlock>(),
            block => block.Text == "챕터 단위 조건 · 스탯");
        Assert.Equal(session.ActiveFile?.Name, graph.FindControl<ComboBox>("ConditionChapterCombo")!.SelectedItem);
        Assert.Contains(Shown(graph), control => (string?)control.Tag == "condition-add-name");

        window.Close();
    });

    [Fact]
    public void 고르면_그것을_보이는_탭으로_가고_콤보는_탭을_바꾸지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        session.Editor.AddChapterStat("ch01", "성실", initial: 1, minimum: 0, maximum: 5);
        Dispatcher.UIThread.RunJobs();

        // 프로젝트 줄 → 화자 탭.
        PickRow(graph, StoryHierarchy.ProjectKey);
        Assert.Equal(SideTab.Speakers, graph.ShownSideTab);

        // 챕터 줄 → 「조건 및 스탯」 탭 — 이름 칸도 그 탭 맨 위다(2026-11-18 · chapter-pick Y1, ⛔ V3에서는 인스펙터였다).
        PickRow(graph, StoryHierarchy.ChapterKey("ch01"));
        Assert.Equal(SideTab.Conditions, graph.ShownSideTab);
        Assert.Contains(Shown(graph), control => control is TextBox { Tag: "chapter-name", Text: "ch01" });

        // 조건 탭에 있다가 카드를 고르면 인스펙터 탭이 그 카드를 보인다.
        graph.ShowSideTab(SideTab.Conditions);
        session.Select(EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(SideTab.Inspector, graph.ShownSideTab);

        // 조건 탭의 작은 드롭다운으로 챕터를 바꾸면 그 챕터로 가고 탭은 그대로다 — 툴바 콤보가 따라온다.
        graph.ShowSideTab(SideTab.Conditions);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Shown(graph), control => (string?)control.Tag == "stat-snippet:성실");
        graph.FindControl<ComboBox>("ConditionChapterCombo")!.SelectedItem = "ch02";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ch02", session.ActiveFile?.Name);
        Assert.Equal("ch02", graph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem);
        Assert.Equal(SideTab.Conditions, graph.ShownSideTab);
        Assert.DoesNotContain(Shown(graph), control => (string?)control.Tag == "stat-snippet:성실");   // ch02에는 스탯이 없다

        // 툴바 콤보로 바꿔도 탭은 그대로다 — 작은 드롭다운과 내용이 따라온다.
        graph.FindControl<ComboBox>("ChapterCombo")!.SelectedItem = "ch01";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ch01", graph.FindControl<ComboBox>("ConditionChapterCombo")!.SelectedItem);
        Assert.Equal(SideTab.Conditions, graph.ShownSideTab);
        Assert.Contains(Shown(graph), control => (string?)control.Tag == "stat-snippet:성실");

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static void PickRow(GraphEditorView graph, string key)
    {
        graph.HierarchyView.RowButton(key)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>지금 보이는 탭의 내용 — 보이는 것만.</summary>
    private static IEnumerable<Control> Shown(GraphEditorView graph) =>
        graph.ShownSideHost.GetLogicalDescendants().OfType<Control>().Distinct()
            .Where(control => control.IsEffectivelyVisible);
}
