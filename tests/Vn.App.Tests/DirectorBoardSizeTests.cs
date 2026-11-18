using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 판이 프로젝트의 판 크기를 본다</b> (2026-11-17 · <c>docs/work-orders/settings-orders.md</c> C2) — 캔버스 크기 · 카드 자리 제한이
/// <see cref="StoryProject.Board"/>를 따르고, 되돌리기로 돌아온다. 그려지는 카드가 판보다 바깥이면(옛 파일 · 손으로 고친 파일) 판을 그 카드까지
/// 넓혀 그린다.
/// </summary>
public sealed class DirectorBoardSizeTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-board-size", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 판의_크기가_프로젝트의_판_크기를_따르고_되돌리기로_돌아온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        Canvas canvas = Canvas(graph);
        Assert.Equal((12000, 8000), (canvas.Width, canvas.Height));

        session.Editor.SetBoardSize(20000, 15000);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal((20000, 15000), (canvas.Width, canvas.Height));

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal((12000, 8000), (canvas.Width, canvas.Height));

        window.Close();
    });

    [Fact]
    public void 줄이면_카드가_판_안에_든다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        Canvas canvas = Canvas(graph);

        session.Editor.SetBoardSize(BoardSize.Minimum, BoardSize.Minimum);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((BoardSize.Minimum, BoardSize.Minimum), (canvas.Width, canvas.Height));
        Assert.All(Cards(session), node =>
        {
            Assert.True(node.Layout.X + BoardSize.CardWidthOf(node) <= canvas.Width);
            Assert.True(node.Layout.Y + BoardSize.CardHeightOf(node) <= canvas.Height);
        });

        window.Close();
    });

    [Fact]
    public void 판_밖의_카드가_있으면_그_카드까지_넓혀_그린다() => HeadlessUi.Run(() =>
    {
        // 손으로 고친 파일처럼 판 크기를 거치지 않고 카드를 판 밖에 둔다 — 카드가 판 밖으로 사라지면 안 된다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        DialogueNode far = Cards(session).First();
        far.Layout.X = 15000;
        far.Layout.Y = 9000;
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        Canvas canvas = Canvas(graph);
        Assert.Equal(BoardSize.Default, session.Project.Board);   // 프로젝트의 값은 그대로다
        Assert.Equal((15000 + BoardSize.CardWidth, 9000 + BoardSize.CardHeight), (canvas.Width, canvas.Height));

        window.Close();
    });

    [Fact]
    public void 늘린_판에서는_새_카드가_옛_가장자리_너머에도_선다() => HeadlessUi.Run(() =>
    {
        // 카드 자리 제한(가장자리에서 멈춘다)이 판 크기를 따른다 — [＋ 대사 노드]는 지금 보는 곳에 세운다.
        (MainWindow window, AuthoringSession session, GraphEditorView graph) = Open();
        session.Editor.SetBoardSize(30000, 20000);
        Dispatcher.UIThread.RunJobs();

        ScrollViewer scroll = graph.FindControl<ScrollViewer>("GraphScroll")!;
        scroll.Offset = new Vector(20000, 12000);
        session.Select(null);   // 고른 카드가 있으면 그 장면 테두리 옆에 선다 — 보는 곳에 세우려면 고른 것이 없어야 한다
        Dispatcher.UIThread.RunJobs();

        graph.FindControl<Button>("AddDialogueButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        StoryNode added = session.SelectedNode!;
        Assert.True(added.Layout.X > 12000 - BoardSize.CardWidth, $"x = {added.Layout.X}");
        Assert.True(added.Layout.Y > 8000 - BoardSize.CardHeight, $"y = {added.Layout.Y}");

        window.Close();
    });

    private static Canvas Canvas(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static IEnumerable<DialogueNode> Cards(AuthoringSession session) =>
        session.Project.EnumerateNodes().OfType<DialogueNode>();

    /// <summary>챕터 하나 — 카드 셋을 가로 0 · 5000 · 10000에(마지막은 세로 6000).</summary>
    private (MainWindow Window, AuthoringSession Session, GraphEditorView Graph) Open()
    {
        Directory.CreateDirectory(_directory);

        var editor = new Vn.Authoring.Editing.ProjectEditor(new StoryProject { Title = "판 크기" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        (string Id, double X, double Y)[] cards = [("a", 0, 100), ("b", 5000, 100), ("c", 10000, 6000)];

        foreach ((string id, double x, double y) in cards)
        {
            editor.AddEpisode("ch01", id, title: id, 0, 0, sceneId: id);
            editor.MoveNode(EpisodeNaming.CardFor(editor.Project, "ch01", id)!.Id, x, y);
        }

        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        Dispatcher.UIThread.RunJobs();

        return (window, session, window.FindControl<GraphEditorView>("Graph")!);
    }
}
