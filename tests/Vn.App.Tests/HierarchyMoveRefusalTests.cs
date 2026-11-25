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
/// <b>하이어아키에서 에피소드를 다른 장면 줄로 끌 때, 장면 입구가 둘이 되게 하는 선은 끊고 옮긴다</b> (2026-11-26 소유자 — 「깨지는 선만 끊고
/// 옮기기」). 끄는 동안 그 장면 줄 옆 풍선과 상태줄이 끊을 선을 미리 말하고, 놓으면 그 선만 끊고 옮긴다(한 되돌리기 칸). 끊어도 입구가 둘인 이동
/// (챕터 시작을 시작점이 있는 장면으로)만 붉은 테두리와 까닭 풍선이 서고 놓아도 옮기지 않는다(2026-11-22 · usability UX37 — ⛔ 그날까지는 입구가
/// 둘이 되는 이동을 모두 그렇게 막았다). 입구가 늘지 않는 이동은 그대로 받는다(<c>HierarchyEditTests</c>).
/// </summary>
public sealed class HierarchyMoveRefusalTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-move-refusal", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 옮기면_장면_입구가_둘이_되는_자리에는_끊을_선을_미리_말하고_놓으면_그_선만_끊고_옮긴다() => HeadlessUi.Run(() =>
    {
        // opening: root → a, hall: b(root → b로 시작점). a를 hall로 옮기면 root → a가 hall의 두 번째 입구다 — 그 선만 끊는다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        StoryHierarchy tree = graph.HierarchyView;
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "이쪽");
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "저쪽");
        Dispatcher.UIThread.RunJobs();
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        long before = session.Editor.Revision;

        // 실제 손으로 — 누르고, 장면 줄 위로 옮긴다.
        Button source = tree.RowButton(StoryHierarchy.EpisodeKey("ch01", "a"))!;
        Button target = tree.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!;
        Point from = source.TranslatePoint(new Point(30, source.Bounds.Height / 2), window)!.Value;
        Point to = target.TranslatePoint(new Point(30, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        // 받는 자리다 — 잔상 · 테두리가 파랗고, 끊을 선을 풍선과 상태줄이 미리 말한다.
        Assert.Equal("a", tree.DragGhostText);
        Assert.Equal(Color.FromRgb(0x3D, 0x7B, 0xD9), Assert.IsType<SolidColorBrush>(tree.DragGhostBorder).Color);
        Assert.Equal(new Thickness(1), target.BorderThickness);
        Assert.Null(tree.RefusalTip);
        Assert.Contains("선 1개를 끊습니다", tree.NoteTip, StringComparison.Ordinal);
        Assert.Contains("'root'→'a' 「이쪽」", tree.NoteTip, StringComparison.Ordinal);
        Assert.Contains("선 1개를 끊습니다", session.StatusMessage, StringComparison.Ordinal);

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("hall", session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == "a").SceneId);
        Assert.Equal([("root", "b")], session.Editor.FindChapter("ch01")!.Edges.Select(edge => (edge.FromEpisodeId, edge.ToEpisodeId)));
        Assert.Equal(before + 1, session.Editor.Revision);   // 끊기와 옮기기가 한 칸
        Assert.Null(tree.NoteTip);
        Assert.Null(tree.DragGhostText);
        Assert.Contains("선 1개를 끊었습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("적힌 채 남습니다", session.StatusMessage, StringComparison.Ordinal);

        // 끊은 선택지의 문구는 root 카드의 칸에 적힌 채다(빈 곳에 놓아 끊기와 같다).
        Assert.Contains("이쪽", SlotTexts(graph, "root"));

        // 되돌리기 한 번 — 선도 장면도 돌아온다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("opening", session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == "a").SceneId);
        Assert.Equal(2, session.Editor.FindChapter("ch01")!.Edges.Count);

        window.Close();
    });

    [Fact]
    public void 챕터_시작을_시작점이_있는_장면으로_옮기면_붉은_테두리와_까닭이_서고_놓아도_옮기지_않는다() => HeadlessUi.Run(() =>
    {
        // 챕터 시작(root)은 선과 상관없이 늘 입구다. hall에는 a → b로 이미 입구가 있다 — 끊을 선이 없는데도 입구가 둘이다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        StoryHierarchy tree = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "이쪽");
        session.Editor.AddEdge("ch01", "a", "b", optionLabel: "저쪽");
        Dispatcher.UIThread.RunJobs();
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        long before = session.Editor.Revision;

        Button source = tree.RowButton(StoryHierarchy.EpisodeKey("ch01", "root"))!;
        Button target = tree.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!;
        Point from = source.TranslatePoint(new Point(30, source.Bounds.Height / 2), window)!.Value;
        Point to = target.TranslatePoint(new Point(30, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        // 끄는 동안 잔상이 포인터를 따라다니고 끌던 줄은 흐려진다(usability UX38) — 못 받는 자리 위에서는 잔상 테두리도 붉다.
        Assert.Equal("root", tree.DragGhostText);
        Assert.Equal(Color.FromRgb(0xDC, 0x26, 0x26), Assert.IsType<SolidColorBrush>(tree.DragGhostBorder).Color);
        Assert.True(((Control)source.Parent!).Opacity < 1);

        Assert.Equal(new Thickness(1), target.BorderThickness);
        Assert.Equal(Color.FromRgb(0xDC, 0x26, 0x26), Assert.IsType<SolidColorBrush>(target.BorderBrush).Color);
        Assert.Null(tree.NoteTip);
        Assert.Contains("옮길 수 없습니다", tree.RefusalTip, StringComparison.Ordinal);
        Assert.Contains("챕터 시작 에피소드는 늘 그 장면의 입구", tree.RefusalTip, StringComparison.Ordinal);
        Assert.Contains("옮길 수 없습니다", session.StatusMessage, StringComparison.Ordinal);

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("opening", session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == "root").SceneId);
        Assert.Equal(before, session.Editor.Revision);   // 아무것도 적지 않았다
        Assert.Null(tree.RefusalTip);
        Assert.Null(tree.DragGhostText);   // 놓으면 잔상이 걷히고 줄이 돌아온다
        Assert.Equal(1, ((Control)source.Parent!).Opacity);
        Assert.Contains("옮길 수 없습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 그냥_누르면_잔상이_서지_않고_끌면_서며_받는_자리_위에서는_파랗다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        StoryHierarchy tree = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        Dispatcher.UIThread.RunJobs();

        Button source = tree.RowButton(StoryHierarchy.EpisodeKey("ch01", "a"))!;
        Button target = tree.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!;
        Point from = source.TranslatePoint(new Point(30, source.Bounds.Height / 2), window)!.Value;
        Point to = target.TranslatePoint(new Point(30, target.Bounds.Height / 2), window)!.Value;

        // 누르고 거의 안 움직였다 — 고르기다.
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Point(1, 1), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(tree.DragGhostText);

        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("a", tree.DragGhostText);
        Assert.Equal(Color.FromRgb(0x3D, 0x7B, 0xD9), Assert.IsType<SolidColorBrush>(tree.DragGhostBorder).Color);

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(tree.DragGhostText);
        Assert.Equal("hall", session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == "a").SceneId);

        window.Close();
    });

    /// <summary>그 에피소드 카드 아래 선택지 칸들의 글 — 문구(이은 · 적어 둔 칸).</summary>
    private static List<string> SlotTexts(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(row => (row.Tag as string)?.StartsWith(episodeId + "#", StringComparison.Ordinal) == true)
            .SelectMany(row => row.Children.OfType<TextBlock>())
            .Select(text => text.Text ?? string.Empty)
            .ToList();
}
