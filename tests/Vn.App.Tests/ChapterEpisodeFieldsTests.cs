using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 에피소드 칸</b> — 머리글이 「장면 - 제목」을 말하고(부제 없음), 칸은 에피소드ID 하나다. 그 왼쪽에 이름표가
/// 선다. 제목은 [대본] 탭에서 고친다. 장면은 여기서 정하지 않는다 — 하이어아키의 장면 줄에서 이름을 바꾸면 그 장면의
/// 에피소드가 모두 따라가고 되돌리기 한 칸이다.
/// </summary>
public sealed class ChapterEpisodeFieldsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-chapter-episode-fields", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 에피소드ID_칸_하나에_이름표가_서고_제목_칸과_장면ID_칸은_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.SelectEpisode("a");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("에피소드ID", LabelOf(view, "IdBox"));
        Assert.Equal("a", view.FindControl<TextBox>("IdBox")!.Text);

        Assert.Null(view.FindControl<TextBox>("EpisodeTitleBox"));
        Assert.Null(view.FindControl<TextBox>("SceneIdBox"));
        Assert.Null(view.FindControl<Button>("ApplySceneGroupButton"));

        window.Close();
    });

    [Fact]
    public void 머리글은_장면_제목이고_부제는_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        TextBlock title = view.FindControl<TextBlock>("InspectorTitle")!;
        TextBlock subtitle = view.FindControl<TextBlock>("InspectorSubtitle")!;

        // ch01 b — 장면 hall.
        view.SelectChapter("ch01");
        view.SelectEpisode("b");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("hall - b", title.Text);
        Assert.False(subtitle.IsVisible);

        // [대본] 탭에서 제목을 고치면 머리글이 따라간다 · 비우면 EpisodeId다.
        session.Editor.UpdateEpisode("ch01", "b", title: "교무실");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("hall - 교무실", title.Text);

        session.Editor.UpdateEpisode("ch01", "b", title: string.Empty);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("hall - b", title.Text);

        // ch02 x — 장면ID가 없으면 제목만.
        view.SelectChapter("ch02");
        view.SelectEpisode("x");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("x", title.Text);

        // 다른 것을 보면 부제가 다시 선다.
        view.Inspect(HierarchyPick.Project);
        Assert.Equal("화자", title.Text);
        Assert.True(subtitle.IsVisible);

        window.Close();
    });

    [Fact]
    public void 하이어아키에서_장면_이름을_바꾸면_그_장면의_에피소드가_모두_따라가고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        StoryHierarchy tree = window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);

        // opening — root · a 둘이 든 장면.
        tree.BeginRename(StoryHierarchy.SceneKey("ch01", "opening"));
        TextBox box = tree.RenameBox!;
        box.Text = "교실";
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("교실", SceneOf(session, "root"));
        Assert.Equal("교실", SceneOf(session, "a"));
        Assert.Equal("hall", SceneOf(session, "b"));

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("opening", SceneOf(session, "root"));
        Assert.Equal("opening", SceneOf(session, "a"));

        window.Close();
    });

    /// <summary>그 칸과 같은 줄의 왼쪽 기둥에 선 이름표.</summary>
    private static string? LabelOf(ChapterGraphView view, string boxName)
    {
        TextBox box = view.FindControl<TextBox>(boxName)!;
        var grid = (Grid)box.Parent!;
        int row = Grid.GetRow(box);

        return grid.Children.OfType<TextBlock>()
            .Single(label => Grid.GetRow(label) == row && Grid.GetColumn(label) == 0)
            .Text;
    }

    private static string? SceneOf(AuthoringSession session, string episodeId) =>
        session.Editor.FindChapter("ch01")!.ToGraphModel("ch01").FindEpisode(episodeId)!.SceneId;
}
