using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>[⚙ 설정] 창</b> (2026-11-17 · settings C3) — 위 띠의 단추가 모달 창을 연다. [이 프로젝트] 묶음의
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
    public void 내보내기_묶음은_처음에_접혀_있고_머리는_복제할_폴더_선택이다() => HeadlessUi.Run(() =>
    {
        // 2026-11-18 소유자 — 「설정창 속에서 내보내기의 내용은 접을 수 있도록 해주십시오.」 같은 날 — 「내보내기(복제할 폴더 선택)이 조금 더
        // 직관적」(Z2 · ⛔ 그 전에는 머리가 지금 보내는 형식을 말했다). 머리는 폴더를 골라도 그대로다.
        (MainWindow window, AuthoringSession session) = Open();
        string game = Path.Combine(_directory, "game");
        Directory.CreateDirectory(game);
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        Assert.False(settings.ExportGroup.IsExpanded);
        Assert.Equal("내보내기 (복제할 폴더 선택)", settings.ExportHeader.Text);

        settings.SetDestination(ExportFormat.Progression, game);
        Assert.Equal("내보내기 (복제할 폴더 선택)", settings.ExportHeader.Text);

        // 묶음마다 「어디에 저장되는가」 줄은 없다(Z2 — 「이런 설명은 필요없어」).
        List<string> texts = settings.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();
        Assert.DoesNotContain(texts, text => text.Contains("에 저장됩니다", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, text => text.Contains("앱 설정", StringComparison.Ordinal));

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

        Assert.Contains("복제하지 않음", box.PlaceholderText, StringComparison.Ordinal);   // Z2 — 「보내지 않음」에서
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

        StackPanel groups = (StackPanel)settings.Body.Content!;   // 묶음들은 스크롤 안에 선다(UX1)
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
            ["창 어디서나", "[대본] 판", "[대본] 카드 글 칸", "[대본] 대사 편집기", "[구성] 판", "하이어아키", "글 칸"],
            tables.Select(grid => ((string)grid.Tag!)["shortcuts:".Length..]));
        Assert.Equal(Shortcuts.Table.Sum(place => place.Entries.Count), tables.Sum(grid => grid.RowDefinitions.Count));

        Assert.Equal("저장", Does(tables[0], "Ctrl+S"));
        Assert.StartsWith("고른 카드", Does(tables[1], "Delete"), StringComparison.Ordinal);
        Assert.StartsWith("앞 칸으로", Does(tables[3], "Ctrl+Tab"), StringComparison.Ordinal);
        Assert.StartsWith("보이는 줄 사이로 고르기", Does(tables[5], "↑ · ↓"), StringComparison.Ordinal);
        Assert.StartsWith("장면 메모 · 에피소드 지시 넣기", Does(tables[6], "Ctrl+Enter"), StringComparison.Ordinal);

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
    public void 기타_묶음의_미니맵_감추기는_처음_꺼져_있고_켜는_즉시_대본_판의_지도가_걷히며_다음에도_감춰져_있다() => HeadlessUi.Run(() =>
    {
        // 「기타」 — 이 PC의 앱 설정이다(2026-11-18 소유자 · side-fold K3). 체크는 누르는 즉시 반영한다(J3). 「미니맵 감추기」 — 처음 꺼져 있고
        // 켜면 걷힌다(Z1 · ⛔ 그 전에는 「[대본] 탭 미니맵 보이기」가 처음 켜져 있었다). 지도 · 배율 단추 · 바탕이 걷히고 지금 배율 글자만 남는다 — 눌림도
        // 받지 않는다(2026-11-25 소유자 — 「텍스트 박스에 걸려서 걸리적 거린다」 · ⛔ 그 전에는 배율 줄이 바탕째 남았다).
        AppSettingsService.SaveShowMinimap(true);

        try
        {
            (MainWindow window, AuthoringSession session) = Open();
            GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
            Canvas map = graph.FindControl<Canvas>("MinimapCanvas")!;
            Assert.True(map.IsVisible);

            // 처음 꺼져 있다 — 켜는 즉시 걷힌다([확인] 전). 배율 글자만 남는다. 프로젝트는 바뀌지 않는다(되돌리기 없음).
            Border panel = graph.FindControl<Border>("MinimapPanel")!;
            TextBlock zoom = graph.FindControl<TextBlock>("ZoomText")!;
            IBrush? shownBackground = panel.Background;
            Assert.True(panel.IsHitTestVisible);
            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("미니맵 감추기", settings.HideMinimapCheck.Content);
            Assert.False(settings.HideMinimapCheck.IsChecked);
            settings.HideMinimapCheck.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.False(map.IsVisible);
            Assert.All(
                new[] { "FitAllButton", "Zoom50Button", "Zoom100Button", "Zoom150Button" },
                name => Assert.False(graph.FindControl<Button>(name)!.IsEffectivelyVisible));
            Assert.True(zoom.IsEffectivelyVisible);
            Assert.EndsWith("%", zoom.Text, StringComparison.Ordinal);
            Assert.Equal(Brushes.Transparent, panel.Background);
            Assert.Equal(new Avalonia.Thickness(0), panel.BorderThickness);
            Assert.False(panel.IsHitTestVisible);
            Assert.False(session.ShowMinimap);
            Assert.False(AppSettingsService.LoadShowMinimap());
            Assert.False(session.Editor.CanUndo);

            // [취소]로 돌아가지 않는다.
            settings.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(map.IsVisible);

            // 다시 열면 켜진 채로 보인다 · 새 창도 감춘 채로 선다.
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.True(settings.HideMinimapCheck.IsChecked);
            var next = new MainWindow();
            next.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.False(next.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("MinimapCanvas")!.IsVisible);
            next.Close();

            // 끄면 바로 선다 — 단추 · 바탕 · 눌림도 돌아온다.
            settings.HideMinimapCheck.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(map.IsVisible);
            Assert.True(graph.FindControl<Button>("FitAllButton")!.IsEffectivelyVisible);
            Assert.Same(shownBackground, panel.Background);
            Assert.True(panel.IsHitTestVisible);
            settings.Close();

            window.Close();
        }
        finally
        {
            AppSettingsService.SaveShowMinimap(true);   // 설정은 이 시험 프로세스가 함께 쓴다
        }
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「대본탭의 간선의 형태도, 설정에서 직선, 곡선, 직각 중에 기본 생성을 선택할 수 있게」. 「기타」의 「새로 잇는 선」 — 처음은 직각, 고르는
    /// 즉시 앱 설정에 들어가고 프로젝트는 그대로이며, 그 뒤 생기는 선이 그 꼴이다. 다시 열면 고른 것이 보인다.
    /// </summary>
    [Fact]
    public void 기타_묶음의_새로_잇는_선은_처음_직각이고_고르면_새_선이_그_꼴로_생긴다() => HeadlessUi.Run(() =>
    {
        BoardLines.Set(Vn.Authoring.Model.EdgeShapeKind.Elbow);

        try
        {
            (MainWindow window, AuthoringSession session) = Open();

            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["직각", "곡선", "직선"], settings.NewLineShapeCombo.Items.Cast<string>());
            Assert.Equal("직각", settings.NewLineShapeCombo.SelectedItem);
            Assert.True(settings.NewLineShapeCombo.IsEffectivelyVisible);

            settings.NewLineShapeCombo.SelectedItem = "직선";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Vn.Authoring.Model.EdgeShapeKind.Straight, BoardLines.Kind);
            Assert.Equal("straight", AppSettingsService.LoadNewLineShape());
            Assert.False(session.Editor.CanUndo);   // 프로젝트는 바뀌지 않는다
            Assert.Equal(Vn.Authoring.Model.EdgeShapeKind.Straight, session.Editor.NewLineShape!());

            settings.Close();
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("직선", settings.NewLineShapeCombo.SelectedItem);
            settings.Close();
            window.Close();
        }
        finally
        {
            BoardLines.Set(Vn.Authoring.Model.EdgeShapeKind.Elbow);
        }
    });

    [Fact]
    public void 기타_묶음의_휠로_판_옮기기는_처음_꺼져_있고_켜는_즉시_판의_휠이_따르며_다음에도_켜져_있다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 「트랙패드 수정」(writer-app WA3) — 이 PC의 앱 설정이고 누르는 즉시 반영한다(「기타」의 다른 체크와 같다).
        // 처음 값은 이 OS의 손이다 — 맥이 아니면 꺼짐(맥은 켜짐 — BoardWheelTests).
        BoardWheel.Set(null);

        try
        {
            (MainWindow window, AuthoringSession session) = Open();

            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("휠로 판 옮기기", settings.WheelPansCheck.Content);
            Assert.False(settings.WheelPansCheck.IsChecked);
            Assert.True(settings.WheelPansCheck.IsEffectivelyVisible);

            settings.WheelPansCheck.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(BoardWheel.Pans);
            Assert.True(AppSettingsService.LoadWheelPansBoard());
            Assert.False(session.Editor.CanUndo);   // 프로젝트는 바뀌지 않는다

            // [취소]로 돌아가지 않는다 · 다시 열면 켜진 채로 보인다.
            settings.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(BoardWheel.Pans);
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.True(settings.WheelPansCheck.IsChecked);

            settings.WheelPansCheck.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(BoardWheel.Pans);
            Assert.False(AppSettingsService.LoadWheelPansBoard());
            settings.Close();

            window.Close();
        }
        finally
        {
            BoardWheel.Set(null);   // 스위치는 이 시험 프로세스가 함께 쓴다
        }
    });

    [Fact]
    public void 창은_끌어서_크기를_바꾸고_줄여도_확인_취소는_늘_보이며_묶음이_스크롤한다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「설정창의 크기를 조절할 수 있게 해주시고, 스크롤 기능역시 추가해주십시오」(usability UX1).
        // ⛔ 그 전에는 크기가 고정이고 창 전체가 스크롤하지 않아, 묶음을 펴거나 글자를 키우면 [확인]이 화면 밖으로 나갔다.
        (MainWindow window, AuthoringSession _) = Open();
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        Assert.True(settings.CanResize);
        Assert.Equal((420, 320), (settings.MinWidth, settings.MinHeight));
        Assert.Equal(SizeToContent.Height, settings.SizeToContent);   // 처음은 내용에 맞춘다

        // 사람이 작게 끌었다(Avalonia는 사람의 크기 바꾸기에서 내용 맞춤을 끈다 — 그 뒤를 짓는다).
        settings.SizeToContent = SizeToContent.Manual;
        settings.Width = 420;
        settings.Height = 360;
        settings.ShortcutGroup.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(360, settings.Bounds.Height, 1);   // 펴도 창은 자라지 않는다
        Assert.True(settings.Body.Extent.Height > settings.Body.Viewport.Height, "묶음들이 스크롤해야 한다");
        AssertInside(settings, settings.ConfirmButton);
        AssertInside(settings, settings.CancelButton);

        // 끝까지 내리면 맨 아래 묶음(「기타」)이 보인다 — 단추는 그대로 아래에 있다.
        settings.Body.Offset = new Avalonia.Vector(0, settings.Body.Extent.Height);
        Dispatcher.UIThread.RunJobs();
        AssertInside(settings, settings.WheelPansCheck);
        AssertInside(settings, settings.ConfirmButton);

        settings.Close();
        window.Close();
    });

    [Fact]
    public void 처음_크기는_화면을_넘지_않고_넘는_만큼은_스크롤한다() => HeadlessUi.Run(() =>
    {
        // 처음 세로는 내용에 맞추되 화면(작업 영역) 높이에서 멈춘다 — 화면을 아는 창이 열기 전에 FitScreen을 부른다(헤드리스는 화면이 없다).
        (MainWindow window, AuthoringSession _) = Open();
        var settings = new SettingsWindow(window.SessionProbe);
        settings.FitScreen(600);
        settings.ShortcutGroup.IsExpanded = true;   // 단축키 표만으로도 600을 넘는다
        settings.Show(window);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(600 - 48, settings.MaxHeight);
        Assert.True(settings.Bounds.Height <= settings.MaxHeight + 0.5, $"창 높이 {settings.Bounds.Height}");
        Assert.True(settings.Body.Extent.Height > settings.Body.Viewport.Height, "넘는 만큼은 스크롤해야 한다");
        AssertInside(settings, settings.ConfirmButton);

        // 작은 화면이어도 가장 작은 창보다 작게 묶지는 않는다.
        settings.FitScreen(200);
        Assert.Equal(settings.MinHeight, settings.MaxHeight);

        settings.Close();
        window.Close();
    });

    [Fact]
    public void 단축키_표는_제_안에서_스크롤하지_않는다() => HeadlessUi.Run(() =>
    {
        // 창 전체가 스크롤한다 — 표 안의 스크롤(⛔ side-fold K2의 380)은 스크롤 안의 스크롤이 되어 휠이 표에 걸렸다(UX1).
        (MainWindow window, AuthoringSession _) = Open();
        SettingsWindow settings = window.OpenSettings();
        settings.ShortcutGroup.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(settings.ShortcutGroup.GetLogicalDescendants().OfType<ScrollViewer>());

        settings.Close();
        window.Close();
    });

    [Fact]
    public void 대본_글_칸_묶음에는_설명_글이_없다() => HeadlessUi.Run(() =>
    {
        // 2026-11-19 소유자 — 「[대본] 판의 카드에서 대본을 쓰는 칸의 글자입니다 ~ 이 설명 문구는 필요없으니 지워주십시오」(usability UX2).
        (MainWindow window, AuthoringSession _) = Open();
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        var section = (StackPanel)settings.ScriptPreview.Parent!;
        Assert.DoesNotContain(section.Children.OfType<TextBlock>(), block => block.Text?.Contains("대본을 쓰는 칸", StringComparison.Ordinal) == true);
        Assert.Same(settings.ScriptPreview, section.Children[1]);   // 글꼴 · 크기 · 간격 표 바로 아래가 미리 보기다

        settings.Close();
        window.Close();
    });

    /// <summary>그 칸이 창 안(위아래 모두)에 서 있다 — 스크롤에 가려지거나 창 밖으로 나가지 않았다.</summary>
    private static void AssertInside(Window window, Control control)
    {
        Assert.True(control.IsEffectivelyVisible);
        Avalonia.Point top = Avalonia.VisualExtensions.TranslatePoint(control, default, window)!.Value;
        Assert.True(top.Y >= 0 && top.Y + control.Bounds.Height <= window.Bounds.Height + 0.5,
            $"{control.GetType().Name} {top.Y:0.#} ~ {top.Y + control.Bounds.Height:0.#} · 창 {window.Bounds.Height:0.#}");
    }

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
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Script;
        Dispatcher.UIThread.RunJobs();

        AuthoringSession session = window.SessionProbe;
        session.Open(manifest);
        Dispatcher.UIThread.RunJobs();

        return (window, session);
    }
}
