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
/// [대본] 탭의 장면 메모 공간 (2026-11-16 · scene-notes S4). 하이어아키에서 장면을 고르면 인스펙터에
/// 장면 메모 칸과, [+ 에피소드]로 골라 붙이는 에피소드 카드(그 에피소드의 지시 = <see cref="ChapterEpisode.Memo"/>)가 선다.
/// 칸에 쓰면 모델에 들어가고 되돌리기로 돌아간다. 지시를 비우면 카드가 빠진다. 메모가 있는 장면은 하이어아키 줄과 판의 장면 이름표에
/// 「메모」가 붙는다. 챕터를 고르면 주인 없는 메모가 [지우기]와 함께 선다.
/// </summary>
public sealed class DirectorSceneNotesTests : IDisposable
{
    private const int GraphTab = MainTab.Script;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-scene-notes", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 장면_메모_칸에_쓰면_모델에_들어가고_되돌리기로_돌아간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickScene(window, "opening");

        TextBox note = Box(graph, "scene-note");
        Assert.Equal(MainWindow.SceneNoteGuide, note.PlaceholderText);
        Assert.True(note.AcceptsReturn);

        note.Text = "  마감 전날.\n하린: 지침 → 결심  ";
        CtrlEnter(note);

        Assert.Equal([("opening", "마감 전날.\n하린: 지침 → 결심")],
            Chapter(session).SceneNotes.Select(item => (item.SceneId, item.Text)));
        Assert.Equal("마감 전날.\n하린: 지침 → 결심", note.Text);   // 칸이 넣은 값으로 맞는다

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Chapter(session).SceneNotes);
        Assert.Equal(string.Empty, Box(graph, "scene-note").Text);

        window.Close();
    });

    [Fact]
    public void Esc는_넣지_않은_글을_걷고_초점이_떠나면_넣는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickScene(window, "opening");
        TextBox note = Box(graph, "scene-note");

        note.Text = "걷힐 글";
        Key(note, Avalonia.Input.Key.Escape, KeyModifiers.None);
        Assert.Equal(string.Empty, note.Text);
        Assert.Empty(Chapter(session).SceneNotes);

        note.Focus();
        note.Text = "남을 글";
        Control(graph, "scene-note-add").Focus();   // 초점이 떠난다
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("남을 글", Assert.Single(Chapter(session).SceneNotes).Text);

        window.Close();
    });

    [Fact]
    public void 에피소드를_골라_붙인_카드는_지시를_비워도_남고_다시_열어도_있다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickScene(window, "opening");
        ComboBox add = (ComboBox)Control(graph, "scene-note-add");

        Assert.Equal(["scene-note-add:root", "scene-note-add:a"], AddTags(add));
        Assert.Null(Find(graph, "episode-note-card:a"));

        add.SelectedIndex = 1;   // a
        Dispatcher.UIThread.RunJobs();

        TextBox box = Box(graph, "episode-note:a");
        Assert.Equal(["scene-note-add:root"], AddTags(add));

        box.Text = "하린을 먼저 세운다";
        CtrlEnter(box);
        Assert.Equal("하린을 먼저 세운다", Episode(session, "a").Memo);

        box.Text = "   ";
        CtrlEnter(box);

        Assert.Null(Episode(session, "a").Memo);
        Assert.True(Episode(session, "a").NoteCard);
        Assert.NotNull(Find(graph, "episode-note-card:a"));   // [빼기]만 뗀다
        Assert.Equal(["scene-note-add:root"], AddTags(add));

        // 빈 채로 초점이 떠나도, 다른 곳을 골랐다 돌아와도 남는다.
        add.SelectedIndex = 0;   // root — 지시를 적지 않는다
        Dispatcher.UIThread.RunJobs();
        Box(graph, "scene-note").Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Find(graph, "episode-note-card:root"));

        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        graph = PickScene(window, "opening");

        Assert.Equal(string.Empty, Box(graph, "episode-note:root").Text);
        Assert.Equal(string.Empty, Box(graph, "episode-note:a").Text);

        window.Close();
    });

    [Fact]
    public void 지시가_있는_에피소드는_다시_열어도_카드이고_빼기로_지운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.UpdateEpisodeMemo("ch01", "a", "뒤의 카드");
        session.Editor.UpdateEpisodeMemo("ch01", "root", "앞의 카드");
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = PickScene(window, "opening");

        // 카드의 차례는 장면 안 에피소드 차례다 — 지시를 단 차례가 아니다.
        Assert.Equal(["episode-note-card:root", "episode-note-card:a"],
            Side(graph).Select(control => control.Tag as string).Where(tag => tag?.StartsWith("episode-note-card:", StringComparison.Ordinal) == true));
        Assert.Equal("뒤의 카드", Box(graph, "episode-note:a").Text);
        Assert.False(((ComboBox)Control(graph, "scene-note-add")).IsEnabled);

        Control(graph, "episode-note-remove:a").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Episode(session, "a").Memo);
        Assert.False(Episode(session, "a").HasNoteCard);
        Assert.Null(Find(graph, "episode-note-card:a"));

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("뒤의 카드", Episode(session, "a").Memo);

        window.Close();
    });

    [Fact]
    public void 메모가_있는_장면은_하이어아키_줄과_판의_이름표에_메모가_붙는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        session.SelectFile(session.EnsureChapterBoard("ch01"));
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("메모", RowTexts(graph, "opening"));
        Assert.DoesNotContain("· 메모", Label(graph, "opening"));

        session.Editor.SetSceneNote("ch01", "opening", "장면 메모");
        session.Editor.UpdateEpisodeMemo("ch01", "b", "에피소드 지시만");
        Dispatcher.UIThread.RunJobs();
        graph.Rebuild();

        Assert.Contains("메모", RowTexts(graph, "opening"));
        Assert.Contains("메모", RowTexts(graph, "hall"));
        Assert.Contains("· 메모", Label(graph, "opening"));
        Assert.Contains("· 메모", Label(graph, "hall"));

        window.Close();
    });

    [Fact]
    public void 에피소드_지시를_붙이고_쓰고_떼어도_판을_다시_짓지_않고_장면_이름표의_메모만_따라간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「에피소드 지시를 추가하고 빼는 것이 조금 느린데」(usability UX7). 판을 통째로 다시 짓는 데 들던 시간이었다 —
        // 에피소드 160개에서 한 번에 2초. 판에는 에피소드 지시가 서지 않고, 장면 이름표의 「메모」만 그것을 본다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickScene(window, "hall");
        int rails = graph.RailDrawCount;
        int frames = graph.SceneFrameDrawCount;
        Assert.DoesNotContain("· 메모", Label(graph, "hall"));

        // 붙인다 — [+ 에피소드]에서 고른다.
        var add = (ComboBox)Control(graph, "scene-note-add");
        add.SelectedIndex = 0;   // b
        Dispatcher.UIThread.RunJobs();
        Assert.True(Episode(session, "b").NoteCard);

        // 쓴다.
        TextBox box = Box(graph, "episode-note:b");
        box.Text = "복도에서 마주친다";
        CtrlEnter(box);
        Assert.Equal("복도에서 마주친다", Episode(session, "b").Memo);
        Assert.Contains("· 메모", Label(graph, "hall"));   // 판을 다시 짓지 않아도 이름표가 따라간다
        Assert.Contains("메모", RowTexts(graph, "hall"));

        // 뗀다.
        var remove = (Button)Control(graph, "episode-note-remove:b");
        remove.Focus();   // 사람이 누르면 초점이 먼저 단추로 간다 — 칸은 그때 넣는다(같은 글이라 적지 않는다)
        Dispatcher.UIThread.RunJobs();
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.False(Episode(session, "b").NoteCard);
        Assert.DoesNotContain("· 메모", Label(graph, "hall"));

        // 셋 다 판(카드 · 철도)을 다시 짓지 않았다 — 테두리 · 이름표만 다시 그었다.
        Assert.Equal(rails, graph.RailDrawCount);
        Assert.Equal(frames + 3, graph.SceneFrameDrawCount);

        // 되돌리면 지시가 돌아오고 장면 메모 공간도 그것을 보인다.
        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("복도에서 마주친다", Episode(session, "b").Memo);
        Assert.Contains("· 메모", Label(graph, "hall"));

        window.Close();
    });

    [Fact]
    public void 챕터를_고르면_주인_없는_메모가_서고_지우기로_지운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.SetSceneNote("ch01", "hall", "홀의 사정");
        session.Editor.UpdateEpisode("ch01", "b", sceneId: "opening");   // hall의 유일한 에피소드를 옮겼다 — 메모는 주인 없이 남는다
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.HierarchyView.RowButton(StoryHierarchy.ChapterKey("ch01"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("주인 없는 장면 메모", Side(graph).OfType<TextBlock>().Select(block => block.Text));
        Assert.Contains(Side(graph).OfType<SelectableTextBlock>(), block => block.Text == "hall — 홀의 사정");

        Control(graph, "orphan-note-remove:hall").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Chapter(session).SceneNotes);
        Assert.Null(Find(graph, "orphan-note:hall"));
        Assert.DoesNotContain("주인 없는 장면 메모", Side(graph).OfType<TextBlock>().Select(block => block.Text));

        window.Close();
    });

    [Fact]
    public void 장면_요약은_에피소드_목록을_세우지_않고_카드는_보이는_제목이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.Editor.UpdateEpisode("ch01", "a", title: "교실 - 반쯤 빈 간판");
        session.Editor.UpdateEpisodeMemo("ch01", "a", "지시");
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = PickScene(window, "opening");

        Assert.DoesNotContain(Side(graph), control =>
            (control.Tag as string)?.StartsWith("summary-episode:", StringComparison.Ordinal) == true);
        Assert.Contains("· 교실 - 반쯤 빈 간판", Side(graph).OfType<TextBlock>().Select(block => block.Text));   // 지시 카드 머리
        Assert.Equal(["root"], AddContents((ComboBox)Control(graph, "scene-note-add")));                     // 남은 에피소드

        window.Close();
    });

    [Fact]
    public void 요약_칸은_넘치면_세로로_스크롤하고_보이기는_요약을_따른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickScene(window, "opening");
        ScrollViewer scroll = graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<ScrollViewer>()
            .Single(viewer => viewer.Tag as string == "director-summary-scroll");

        Assert.True(scroll.IsVisible);
        Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
        Assert.Contains(Box(graph, "scene-note"), ((Control)scroll.Content!).GetLogicalDescendants());

        // 카드를 고르면 요약이 풀린다 — 빈 스크롤 칸이 대사 편집기 위에 남지 않는다.
        graph.HierarchyView.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        graph.HierarchyView.RowButton(StoryHierarchy.EpisodeKey("ch01", "a"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.False(scroll.IsVisible);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static GraphEditorView PickScene(MainWindow window, string sceneId)
    {
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", sceneId))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(sceneId, graph.InspectorHeaderText);   // 머리는 이름만(2026-11-18)
        return graph;
    }

    private static ChapterDocument Chapter(AuthoringSession session) => session.Editor.FindChapter("ch01")!;

    private static ChapterEpisode Episode(AuthoringSession session, string episodeId) =>
        Chapter(session).Episodes.Single(episode => episode.EpisodeId == episodeId);

    private static IEnumerable<Control> Side(GraphEditorView graph) =>
        graph.FindControl<ContentControl>("SideHost")!.GetLogicalDescendants().OfType<Control>().Distinct()
            .Where(control => control.IsEffectivelyVisible);

    private static Control? Find(GraphEditorView graph, string tag) =>
        Side(graph).SingleOrDefault(control => (string?)control.Tag == tag);

    private static Control Control(GraphEditorView graph, string tag) =>
        Find(graph, tag) ?? throw new Xunit.Sdk.XunitException($"'{tag}'가 없습니다.");

    private static TextBox Box(GraphEditorView graph, string tag) => (TextBox)Control(graph, tag);

    private static IEnumerable<string> AddContents(ComboBox add) =>
        add.Items.OfType<ComboBoxItem>().Select(item => item.Content as string ?? string.Empty);

    private static List<string?> AddTags(ComboBox add) =>
        add.Items.OfType<ComboBoxItem>().Select(item => item.Tag as string).ToList();

    private static void CtrlEnter(TextBox box) => Key(box, Avalonia.Input.Key.Enter, KeyModifiers.Control);

    private static void Key(TextBox box, Key key, KeyModifiers modifiers)
    {
        box.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = box
        });
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> RowTexts(GraphEditorView graph, string sceneId) =>
        ((StackPanel)graph.HierarchyView.RowButton(StoryHierarchy.SceneKey("ch01", sceneId))!.Content!)
            .Children.OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();

    private static string Label(GraphEditorView graph, string sceneId) =>
        ((Panel)graph.FindControl<Canvas>("FrameLabelLayer")!.Children.OfType<Border>()   // 이름표는 판 위의 화면 겹에 선다 — 점 · 이름 줄
            .Single(border => border.Tag as string == GraphEditorView.SceneLabelTag + sceneId).Child!).Children.OfType<TextBlock>().Single().Text ?? string.Empty;
}
