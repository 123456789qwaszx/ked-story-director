using Avalonia;
using Avalonia.Controls;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;
using Path = System.IO.Path;

namespace Vn.App.Tests;

/// <summary>
/// <b>[구성] 탭의 [화자] 탭 = 캐릭터 키</b> (2026-11-16 · <c>docs/work-orders/writer-fields-orders.md</c> W3).
///
/// 화자 이름은 [대본] 탭(작가)이 더하고 고치고 지운다(<see cref="DirectorSpeakersTests"/>). 여기는 그 이름마다 캐릭터 키를 잇는
/// 자리다 — 이름은 읽기만 하고, 더하는 폼도 ✕도 없다. 키가 없는 화자는 알린다(초상화 · 표정이 붙지 않는다).
/// 값의 집은 프로젝트의 화자 목록이다(2026-11-17 · definition-split S3 — ⛔ 그 전에는 <c>game.definition.json</c>의 speakers).
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

        var editor = new ProjectEditor(new StoryProject { Title = "캐스트" });
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.AddEpisode("ch01", "ep01", title: "ep01", 0, 0);

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
    public void 이름은_읽기만_하고_더하는_폼도_지우기도_없다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.SetSpeakers(("라루", "laru"));
        Dispatcher.RunJobs();

        Assert.Null(view.FindControl<TextBox>("SpeakerNameBox"));
        Assert.Null(view.FindControl<Button>("SpeakerAddButton"));

        Grid row = Assert.Single(Rows(view));
        Assert.Equal("라루", row.Children.OfType<TextBlock>().Single().Text);
        Assert.Equal("laru", row.Children.OfType<TextBox>().Single().Text);   // 글자 칸은 키 하나뿐이다
        Assert.Empty(row.Children.OfType<Button>());
    });

    [Fact]
    public void 캐릭터_키를_적으면_프로젝트에_남고_이름은_그대로다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.SetSpeakers(("라루", ""));
        Dispatcher.RunJobs();

        TextBox key = Rows(view)[0].Children.OfType<TextBox>().Single();
        key.Text = "  laru  ";
        Enter(key);

        Speaker saved = Assert.Single(session.Project.Speakers);
        Assert.Equal(("라루", "laru"), (saved.Name, saved.CharacterId));
        Assert.Contains("캐릭터 키 'laru'", session.StatusMessage);

        // 되돌리기 한 칸이고 — 되돌리면 탭도 다시 선다.
        session.Editor.Undo();
        Dispatcher.RunJobs();
        Assert.Equal(string.Empty, Rows(view)[0].Children.OfType<TextBox>().Single().Text);
    });

    [Fact]
    public void 캐릭터_키가_없는_화자를_알린다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        session.SetSpeakers(("라루", "laru"), ("윌로", ""));
        Dispatcher.RunJobs();

        TextBlock warning = view.FindControl<StackPanel>("SpeakerListPanel")!.Children.OfType<TextBlock>()
            .Single(block => (string?)block.Tag == "speaker-missing-keys");
        Assert.Contains("1명", warning.Text);
        Assert.Equal(["라루", "⚠ 윌로"], Rows(view).Select(row => row.Children.OfType<TextBlock>().Single().Text));

        TextBox key = Rows(view)[1].Children.OfType<TextBox>().Single();
        key.Text = "willo";
        Enter(key);

        Assert.DoesNotContain(view.FindControl<StackPanel>("SpeakerListPanel")!.Children.OfType<TextBlock>(),
            block => (string?)block.Tag == "speaker-missing-keys");
    });

    [Fact]
    public void 대본_탭에서_더한_화자가_여기에도_선다() => HeadlessUi.Run(() =>
    {
        (ChapterGraphView view, AuthoringSession session) = Show();
        Assert.Empty(Rows(view));

        Assert.Null(session.AddSpeakerName("윌로"));
        Dispatcher.RunJobs();

        Assert.Equal("⚠ 윌로", Assert.Single(Rows(view)).Children.OfType<TextBlock>().Single().Text);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static class Dispatcher
    {
        public static void RunJobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Enter(TextBox box)
    {
        box.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Enter
        });

        Dispatcher.RunJobs();
    }

    private static List<Grid> Rows(ChapterGraphView view) =>
        view.FindControl<StackPanel>("SpeakerListPanel")!.Children.OfType<Grid>().ToList();

    private (ChapterGraphView View, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        session.Open(ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 900, Height = 600, Content = view };
        window.Show();
        view.Attach(session);
        view.Inspect(HierarchyPick.Project);

        window.Measure(new Size(900, 600));
        window.Arrange(new Rect(0, 0, 900, 600));
        Dispatcher.RunJobs();

        _ui.Own(view, window);

        return (view, session);
    }
}
