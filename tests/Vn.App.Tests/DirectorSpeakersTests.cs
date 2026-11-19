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
    private const int GraphTab = MainTab.Script;

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
        Assert.Equal(("라루", "temp_raru"), (saved.Name, saved.CharacterId));   // 키는 임시 키로 선다(UX29)
        Assert.Contains("캐릭터 키는 임시로 'temp_raru'", session.StatusMessage, StringComparison.Ordinal);
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

    // ── 대본에만 있는 이름 (usability UX34) ─────────────────────────────────

    [Fact]
    public void 대본에만_있는_이름이_줄_수와_함께_모이고_등록하면_목록으로_올라간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「화자를 추가하지 않고도 그대로 대사에 speaker로 바로 쓰기도 합니다. 이런 등록되지 않은 speaker들 역시 화자 탭에서
        // 모아 둔 뒤에 관리하는 기능」.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.SetSpeakers(("라루", "laru"));
        WriteDialogue(session, "ch01", "root", "윌로: 안녕.\n라루: 응.\n윌로: 또 보자.");
        GraphEditorView graph = PickProject(window);

        Assert.Equal("대본에만 있는 이름 1개", ((TextBlock)Control(graph, "speaker-unlisted-header")).Text);
        Assert.Equal("윌로", Box(graph, "speaker-unlisted:윌로").Text);
        Assert.Equal("2줄", ((TextBlock)Control(graph, "speaker-unlisted-lines:윌로")).Text);
        Assert.Null(Find(graph, "speaker-unlisted:라루"));      // 목록에 있는 이름은 모이지 않는다
        Assert.Null(Find(graph, "speaker-register-all"));     // 하나뿐이면 [모두 등록]이 없다

        long revision = session.Editor.Revision;
        Click(Control(graph, "speaker-register:윌로"));

        Assert.Equal(revision + 1, session.Editor.Revision);
        Assert.Equal([new Speaker("라루", "laru"), new Speaker("윌로", "temp_willo")], session.Project.Speakers);
        Assert.Contains("임시로 'temp_willo'", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("윌로", Box(graph, "speaker-name:윌로").Text);
        Assert.Null(Find(graph, "speaker-unlisted-header"));   // 다 올라가면 칸이 걷힌다

        window.Close();
    });

    [Fact]
    public void 모두_등록은_되돌리기_한_칸이고_되돌리면_다시_모인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        WriteDialogue(session, "ch01", "root", "윌로: 안녕.");
        WriteDialogue(session, "ch02", "x", "시구레: 누구야?");
        GraphEditorView graph = PickProject(window);
        Assert.Equal("대본에만 있는 이름 2개", ((TextBlock)Control(graph, "speaker-unlisted-header")).Text);

        long revision = session.Editor.Revision;
        Click(Control(graph, "speaker-register-all"));

        Assert.Equal(revision + 1, session.Editor.Revision);
        Assert.Equal(["시구레", "윌로"], session.Project.Speakers.Select(speaker => speaker.Name));   // 칸의 차례(가나다)대로
        Assert.Contains("화자 2명", session.StatusMessage, StringComparison.Ordinal);
        Assert.Null(Find(graph, "speaker-unlisted-header"));

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(session.Project.Speakers);
        Assert.NotNull(Find(graph, "speaker-unlisted:윌로"));
        Assert.NotNull(Find(graph, "speaker-unlisted:시구레"));

        window.Close();
    });

    [Fact]
    public void 오타를_목록의_이름으로_고치면_모든_챕터의_줄이_그_화자로_합쳐진다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        session.SetSpeakers(("윌로", "willo"));
        WriteDialogue(session, "ch01", "root", "윌러: 오타다.\n윌로: 맞다.");
        WriteDialogue(session, "ch02", "x", "윌러: 또 오타다.");
        GraphEditorView graph = PickProject(window);
        long revision = session.Editor.Revision;

        TextBox typo = Box(graph, "speaker-unlisted:윌러");
        typo.Text = "윌로";
        Key(typo, Avalonia.Input.Key.Enter);

        Assert.Equal(revision + 1, session.Editor.Revision);
        Assert.Equal("윌로", FirstSpeaker(session, "ch01", "root"));
        Assert.Equal("윌로", FirstSpeaker(session, "ch02", "x"));
        Assert.Contains("줄 2개를 화자 '윌로'로 합쳤습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal([new Speaker("윌로", "willo")], session.Project.Speakers);
        Assert.Null(Find(graph, "speaker-unlisted-header"));

        window.Close();
    });

    [Fact]
    public void 대본에만_있는_이름은_비우거나_띄어쓴_새_이름으로_고치지_않고_칸이_돌아온다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        WriteDialogue(session, "ch01", "root", "상인: 어서 오게.");
        GraphEditorView graph = PickProject(window);
        long revision = session.Editor.Revision;

        TextBox name = Box(graph, "speaker-unlisted:상인");
        name.Text = "  ";
        Key(name, Avalonia.Input.Key.Enter);
        Assert.Contains("비울 수 없습니다", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("상인", name.Text);

        // 띄어쓴 이름은 목록에 있어야 대본이 화자로 읽는다 — 먼저 더하라고 말한다.
        name.Text = "늙은 상인";
        Key(name, Avalonia.Input.Key.Enter);
        Assert.Contains("먼저 위에서 화자로 더한 뒤", session.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("상인", name.Text);

        Assert.Equal(revision, session.Editor.Revision);
        Assert.Equal("상인", FirstSpeaker(session, "ch01", "root"));

        // 목록에 있는 띄어쓴 이름으로는 합쳐진다.
        session.SetSpeakers(("늙은 상인", "merchant"));
        Dispatcher.UIThread.RunJobs();
        name = Box(graph, "speaker-unlisted:상인");
        name.Text = "늙은 상인";
        Key(name, Avalonia.Input.Key.Enter);
        Assert.Equal("늙은 상인", FirstSpeaker(session, "ch01", "root"));

        window.Close();
    });

    [Fact]
    public void 이름을_고친_뒤의_등록은_더는_쓰이지_않는_옛_이름을_올리지_않는다() => HeadlessUi.Run(() =>
    {
        // 이름 칸을 고치다 바로 [등록]을 누르면 초점이 떠나며 먼저 고쳐진다 — 그 뒤 옛 이름의 [등록]은 할 일이 없다.
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        WriteDialogue(session, "ch01", "root", "윌러: 오타다.");
        GraphEditorView graph = PickProject(window);
        Button register = (Button)Control(graph, "speaker-register:윌러");

        TextBox typo = Box(graph, "speaker-unlisted:윌러");
        typo.Text = "윌로";
        Key(typo, Avalonia.Input.Key.Enter);
        long revision = session.Editor.Revision;

        Click(register);

        Assert.Equal(revision, session.Editor.Revision);
        Assert.Empty(session.Project.Speakers);
        Assert.NotNull(Find(graph, "speaker-unlisted:윌로"));

        window.Close();
    });

    [Fact]
    public void 카드에_목록에_없는_화자를_쓰면_화자_탭에_바로_모인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        GraphEditorView graph = PickProject(window);
        Assert.Null(Find(graph, "speaker-unlisted-header"));

        WriteDialogue(session, "ch01", "root", "시구레: 누구야?");
        Assert.Equal("1줄", ((TextBlock)Control(graph, "speaker-unlisted-lines:시구레")).Text);

        WriteDialogue(session, "ch02", "x", "시구레: 또 누구야?");
        Assert.Equal("2줄", ((TextBlock)Control(graph, "speaker-unlisted-lines:시구레")).Text);

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
