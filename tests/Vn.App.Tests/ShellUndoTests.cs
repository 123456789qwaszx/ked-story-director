using Avalonia.Controls;
using Avalonia.Input;
using Vn.App.Views;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>창 전체의 Ctrl+Z</b> — 되돌리기는 단추만이 아니라 단축키로도 되고, 글을 쓰는 칸에서는
/// 가로채지 않는다.
///
/// ⚠ 그래서 이 클래스는 화면 하나가 아니라 <b>창</b>을 띄운다. 단축키는 창이 Tunnel로 받는다 —
/// 화면 혼자서는 맞아도 둘을 잇는 자리가 비어 있으면 사람은 키를 눌러도 아무 일이 없다.
///
/// (2026-10-29: 이 파일은 [대본] 탭의 Ctrl+S 테스트와 한 클래스였다. 탭이 걷히며 그 둘은 함께
/// 사라졌고, 창의 되돌리기 둘만 남았다.)
/// </summary>
public sealed class ShellUndoTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-shell-undo", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Ctrl_Z가_되돌린다() => HeadlessUi.Run(() =>
    {
        // ⛔ <b>단축키가 없었다</b> (2026-09-18). 되돌리기는 창 위쪽 띠의 단추로만 됐는데,
        //    실수로 지운 사람은 거기까지 찾아 올라가야 했다. 지우는 단추를 세우며
        //    소유자가 건 조건이 *"ctrl+z로 롤백이 가능해야"*다.
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        DialogueNode node = SeedProject(window, _directory);

        window.SessionProbe.Editor.AddEpisode("ch01", "ep02", title: string.Empty, 0, 0);
        Assert.Equal(2, window.SessionProbe.Editor.FindChapter("ch01")!.Episodes.Count);

        Press(window, Key.Z, KeyModifiers.Control);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Single(window.SessionProbe.Editor.FindChapter("ch01")!.Episodes);
        Assert.NotNull(window.SessionProbe.Project.FindNode(node.Id));

        window.Close();
    });

    [Fact]
    public void 글을_쓰는_중에는_Ctrl_Z를_가로채지_않는다() => HeadlessUi.Run(() =>
    {
        // ⛔ 입력칸에서 Ctrl+Z는 <b>타이핑</b>을 되돌리는 것이다. 그것을 프로젝트 되돌리기로
        //    바꾸면 쓰던 문장이 통째로 날아간다 — 지우기를 되돌리려다 글을 잃는 꼴이다.
        var window = new MainWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        SeedProject(window, _directory);

        // 첫 탭(챕터 그래프) 인스펙터의 화자 이름 칸(하이어아키의 프로젝트 줄 — 2026-10-30 HY2) — 늘 서 있는 글자 칸이다.
        var chapterGraph = window.FindControl<ChapterGraphView>("ChapterGraph")!;
        chapterGraph.Inspect(HierarchyPick.Project);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var box = chapterGraph.FindControl<TextBox>("SpeakerNameBox")!;
        box.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Same(box, window.FocusManager?.GetFocusedElement());   // 정말 글 칸에 초점이 있다

        window.SessionProbe.Editor.AddEpisode("ch01", "ep02", title: string.Empty, 0, 0);

        Press(window, Key.Z, KeyModifiers.Control);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 프로젝트는 안 되돌아갔다 — 입력칸이 제 몫을 했다.
        Assert.Equal(2, window.SessionProbe.Editor.FindChapter("ch01")!.Episodes.Count);

        window.Close();
    });

    /// <summary>챕터 하나 · 에피소드 하나(카드와 대본이 함께 선다)를 세우고 그 카드를 고른다.</summary>
    private static DialogueNode SeedProject(MainWindow window, string directory)
    {
        Vn.App.Services.AuthoringSession session = window.SessionProbe;

        Directory.CreateDirectory(directory);
        string manifest = Path.Combine(directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "되돌리기 검증" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");

        // ⚠ 카드와 대본까지 함께 선다 (2026-09-18) — 여기서 또 만들면 카드가 둘이 된다.
        session.Editor.AddEpisode("ch01", "ep01", title: "ep01", 0, 0);

        DialogueNode node = session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(item => Vn.Authoring.Chapters.EpisodeNaming.EpisodeIdOf(item) == "ep01");

        session.Select(node.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return node;
    }

    /// <summary>창이 Tunnel로 받는 그 키 — 어디에 포커스가 있어도 단축키는 창이 먼저 본다.</summary>
    private static void Press(Window window, Key key, KeyModifiers modifiers) =>
        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers
        });
}
