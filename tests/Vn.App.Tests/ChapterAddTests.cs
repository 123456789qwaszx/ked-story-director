using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>새 챕터는 [대본] 탭 하이어아키의 프로젝트 줄(또는 빈 곳) 우클릭 「챕터 추가」로 바로 선다</b>(2026-11-18 소유자) — 이름 칸 없이 「새 챕터」(자동 ID)가 서고 그 챕터의
/// 판이 열리고 그 챕터가 골라진다. 이름은 하이어아키에서 두 번 눌러 고친다. ⛔ 하이어아키 머리의 [＋]와 이름을 먼저 받던 창구(<c>ChapterAddFlyout</c>)는 걷혔다.
/// </summary>
public sealed class ChapterAddTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-chapter-add", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 챕터_추가는_이름_칸_없이_새_챕터를_바로_세우고_그_챕터를_고른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        long before = session.Editor.Revision;

        // 하이어아키 머리의 [＋]는 두 탭 어디에도 없다.
        Assert.Null(graph.FindControl<Button>("AddChapterButton"));
        Assert.Null(chapterGraph.FindControl<Button>("AddChapterButton"));

        ((IEnumerable<object>)graph.HierarchyView.RowButton(StoryHierarchy.ProjectKey)!.ContextMenu!.ItemsSource!)
            .OfType<MenuItem>().Single(item => (string?)item.Header == "챕터 추가")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // ID는 자동(ch01 · ch02가 있으니 ch03), 이름은 「새 챕터」 — 한 되돌리기 칸.
        Assert.Equal(NewNames.Chapter, session.Editor.FindChapter("ch03")!.DisplayName);
        Assert.Equal("ch03", session.ActiveFile?.Name);   // 그 챕터의 판이 열린다
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Contains(graph.HierarchyView.Rows, row => row.Key == StoryHierarchy.ChapterKey("ch03"));

        // 만든 챕터가 골라진다 — 하이어아키에서 그 줄을 누른 것과 같다(2026-11-18 소유자).
        Assert.Equal(new HierarchyPick(HierarchyKind.Chapter, "ch03"), graph.HierarchyView.Selection);
        Assert.Equal(NewNames.Chapter, graph.FindControl<TextBlock>("InspectorTitle")!.Text);

        window.Close();
    });
}
