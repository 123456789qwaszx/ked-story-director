using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

        // 장면 밖 · 챕터 테두리 안(왼위 구석의 여백) — 그 챕터를 고른다. 「조건 및 스탯」 탭이 열려 그 챕터를 보인다(이름 칸 · 스탯 · 조건).
        Click(graph, InsideChapter(graph, "ch01"));

        Assert.Null(session.SelectedNodeId);
        Assert.Equal("ch01", Title(graph));
        Assert.Equal(SideTab.Conditions, graph.ShownSideTab);   // 챕터는 「조건 및 스탯」 탭(chapter-pick Y1)
        Assert.Equal("ch01", graph.FindControl<ComboBox>("ConditionChapterCombo")!.SelectedItem);
        Assert.Equal(StoryHierarchy.ChapterKey("ch01"), graph.HierarchyView.Selection?.Key);

        // 챕터 밖의 빈 판 — 프로젝트를 고른다: 「화자」 탭이 열린다(side-tabs V3).
        Click(graph, new Point(3000, 3000));
        Assert.Equal("프로젝트", Title(graph));
        Assert.Equal(SideTab.Speakers, graph.ShownSideTab);
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
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Script;
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

        // 챕터 안 · 장면 밖(왼위 구석의 여백) — 새 장면과 첫 에피소드가 누른 자리에.
        Point inChapter = InsideChapter(graph, "ch01");
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

    [Fact]
    public void 우클릭을_빠르게_거듭해도_판_차림표는_하나뿐이고_한_번_고르면_하나만_선다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 보고 — 우클릭을 빠르게 하면 차림표가 겹겹이 떠 챕터 생성 · 에피소드 생성이 「복제」됐다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Point inHall = InsideScene(graph, "hall");
        int before = session.Editor.FindChapter("ch01")!.Episodes.Count;

        RightClick(graph, inHall);
        ContextMenu menu = graph.BoardMenu!;
        RightClick(graph, inHall + new Point(2, 0));
        RightClick(graph, inHall + new Point(4, 0));

        Assert.Same(menu, graph.BoardMenu);   // 새로 짓지 않는다 — 떠 있는 것은 늘 하나다
        Assert.True(menu.IsOpen);
        Choose(graph, "에피소드 추가");
        Assert.Equal(before + 1, session.Editor.FindChapter("ch01")!.Episodes.Count);
        Assert.False(menu.IsOpen);

        // 떠 있는 동안 다른 데를 누르면 닫힌다(아무것도 더하지 않는다).
        RightClick(graph, inHall);
        Assert.True(menu.IsOpen);
        window.MouseDown(new Point(100, 700), MouseButton.Left);
        window.MouseUp(new Point(100, 700), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu.IsOpen);
        Assert.Equal(before + 1, session.Editor.FindChapter("ch01")!.Episodes.Count);

        window.Close();
    });

    [Fact]
    public void 차림표가_떠_있을_때_다른_곳을_우클릭하면_닫히고_그_자리의_차림표가_곧바로_뜬다() => HeadlessUi.Run(() =>
    {
        // 2026-11-25 소유자 — 「다른 영역을 다시 우클릭하면 기능 목록이 나오는 대신 기존 목록이 꺼진다 … 끄는 것과 동시에 바로 기능 목록이
        // 나오도록」. 창에 실제로 누른다 — 떠 있는 차림표의 바깥 누름 받이를 거쳐야 그 일이 재현된다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        int episodes = session.Editor.FindChapter("ch01")!.Episodes.Count;

        // 장면 hall 테두리의 아래 가장자리 바로 안은 장면 안, 그 아래는 장면 밖 · 챕터 안이다(챕터 테두리가 장면 테두리보다 40 더 나와 있다).
        Rect hallFrame = graph.SceneAreaOf("hall")!.Value;
        double hallBottom = hallFrame.Bottom;
        double hallLeft = hallFrame.X;
        var inHall = new Point(hallLeft + 30, hallBottom - 4);
        var inChapter = new Point(hallLeft + 30, hallBottom + 24);
        Rect card = Bounds(graph, CardId(session, "b"));
        var onCard = new Point(card.X + 30, card.Y + 12);

        // 그 자리들이 판의 보이는 곳에 오게 둔다 — 창에 누르는 것이라 가려져 있으면 다른 것이 받는다.
        var scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        Point corner = Canvas(graph).TranslatePoint(new Point(card.X - 60, card.Y - 60), scroll)!.Value;
        scroll.Offset += new Vector(corner.X, corner.Y);
        Dispatcher.UIThread.RunJobs();

        void RightClickOnWindow(Point onCanvas)
        {
            window.MouseDown(At(graph, onCanvas), MouseButton.Right);
            window.MouseUp(At(graph, onCanvas), MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
        }

        // 차림표는 누른 자리에서 오른쪽 아래로 뜬다 — 다음 우클릭은 그 차림표 밖(위쪽)에 한다.
        RightClickOnWindow(inChapter);
        ContextMenu menu = graph.BoardMenu!;
        Assert.True(menu.IsOpen);
        Assert.Equal("장면 추가", Assert.Single(MenuItems(graph)).Header);

        // 떠 있는 채로 장면 안을 우클릭 — 한 번에 그 자리의 차림표로 바뀐다.
        RightClickOnWindow(inHall);
        Assert.True(menu.IsOpen);
        Assert.Equal("에피소드 추가", Assert.Single(MenuItems(graph)).Header);

        // 카드 위로도 — 카드 차림표로 바뀐다.
        RightClickOnWindow(onCard);
        Assert.True(menu.IsOpen);
        Assert.Contains(MenuItems(graph), item => item.Header as string == "제목 고치기");

        // 다시 빈 곳으로 — 판 차림표로 돌아온다.
        RightClickOnWindow(inChapter);
        Assert.True(menu.IsOpen);
        Assert.Equal("장면 추가", Assert.Single(MenuItems(graph)).Header);

        // 왼쪽 하이어아키 줄로도 — 판 차림표가 닫히고 그 줄의 차림표가 뜬다. 거꾸로 줄 차림표가 떠 있을 때 판을 우클릭하면 판 차림표가 뜬다.
        Button row = graph.HierarchyView.GetVisualDescendants().OfType<Button>()
            .First(button => button.ContextMenu is not null && button.IsEffectivelyVisible && button.Bounds.Width > 0);
        Point onRow = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(onRow, MouseButton.Right);
        window.MouseUp(onRow, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu.IsOpen);
        Assert.True(row.ContextMenu!.IsOpen);

        RightClickOnWindow(inHall);
        Assert.False(row.ContextMenu!.IsOpen);
        Assert.True(menu.IsOpen);
        Assert.Equal("에피소드 추가", Assert.Single(MenuItems(graph)).Header);

        // 바깥 좌클릭은 지금처럼 닫기만 한다.
        window.MouseDown(new Point(100, 700), MouseButton.Left);
        window.MouseUp(new Point(100, 700), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu.IsOpen);
        Assert.Equal(episodes, session.Editor.FindChapter("ch01")!.Episodes.Count);   // 우클릭만으로는 아무것도 더하지 않는다

        window.Close();
    });

    private static List<MenuItem> MenuItems(GraphEditorView graph) =>
        ((IEnumerable<object>)graph.BoardMenu!.ItemsSource!).OfType<MenuItem>().ToList();

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
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

    /// <summary>그 장면 테두리 안의 빈 곳 — 왼위 구석(이름표는 그보다 오른쪽에 서고, 카드는 위 여백 26 아래에 선다).</summary>
    private static Point InsideScene(GraphEditorView graph, string sceneId)
    {
        Rect area = graph.SceneAreaOf(sceneId)!.Value;
        return new Point(area.X + 5, area.Y + 8);
    }

    /// <summary>그 챕터 테두리 안 · 장면 밖의 빈 곳 — 왼위 구석(장면 테두리는 그보다 옆 40 · 위 46 안쪽에 선다).</summary>
    private static Point InsideChapter(GraphEditorView graph, string chapterId)
    {
        Rect area = graph.ChapterAreaOf(chapterId)!.Value;
        return new Point(area.X + 6, area.Y + 12);
    }

    private static string? Title(GraphEditorView graph) => graph.InspectorHeaderText;

    private static IEnumerable<Control> Side(GraphEditorView graph) =>
        graph.ShownSideHost.GetLogicalDescendants().OfType<Control>().Distinct()
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
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));   // 차림표는 항목이 닫는다
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
