using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
/// <b>[⚙ 설정] 창</b> (2026-11-17 · <c>docs/work-orders/settings-orders.md</c> C3) — 위 띠의 단추가 모달 창을 연다. [이 프로젝트] 묶음의
/// 「[대본] 판 크기」를 고치고 [확인]하면 판이 바뀌고 되돌리기 한 칸이다. [취소] · Esc는 버린다. 줄여서 카드가 움직이면 미리 말한다.
/// [내보내기] 묶음은 형식마다 보낼 곳을 정한다(export-destinations G2).
/// </summary>
public sealed class SettingsWindowTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-settings-window", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 위_띠의_설정_단추가_설정_창을_열고_지금_판_크기를_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Open();
        session.Editor.SetBoardSize(20000, 15000);
        Dispatcher.UIThread.RunJobs();

        Button settingsButton = window.FindControl<Button>("SettingsButton")!;
        Assert.Equal("⚙ 설정", settingsButton.Content);
        settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        SettingsWindow settings = Assert.Single(window.OwnedWindows.OfType<SettingsWindow>());
        Assert.Equal("설정", settings.Title);
        Assert.Equal((20000m, 15000m), (settings.BoardWidthBox.Value, settings.BoardHeightBox.Value));
        Assert.False(settings.BoardNote.IsVisible);

        settings.Close();
        window.Close();
    });

    [Fact]
    public void 고치고_확인하면_판이_바뀌고_되돌리기_한_칸이다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Open();
        Canvas canvas = window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!;
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        settings.BoardWidthBox.Value = 24000;
        settings.BoardHeightBox.Value = 16000;
        settings.ConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(settings.IsVisible);
        Assert.Equal(new BoardSize(24000, 16000), session.Project.Board);
        Assert.Equal((24000, 16000), (canvas.Width, canvas.Height));
        Assert.True(session.IsDirty);

        session.Editor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(BoardSize.Default, session.Project.Board);
        Assert.Equal((12000, 8000), (canvas.Width, canvas.Height));

        window.Close();
    });

    [Fact]
    public void 취소와_Esc는_버린다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Open();

        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();
        settings.BoardWidthBox.Value = 30000;
        settings.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.False(settings.IsVisible);
        Assert.Equal(BoardSize.Default, session.Project.Board);

        settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();
        settings.BoardWidthBox.Value = 30000;
        settings.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(settings.IsVisible);
        Assert.Equal(BoardSize.Default, session.Project.Board);
        Assert.False(session.Editor.CanUndo);

        window.Close();
    });

    [Fact]
    public void 줄여서_카드가_움직이면_확인_전에_미리_말한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Open();
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        // 늘리기는 말할 것이 없다.
        settings.BoardWidthBox.Value = 20000;
        Assert.False(settings.BoardNote.IsVisible);

        // 가로 10000에 카드가 있다 — 4000으로 줄이면 카드가 움직인다.
        settings.BoardWidthBox.Value = 4000;
        Assert.True(settings.BoardNote.IsVisible);
        Assert.Contains("카드 2장", settings.BoardNote.Text, StringComparison.Ordinal);   // 0에 선 카드는 그대로다
        Assert.Contains("되돌리기", settings.BoardNote.Text, StringComparison.Ordinal);

        // 미리 말한 것과 바뀐 것이 같다.
        settings.Confirm();
        Dispatcher.UIThread.RunJobs();
        Assert.All(session.Project.EnumerateNodes().OfType<DialogueNode>(),
            node => Assert.True(node.Layout.X + BoardSize.CardWidthOf(node) <= session.Project.Board.Width));

        window.Close();
    });

    [Fact]
    public void 기본값으로는_12000x8000을_칸에_넣고_범위_밖의_값은_받지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Open();
        session.Editor.SetBoardSize(30000, 20000);
        Dispatcher.UIThread.RunJobs();
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        settings.DefaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal((12000m, 8000m), (settings.BoardWidthBox.Value, settings.BoardHeightBox.Value));

        // 칸은 범위를 넘는 값을 들인다(4,000 ~ 60,000).
        settings.BoardWidthBox.Value = 100000;
        settings.BoardHeightBox.Value = 10;
        Assert.Equal((60000m, 4000m), (settings.BoardWidthBox.Value, settings.BoardHeightBox.Value));

        settings.Close();
        window.Close();
    });

    /// <summary>챕터 하나 — 카드 셋을 가로 0 · 5000 · 10000에.</summary>
    [Fact]
    public void 내보내기_묶음은_형식마다_보낼_곳을_정하고_확인하면_runtime_target_json에_남는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 소유자 — 위 띠의 [🎮 게임 폴더…]를 설정 창의 형식별 보낼 곳으로(export-destinations G2). 되돌리기 밖이고 저장할 때 보낸다.
        (MainWindow window, AuthoringSession session) = Open();
        string game = Path.Combine(_directory, "game");
        string yarn = Path.Combine(_directory, "dialogue-yarn");
        Directory.CreateDirectory(game);
        Directory.CreateDirectory(yarn);
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(window.FindControl<Button>("GameFolderButton"));
        Assert.All(ExportDestinations.Formats, format => Assert.Equal("(보내지 않음)", settings.DestinationText(format).Text));

        Assert.Null(settings.SetDestination(ExportFormat.Progression, game));
        Assert.Equal("game", settings.DestinationText(ExportFormat.Progression).Text);

        // 대사 Yarn은 게임 폴더와 같은 곳으로 받지 않는다 — 까닭을 보인다.
        Assert.NotNull(settings.SetDestination(ExportFormat.DialogueYarn, game));
        Assert.True(settings.ExportNote.IsVisible);
        Assert.Equal("(보내지 않음)", settings.DestinationText(ExportFormat.DialogueYarn).Text);
        Assert.Null(settings.SetDestination(ExportFormat.DialogueYarn, yarn));
        Assert.False(settings.ExportNote.IsVisible);

        // 없는 폴더는 표시한다(만들지 않는다).
        Assert.Null(settings.SetDestination(ExportFormat.Scenes, Path.Combine(_directory, "nowhere")));
        Assert.StartsWith("⚠ 없는 폴더", settings.DestinationText(ExportFormat.Scenes).Text);
        Assert.False(Directory.Exists(Path.Combine(_directory, "nowhere")));

        // [확인] 전에는 파일에 안 남는다.
        Assert.True(session.Destinations.IsEmpty);
        long before = session.Editor.Revision;
        settings.Confirm();
        Dispatcher.UIThread.RunJobs();

        ExportDestinations saved = ExportDestinations.LoadBeside(session.ProjectPath!);
        Assert.Equal(("game", "nowhere", "dialogue-yarn"), (saved.Progression, saved.Scenes, saved.DialogueYarn));
        Assert.Equal(saved, session.Destinations);
        Assert.Equal(before, session.Editor.Revision);   // 되돌리기 밖이다
        Assert.Contains("다음 저장(Ctrl+S)부터", session.StatusMessage);

        window.Close();
    });

    [Fact]
    public void 내보내기에서_취소하면_보낼_곳을_버리고_다시_열면_지금_값을_보인다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = Open();
        string game = Path.Combine(_directory, "game");
        Directory.CreateDirectory(game);
        ExportDestinations.SaveBeside(session.ProjectPath!, ExportDestinations.None.With(ExportFormat.Progression, "game"));
        session.ReloadDestinations();

        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("game", settings.DestinationText(ExportFormat.Progression).Text);

        settings.SetDestination(ExportFormat.Progression, null);
        Assert.Equal("(보내지 않음)", settings.DestinationText(ExportFormat.Progression).Text);
        settings.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("game", ExportDestinations.LoadBeside(session.ProjectPath!).Progression);
        Assert.Equal("game", window.OpenSettings().DestinationText(ExportFormat.Progression).Text);

        window.Close();
    });

    private (MainWindow Window, AuthoringSession Session) Open()
    {
        Directory.CreateDirectory(_directory);

        var editor = new Vn.Authoring.Editing.ProjectEditor(new StoryProject { Title = "설정" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        (string Id, double X)[] cards = [("a", 0), ("b", 5000), ("c", 10000)];

        foreach ((string id, double x) in cards)
        {
            editor.AddEpisode("ch01", id, title: id, 0, 0);
            editor.MoveNode(EpisodeNaming.CardFor(editor.Project, "ch01", id)!.Id, x, 100);
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

        return (window, session);
    }
}
