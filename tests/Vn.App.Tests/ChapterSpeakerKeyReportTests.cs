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

        // 「화자」 탭의 키 칸에 적는다.
        TextBox key = view.FindControl<StackPanel>("SpeakerListPanel")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(box => Equals(box.Tag, "speaker-key:샤통"));
        key.Text = "chatong";
        key.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("chatong", session.Project.FindSpeakerCharacterId("샤통"));
        Assert.DoesNotContain(Lines(view), line => line.Contains("'샤통'", StringComparison.Ordinal));
    });

    private static string Header(ChapterGraphView view) => (string)view.FindControl<Expander>("DiagnosticsExpander")!.Header!;

    private static List<string> Lines(ChapterGraphView view) =>
        view.FindControl<StackPanel>("DiagnosticsPanel")!.GetLogicalDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty).ToList();

    /// <summary>에피소드 하나에 샤통(키 없음)의 대사 한 줄이 선 챕터.</summary>
    private (ChapterGraphView View, AuthoringSession Session) Show()
    {
        Directory.CreateDirectory(_directory);
        var editor = new ProjectEditor(new StoryProject { Title = "화자 키" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "a", title: "사냥", 0, 0, sceneId: "a");
        editor.AddSpeaker("샤통");
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
