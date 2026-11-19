using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>판 양옆의 기둥을 접는다</b> — 왼쪽 하이어아키, 오른쪽 탭 셋(인스펙터 · 화자 · 조건 및 스탯 — 2026-11-18 소유자 · side-fold K1).
///
/// 「‹」(오른쪽은 「›」)를 누르면 기둥이 좁은 띠로 접혀 판이 넓어지고, 띠를 누르면 사람이 끌어 둔 폭으로 돌아온다. 두 그래프가 함께 접히고,
/// 다음에 창을 열 때도 접혀 있다(사용자 설정). 두 기둥은 따로 접힌다.
/// </summary>
public sealed class ColumnDockTests
{
    [Fact]
    public void 접으면_띠만_남아_판이_넓어지고_두_탭이_함께_접히며_다음에도_접혀_있다() => HeadlessUi.Run(() =>
    {
        AppSettingsService.SaveHierarchyFolded(false);

        try
        {
            var window = new MainWindow { Width = 1400, Height = 900 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
            ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
            Grid columns = graph.FindControl<Grid>("BoardColumns")!;

            // 사람이 끌어 둔 폭 — 펼 때 이 폭으로 돌아와야 한다.
            columns.ColumnDefinitions[0].Width = new GridLength(300);
            Dispatcher.UIThread.RunJobs();

            Click(graph.FindControl<Button>("FoldHierarchyButton")!);

            Assert.Equal(ColumnDock.FoldedWidth, columns.ColumnDefinitions[0].Width.Value);
            Assert.False(graph.HierarchyView.IsEffectivelyVisible);
            Assert.True(graph.FindControl<Button>("HierarchyStrip")!.IsVisible);
            Assert.False(graph.FindControl<GridSplitter>("HierarchySplitter")!.IsVisible);

            // 챕터 그래프도 함께 접혔다.
            Assert.True(chapterGraph.HierarchyDock.IsFolded);
            Assert.Equal(ColumnDock.FoldedWidth, chapterGraph.FindControl<Grid>("BoardColumns")!.ColumnDefinitions[0].Width.Value);

            // 다음에 열어도 접혀 있다.
            Assert.True(AppSettingsService.LoadHierarchyFolded());
            var next = new MainWindow { Width = 1400, Height = 900 };
            next.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.True(next.FindControl<GraphEditorView>("Graph")!.HierarchyDock.IsFolded);
            next.Close();

            // 띠를 누르면 — 끌어 둔 폭으로 돌아오고 두 탭이 함께 펴진다.
            Click(chapterGraph.FindControl<Button>("HierarchyStrip")!);

            Assert.Equal(300, columns.ColumnDefinitions[0].Width.Value);
            Assert.True(graph.FindControl<GridSplitter>("HierarchySplitter")!.IsVisible);
            Assert.False(graph.HierarchyDock.IsFolded);
            Assert.False(chapterGraph.HierarchyDock.IsFolded);
            Assert.False(AppSettingsService.LoadHierarchyFolded());

            window.Close();
        }
        finally
        {
            // 설정은 이 시험 프로세스가 함께 쓴다 — 중간에 실패해도 다른 시험의 창이 접힌 채 서지 않게.
            AppSettingsService.SaveHierarchyFolded(false);
        }
    });

    [Fact]
    public void 오른쪽_기둥도_접으면_띠만_남고_두_탭이_함께_접히며_다음에도_접혀_있다() => HeadlessUi.Run(() =>
    {
        AppSettingsService.SaveSideFolded(false);
        AppSettingsService.SaveHierarchyFolded(false);

        try
        {
            var window = new MainWindow { Width = 1400, Height = 900 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
            ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
            Grid columns = chapterGraph.FindControl<Grid>("BoardColumns")!;

            // 사람이 끌어 둔 폭 — 펼 때 이 폭으로 돌아와야 한다.
            columns.ColumnDefinitions[4].Width = new GridLength(380);
            Dispatcher.UIThread.RunJobs();

            Click(chapterGraph.FindControl<Button>("FoldSideButton")!);

            Assert.Equal(ColumnDock.FoldedWidth, columns.ColumnDefinitions[4].Width.Value);
            Assert.False(chapterGraph.FindControl<TabControl>("SideTabs")!.IsEffectivelyVisible);
            Assert.True(chapterGraph.FindControl<Button>("SideStrip")!.IsVisible);
            Assert.False(chapterGraph.FindControl<GridSplitter>("SideSplitter")!.IsVisible);

            // [대본] 탭도 함께 접혔다. 하이어아키는 그대로다 — 두 기둥은 따로 접힌다.
            Assert.True(graph.SideDock.IsFolded);
            Assert.Equal(ColumnDock.FoldedWidth, graph.FindControl<Grid>("BoardColumns")!.ColumnDefinitions[4].Width.Value);
            Assert.False(graph.HierarchyDock.IsFolded);
            Assert.False(chapterGraph.HierarchyDock.IsFolded);

            // 다음에 열어도 접혀 있다.
            Assert.True(AppSettingsService.LoadSideFolded());
            var next = new MainWindow { Width = 1400, Height = 900 };
            next.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.True(next.FindControl<GraphEditorView>("Graph")!.SideDock.IsFolded);
            Assert.True(next.FindControl<ChapterGraphView>("ChapterGraph")!.SideDock.IsFolded);
            next.Close();

            // [대본]의 띠를 누르면 — 끌어 둔 폭으로 돌아오고 두 탭이 함께 펴진다.
            Click(graph.FindControl<Button>("SideStrip")!);

            Assert.Equal(380, columns.ColumnDefinitions[4].Width.Value);
            Assert.True(chapterGraph.FindControl<GridSplitter>("SideSplitter")!.IsVisible);
            Assert.True(graph.FindControl<TabControl>("SideTabs")!.IsVisible);
            Assert.False(graph.SideDock.IsFolded);
            Assert.False(chapterGraph.SideDock.IsFolded);
            Assert.False(AppSettingsService.LoadSideFolded());

            window.Close();
        }
        finally
        {
            AppSettingsService.SaveSideFolded(false);
        }
    });

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
