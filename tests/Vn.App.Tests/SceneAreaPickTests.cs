using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판에서 장면의 영역을 누른다</b> (2026-11-17 소유자). 장면 테두리 안의 빈 곳을 누르면 하이어아키에서 그 장면 줄을 고른 것과
/// 같다 — 인스펙터에 메모 공간이 서고 하이어아키가 그 줄을 비춘다. 그 상태에서 [＋ 대사 노드]는 그 장면에 세운다(하이어아키에서
/// 장면을 골랐을 때도 같다). 끌면 범위 선택이고, 테두리 밖의 빈 곳은 지금까지와 같다.
/// </summary>
public sealed class SceneAreaPickTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-scene-area", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 장면_영역의_빈_곳을_누르면_그_장면의_메모_공간이_서고_하이어아키가_그_줄을_비춘다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        session.Select(CardId(session, "root"));
        Dispatcher.UIThread.RunJobs();

        Click(graph, InsideScene(graph, "hall"));

        Assert.Null(session.SelectedNodeId);
        Assert.Equal("장면 · hall", Title(graph));
        Assert.Contains(Side(graph), control => (string?)control.Tag == "scene-note");
        Assert.Equal(StoryHierarchy.SceneKey("ch01", "hall"), graph.HierarchyView.Selection?.Key);

        // 다른 장면을 누르면 그 장면으로 바뀐다.
        Click(graph, InsideScene(graph, "opening"));
        Assert.Equal("장면 · opening", Title(graph));

        window.Close();
    });

    [Fact]
    public void 챕터_영역은_챕터를_챕터_밖의_빈_판은_프로젝트를_고르고_끌기는_아무것도_고르지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        session.Select(CardId(session, "root"));
        Dispatcher.UIThread.RunJobs();

        // 장면 밖 · 챕터 테두리 안(챕터 이름표 왼쪽의 여백) — 그 챕터의 스탯 · 조건이 선다.
        Border chapterLabel = Canvas(graph).Children.OfType<Border>()
            .Single(border => border.Child is TextBlock { Text: "챕터 ch01 ⌄" });
        Click(graph, new Point(Avalonia.Controls.Canvas.GetLeft(chapterLabel) - 6, Avalonia.Controls.Canvas.GetTop(chapterLabel) + 2));

        Assert.Null(session.SelectedNodeId);
        Assert.Equal("챕터 · ch01", Title(graph));
        Assert.Contains(Side(graph), control => (string?)control.Tag == "condition-add-name");
        Assert.Equal(StoryHierarchy.ChapterKey("ch01"), graph.HierarchyView.Selection?.Key);

        // 챕터 밖의 빈 판 — 화자 목록이 선다.
        Click(graph, new Point(3000, 3000));
        Assert.Equal("화자", Title(graph));
        Assert.Contains(Side(graph), control => (string?)control.Tag == "speaker-add-name");
        Assert.Equal(StoryHierarchy.ProjectKey, graph.HierarchyView.Selection?.Key);

        string? before = Title(graph);

        // 장면 안에서 시작해도 끌었으면 범위 선택이다 — 아무것도 못 잡았으면 아무것도 고르지 않는다.
        Point inside = InsideScene(graph, "hall");
        Press(Canvas(graph), At(graph, inside));
        Move(Canvas(graph), At(graph, inside + new Point(0, -60)));
        Release(Canvas(graph), At(graph, inside + new Point(0, -60)));
        Assert.Equal(before, Title(graph));

        window.Close();
    });

    [Fact]
    public void 장면을_고른_채_대사_노드를_더하면_그_장면에_서고_다른_카드와_겹치지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Click(graph, InsideScene(graph, "hall"));

        graph.FindControl<Button>("AddDialogueButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode added = session.Editor.FindChapter("ch01")!.Episodes
            .Single(episode => episode.EpisodeId is not ("root" or "a" or "b"));
        Assert.Equal("hall", added.SceneId);
        Assert.Equal(NewNames.Episode, added.Title);

        // 새 카드가 골라지고(대사 편집기), 판에서 다른 카드와 겹치지 않는다.
        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", added.EpisodeId)!;
        Assert.Equal(card.Id, session.SelectedNodeId);
        Rect mine = Bounds(graph, card.Id);
        Assert.All(new[] { "root", "a", "b" }, other => Assert.False(mine.Intersects(Bounds(graph, CardId(session, other)))));

        // 되돌리기 한 칸이다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, session.Editor.FindChapter("ch01")!.Episodes.Count);

        window.Close();
    });

    [Fact]
    public void 하이어아키에서_장면을_골라도_같고_장면을_고르지_않았으면_장면_없이_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Button add = graph.FindControl<Button>("AddDialogueButton")!;

        graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "opening"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode first = session.Editor.FindChapter("ch01")!.Episodes[^1];
        Assert.Equal("opening", first.SceneId);

        // 이제 새 카드가 골라져 있다(장면 요약이 풀렸다) — 다시 더하면 장면 없이 선다.
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode second = session.Editor.FindChapter("ch01")!.Episodes[^1];
        Assert.NotEqual(first.EpisodeId, second.EpisodeId);
        Assert.True(string.IsNullOrEmpty(second.SceneId));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        session.SetFileExpanded(EpisodeNaming.BoardOf(session.Project, "ch02")!.Id, expanded: false);
        Dispatcher.UIThread.RunJobs();
        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    private static Canvas Canvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static Rect Bounds(GraphEditorView graph, string nodeId)
    {
        Border card = Canvas(graph).Children.OfType<Border>().Single(item => item.Tag as string == nodeId);
        return new Rect(Avalonia.Controls.Canvas.GetLeft(card), Avalonia.Controls.Canvas.GetTop(card), card.Bounds.Width, card.Bounds.Height);
    }

    /// <summary>그 장면 테두리 안의 빈 곳 — 이름표 왼쪽의 여백(테두리는 이름표보다 왼쪽으로 10, 위로 6 나와 있다).</summary>
    private static Point InsideScene(GraphEditorView graph, string sceneId)
    {
        Border label = Canvas(graph).Children.OfType<Border>()
            .Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + sceneId);
        return new Point(Avalonia.Controls.Canvas.GetLeft(label) - 5, Avalonia.Controls.Canvas.GetTop(label) + 2);
    }

    private static string? Title(GraphEditorView graph) => graph.FindControl<TextBlock>("InspectorTitle")!.Text;

    private static IEnumerable<Control> Side(GraphEditorView graph) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Control>().Distinct()
            .Where(control => control.IsEffectivelyVisible);

    private static void Click(GraphEditorView graph, Point onCanvas)
    {
        Press(Canvas(graph), At(graph, onCanvas));
        Release(Canvas(graph), At(graph, onCanvas));
    }

    private static Point At(GraphEditorView graph, Point onCanvas) =>
        Canvas(graph).TranslatePoint(onCanvas, (Visual)TopLevel.GetTopLevel(graph)!)!.Value;

    private static void Press(Control control, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Move(Canvas canvas, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, canvas, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Canvas canvas, Point at)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, at, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }
}
