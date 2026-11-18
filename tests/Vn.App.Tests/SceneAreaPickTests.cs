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
/// 같다 — 인스펙터에 메모 공간이 서고 하이어아키가 그 줄을 비춘다. 그 상태에서 [＋ 에피소드]는 그 장면에 세운다(하이어아키에서
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
        Assert.Equal("hall", Title(graph));
        Assert.Contains(Side(graph), control => (string?)control.Tag == "scene-note");
        Assert.Equal(StoryHierarchy.SceneKey("ch01", "hall"), graph.HierarchyView.Selection?.Key);

        // 다른 장면을 누르면 그 장면으로 바뀐다.
        Click(graph, InsideScene(graph, "opening"));
        Assert.Equal("opening", Title(graph));

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
        Assert.Equal("ch01", Title(graph));
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
    public void 장면을_고른_채_에피소드를_더하면_그_장면에_서고_다른_카드와_겹치지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Click(graph, InsideScene(graph, "hall"));

        graph.FindControl<Button>("AddEpisodeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
    public void 고른_장면_고른_카드의_장면_아니면_새_장면에_선다_장면_없는_에피소드는_서지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 장면 없는 에피소드를 허용하지 않는다. 고른 장면 → 고른 카드의 장면 → 새 장면 차례.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Button add = graph.FindControl<Button>("AddEpisodeButton")!;

        graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", "opening"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode first = session.Editor.FindChapter("ch01")!.Episodes[^1];
        Assert.Equal("opening", first.SceneId);

        // 이제 새 카드가 골라져 있다(장면 요약이 풀렸다) — 다시 더하면 그 카드의 장면에 선다.
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode second = session.Editor.FindChapter("ch01")!.Episodes[^1];
        Assert.NotEqual(first.EpisodeId, second.EpisodeId);
        Assert.Equal("opening", second.SceneId);

        // 아무것도 안 고르면 새 장면(「새 장면」)과 함께 선다 — 되돌리기 한 칸에 둘 다 걷힌다.
        session.Select(null);
        Dispatcher.UIThread.RunJobs();
        long before = session.Editor.Revision;
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        ChapterEpisode third = chapter.Episodes[^1];
        Assert.False(string.IsNullOrWhiteSpace(third.SceneId));
        Assert.NotEqual("opening", third.SceneId);
        Assert.NotEqual("hall", third.SceneId);
        Assert.Equal(NewNames.Scene, ChapterSceneNotes.NameOf(chapter.SceneNotes, third.SceneId!));
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.All(chapter.Episodes, episode => Assert.False(string.IsNullOrWhiteSpace(episode.SceneId)));

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(session.Editor.FindChapter("ch01")!.Episodes, episode => episode.EpisodeId == third.EpisodeId);
        Assert.Null(ChapterSceneNotes.NameOf(session.Editor.FindChapter("ch01")!.SceneNotes, third.SceneId!));

        window.Close();
    });

    [Fact]
    public void 빈_챕터에서도_새_장면과_함께_선다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        (string? chapterId, _) = session.CreateNamedChapter("빈 챕터");
        Dispatcher.UIThread.RunJobs();
        session.Select(null);

        graph.FindControl<Button>("AddEpisodeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode made = Assert.Single(session.Editor.FindChapter(chapterId!)!.Episodes);
        Assert.False(string.IsNullOrWhiteSpace(made.SceneId));

        window.Close();
    });

    [Fact]
    public void 챕터가_하나도_없으면_챕터와_장면을_함께_세운다_되돌리기_한_칸() => HeadlessUi.Run(() =>
    {
        // 처음 쓰는 사람을 위해(2026-11-17 소유자) — 아무것도 없는 프로젝트에서 [＋ 에피소드]를 누르면 「새 챕터」 · 「새 장면」이 선다.
        string folder = Path.Combine(_directory, "empty");
        Directory.CreateDirectory(folder);
        string manifest = Path.Combine(folder, "p" + Vn.Authoring.Serialization.ProjectManifestJson.FileExtension);
        Vn.Authoring.Serialization.ProjectStore.Save(manifest, new StoryProject { Title = "빈 프로젝트" });

        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        long before = session.Editor.Revision;

        graph.FindControl<Button>("AddEpisodeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        ChapterDocument chapter = Assert.Single(session.Project.Chapters);
        Assert.Equal(NewNames.Chapter, chapter.DisplayName);
        ChapterEpisode made = Assert.Single(chapter.Episodes);
        Assert.Equal(NewNames.Scene, ChapterSceneNotes.NameOf(chapter.SceneNotes, made.SceneId!));
        Assert.NotNull(EpisodeNaming.CardFor(session.Project, chapter.ChapterId, made.EpisodeId));
        Assert.Equal(EpisodeNaming.CardFor(session.Project, chapter.ChapterId, made.EpisodeId)!.Id, session.SelectedNodeId);
        Assert.Contains("새 챕터", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(before + 1, session.Editor.Revision);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(session.Project.Chapters);

        window.Close();
    });

    [Fact]
    public void 판의_빈_곳_우클릭은_장면_안에서_에피소드를_챕터_안에서_장면을_챕터_밖에서_챕터를_누른_자리에_더한다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「챕터 바깥 빈 공간에서 우클릭 하면 챕터 추가. 챕터 내에서 우클릭 하면 장면 추가, 장면 내에서 우클릭 하면 에피소드 추가」.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        ChapterDocument Ch01() => session.Editor.FindChapter("ch01")!;
        string SceneOf(DialogueNode card) =>
            Ch01().Episodes.Single(episode => episode.EpisodeId == EpisodeNaming.EpisodeIdOf(card)).SceneId!;

        // 챕터 안 · 장면 밖(챕터 이름표 왼쪽의 여백) — 새 장면과 첫 에피소드가 누른 자리에.
        Border chapterLabel = Canvas(graph).Children.OfType<Border>()
            .Single(border => border.Child is TextBlock { Text: "챕터 ch01 ⌄" });
        var inChapter = new Point(Avalonia.Controls.Canvas.GetLeft(chapterLabel) - 6, Avalonia.Controls.Canvas.GetTop(chapterLabel) + 2);
        int scenes = Ch01().Episodes.Select(episode => episode.SceneId).Distinct().Count();
        long before = session.Editor.Revision;
        RightClick(graph, inChapter);
        Choose(graph, "장면 추가");

        DialogueNode sceneCard = (DialogueNode)session.SelectedNode!;
        Assert.Equal(scenes + 1, Ch01().Episodes.Select(episode => episode.SceneId).Distinct().Count());
        Assert.Equal(NewNames.Scene, ChapterSceneNotes.NameOf(Ch01().SceneNotes, SceneOf(sceneCard)));
        Assert.Equal(before + 1, session.Editor.Revision);   // 장면 · 카드 · 이름이 한 칸
        Assert.True(Math.Abs(sceneCard.Layout.Y - (inChapter.Y - 20)) < 1 || sceneCard.Layout.Y == 0);

        // 장면 안 — 그 장면에, 누른 자리에.
        Point inHall = InsideScene(graph, "hall");
        before = session.Editor.Revision;
        RightClick(graph, inHall);
        Choose(graph, "에피소드 추가");

        DialogueNode hallCard = (DialogueNode)session.SelectedNode!;
        Assert.Equal("hall", SceneOf(hallCard));
        Assert.Equal(before + 1, session.Editor.Revision);
        Assert.Equal(inHall.Y - 20, hallCard.Layout.Y, 1);

        // 챕터 밖 — 하이어아키의 「챕터 추가」와 같다(세우고 고른다). 첫 장면 · 에피소드의 카드는 누른 자리에 선다.
        RightClick(graph, new Point(3000, 3000));
        Choose(graph, "챕터 추가");
        ChapterEpisode first = Assert.Single(session.Editor.FindChapter("ch03")!.Episodes);
        Assert.Equal(StoryHierarchy.ChapterKey("ch03"), graph.HierarchyView.Selection?.Key);
        Assert.Equal(3000 - 20, EpisodeNaming.CardFor(session.Project, "ch03", first.EpisodeId)!.Layout.Y, 1);

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

    private static void RightClick(GraphEditorView graph, Point onCanvas)
    {
        Canvas canvas = Canvas(graph);
        canvas.RaiseEvent(new PointerPressedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), (Visual)TopLevel.GetTopLevel(canvas)!, At(graph, onCanvas), 0,
            new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>판 우클릭 차림표의 항목 — 자리마다 하나뿐이다.</summary>
    private static void Choose(GraphEditorView graph, string header)
    {
        MenuItem item = Assert.Single(((IEnumerable<object>)graph.BoardMenu!.ItemsSource!).OfType<MenuItem>());
        Assert.Equal(header, item.Header);
        graph.BoardMenu.Close();
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
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
