using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 탭에서 화자 이름을 더하고 고치고 지운다</b> (2026-11-16 · <c>docs/work-orders/writer-fields-orders.md</c> W3 — 2026-08-23의
/// 「화자는 [화자] 탭에서만」 결정을 소유자가 바꿨다). 하이어아키의 프로젝트 줄을 고르면 인스펙터에 화자 목록이 선다.
///
/// 못 박는 것: ① 더하면 프로젝트의 화자 목록에 남는다(캐릭터 키는 비어 있다 — [구성] 탭의 일 · 저장하면 매니페스트에) ② 같은 이름 · 빈 이름은 서지 않는다
/// ③ ✕로 지우면 목록에서 빠진다 ④ 이름을 고치면 <b>모든 챕터의</b> 이미 쓰인 대사가 따라가고 캐릭터 키는 그대로다
/// ⑤ 이미 있는 이름 · 빈 이름으로는 바꾸지 않고 칸이 돌아온다 ⑥ Esc는 넣지 않은 글을 걷는다.
/// </summary>
public sealed class DirectorSpeakersTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-director-speakers", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 더하면_프로젝트에_남고_목록에_서며_더하기_칸에_초점이_남는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickProject(window);

        TextBox add = Box(graph, "speaker-add-name");
        add.Text = "  라루 ";
        Key(add, Avalonia.Input.Key.Enter);

        Speaker saved = Assert.Single(session.Project.Speakers);
        Assert.Equal(("라루", string.Empty), (saved.Name, saved.CharacterId));
        session.Save();
        Assert.Contains("라루", File.ReadAllText(session.ProjectPath!));
        Assert.Equal("라루", Box(graph, "speaker-name:라루").Text);

        TextBox again = Box(graph, "speaker-add-name");   // 요약이 다시 섰다 — 새 칸에 초점이 간다
        Assert.True(string.IsNullOrEmpty(again.Text));
        Assert.Same(again, window.FocusManager?.GetFocusedElement());

        window.Close();
    });

    [Fact]
    public void 같은_이름과_빈_이름은_더하지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.SetSpeakers(("라루", "laru"));
        GraphEditorView graph = PickProject(window);

        TextBox add = Box(graph, "speaker-add-name");
        add.Text = "라루";
        Click(Control(graph, "speaker-add"));
        Assert.Contains("이미 있습니다", session.StatusMessage);

        add.Text = "   ";
        Click(Control(graph, "speaker-add"));
        Assert.Contains("적어 주세요", session.StatusMessage);

        Assert.Equal(["라루"], session.Project.Speakers.Select(speaker => speaker.Name));

        window.Close();
    });

    [Fact]
    public void 지우면_목록에서_빠지고_대본에_적힌_이름은_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.SetSpeakers(("라루", ""), ("윌로", ""));
        WriteDialogue(session, "ch01", "root", "라루: 안녕.");
        GraphEditorView graph = PickProject(window);

        Click(Control(graph, "speaker-remove:라루"));

        Assert.Equal(["윌로"], session.Project.Speakers.Select(speaker => speaker.Name));
        Assert.Null(Find(graph, "speaker-name:라루"));
        Assert.Equal("라루", FirstSpeaker(session, "ch01", "root"));

        window.Close();
    });

    [Fact]
    public void 이름을_고치면_모든_챕터의_대사가_따라가고_캐릭터_키는_그대로다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.SetSpeakers(("늙은 상인", "merchant"));

        // 공백 든 이름은 등록돼 있어야 파서가 화자로 읽는다 — 등록이 먼저다.
        WriteDialogue(session, "ch01", "root", "늙은 상인: 어서 오게.");
        WriteDialogue(session, "ch02", "x", "늙은 상인: 또 왔군.");
        GraphEditorView graph = PickProject(window);

        TextBox name = Box(graph, "speaker-name:늙은 상인");
        name.Text = "떠돌이 상인";
        Key(name, Avalonia.Input.Key.Enter);

        Speaker saved = Assert.Single(session.Project.Speakers);
        Assert.Equal(("떠돌이 상인", "merchant"), (saved.Name, saved.CharacterId));
        Assert.Equal("떠돌이 상인", FirstSpeaker(session, "ch01", "root"));
        Assert.Equal("떠돌이 상인", FirstSpeaker(session, "ch02", "x"));
        Assert.Contains("줄 2개가 따라갔습니다", session.StatusMessage);
        Assert.Equal("떠돌이 상인", Box(graph, "speaker-name:떠돌이 상인").Text);

        window.Close();
    });

    [Fact]
    public void 이미_있는_이름이나_빈_이름으로는_바꾸지_않고_칸이_돌아온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.SetSpeakers(("라루", ""), ("윌로", ""));
        GraphEditorView graph = PickProject(window);

        TextBox willo = Box(graph, "speaker-name:윌로");
        willo.Text = "라루";
        Key(willo, Avalonia.Input.Key.Enter);
        Assert.Contains("이미 있습니다", session.StatusMessage);
        Assert.Equal("윌로", willo.Text);

        willo.Text = "  ";
        Key(willo, Avalonia.Input.Key.Enter);
        Assert.Contains("비울 수 없습니다", session.StatusMessage);
        Assert.Equal("윌로", willo.Text);

        Assert.Equal(["라루", "윌로"], session.Project.Speakers.Select(speaker => speaker.Name));

        window.Close();
    });

    [Fact]
    public void Esc는_넣지_않은_이름을_걷는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.SetSpeakers(("라루", ""));
        GraphEditorView graph = PickProject(window);

        TextBox name = Box(graph, "speaker-name:라루");
        name.Text = "라루루";
        Key(name, Avalonia.Input.Key.Escape);

        Assert.Equal("라루", name.Text);
        Assert.Equal(["라루"], session.Project.Speakers.Select(speaker => speaker.Name));

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static GraphEditorView PickProject(MainWindow window)
    {
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        graph.HierarchyView.RowButton(StoryHierarchy.ProjectKey)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        // 프로젝트 줄은 「화자」 탭을 연다(side-tabs V3) — 인스펙터는 옆 탭 안내다.
        Assert.Equal(SideTab.Speakers, graph.ShownSideTab);
        Assert.Equal("프로젝트", graph.InspectorHeaderText);
        return graph;
    }

    /// <summary>그 에피소드 카드의 대본에 대사를 넣는다 — 카드 안 글 칸의 넣기와 같은 길.</summary>
    private static void WriteDialogue(AuthoringSession session, string chapterId, string episodeId, string text)
    {
        DialogueNode card = EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!;
        ScenarioPasteOutcome outcome = session.Editor.ApplyScenarioText(card.Id, text, confirmDeletes: true);
        Assert.True(outcome.Applied, outcome.Summary());
        Dispatcher.UIThread.RunJobs();
    }

    private static string FirstSpeaker(AuthoringSession session, string chapterId, string episodeId)
    {
        ScriptDocument script = EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!.Script;
        return script.ActiveLines.Select(line => script.Text(line.Id).Speaker).First(speaker => speaker.Length > 0);
    }

    private static IEnumerable<Control> Side(GraphEditorView graph) =>
        graph.ShownSideHost.GetLogicalDescendants().OfType<Control>().Distinct()
            .Where(control => control.IsEffectivelyVisible);

    private static Control? Find(GraphEditorView graph, string tag) =>
        Side(graph).SingleOrDefault(control => (string?)control.Tag == tag);

    private static Control Control(GraphEditorView graph, string tag) =>
        Find(graph, tag) ?? throw new Xunit.Sdk.XunitException($"'{tag}'가 없습니다.");

    private static TextBox Box(GraphEditorView graph, string tag) => (TextBox)Control(graph, tag);

    private static void Click(Control button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(TextBox box, Key key)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = box });
        Dispatcher.UIThread.RunJobs();
    }
}
