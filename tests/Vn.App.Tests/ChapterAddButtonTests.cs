using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>새 챕터는 [대본] 탭 하이어아키의 [＋]에서 만든다</b> — [구성] 탭 하이어아키 머리에는 [＋]가 없다. [＋]는 챕터 Id 한 칸짜리
/// 창구를 열고, Enter로 만들면 그 챕터의 판이 열린다.
/// </summary>
public sealed class ChapterAddButtonTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-chapter-add-button", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 대본_탭_하이어아키의_더하기로_챕터를_만들고_구성_탭_하이어아키에는_더하기가_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        ChapterGraphView chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;

        Assert.Null(chapterGraph.FindControl<Button>("AddChapterButton"));
        Assert.Equal("＋", graph.ChapterAddButton.Content);

        graph.ChapterAddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        TextBox name = ChapterIdBox(window);
        name.Text = "ch09";
        name.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(session.Editor.FindChapter("ch09"));
        Assert.Equal("ch09", session.ActiveFile?.Name);   // 그 챕터의 판이 열린다

        window.Close();
    });

    /// <summary>열린 챕터 창구의 Id 칸 — 플라이아웃 틀 안에서만 찾는다.</summary>
    private static TextBox ChapterIdBox(Window window) =>
        window.GetVisualDescendants().OfType<TextBox>()
            .First(box => box.IsEffectivelyVisible
                && box.FindAncestorOfType<FlyoutPresenter>() is not null
                && box.PlaceholderText?.StartsWith("챕터 Id", StringComparison.Ordinal) == true);
}
