using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// [구성] 검증 보고 — <b>캐릭터 키가 없는 화자</b>를 경고(🟡)로 알린다(2026-11-18 소유자 — 「구성탭에서 캐릭터 키가 없을 경우, 검증보고에서 오류
/// 알림을」 · <c>docs/work-orders/writer-comfort-orders.md</c> WC6). 「화자」 탭에서 키를 적으면 그 줄이 걷힌다.
/// </summary>
public sealed class ChapterSpeakerKeyReportTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-speaker-key-report", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 키가_없는_화자는_검증_보고의_경고이고_키를_적으면_걷힌다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();

        Assert.Contains("🟡", Header(view));
        Assert.Contains(Lines(view), line => line.Contains("화자 '샤통'의 캐릭터 키가 없습니다", StringComparison.Ordinal));

        // 화자 탭의 머리 — 없는 키를 센다.
        Assert.Equal("⚠ 캐릭터 키가 없는 화자 1명 — 그 화자의 대사에는 초상화 · 표정이 붙지 않습니다.", MissingLine(view));

        // 「화자」 탭의 키 칸에 적는다.
        TextBox key = view.FindControl<StackPanel>("SpeakerListPanel")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(box => Equals(box.Tag, "speaker-key:샤통"));
        key.Text = "chatong";
        key.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("chatong", session.Project.FindSpeakerCharacterId("샤통"));
        Assert.DoesNotContain(Lines(view), line => line.Contains("'샤통'", StringComparison.Ordinal));
    });

    [Fact]
    public void 임시_키인_화자도_검증_보고와_화자_탭이_짚고_키를_바꾸면_걷힌다() => HeadlessUi.Run(() =>
    {
        // 화자를 더하면 키가 temp_ + 이름의 로마자로 선다(usability UX29) — 그대로면 그림과 맞지 않는다.
        (ChapterGraphView view, AuthoringSession session) = Show(keepTempKey: true);

        Assert.Equal("temp_syatong", session.Project.FindSpeakerCharacterId("샤통"));
        Assert.Contains("🟡", Header(view));
        Assert.Contains(Lines(view), line => line.Contains("화자 '샤통'의 캐릭터 키가 아직 임시 키 'temp_syatong'입니다", StringComparison.Ordinal));
        Assert.Equal("⚠ 임시 키(temp_)인 화자 1명 — 그림에 맞는 키로 바꿔 주세요.", MissingLine(view));
        Assert.Contains(view.FindControl<StackPanel>("SpeakerListPanel")!.GetLogicalDescendants().OfType<TextBlock>(),
            block => Equals(block.Tag, "speaker-name:샤통") && block.Text == "⚠ 샤통");

        TextBox key = view.FindControl<StackPanel>("SpeakerListPanel")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(box => Equals(box.Tag, "speaker-key:샤통"));
        Assert.Equal("temp_syatong", key.Text);
        key.Text = "chatong";
        key.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Lines(view), line => line.Contains("'샤통'", StringComparison.Ordinal));
        Assert.Null(MissingLine(view));
    });

    private static string? MissingLine(ChapterGraphView view) =>
        view.FindControl<StackPanel>("SpeakerListPanel")!.GetLogicalDescendants().OfType<TextBlock>()
            .SingleOrDefault(block => Equals(block.Tag, "speaker-missing-keys"))?.Text;

    private static string Header(ChapterGraphView view) => (string)view.FindControl<Expander>("DiagnosticsExpander")!.Header!;

    private static List<string> Lines(ChapterGraphView view) =>
        view.FindControl<StackPanel>("DiagnosticsPanel")!.GetLogicalDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty).ToList();

    /// <summary>에피소드 하나에 샤통의 대사 한 줄이 선 챕터 — 샤통의 키는 비웠다(<paramref name="keepTempKey"/>면 더할 때 선 임시 키 그대로).</summary>
    private (ChapterGraphView View, AuthoringSession Session) Show(bool keepTempKey = false)
    {
        Directory.CreateDirectory(_directory);
        var editor = new ProjectEditor(new StoryProject { Title = "화자 키" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "a", title: "사냥", 0, 0, sceneId: "a");
        editor.AddSpeaker("샤통");

        if (!keepTempKey)
        {
            editor.SetSpeakerCharacterId("샤통", null);
        }

        DialogueNode card = EpisodeNaming.CardFor(editor.Project, "ch01", "a")!;
        editor.SetScriptLineText(card.Id, card.Script.ActiveLines.First().Id, "샤통", "사냥이다.");
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, editor.Project);

        var session = new AuthoringSession();
        session.Open(manifest);
        var view = new ChapterGraphView();
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        view.Attach(session);
        Dispatcher.UIThread.RunJobs();
        return (view, session);
    }
}
