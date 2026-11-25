using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판의 찾기 (Ctrl+F)</b> — 2026-11-25 소유자 — 「대본의 그래프뷰에서 Ctrl+F 기능이 있으면 좋겠다는 의견」.
/// 판의 모든 대사 카드의 글(화자 · 대사)과 카드 제목을 찾는다 — 판의 차례로 세고, 그 카드를 고르고 찾은 글자를 고른다. 접힌 장면 · 챕터면 펼친다.
/// Enter = 다음 · Shift+Enter = 앞 · Esc = 닫고 찾은 대사에서 이어 쓴다. 찾기는 아무것도 바꾸지 않는다.
/// </summary>
public sealed class BoardFindTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-board-find", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Ctrl_F로_띠가_서고_대사를_판의_차례로_찾아_그_카드를_고르며_Enter로_돈다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Write(session, "ch01", "root", "라루: 바다로 가자.\n윌로: 바다? 좋아.");
        Write(session, "ch01", "b", "라루: 오늘은 집에 있자.");
        Write(session, "ch02", "y", "윌로: 바다 냄새가 난다.");
        long revision = session.Editor.Revision;

        PressCtrlF(window);
        Assert.True(graph.IsFindOpen);
        TextBox find = FindBox(graph);
        Assert.True(find.IsFocused);

        find.Text = "바다";
        Dispatcher.UIThread.RunJobs();

        // root 둘 → ch02의 y 하나(챕터 판 차례 · 판 안은 위에서 아래). b에는 없다.
        Assert.Equal(3, graph.FindHits.Count);
        Assert.Equal(["root", "root", "y"], graph.FindHits.Select(hit => EpisodeOf(session, hit.NodeId)));
        Assert.Equal("1/3", Count(graph));
        string root = Card(session, "ch01", "root").Id;
        Assert.Equal(root, session.SelectedNodeId);
        TextBox writing = Writing(graph, root);
        Assert.Equal("바다", writing.SelectedText);
        Assert.Equal(writing.Text!.IndexOf("바다", StringComparison.Ordinal), writing.SelectionStart);
        Assert.True(find.IsFocused);   // 초점은 찾기 칸에 남는다 — Enter로 이어 찾는다

        Key(find, Avalonia.Input.Key.Enter);
        Assert.Equal("2/3", Count(graph));
        Assert.Equal(writing.Text!.LastIndexOf("바다", StringComparison.Ordinal), Writing(graph, root).SelectionStart);

        Key(find, Avalonia.Input.Key.Enter);
        Assert.Equal("3/3", Count(graph));
        Assert.Equal(Card(session, "ch02", "y").Id, session.SelectedNodeId);

        // 끝에서 처음으로 돈다 · Shift+Enter는 앞으로.
        Key(find, Avalonia.Input.Key.Enter);
        Assert.Equal("1/3", Count(graph));
        Key(find, Avalonia.Input.Key.Enter, KeyModifiers.Shift);
        Assert.Equal("3/3", Count(graph));

        // 없는 말 — 「없음」, 고른 카드는 그대로다.
        find.Text = "구름";
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(graph.FindHits);
        Assert.Equal("없음", Count(graph));

        Assert.Equal(revision, session.Editor.Revision);   // 찾기는 아무것도 바꾸지 않는다
        window.Close();
    });

    [Fact]
    public void 카드_제목도_찾고_대소문자를_가리지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        session.Editor.UpdateEpisode("ch01", "a", title: "Harbor 장면");
        Write(session, "ch01", "b", "라루: harbor로 간다.");
        Dispatcher.UIThread.RunJobs();

        PressCtrlF(window);
        FindBox(graph).Text = "HARBOR";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, graph.FindHits.Count);
        Assert.True(graph.FindHits[0].InTitle);
        Assert.Equal(Card(session, "ch01", "a").Id, session.SelectedNodeId);
        Assert.False(graph.FindHits[1].InTitle);

        window.Close();
    });

    [Fact]
    public void 접힌_장면과_챕터_안이면_펼쳐서_간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Write(session, "ch01", "b", "라루: 숨은 대사.");
        Write(session, "ch02", "x", "윌로: 또 숨은 대사.");
        string ch01 = Board(session, "ch01").Id;
        string ch02 = Board(session, "ch02").Id;
        session.SetSceneCollapsed(ch01, "hall", collapsed: true);
        session.SetFileExpanded(ch02, expanded: false);
        Dispatcher.UIThread.RunJobs();
        string b = Card(session, "ch01", "b").Id;
        Assert.Null(CardOrNull(graph, b));

        PressCtrlF(window);
        FindBox(graph).Text = "숨은";
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(session.CollapsedScenes, key => key.FileId == ch01 && key.SceneId == "hall");
        Assert.NotNull(CardOrNull(graph, b));
        Assert.Equal(b, session.SelectedNodeId);
        Assert.Equal("숨은", Writing(graph, b).SelectedText);

        Key(FindBox(graph), Avalonia.Input.Key.Enter);
        Dispatcher.UIThread.RunJobs();
        string x = Card(session, "ch02", "x").Id;
        Assert.Contains(ch02, session.ExpandedFileIds);
        Assert.NotNull(CardOrNull(graph, x));
        Assert.Equal(x, session.SelectedNodeId);

        window.Close();
    });

    [Fact]
    public void Esc는_띠를_닫고_찾은_대사에서_이어_쓰게_한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Write(session, "ch01", "a", "라루: 첫 줄.\n윌로: 여기서 이어 쓴다.");
        string a = Card(session, "ch01", "a").Id;

        PressCtrlF(window);
        FindBox(graph).Text = "이어";
        Dispatcher.UIThread.RunJobs();
        Key(FindBox(graph), Avalonia.Input.Key.Escape);

        Assert.False(graph.IsFindOpen);
        TextBox writing = Writing(graph, a);
        Assert.True(writing.IsFocused);
        Assert.Equal("이어", writing.SelectedText);

        // 다시 열면 지난 말이 그대로 있고 모두 골라져 있다.
        PressCtrlF(window);
        Assert.Equal("이어", FindBox(graph).Text);
        Assert.Equal("이어", FindBox(graph).SelectedText);

        window.Close();
    });

    [Fact]
    public void 쓰던_칸에서_고른_글이_있으면_그_글로_찾는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Show();
        Write(session, "ch01", "root", "라루: 등대가 보인다.");
        Write(session, "ch01", "b", "윌로: 등대까지 가자.");
        TextBox writing = Writing(graph, Card(session, "ch01", "root").Id);
        writing.Focus();
        writing.SelectionStart = writing.Text!.IndexOf("등대", StringComparison.Ordinal);
        writing.SelectionEnd = writing.SelectionStart + 2;
        Dispatcher.UIThread.RunJobs();

        PressCtrlF(window);

        Assert.Equal("등대", FindBox(graph).Text);
        Assert.Equal(2, graph.FindHits.Count);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }

    private static StoryFile Board(AuthoringSession session, string chapterId) =>
        session.Project.Files.Single(file => string.Equals(file.Name, chapterId, StringComparison.Ordinal));

    private static DialogueNode Card(AuthoringSession session, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!;

    private static string EpisodeOf(AuthoringSession session, string nodeId) =>
        EpisodeNaming.EpisodeIdOf(session.Project.FindDialogue(nodeId)!);

    private static void Write(AuthoringSession session, string chapterId, string episodeId, string text)
    {
        Assert.True(session.Editor.ApplyScenarioText(Card(session, chapterId, episodeId).Id, text).Applied);
        Dispatcher.UIThread.RunJobs();
    }

    private static Border? CardOrNull(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().SingleOrDefault(card => card.Tag as string == nodeId);

    private static TextBox Writing(GraphEditorView graph, string nodeId) =>
        CardOrNull(graph, nodeId)!.GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);

    private static TextBox FindBox(GraphEditorView graph) => graph.FindControl<TextBox>("FindBox")!;

    private static string Count(GraphEditorView graph) => graph.FindControl<TextBlock>("FindCount")!.Text ?? string.Empty;

    /// <summary>창의 터널 처리기부터 지나는 Ctrl+F — 초점이 있는 곳에서 누른다.</summary>
    private static void PressCtrlF(MainWindow window)
    {
        var target = (Control?)window.FocusManager!.GetFocusedElement() ?? window;
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.F,
            KeyModifiers = Vn.App.Views.PlatformKeys.IsMac ? KeyModifiers.Meta : KeyModifiers.Control,
            Source = target
        });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(Control target, Avalonia.Input.Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = target });
        Dispatcher.UIThread.RunJobs();
    }
}
