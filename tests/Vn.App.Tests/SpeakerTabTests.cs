using Avalonia;
using Avalonia.Controls;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Definition;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;
using Path = System.IO.Path;

namespace Vn.App.Tests;

/// <summary>
/// <b>[화자] 탭 = 프로젝트의 캐스트</b> (2026-08-23 소유자).
///
/// *"챕터 엑셀을 눌러보면, 엑셀 내 어떤 것에서도 화자를 사용하지 않는다. 즉 애초부터
/// 챕터엑셀에 화자가 들어갈 이유가 전혀없다. 화자는 툴 내부에서, 직접 정의해서 쓰는 게 맞는
/// 것이였다. … 이 화자탭에서 화자 추가 삭제를 하는 것이 구조적으로 옳다. 여기서 추가한
/// 화자는 게임 project에 저장되며, 모든 챕터가 공유해서 쓴다."*
///
/// 못 박는 것 넷: ① 탭에서 더하면 정의 파일에 남는다 ② ✕로 지우면 목록에서 빠진다
/// ③ 목록이 신원이라 같은 이름은 두 번 서지 않는다 ④ 개명하면 <b>모든 챕터의</b> 이미 쓰인
/// 대사가 따라간다.
/// </summary>
public sealed class SpeakerTabTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-speaker-tab", Guid.NewGuid().ToString("N"));

    private readonly OpenChapterViews _ui = new();

    private string ManifestPath => Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

    public SpeakerTabTests()
    {
        Directory.CreateDirectory(_directory);

        // 챕터 둘 — "모든 챕터가 공유해서 쓴다"를 볼 수 있는 최소 모양. 에피소드마다 카드가 함께 선다.
        var editor = new ProjectEditor(new StoryProject { Title = "캐스트" });

        foreach ((string chapterId, string episodeId) in new[] { ("ch01", "ep01"), ("ch02", "ep02") })
        {
            editor.EnsureChapter(chapterId).Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10, SourceRow: 0));
            editor.AddEpisode(chapterId, episodeId, title: episodeId, 0, 0);
        }

        ProjectStore.Save(ManifestPath, editor.Project);
    }

    public void Dispose()
    {
        _ui.CloseAll();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 탭에서_더한_화자가_프로젝트에_남는다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session, _) = Show();

        Add(view, "라루", "laru");

        // ① 프로젝트에 남았다 — 메모리와 디스크 양쪽.
        SpeakerSpec saved = Assert.Single(session.Definition.Speakers);
        Assert.Equal("라루", saved.Name);
        Assert.Equal("laru", saved.CharacterId);
        Assert.Contains("라루", File.ReadAllText(GameDefinition.PathFor(ManifestPath)));
    });

    [Fact]
    public void 지우면_목록에서_빠진다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session, _) = Show();

        Add(view, "라루", "laru");
        Add(view, "윌로", null);

        // 줄의 ✕ — 첫 줄(라루)을 지운다.
        Button remove = Rows(view)[0].Children.OfType<Button>().Single();
        remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.RunJobs();

        SpeakerSpec left = Assert.Single(session.Definition.Speakers);
        Assert.Equal("윌로", left.Name);

    });

    [Fact]
    public void 같은_이름은_두_번_안_선다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session, _) = Show();

        Add(view, "라루", "laru");
        Add(view, "라루", "raru2");

        Assert.Single(session.Definition.Speakers);
        Assert.Contains("이미 있습니다", session.StatusMessage);
    });

    // ── 개명은 참조를 끌고 간다 (2026-08-24 소유자) ─────────────────────────
    //
    // "조건이나 화자의 이름을 편집한 경우도 마찬가지입니다." 등록부에서만 갈면 그 이름을
    // 쓰던 대사 줄이 전부 미등록이 된다 — 드롭다운에서 사라지고, 초상화 매핑이 끊기고,
    // 공백 있는 이름은 파서가 산문으로 읽어 대사와 합쳐진다.

    [Fact]
    public void 개명하면_이미_쓰인_대사의_화자가_따라간다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session, _) = Show();

        Add(view, "늙은 상인", "merchant");

        // 두 챕터의 카드에 이미 쓰인 원고. 등록이 먼저다: 공백 든 이름은 등록돼 있어야
        // 파서가 화자로 읽는다.
        WriteDialogue(session, "ch01", "ep01", "늙은 상인: 어서 오게.");
        WriteDialogue(session, "ch02", "ep02", "늙은 상인: 또 왔군.");

        Rename(view, 0, "떠돌이 상인");

        SpeakerSpec saved = Assert.Single(session.Definition.Speakers);
        Assert.Equal("떠돌이 상인", saved.Name);

        // 챕터를 가리지 않는다 — 목록이 프로젝트의 것이므로 개명도 프로젝트의 것이다.
        Assert.Equal("떠돌이 상인", FirstSpeaker(session, "ch01", "ep01"));
        Assert.Equal("떠돌이 상인", FirstSpeaker(session, "ch02", "ep02"));

        // ⚠ 보고는 저장 뒤에 선다 — 이어지는 어휘 밀기의 메시지가 덮으면 사람은 무엇이
        // 따라갔는지 못 본다.
        Assert.Contains("따라갔습니다", session.StatusMessage);
    });

    [Fact]
    public void 이미_있는_이름으로는_개명하지_않는다() => HeadlessUi.Run(() =>
    {
        // 목록이 신원이라 두 줄이 같은 이름을 가질 수 없다 — [＋ 추가]와 같은 규칙이다.
        // 허용하면 두 화자의 줄이 한 이름으로 합쳐지고, 되돌릴 손잡이가 없다.
        (ChapterGraphView view, AuthoringSession session, _) = Show();

        Add(view, "라루", "laru");
        Add(view, "윌로", "willo");

        Rename(view, 1, "라루");

        Assert.Equal(["라루", "윌로"], session.Definition.Speakers.Select(speaker => speaker.Name));
        Assert.Contains("이미 있습니다", session.StatusMessage);
    });

    // (2026-10-29 · D2: 구판 `화자` 시트를 정의 파일로 옮기던 이행과 그 테스트는 엑셀 연동과 함께 걷혔다.)

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static class Dispatcher
    {
        public static void RunJobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>탭의 한 줄 폼을 채우고 [＋ 추가]를 누른다 — 사람이 하는 그대로.</summary>
    private static void Add(ChapterGraphView view, string name, string? characterId)
    {
        view.FindControl<TextBox>("SpeakerNameBox")!.Text = name;
        view.FindControl<TextBox>("SpeakerCharacterIdBox")!.Text = characterId ?? string.Empty;
        view.FindControl<Button>("SpeakerAddButton")!
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Dispatcher.RunJobs();
    }

    /// <summary>그 줄의 이름 칸을 고치고 Enter로 확정한다 — 사람이 하는 그대로.</summary>
    private static void Rename(ChapterGraphView view, int row, string name)
    {
        TextBox box = Rows(view)[row].Children.OfType<TextBox>().First();
        box.Text = name;
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Enter
        });

        Dispatcher.RunJobs();
    }

    private static List<Grid> Rows(ChapterGraphView view) =>
        view.FindControl<StackPanel>("SpeakerListPanel")!.Children.OfType<Grid>().ToList();

    /// <summary>그 에피소드 카드의 대본에 대사를 넣는다 — 카드 안 글 칸의 넣기와 같은 길.</summary>
    private static void WriteDialogue(AuthoringSession session, string chapterId, string episodeId, string text)
    {
        DialogueNode card = EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!;
        ScenarioPasteOutcome outcome = session.Editor.ApplyScenarioText(
            card.Id, text, session.Definition, confirmDeletes: true);

        Assert.True(outcome.Applied, outcome.Summary());
    }

    /// <summary>그 카드 대본에서 화자가 적힌 첫 줄의 화자.</summary>
    private static string FirstSpeaker(AuthoringSession session, string chapterId, string episodeId)
    {
        DialogueNode card = EpisodeNaming.CardFor(session.Project, chapterId, episodeId)!;
        ScriptDocument script = session.Project.FindScript(card.ScriptId)!;

        return script.ActiveLines
            .Select(line => script.Text(line.Id).Speaker)
            .First(speaker => speaker.Length > 0);
    }

    private (ChapterGraphView View, AuthoringSession Session, Window Window) Show()
    {
        var session = new AuthoringSession();
        session.Open(ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        view.Attach(session);

        window.Measure(new Size(900, 600));
        window.Arrange(new Rect(0, 0, 900, 600));
        Dispatcher.RunJobs();

        _ui.Own(view, window);

        return (view, session, window);
    }
}
