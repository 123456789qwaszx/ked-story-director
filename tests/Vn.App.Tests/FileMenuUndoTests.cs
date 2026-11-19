using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>File 메뉴의 되돌리기 · 다시 실행</b>(2026-11-18 소유자 — 「종종 키보드를 안 쓰는 사람들을 위해서기도 하고, 중요한 기능이니만큼 단축키를
/// 몰라도 사용할 수 있도록」 · <c>docs/work-orders/finishing-orders.md</c> FX7).
///
/// 못 박는 것: ① 메뉴 글 옆에 단축키가 적혀 있다 ② 할 것이 없으면 꺼지고, 편집하면 켜진다 ③ 누르면 Ctrl+Z · Ctrl+Y와 같은 일을 한다 —
/// 한 칸씩 ④ [대본] 카드 글 칸에서 쓰던 중이면 키처럼 넣지 않은 글부터 걷는다.
/// </summary>
public sealed class FileMenuUndoTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-file-menu-undo", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 메뉴의_되돌리기와_다시_실행은_단축키를_적고_할_것이_있을_때만_켜지며_한_칸씩_한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        MenuItem undo = window.FindControl<MenuItem>("UndoMenuItem")!;
        MenuItem redo = window.FindControl<MenuItem>("RedoMenuItem")!;

        Assert.Equal(KeyGesture.Parse("Ctrl+Z"), undo.InputGesture);
        Assert.Equal(KeyGesture.Parse("Ctrl+Y"), redo.InputGesture);

        // 막 연 프로젝트 — 되돌릴 것도 다시 할 것도 없다.
        Assert.False(undo.IsEnabled);
        Assert.False(redo.IsEnabled);

        session.Editor.UpdateEpisode("ch01", "a", title: "첫째");
        session.Editor.UpdateEpisode("ch01", "a", title: "둘째");
        Dispatcher.UIThread.RunJobs();
        Assert.True(undo.IsEnabled);
        Assert.False(redo.IsEnabled);

        // 한 번 누르면 한 칸.
        Click(undo);
        Assert.Equal("첫째", Title(session));
        Assert.True(redo.IsEnabled);

        Click(undo);
        Assert.Equal("a", Title(session));
        Assert.False(undo.IsEnabled);   // 처음까지 왔다

        Click(redo);
        Click(redo);
        Assert.Equal("둘째", Title(session));
        Assert.False(redo.IsEnabled);

        window.Close();
    });

    [Fact]
    public void 카드_글_칸에서_쓰던_중이면_메뉴의_되돌리기는_넣지_않은_글부터_걷는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        string cardId = EpisodeNaming.CardFor(session.Project, "ch01", "a")!.Id;
        session.Editor.UpdateEpisode("ch01", "a", title: "첫째");   // 되돌릴 것이 있어 메뉴가 켜진다
        Dispatcher.UIThread.RunJobs();

        TextBox box = graph.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(item => item.Tag as string == GraphEditorView.WritingTag + cardId);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        string shown = box.Text ?? string.Empty;
        long revision = session.Editor.Revision;

        box.Text = shown + "아직 넣지 않은 줄";
        Click(window.FindControl<MenuItem>("UndoMenuItem")!);

        // Ctrl+Z와 같다 — 쓰던 글만 걷히고 프로젝트는 그대로다(제목도 그대로).
        Assert.Equal(shown, box.Text ?? string.Empty);
        Assert.Equal(revision, session.Editor.Revision);
        Assert.Equal("첫째", Title(session));

        window.Close();
    });

    private static string Title(AuthoringSession session) =>
        session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == "a").Title;

    private static void Click(MenuItem item)
    {
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
