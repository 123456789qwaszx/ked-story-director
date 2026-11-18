using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
        Assert.All(ExportDestinations.Formats, format => Assert.Equal(string.Empty, settings.DestinationBox(format).Text));   // 비면 보내지 않는다

        Assert.Null(settings.SetDestination(ExportFormat.Progression, game));
        Assert.Equal(game, settings.DestinationBox(ExportFormat.Progression).Text);   // 탐색기처럼 절대 경로가 보인다

        // 대사 Yarn은 게임 폴더와 같은 곳으로 받지 않는다 — 까닭을 보인다.
        Assert.NotNull(settings.SetDestination(ExportFormat.DialogueYarn, game));
        Assert.True(settings.ExportNote.IsVisible);
        Assert.Equal(string.Empty, settings.DestinationBox(ExportFormat.DialogueYarn).Text);
        Assert.Null(settings.SetDestination(ExportFormat.DialogueYarn, yarn));
        Assert.False(settings.ExportNote.IsVisible);

        // 없는 폴더는 표시한다(만들지 않는다).
        Assert.Null(settings.SetDestination(ExportFormat.DialogueYarn, Path.Combine(_directory, "nowhere")));
        Assert.True(settings.MissingNote(ExportFormat.DialogueYarn).IsVisible);
        Assert.StartsWith("⚠ 없는 폴더", settings.MissingNote(ExportFormat.DialogueYarn).Text);
        Assert.False(Directory.Exists(Path.Combine(_directory, "nowhere")));

        // [확인] 전에는 파일에 안 남는다.
        Assert.True(session.Destinations.IsEmpty);
        long before = session.Editor.Revision;
        settings.Confirm();
        Dispatcher.UIThread.RunJobs();

        ExportDestinations saved = ExportDestinations.LoadBeside(session.ProjectPath!);
        Assert.Equal(("game", "nowhere"), (saved.Progression, saved.DialogueYarn));
        Assert.Equal(saved, session.Destinations);
        Assert.Equal(before, session.Editor.Revision);   // 되돌리기 밖이다
        Assert.Contains("다음 저장(Ctrl+S)부터", session.StatusMessage);

        window.Close();
    });

    [Fact]
    public void 내보내기_묶음은_처음에_접혀_있고_머리가_보내는_형식을_말한다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「설정창 속에서 내보내기의 내용은 접을 수 있도록 해주십시오.」
        (MainWindow window, AuthoringSession session) = Open();
        string game = Path.Combine(_directory, "game");
        Directory.CreateDirectory(game);
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        Assert.False(settings.ExportGroup.IsExpanded);
        Assert.Equal("내보내기 — 작업 폴더에만 냅니다", settings.ExportHeader.Text);

        settings.SetDestination(ExportFormat.Progression, game);
        Assert.Equal("내보내기 — 진행 JSON을(를) 보낼 곳에도 냅니다", settings.ExportHeader.Text);

        settings.ExportGroup.IsExpanded = true;   // 머리를 눌러 편다
        Dispatcher.UIThread.RunJobs();
        Assert.True(settings.ExportGroup.IsExpanded);

        window.Close();
    });

    [Fact]
    public void 형식_이름을_누르면_작업_폴더에서_그_파일을_보인다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「진행 JSON, 장면 파일, 대사 Yarn은 솔직히 뭐가 뭔지 모르겠으니, 저기서 각각을 클릭하면 작업 폴더가 열리면서
        // 해당하는 파일이 선택되거나 열리도록」. 탐색기는 띄우지 않고 무엇을 보였는지 잰다.
        (MainWindow window, AuthoringSession session) = Open();
        SettingsWindow settings = window.OpenSettings();
        var shown = new List<string>();
        settings.Reveal = shown.Add;
        string work = Path.Combine(_directory, ChapterExportService.ExportFolderName);

        foreach (ExportFormat format in ExportDestinations.Formats)
        {
            Assert.Equal(ExportDestinations.LabelOf(format), settings.FormatLink(format).Content);
            Assert.NotNull(ToolTip.GetTip(settings.FormatLink(format)));   // 무엇인지 말한다
            settings.FormatLink(format).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Assert.Equal(
            [
                Path.Combine(work, "ch01" + ChapterExportService.ExportFileSuffix),   // 지금 챕터의 진행 JSON — 고른 채 연다
                ChapterYarnExporter.FolderFor(session.ProjectPath!)                     // 대사 Yarn은 yarn/ 폴더째
            ],
            shown);

        // 아직 나간 것이 없으면 그렇다고 말한다.
        Directory.Delete(work, recursive: true);
        settings.FormatLink(ExportFormat.Progression).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, shown.Count);
        Assert.True(settings.ExportNote.IsVisible);
        Assert.Contains("아직 작업 폴더에 나간 진행 JSON", settings.ExportNote.Text, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 보낼_곳은_경로_칸에_적거나_붙여_넣고_비우면_보내지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「(보내지 않음)으로는 뭐가뭔지 잘 이해가 안 가니, 다른 윈도우 탐색기처럼 경로를 연결하는 형식을」.
        (MainWindow window, AuthoringSession session) = Open();
        string game = Path.Combine(_directory, "game");
        Directory.CreateDirectory(game);
        SettingsWindow settings = window.OpenSettings();
        TextBox box = settings.DestinationBox(ExportFormat.Progression);

        void Enter()
        {
            box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Contains("보내지 않음", box.PlaceholderText, StringComparison.Ordinal);
        Assert.Contains("clearButton", box.Classes);   // 칸 안의 ✕로 비운다
        Assert.Equal(["export-browse:Progression"], settings.GetLogicalDescendants().OfType<Button>()
            .Where(button => (button.Tag as string)?.EndsWith(":Progression", StringComparison.Ordinal) == true && button is not HyperlinkButton)
            .Select(button => (string)button.Tag!));   // [찾아보기…] 하나 — ⛔ [고르기…] · [✕]는 걷혔다

        // 붙여 넣은 절대 경로(따옴표째여도) — Enter.
        box.Text = $"\"{game}\"";
        Enter();
        Assert.Equal("game", ExportDestinationsOf(settings).Progression);
        Assert.Equal(game, box.Text);

        // 상대 경로는 프로젝트 폴더 기준이다.
        box.Text = "game";
        Enter();
        Assert.Equal(game, box.Text);

        // 비우면 보내지 않는다.
        box.Text = string.Empty;
        Enter();
        Assert.Null(ExportDestinationsOf(settings).Progression);

        // 적다 만 글도 [확인]이 반영한다.
        box.Text = game;
        settings.Confirm();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("game", ExportDestinations.LoadBeside(session.ProjectPath!).Progression);

        window.Close();
    });

    /// <summary>창 안에서 고친(아직 [확인] 전) 보낼 곳 — 칸의 글로 읽는다.</summary>
    private ExportDestinations ExportDestinationsOf(SettingsWindow settings) =>
        ExportDestinations.Formats.Aggregate(ExportDestinations.None, (all, format) =>
            all.With(format, settings.DestinationBox(format).Text is { Length: > 0 } full
                ? ExportDestinations.Relativize(Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension), full)
                : null));

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
        Assert.Equal(game, settings.DestinationBox(ExportFormat.Progression).Text);

        settings.SetDestination(ExportFormat.Progression, null);
        Assert.Equal(string.Empty, settings.DestinationBox(ExportFormat.Progression).Text);
        settings.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("game", ExportDestinations.LoadBeside(session.ProjectPath!).Progression);
        Assert.Equal(game, window.OpenSettings().DestinationBox(ExportFormat.Progression).Text);

        window.Close();
    });

    [Fact]
    public void 단축키_묶음은_처음에_접혀_있고_펼치면_자리별_표가_선다() => HeadlessUi.Run(() =>
    {
        // 「설정 안에 사용가능한 단축키들을 정리해두는 것이 필요」(2026-11-18 소유자 · side-fold K2). 묶음 차례는 이 프로젝트 · 내보내기 · 단축키 · 기타.
        (MainWindow window, _) = Open();
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        StackPanel groups = (StackPanel)settings.Content!;
        Assert.Equal(1, groups.Children.IndexOf(settings.ExportGroup));
        Assert.Equal(2, groups.Children.IndexOf(settings.ShortcutGroup));
        Assert.Equal("단축키", ((TextBlock)settings.ShortcutGroup.Header!).Text);
        Assert.False(settings.ShortcutGroup.IsExpanded);

        settings.ShortcutGroup.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        // 자리마다 표 하나 — 표의 원천(Shortcuts.Table)과 같은 차례 · 같은 글이다.
        List<Grid> tables = settings.ShortcutGroup.GetLogicalDescendants().OfType<Grid>()
            .Where(grid => (grid.Tag as string)?.StartsWith("shortcuts:", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(
            ["창 어디서나", "[대본] 판", "[대본] 카드 글 칸", "[구성] 판", "하이어아키", "글 칸"],
            tables.Select(grid => ((string)grid.Tag!)["shortcuts:".Length..]));
        Assert.Equal(Shortcuts.Table.Sum(place => place.Entries.Count), tables.Sum(grid => grid.RowDefinitions.Count));

        Assert.Equal("저장", Does(tables[0], "Ctrl+S"));
        Assert.StartsWith("고른 카드", Does(tables[1], "Delete"), StringComparison.Ordinal);
        Assert.StartsWith("보이는 줄 사이로 고르기", Does(tables[4], "↑ · ↓"), StringComparison.Ordinal);
        Assert.StartsWith("장면 메모 · 에피소드 지시 넣기", Does(tables[5], "Ctrl+Enter"), StringComparison.Ordinal);

        settings.Close();
        window.Close();
    });

    /// <summary>표에서 그 키 줄의 「하는 일」 글.</summary>
    private static string Does(Grid table, string keys)
    {
        TextBlock key = table.Children.OfType<TextBlock>().Single(block => Grid.GetColumn(block) == 0 && block.Text == keys);
        return table.Children.OfType<TextBlock>().Single(block => Grid.GetColumn(block) == 1 && Grid.GetRow(block) == Grid.GetRow(key)).Text!;
    }

    [Fact]
    public void 기타_묶음의_미니맵을_끄고_확인하면_대본_판의_지도가_걷히고_다음에도_꺼져_있다() => HeadlessUi.Run(() =>
    {
        // 「기타」 — 이 PC의 앱 설정이다(2026-11-18 소유자 · side-fold K3). 끄면 지도만 걷고 배율 줄은 남는다.
        AppSettingsService.SaveShowMinimap(true);

        try
        {
            (MainWindow window, AuthoringSession session) = Open();
            GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
            Canvas map = graph.FindControl<Canvas>("MinimapCanvas")!;
            Assert.True(map.IsVisible);

            // 취소는 버린다.
            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.True(settings.MinimapCheck.IsChecked);
            settings.MinimapCheck.IsChecked = false;
            settings.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(map.IsVisible);
            Assert.True(session.ShowMinimap);

            // 확인하면 걷힌다 — 배율 줄은 남는다. 프로젝트는 바뀌지 않는다(되돌리기 없음).
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            settings.MinimapCheck.IsChecked = false;
            settings.ConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(map.IsVisible);
            Assert.True(graph.FindControl<Button>("FitAllButton")!.IsEffectivelyVisible);
            Assert.False(session.ShowMinimap);
            Assert.False(session.Editor.CanUndo);
            Assert.False(AppSettingsService.LoadShowMinimap());

            // 다시 열면 꺼진 채로 보인다 · 새 창도 꺼진 채로 선다.
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.False(settings.MinimapCheck.IsChecked);
            settings.Close();
            var next = new MainWindow();
            next.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.False(next.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("MinimapCanvas")!.IsVisible);
            next.Close();

            // 다시 켜면 선다.
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            settings.MinimapCheck.IsChecked = true;
            settings.ConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(map.IsVisible);

            window.Close();
        }
        finally
        {
            AppSettingsService.SaveShowMinimap(true);   // 설정은 이 시험 프로세스가 함께 쓴다
        }
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
            editor.AddEpisode("ch01", id, title: id, 0, 0, sceneId: id);
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
