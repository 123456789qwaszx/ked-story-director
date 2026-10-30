using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>하이어아키 기둥을 접는다</b> (2026-10-30 소유자: *"하이어아키를 접을 수 있도록 해줘"* — <c>docs/plans/hierarchy.md</c> HC).
///
/// 「‹」를 누르면 기둥이 좁은 띠로 접혀 판이 넓어지고, 띠를 누르면 사람이 끌어 둔 폭으로 돌아온다. 두 그래프가 함께 접히고,
/// 다음에 창을 열 때도 접혀 있다(사용자 설정).
/// </summary>
public sealed class HierarchyDockTests
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

            Assert.Equal(HierarchyDock.FoldedWidth, columns.ColumnDefinitions[0].Width.Value);
            Assert.False(graph.HierarchyView.IsEffectivelyVisible);
            Assert.True(graph.FindControl<Button>("HierarchyStrip")!.IsVisible);
            Assert.False(graph.FindControl<GridSplitter>("HierarchySplitter")!.IsVisible);

            // 챕터 그래프도 함께 접혔다.
            Assert.True(chapterGraph.HierarchyDock.IsFolded);
            Assert.Equal(HierarchyDock.FoldedWidth, chapterGraph.FindControl<Grid>("BoardColumns")!.ColumnDefinitions[0].Width.Value);

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

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
