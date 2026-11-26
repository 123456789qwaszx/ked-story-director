using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>하이어아키에서 에피소드를 다른 챕터의 장면 줄로 끌어 옮긴다</b> (2026-11-26 소유자 — 「에피소드를 다른 챕터의 다른 장면으로 옮기고 싶어질
/// 때가 있다」). 끄는 동안 풍선이 끊을 선을 미리 말하고(챕터끼리는 잇지 못해 모두), 놓으면 끊고 옮긴다(한 되돌리기 칸). 끊은 선택지의 문구는
/// 그 칸에 적힌 채 남는다. 챕터 시작은 붉은 테두리와 까닭이 서고 옮기지 않는다(<c>ChapterMoveGuard</c>).
/// </summary>
public sealed class HierarchyChapterMoveTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-chapter-move", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 다른_챕터의_장면_줄로_끌면_끊을_선을_미리_말하고_놓으면_모두_끊고_옮긴다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        StoryHierarchy tree = graph.HierarchyView;
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "이쪽");
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "저쪽");
        Dispatcher.UIThread.RunJobs();
        Unfold(tree);
        long before = session.Editor.Revision;

        (Point from, Point to, Button target) = Points(window, tree, "a");
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        // 받는 자리다 — 파란 테두리, 끊을 선(모두)을 풍선과 상태줄이 미리 말한다.
        Assert.Equal(Color.FromRgb(0x3D, 0x7B, 0xD9), Assert.IsType<SolidColorBrush>(tree.DragGhostBorder).Color);
        Assert.Equal(new Thickness(1), target.BorderThickness);
        Assert.Null(tree.RefusalTip);
        Assert.Contains("챕터끼리는 이을 수 없어 이 에피소드에 닿은 선 2개를 모두 끊습니다", tree.NoteTip, StringComparison.Ordinal);
        Assert.Contains("'root'→'a' 「이쪽」", tree.NoteTip, StringComparison.Ordinal);
        Assert.Contains("선 2개를 모두 끊습니다", session.StatusMessage, StringComparison.Ordinal);

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(session.Editor.FindChapter("ch01")!.Episodes, episode => episode.EpisodeId == "a");
        Assert.Empty(session.Editor.FindChapter("ch01")!.Edges);
        Assert.Equal("street", session.Editor.FindChapter("ch02")!.Episodes.Single(episode => episode.EpisodeId == "a").SceneId);
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Contains("챕터 'ch02'의 장면 'street'로 옮겼습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("선 2개를 끊었습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("적힌 채 남습니다", session.StatusMessage, StringComparison.Ordinal);

        // 끊은 선택지의 문구는 남는 카드(root)의 칸에 적힌 채다.
        Assert.Contains("이쪽", SlotTexts(graph, "root"));

        // 하이어아키도 새 챕터 밑에 세운다.
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "a"));
        Assert.DoesNotContain(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "a"));

        // 되돌리기 한 번 — 모두 돌아온다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(session.Editor.FindChapter("ch01")!.Episodes, episode => episode.EpisodeId == "a");
        Assert.Equal(2, session.Editor.FindChapter("ch01")!.Edges.Count);
        Assert.DoesNotContain(session.Editor.FindChapter("ch02")!.Episodes, episode => episode.EpisodeId == "a");

        window.Close();
    });

    [Fact]
    public void 챕터_시작은_다른_챕터로_끌면_붉은_테두리와_까닭이_서고_놓아도_옮기지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        StoryHierarchy tree = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        Unfold(tree);
        long before = session.Editor.Revision;

        (Point from, Point to, Button target) = Points(window, tree, "root");
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Color.FromRgb(0xDC, 0x26, 0x26), Assert.IsType<SolidColorBrush>(target.BorderBrush).Color);
        Assert.Null(tree.NoteTip);
        Assert.Contains("시작 에피소드입니다", tree.RefusalTip, StringComparison.Ordinal);

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("root", session.Editor.FindChapter("ch01")!.Episodes[0].EpisodeId);
        Assert.Equal(before, session.Editor.Revision);
        Assert.Contains("시작 에피소드입니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    /// <summary>그 에피소드 카드 아래 선택지 칸들의 글 — 문구(이은 · 적어 둔 칸).</summary>
    private static List<string> SlotTexts(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true)
            .SelectMany(row => row.Children.OfType<TextBlock>())
            .Select(text => text.Text ?? string.Empty)
            .ToList();

    private static void Unfold(StoryHierarchy tree)
    {
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>ch01의 그 에피소드 줄과 ch02 street 장면 줄 — 창 좌표.</summary>
    private static (Point From, Point To, Button Target) Points(MainWindow window, StoryHierarchy tree, string episodeId)
    {
        Button source = tree.RowButton(StoryHierarchy.EpisodeKey("ch01", episodeId))!;
        Button target = tree.RowButton(StoryHierarchy.SceneKey("ch02", "street"))!;
        return (
            source.TranslatePoint(new Point(30, source.Bounds.Height / 2), window)!.Value,
            target.TranslatePoint(new Point(30, target.Bounds.Height / 2), window)!.Value,
            target);
    }
}
