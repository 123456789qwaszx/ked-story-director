using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// [구성] 탭은 ID를 다룬다 (2026-11-16 · <c>docs/work-orders/display-names-orders.md</c> N4) — 챕터 · 장면 인스펙터에 ID 칸, 고치고 Enter.
/// 챕터의 표시 이름 칸은 걷었다(이름은 [대본] 탭에서).
/// </summary>
public sealed class StructureIdFieldsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-structure-ids", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 챕터ID_칸에서_챕터를_개명하고_표시_이름_칸은_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        session.Editor.UpdateChapterDisplayName("ch01", "축제 전날");
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(view.FindControl<TextBox>("ChapterDisplayNameBox"));
        TextBox id = view.FindControl<TextBox>("ChapterIdBox")!;
        Assert.Equal("ch01", id.Text);

        id.Text = "festival";
        Enter(id);

        Assert.Null(session.Editor.FindChapter("ch01"));
        Assert.Equal("축제 전날", session.Editor.FindChapter("festival")!.DisplayName);   // 이름은 그대로 따라간다

        window.Close();
    });

    [Fact]
    public void 장면ID_칸에서_장면을_개명하고_이름과_메모가_따라가며_다른_장면의_ID로는_바꾸지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Structure);
        session.Editor.SetSceneName("ch01", "opening", "교실");
        session.Editor.SetSceneNote("ch01", "opening", "사정");
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.HierarchyView.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        view.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "opening"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        TextBox id = view.FindControl<TextBox>("SceneIdBox")!;
        Assert.True(id.IsEffectivelyVisible);
        Assert.Equal("opening", id.Text);

        // 다른 장면이 쓰는 ID — 바꾸면 합쳐진다. 거부한다.
        id.Text = "hall";
        Enter(id);
        Assert.Equal("opening", Scene(session, "root"));
        Assert.Contains("다른 장면이 씁니다", session.StatusMessage, StringComparison.Ordinal);

        id.Text = "classroom";
        Enter(id);

        Assert.Equal("classroom", Scene(session, "root"));
        Assert.Equal("classroom", Scene(session, "a"));
        ChapterSceneNote entry = Assert.Single(session.Editor.FindChapter("ch01")!.SceneNotes);
        Assert.Equal(("classroom", "교실", "사정"), (entry.SceneId, entry.Name, entry.Text));

        window.Close();
    });

    private static string? Scene(AuthoringSession session, string episodeId) =>
        session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == episodeId).SceneId;

    private static void Enter(TextBox box)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();
    }
}
