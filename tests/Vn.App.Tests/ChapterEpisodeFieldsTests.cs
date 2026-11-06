using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 에피소드 칸</b> — 에피소드ID · 제목 · 장면ID 칸 왼쪽에 이름표가 서서 무슨 칸인지 말한다. 장면 전체의 이름은
/// 「같은 장면 전체에 적용」 단추 대신 하이어아키의 장면 줄에서 바꾼다 — 그 장면의 에피소드가 모두 따라가고 되돌리기 한 칸이다.
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
    public void 세_칸_왼쪽에_이름표가_서고_장면_일괄_단추는_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory, tab: 0);
        ChapterGraphView view = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        view.SelectChapter("ch01");
        view.SelectEpisode("a");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("에피소드ID", LabelOf(view, "IdBox"));
        Assert.Equal("제목", LabelOf(view, "EpisodeTitleBox"));
        Assert.Equal("장면ID", LabelOf(view, "SceneIdBox"));
        Assert.Equal("a", view.FindControl<TextBox>("IdBox")!.Text);
        Assert.Equal("opening", view.FindControl<TextBox>("SceneIdBox")!.Text);

        Assert.Null(view.FindControl<Button>("ApplySceneGroupButton"));

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
