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
/// <b>하이어아키에서 에피소드를 다른 장면 줄로 끌 때, 옮기면 장면 입구가 둘이 되는 이동은 받지 않고 왜인지 말한다</b>
/// (2026-11-22 소유자 — 「그냥 안 되게 하기보다 왜 안 되는지 설명을」 · usability UX37). 끄는 동안 그 장면 줄에 붉은 테두리와 까닭 풍선이 서고,
/// 놓아도 옮기지 않으며 상태줄이 같은 까닭을 말한다. 입구가 늘지 않는 이동은 그대로 받는다(<c>HierarchyEditTests</c>).
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
    public void 옮기면_장면_입구가_둘이_되는_자리에는_붉은_테두리와_까닭이_서고_놓아도_옮기지_않는다() => HeadlessUi.Run(() =>
    {
        // opening: root → a, hall: b(root → b로 시작점). a를 hall로 옮기면 hall의 입구가 b · a 둘이다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        StoryHierarchy tree = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
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

        // 끄는 동안 잔상이 포인터를 따라다니고 끌던 줄은 흐려진다(usability UX38) — 못 받는 자리 위에서는 잔상 테두리도 붉다.
        Assert.Equal("a", tree.DragGhostText);
        Assert.Equal(Color.FromRgb(0xDC, 0x26, 0x26), Assert.IsType<SolidColorBrush>(tree.DragGhostBorder).Color);
        Assert.True(((Control)source.Parent!).Opacity < 1);

        Assert.Equal(new Thickness(1), target.BorderThickness);
        Assert.Equal(Color.FromRgb(0xDC, 0x26, 0x26), Assert.IsType<SolidColorBrush>(target.BorderBrush).Color);
        Assert.Contains("옮길 수 없습니다", tree.RefusalTip, StringComparison.Ordinal);
        Assert.Contains("밖에서 들어오는 자리가 2개", tree.RefusalTip, StringComparison.Ordinal);
        Assert.Contains("옮길 수 없습니다", session.StatusMessage, StringComparison.Ordinal);

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("opening", session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == "a").SceneId);
        Assert.Equal(before, session.Editor.Revision);   // 아무것도 적지 않았다
        Assert.Null(tree.RefusalTip);
        Assert.Null(tree.DragGhostText);   // 놓으면 잔상이 걷히고 줄이 돌아온다
        Assert.Equal(1, ((Control)source.Parent!).Opacity);
        Assert.Contains("옮길 수 없습니다", session.StatusMessage, StringComparison.Ordinal);

        // 길을 끊으면 옮겨진다 — 안내한 대로.
        session.Editor.RemoveEdge("ch01", "root", "a", "이쪽");
        Dispatcher.UIThread.RunJobs();
        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "a"), StoryHierarchy.SceneKey("ch01", "hall"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("hall", session.Editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == "a").SceneId);

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
}
