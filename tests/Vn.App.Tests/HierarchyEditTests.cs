using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>하이어아키에서 고치기</b> — 우클릭 차림표와 두 번 눌러 이름 바꾸기. 고치는 하이어아키는 [대본] 탭의 것이다 — [구성] 탭
/// 하이어아키는 고르기 · 접기만 한다(읽기 전용).
///
/// 「챕터 · 장면 추가」는 어느 줄에서나 같은 자리에 서고(어디서 눌러야 무엇이 나오는지 외울 것이 없게) 어디에 붙일지는 누른 줄이
/// 정한다. 「에피소드 추가」는 장면 줄에만 서고 길 없이 세운다(2026-11-18 소유자). 그 아래에 그 줄만의 일이 선다. 새 장면은 첫
/// 에피소드와 함께 선다 — 장면은 에피소드가 장면ID를 들어야 있는 것이다.
/// </summary>
public sealed class HierarchyEditTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-hierarchy-edit", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 줄마다_제_일만_서고_에피소드_추가는_장면_줄에만_선다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 장면 없는 에피소드를 허용하지 않는다. 챕터 줄 「에피소드 추가」 · 「장면 풀기」 · 「장면에서 빼기」를 걷었다.
        // 2026-11-18 소유자 — 에피소드 줄 「에피소드 추가」(그 뒤에 「계속」 길로 잇던 것)도 걷었다.
        (MainWindow window, StoryHierarchy tree) = Open();
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);

        // 2026-11-18 소유자 — 줄마다 제 일만 선다. 챕터 · 장면 지우기는 차림표에 없다 — 줄을 고르고 툴바의 [삭제]로 지운다.
        Assert.Equal(["챕터 추가"], Headers(tree, StoryHierarchy.ProjectKey));
        Assert.Equal(["장면 추가", "이름 바꾸기"], Headers(tree, StoryHierarchy.ChapterKey("ch01")));
        Assert.Equal(["에피소드 추가", "이름 바꾸기"], Headers(tree, StoryHierarchy.SceneKey("ch01", "opening")));
        Assert.Equal(["이름 바꾸기", "에피소드 지우기"], Headers(tree, StoryHierarchy.EpisodeKey("ch01", "root")));

        window.Close();
    });

    [Fact]
    public void 장면_줄의_에피소드_추가는_그_장면에_길_없이_서고_두_탭이_그것을_고른다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        int edges = Chapter(session).Edges.Count;

        Click(tree, StoryHierarchy.SceneKey("ch01", "opening"), "에피소드 추가");

        ChapterEpisode added = Assert.Single(Chapter(session).Episodes, episode => episode.EpisodeId == "ch01_opening_new01");
        Assert.Equal("opening", added.SceneId);
        Assert.Equal(edges, Chapter(session).Edges.Count);   // 길은 잇지 않는다 — 잇기는 [대본] 판에서 선택지 칸을 끌어서
        Assert.Equal(EpisodeNaming.CardFor(session.Project, "ch01", "ch01_opening_new01")!.Id, session.SelectedNodeId);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "ch01_opening_new01"), tree.Selection?.Key);

        // 되돌리기 한 칸 — 에피소드 · 카드가 함께 물러난다.
        session.Editor.Undo();
        Assert.DoesNotContain(Chapter(session).Episodes, episode => episode.EpisodeId == "ch01_opening_new01");

        window.Close();
    });

    [Fact]
    public void 장면_추가는_첫_에피소드와_함께_새_장면을_세운다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;

        Click(tree, StoryHierarchy.ChapterKey("ch01"), "장면 추가");
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode added = Assert.Single(Chapter(session).Episodes, episode => episode.EpisodeId == "ch01_scene01_new01");
        Assert.Equal("scene01", added.SceneId);
        // 새로 만든 것에 간선을 바로 잇지는 않는다.
        Assert.DoesNotContain(Chapter(session).Edges, edge => edge.ToEpisodeId == "ch01_scene01_new01");
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.SceneKey("ch01", "scene01"));

        // ID는 자동이고 작가에게는 처음 이름이 보인다(display-names N2).
        Assert.Equal(NewNames.Episode, added.Title);
        Assert.Equal(NewNames.Scene, ChapterSceneNotes.NameOf(Chapter(session).SceneNotes, "scene01"));

        // 되돌리기 한 칸 — 에피소드 · 장면 이름이 함께 물러난다.
        session.Editor.Undo();
        Assert.DoesNotContain(Chapter(session).Episodes, episode => episode.EpisodeId == "ch01_scene01_new01");
        Assert.Empty(Chapter(session).SceneNotes);

        window.Close();
    });

    [Fact]
    public void 에피소드를_지우면_사라진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);

        Click(tree, StoryHierarchy.EpisodeKey("ch02", "y"), "에피소드 지우기");
        Assert.DoesNotContain(Chapter(session, "ch02").Episodes, episode => episode.EpisodeId == "y");

        window.Close();
    });

    [Fact]
    public void 구성_탭_하이어아키는_고르기만_하고_우클릭_이름_고치기_끌기가_없다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 0);
        StoryHierarchy tree = window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        string a = StoryHierarchy.EpisodeKey("ch01", "a");

        // 우클릭 차림표가 없다 — 빈 자리에도, 줄마다에도.
        Assert.True(tree.ReadOnly);
        Assert.Null(tree.FindControl<ScrollViewer>("TreeScroll")!.ContextMenu);
        Assert.All(
            [StoryHierarchy.ChapterKey("ch01"), StoryHierarchy.SceneKey("ch01", "opening"), a],
            key => Assert.Null(tree.RowButton(key)!.ContextMenu));

        // 이름 칸이 열리지 않고, 끌어도 장면이 바뀌지 않는다.
        tree.BeginRename(a);
        Assert.Null(tree.RenameBox);
        tree.Drag(a, StoryHierarchy.SceneKey("ch01", "hall"));
        Assert.Equal("opening", Chapter(session).FindEpisode("a")!.SceneId);

        // 고르기는 된다.
        tree.RowButton(a)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(a, tree.Selection?.Key);

        // [대본] 탭 하이어아키는 그대로 고친다.
        StoryHierarchy director = window.FindControl<GraphEditorView>("Graph")!.HierarchyView;
        Assert.False(director.ReadOnly);
        Assert.NotNull(director.RowButton(a)!.ContextMenu);

        window.Close();
    });

    [Fact]
    public void 두_번_눌러_이름을_바꾸고_Esc는_없던_일로_한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);

        // 에피소드 — [대본] 탭은 보이는 제목을 고친다(EpisodeId는 [구성] 탭 인스펙터에서).
        Rename(tree, StoryHierarchy.EpisodeKey("ch02", "x"), "x2", Key.Enter);
        Assert.Equal("x2", Chapter(session, "ch02").FindEpisode("x")!.Title);
        Assert.Equal("x2", tree.Rows.Single(row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x")).Text);

        // 장면 — 이름만. 장면ID는 그대로다(display-names N3).
        Rename(tree, StoryHierarchy.SceneKey("ch01", "hall"), "lobby", Key.Enter);
        Assert.Equal("hall", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "b").SceneId);
        Assert.Equal("lobby", ChapterSceneNotes.NameOf(Chapter(session).SceneNotes, "hall"));

        // 챕터 — 표시 이름만. 챕터 ID는 그대로다.
        Rename(tree, StoryHierarchy.ChapterKey("ch02"), "두 번째 장", Key.Enter);
        Assert.Equal("두 번째 장", Chapter(session, "ch02").DisplayName);
        Assert.Equal("두 번째 장", tree.Rows.Single(row => row.Key == StoryHierarchy.ChapterKey("ch02")).Text);

        // Esc — 없던 일.
        Rename(tree, StoryHierarchy.EpisodeKey("ch02", "y"), "zzz", Key.Escape);
        Assert.NotNull(Chapter(session, "ch02").FindEpisode("y"));
        Assert.Null(tree.RenameBox);

        window.Close();
    });

    [Fact]
    public void 첫_누름은_줄을_다시_세우지_않아_둘째_누름이_같은_줄에_떨어진다() => HeadlessUi.Run(() =>
    {
        // 두 번 눌러 이름 고치기가 서는 조건 — 첫 누름(고르기)이 판 전환 · 탭 맞추기 · 하이어아키 다시 그리기를 부르는데,
        // 그때 줄이 새 컨트롤로 갈리면 둘째 누름이 새 단추에 떨어져 두 번 누름이 성립하지 않는다.
        // 헤드리스는 두 번 누름의 시간 창이 없어 합성 더블클릭을 못 만든다 — 그래서 같은 단추에 떨어지는지를 잰다.
        (MainWindow window, StoryHierarchy tree) = Open();
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);
        string key = StoryHierarchy.EpisodeKey("ch02", "x");

        // 하이어아키 칸은 높이 200이다 — ch01을 접어 x 줄이 칸 안에 보이게 한다.
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: true);
        Dispatcher.UIThread.RunJobs();
        Button row = tree.RowButton(key)!;
        Point spot = row.TranslatePoint(new Point(20, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(spot, MouseButton.Left);
        window.MouseUp(spot, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(key, tree.Selection?.Key);                 // 정말 골랐다 — 챕터가 바뀌고 카드가 골라졌다
        Assert.Equal("ch02", window.SessionProbe.ActiveFile?.Name);
        Assert.Same(row, tree.RowButton(key));                   // 그래도 줄은 그대로다

        // 이름 칸은 그 줄의 이름(Id)을 들고 열린다.
        tree.BeginRename(key);
        Assert.Equal("x", tree.RenameBox!.Text);

        window.Close();
    });

    [Fact]
    public void 에피소드를_다른_장면_줄로_끌면_그_장면으로_옮겨진다() => HeadlessUi.Run(() =>
    {
        // HY4b — 장면은 장면ID로 묶이는 것이라, 에피소드를 다른 장면에 놓는 것이 곧 그 에피소드의 장면ID를 바꾸는 일이다.
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        Dispatcher.UIThread.RunJobs();   // 다시 세운 줄들이 자리를 잡는다

        // 실제 손으로 — 누르고, 장면 줄 위로 옮기고, 놓는다(단추가 포인터를 잡아도 놓은 자리는 좌표로 찾는다).
        Button source = tree.RowButton(StoryHierarchy.EpisodeKey("ch01", "a"))!;
        Button target = tree.RowButton(StoryHierarchy.SceneKey("ch01", "hall"))!;
        Point from = source.TranslatePoint(new Point(30, source.Bounds.Height / 2), window)!.Value;
        Point to = target.TranslatePoint(new Point(30, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("hall", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "a").SceneId);

        // 되돌리기 한 칸.
        session.Editor.Undo();
        Assert.Equal("opening", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "a").SceneId);

        // 받지 않는 자리 — 같은 장면 · 에피소드 줄 · 다른 챕터.
        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "a"), StoryHierarchy.SceneKey("ch01", "opening"));
        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "a"), StoryHierarchy.EpisodeKey("ch01", "root"));
        tree.Drag(StoryHierarchy.EpisodeKey("ch02", "x"), StoryHierarchy.SceneKey("ch01", "hall"));
        Assert.Equal("opening", Chapter(session).Episodes.Single(episode => episode.EpisodeId == "a").SceneId);
        Assert.Equal("street", Chapter(session, "ch02").Episodes.Single(episode => episode.EpisodeId == "x").SceneId);

        window.Close();
    });

    [Fact]
    public void 접힘은_두_탭이_함께_쓰고_프로젝트를_다시_열어도_남는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy director) = Open();
        StoryHierarchy tree = window.FindControl<ChapterGraphView>("ChapterGraph")!.HierarchyView;

        tree.Fold(StoryHierarchy.ChapterKey("ch02"), collapse: false);

        tree.Fold(StoryHierarchy.SceneKey("ch02", "street"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(director.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x"));   // 연출 쪽도 펴졌다

        director.Fold(StoryHierarchy.SceneKey("ch01", "hall"), collapse: false);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "b"));      // 챕터 쪽도 펴졌다
        window.Close();

        // 같은 프로젝트를 다시 연다 — 사람이 편 것이 그대로다(사용자 설정에 프로젝트별로).
        (MainWindow again, StoryHierarchy reopened) = Open();
        Assert.Contains(reopened.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch02", "x"));
        Assert.Contains(reopened.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "b"));
        again.Close();
    });

    [Fact]
    public void 화살표로_고르고_접고_편다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();

        // 시작하면 세션이 고른 첫 카드(root)가 골라져 있다.
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);

        Press(tree, Key.Down);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "a"), tree.Selection?.Key);
        Assert.Equal("a", window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<TextBox>("IdBox")!.Text);

        Press(tree, Key.Up);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);

        // ← 에피소드에서 = 부모(장면)로, 다시 ← = 그 장면을 접는다, → = 편다, 다시 → = 첫 자식으로.
        Press(tree, Key.Left);
        string opening = StoryHierarchy.SceneKey("ch01", "opening");
        Assert.Equal(opening, tree.Selection?.Key);

        Press(tree, Key.Left);
        Assert.DoesNotContain(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "root"));

        Press(tree, Key.Right);
        Assert.Contains(tree.Rows, row => row.Key == StoryHierarchy.EpisodeKey("ch01", "root"));

        Press(tree, Key.Right);
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);

        window.Close();
    });

    [Fact]
    public void 자동_ID인_에피소드를_다른_장면으로_끌면_ID가_그_장면으로_따라가고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 자동 모양 그대로인 ID만 따라간다(auto-id-orders I3). 사람이 지은 ID(root · a · b)는 그대로다.
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        Click(tree, StoryHierarchy.SceneKey("ch01", "opening"), "에피소드 추가");
        Assert.Contains(Chapter(session).Episodes, episode => episode.EpisodeId == "ch01_opening_new01");
        session.Editor.AddEdge("ch01", "root", "ch01_opening_new01", optionLabel: "계속");   // 길이 옮기기를 따라가는지 본다
        long before = session.Editor.Revision;

        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "ch01_opening_new01"), StoryHierarchy.SceneKey("ch01", "hall"));
        Dispatcher.UIThread.RunJobs();

        ChapterEpisode moved = Assert.Single(Chapter(session).Episodes, episode => episode.EpisodeId == "ch01_hall_new01");
        Assert.Equal("hall", moved.SceneId);
        Assert.Contains(Chapter(session).Edges, edge => edge.FromEpisodeId == "root" && edge.ToEpisodeId == "ch01_hall_new01");
        Assert.Contains("ch01_hall_new01", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(before + 1, session.Editor.Revision);

        // 사람이 지은 ID는 옮겨도 그대로다.
        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "a"), StoryHierarchy.SceneKey("ch01", "hall"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("hall", Chapter(session).FindEpisode("a")!.SceneId);

        window.Close();
    });

    [Fact]
    public void 챕터_줄에는_놓지_못한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);

        // 챕터 줄은 받지 않는다 — 장면에서 빼는 길은 없다(2026-11-17).
        HierarchyRow a = tree.Rows.Single(row => row.Key == StoryHierarchy.EpisodeKey("ch01", "a"));
        Assert.False(StoryHierarchy.Accepts(a, tree.Rows.Single(row => row.Key == StoryHierarchy.ChapterKey("ch01"))));
        tree.Drag(StoryHierarchy.EpisodeKey("ch01", "a"), StoryHierarchy.ChapterKey("ch01"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("opening", Chapter(session).FindEpisode("a")!.SceneId);

        window.Close();
    });

    [Fact]
    public void 이름_칸을_연_채로_다른_에피소드를_눌러도_줄은_한_벌이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 보고 — 이름 칸을 연 채로 다른 에피소드를 누르면 프로젝트가 둘로 보였다. 고르기가 하이어아키를 다시 세우는
        // 도중(줄을 지우는 순간) 초점을 쥔 이름 칸이 판에서 빠져 「없던 일로」가 다시 세우기를 또 불렀고, 바깥 세우기가 이어서 한 벌을 더
        // 붙였다(단추는 초점을 받지 않아 이름 칸이 초점을 쥔 채 남는다).
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        tree.Fold(StoryHierarchy.SceneKey("ch01", "opening"), collapse: false);
        Dispatcher.UIThread.RunJobs();

        tree.BeginRename(StoryHierarchy.EpisodeKey("ch01", "a"));
        Dispatcher.UIThread.RunJobs();
        Assert.True(tree.RenameBox!.IsFocused);

        tree.RowButton(StoryHierarchy.EpisodeKey("ch01", "root"))!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        StackPanel host = tree.FindControl<StackPanel>("RowHost")!;
        Assert.Equal(tree.Rows.Count, host.Children.OfType<DockPanel>().Count());
        Assert.Single(tree.Rows, row => row.Kind == HierarchyKind.Project);
        Assert.Null(tree.RenameBox);                                   // 이름 칸은 없던 일로 닫혔다
        Assert.Equal("a", Chapter(session).FindEpisode("a")!.Title);   // 이름은 그대로다
        Assert.Equal(StoryHierarchy.EpisodeKey("ch01", "root"), tree.Selection?.Key);

        window.Close();
    });

    [Fact]
    public void 챕터_줄의_이름_바꾸기는_줄_안에서_하고_지우기는_고른_뒤_삭제_단추로_한_번_확인한_뒤_지운다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「이름 바꾸기 · 제거…」를 나눈다. 이름 바꾸기는 장면 · 에피소드와 같이 줄 안에서, 제거만 한 번 확인한다.
        // 같은 날 — 제거는 차림표가 아니라 툴바의 [삭제]로.
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        string key = StoryHierarchy.ChapterKey("ch02");

        Click(tree, key, "이름 바꾸기");
        Assert.NotNull(tree.RenameBox);   // 줄 안의 이름 칸 — 셸의 플라이아웃이 아니다
        Rename(tree, key, "축제 당일", Key.Enter);
        Assert.Equal("축제 당일", session.Editor.FindChapter("ch02")!.DisplayName);

        // 지우기 — 줄을 고르면 툴바의 [삭제]가 「챕터 삭제」가 되고, 누르면 확인 칸만 선다(아직 지우지 않는다 · 2026-11-18 소유자).
        Pick(tree, key);
        Button delete = DeleteButton(window);
        Assert.Equal(("챕터 삭제", true), ((string?)delete.Content, delete.IsEnabled));
        Press(delete);
        Assert.NotNull(session.Editor.FindChapter("ch02"));
        Button confirm = window.RemoveConfirmButton!;
        Assert.Equal("챕터 지우기", confirm.Content);

        long before = session.Editor.Revision;
        confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(session.Editor.FindChapter("ch02"));
        Assert.Null(EpisodeNaming.BoardOf(session.Project, "ch02"));
        Assert.DoesNotContain(tree.Rows, row => row.Key == key);
        Assert.Equal(before + 1, session.Editor.Revision);   // 되돌리기 한 칸
        Assert.Equal(("삭제", false), ((string?)delete.Content, delete.IsEnabled));   // 고른 것이 사라졌다 — 다시 꺼진 「삭제」

        session.Editor.Undo();
        Assert.NotNull(session.Editor.FindChapter("ch02"));

        window.Close();
    });

    [Fact]
    public void 장면은_고른_뒤_삭제_단추로_한_번_확인하고_그_장면의_에피소드와_분기_카드와_항목을_한_칸에_걷는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 장면을 고르면 툴바의 [삭제]가 「장면 삭제」가 된다(차림표의 「장면 지우기」는 걷혔다).
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        tree.Fold(StoryHierarchy.ChapterKey("ch01"), collapse: false);
        session.Editor.SetSceneNote("ch01", "opening", "교실의 사정");
        DialogueNode root = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        DialogueNode branch = session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);   // root가 부르는 분기 카드
        Dispatcher.UIThread.RunJobs();
        string key = StoryHierarchy.SceneKey("ch01", "opening");

        Pick(tree, key);
        Button delete = DeleteButton(window);
        Assert.Equal(("장면 삭제", true), ((string?)delete.Content, delete.IsEnabled));
        Press(delete);
        Assert.Equal(3, Chapter(session).Episodes.Count);   // 확인 전에는 지우지 않는다
        Button confirm = window.RemoveConfirmButton!;
        Assert.Equal("장면 지우기", confirm.Content);

        long before = session.Editor.Revision;
        confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["b"], Chapter(session).Episodes.Select(episode => episode.EpisodeId));   // opening의 root · a가 걷혔다
        Assert.Null(EpisodeNaming.CardFor(session.Project, "ch01", "root"));
        Assert.Null(EpisodeNaming.CardFor(session.Project, "ch01", "a"));
        Assert.Empty(Chapter(session).SceneNotes);                                             // 장면 항목도 함께
        Assert.Null(session.Project.FindNode(branch.Id));                                      // 부르던 분기 카드도 함께(2026-11-18 소유자)
        Assert.Contains("분기 카드 1장", session.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(tree.Rows, row => row.Key == key);
        Assert.Equal(before + 1, session.Editor.Revision);                                     // 되돌리기 한 칸
        Assert.Equal(("삭제", false), ((string?)delete.Content, delete.IsEnabled));

        session.Editor.Undo();
        Assert.Equal(3, Chapter(session).Episodes.Count);
        Assert.Equal("교실의 사정", ChapterSceneNotes.TextOf(Chapter(session).SceneNotes, "opening"));

        window.Close();
    });

    [Fact]
    public void 확인_칸은_바깥을_누르거나_Esc면_닫히고_아무것도_지우지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「확인창을 끌 방법이 없습니다. 다른 공간을 클릭하면 자동으로 꺼지도록」.
        (MainWindow window, StoryHierarchy tree) = Open();
        AuthoringSession session = window.SessionProbe;
        long before = session.Editor.Revision;
        Pick(tree, StoryHierarchy.ChapterKey("ch02"));

        // 판의 빈 곳을 누른다.
        Press(DeleteButton(window));
        Button first = window.RemoveConfirmButton!;
        window.MouseDown(new Point(700, 600), MouseButton.Left);
        window.MouseUp(new Point(700, 600), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(window.RemoveConfirmButton);
        Assert.False(first.IsAttachedToVisualTree());

        // Esc.
        Pick(tree, StoryHierarchy.ChapterKey("ch02"));
        Press(DeleteButton(window));
        Button second = window.RemoveConfirmButton!;
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(window.RemoveConfirmButton);
        Assert.False(second.IsAttachedToVisualTree());

        Assert.NotNull(session.Editor.FindChapter("ch02"));
        Assert.Equal(before, session.Editor.Revision);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static void Press(StoryHierarchy tree, Key key)
    {
        tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>[대본] 탭을 연 창과 그 탭의 하이어아키 — 고치는 하이어아키다.</summary>
    private (MainWindow Window, StoryHierarchy Tree) Open()
    {
        (MainWindow window, _) = HierarchyFixture.Open(_directory, tab: 1);
        return (window, window.FindControl<GraphEditorView>("Graph")!.HierarchyView);
    }

    private static ChapterGraphModel Chapter(AuthoringSession session, string chapterId = "ch01") =>
        session.Editor.FindChapter(chapterId)!.ToGraphModel(chapterId);

    private static IEnumerable<MenuItem> Items(StoryHierarchy tree, string key) =>
        ((IEnumerable<object>)tree.RowButton(key)!.ContextMenu!.ItemsSource!).OfType<MenuItem>();

    private static string[] Headers(StoryHierarchy tree, string key) =>
        Items(tree, key).Select(item => (string)item.Header!).ToArray();

    /// <summary>그 줄의 차림표에서 그 항목을 누른다.</summary>
    /// <summary>하이어아키의 그 줄을 누른다(고르기).</summary>
    private static void Pick(StoryHierarchy tree, string key)
    {
        tree.RowButton(key)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>[대본] 툴바의 [삭제].</summary>
    private static Button DeleteButton(MainWindow window) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Button>("DeleteButton")!;

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(StoryHierarchy tree, string key, string header)
    {
        Items(tree, key).Single(item => (string?)item.Header == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>그 줄의 이름 칸을 열고 적은 뒤 그 키를 누른다.</summary>
    private static void Rename(StoryHierarchy tree, string key, string text, Key press)
    {
        tree.BeginRename(key);
        TextBox box = tree.RenameBox!;
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = press });
        Dispatcher.UIThread.RunJobs();
    }
}
