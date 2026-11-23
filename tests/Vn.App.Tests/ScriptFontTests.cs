using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>설정 창 「대본 글 칸」</b> — [대본] 판 카드 글 칸의 글꼴 · 글자 크기 · 줄 간격(2026-11-18 소유자 — 「글꼴이나, 줄 사이의 간격, 폰트 크기
/// 같은 것들을 조정할 수 있다면 작가들이 좋아하겠습니다. 이런 것들을 설정에서」 · <c>docs/work-orders/writer-comfort-orders.md</c> WC5).
///
/// 이 PC의 앱 설정이고 고치는 즉시 앱 설정에 들어간다(「기타」의 체크와 같은 규칙). 글꼴 · 글자 크기 · 줄 간격은 새로 만드는 카드의 처음 값이다(카드에
/// 적힌다 · writer-formatting WF-D13 · WF-D19 — ⛔ WF-D19 전에는 줄 간격만 서 있는 글 칸이 바로 따랐다). 줄 높이는 그 글꼴의 가장 낮은 줄 높이 밑으로
/// 내려가지 않는다(<see cref="Expected"/>). 설정은 이 시험 프로세스가 함께 쓰므로 끝에 기본값으로 돌린다.
/// </summary>
public sealed class ScriptFontTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-script-font", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 처음에는_기본_글꼴_11_줄_간격_140이고_칸이_그것을_입는다() => HeadlessUi.Run(() =>
    {
        ScriptFont.Reset();
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        TextBox box = WritingBox(window, session);

        Assert.Equal(11, box.FontSize);
        Assert.Equal(Expected(box, 140), box.LineHeight);
        Assert.False(box.IsSet(TextBox.FontFamilyProperty));   // 테마의 글꼴을 물려받는다

        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(SettingsWindow.DefaultFontName, settings.ScriptFontCombo.SelectedItem);
        Assert.Equal(SettingsWindow.DefaultFontName, settings.ScriptFontCombo.Items[0]);
        Assert.Equal(11m, settings.ScriptFontSizeBox.Value);
        Assert.Equal(140m, settings.ScriptLineSpacingBox.Value);
        settings.Close();
        window.Close();
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「설정 내부에 대본 글칸속의 글꼴과 글자 크기는 이제 처음 생성시에 기본값으로만 사용하는 게 맞겠습니다. 지금처럼 즉각 갱신되는게
    /// 아니라요」(writer-formatting WF-D13) · 같은 날 「줄 간격을 새 카드의 기본값으로 바꿉시다」(WF-D19). 서 있는 카드는 처음 글꼴 · 크기 · 줄 간격 그대로다.
    /// 새로 만든 카드가 그 글꼴 · 크기 · 줄 간격으로 서고 카드에 적힌다. ⛔ 그 전에는 모든 칸이 셋 다(WF-D13 뒤에는 줄 간격만) 바로 따랐다.
    /// </summary>
    [Fact]
    public void 고치면_서_있는_칸은_그대로이고_새_카드가_그_글꼴_크기_줄_간격으로_선다() => HeadlessUi.Run(() =>
    {
        try
        {
            ScriptFont.Reset();
            (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
            TextBox box = WritingBox(window, session);
            string font = FontManager.Current.SystemFonts.First().Name;

            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            settings.ScriptFontSizeBox.Value = 16;
            settings.ScriptLineSpacingBox.Value = 180;
            settings.ScriptFontCombo.SelectedItem = font;
            Dispatcher.UIThread.RunJobs();

            // 같은 칸이다 — 판을 다시 짓지 않았다. 글꼴 · 크기 · 줄 간격 모두 그 카드의 처음 값 그대로다.
            Assert.Same(box, WritingBox(window, session));
            Assert.Equal((11d, Expected(box, 140)), (box.FontSize, box.LineHeight));
            Assert.False(box.IsSet(TextBox.FontFamilyProperty));
            Assert.Equal((16d, Expected(settings.ScriptPreview, 180)), (settings.ScriptPreview.FontSize, settings.ScriptPreview.LineHeight));   // 미리 보기는 새 카드의 모습
            Assert.Equal((font, 16d, 180), AppSettingsService.LoadScriptFont());
            Assert.False(session.Editor.CanUndo);   // 프로젝트는 바뀌지 않는다
            Assert.Equal(new WriterBase(string.Empty, 11, LineSpacing: 140), Card(session).WriterBase);

            // [취소]로 돌아가지 않고, 새로 만든 카드는 그 글꼴 · 크기로 선다 — 카드에 적힌다.
            settings.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(11, box.FontSize);
            session.Editor.AddEpisode("ch01", "z", title: "z", 0, 0, sceneId: "a");   // 판이 다시 선다
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new WriterBase(font, 16, LineSpacing: 180), Card(session, "z").WriterBase);
            TextBox fresh = WritingBox(window, session, "z");
            Assert.Equal((16d, Expected(fresh, 180), font), (fresh.FontSize, fresh.LineHeight, fresh.FontFamily.Name));
            Assert.Equal(11, WritingBox(window, session).FontSize);

            // 기본값으로 — 설정만 돌아간다. 두 카드 모두 제 글꼴 · 크기 · 줄 간격 그대로다.
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(font, settings.ScriptFontCombo.SelectedItem);
            settings.ScriptFontDefaultButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((11d, Expected(box, 140)), (WritingBox(window, session).FontSize, WritingBox(window, session).LineHeight));
            Assert.False(WritingBox(window, session).IsSet(TextBox.FontFamilyProperty));
            fresh = WritingBox(window, session, "z");
            Assert.Equal((16d, Expected(fresh, 180), font), (fresh.FontSize, fresh.LineHeight, fresh.FontFamily.Name));
            Assert.Equal((SettingsWindow.DefaultFontName, 11m, 140m),
                (settings.ScriptFontCombo.SelectedItem, settings.ScriptFontSizeBox.Value, settings.ScriptLineSpacingBox.Value));
            Assert.Equal((string.Empty, 11d, 140), AppSettingsService.LoadScriptFont());

            settings.Close();
            window.Close();
        }
        finally
        {
            ScriptFont.Reset();   // 설정은 이 시험 프로세스가 함께 쓴다
        }
    });

    /// <summary>
    /// 2026-11-23 소유자 — 「설정 창에서 원한다면, Bold로써 굵게 하는 걸 기본값으로 쓸 수도 있게 해줘. 글자 색깔도 같이 조정할 수 있으면 좋겠고」(writer-formatting
    /// WF-D16). 굵게 · 글자색도 새로 만드는 카드의 처음 값이다 — 미리 보기가 따르고, 서 있는 카드는 그대로다. 처음부터 굵은 카드에서 서식의 굵게는 일부만 가늘게
    /// 하지 못한다고 말한다.
    /// </summary>
    [Fact]
    public void 굵게와_글자색도_새_카드의_처음_값이다() => HeadlessUi.Run(() =>
    {
        try
        {
            ScriptFont.Reset();
            (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
            TextBox box = WritingBox(window, session);
            IBrush? themeForeground = box.Foreground;

            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.False(settings.ScriptBoldCheck.IsChecked);
            settings.ScriptBoldCheck.IsChecked = true;
            settings.ScriptColorChooser.Hex.Text = "#204080";
            settings.ScriptColorChooser.Apply.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var blue = new WriterRgb(0x20, 0x40, 0x80);
            Assert.Equal((true, "#204080"), AppSettingsService.LoadScriptLook());
            Assert.Equal(FontWeight.Bold, settings.ScriptPreview.FontWeight);
            Assert.Equal(Color.FromRgb(0x20, 0x40, 0x80), Assert.IsAssignableFrom<ISolidColorBrush>(settings.ScriptPreview.Foreground).Color);

            // 서 있는 카드는 그대로다.
            Assert.Same(box, WritingBox(window, session));
            Assert.False(box.IsSet(TextBox.FontWeightProperty));
            Assert.Same(themeForeground, box.Foreground);   // 테마의 글자색 그대로
            Assert.Equal(new WriterBase(string.Empty, 11, LineSpacing: 140), Card(session).WriterBase);

            // 새로 만든 카드는 굵고 그 색이다 — 카드에 적힌다.
            settings.Close();
            session.Editor.AddEpisode("ch01", "z", title: "z", 0, 0, sceneId: "a");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new WriterBase(string.Empty, 11, Bold: true, Color: blue, LineSpacing: 140), Card(session, "z").WriterBase);
            TextBox fresh = WritingBox(window, session, "z");
            Assert.Equal(FontWeight.Bold, fresh.FontWeight);
            Assert.Equal(Color.FromRgb(0x20, 0x40, 0x80), Assert.IsAssignableFrom<ISolidColorBrush>(fresh.Foreground).Color);

            // 처음부터 굵은 카드 — 서식 도구의 B는 눌려 보이고, 누르면 일부만 가늘게 할 수 없다고 말한다(서식은 그대로).
            session.Editor.ApplyScenarioText(Card(session, "z").Id, "오늘은 맑다");
            Dispatcher.UIThread.RunJobs();
            fresh = WritingBox(window, session, "z");
            fresh.Focus();
            fresh.SelectionStart = 0;
            fresh.SelectionEnd = 3;
            Dispatcher.UIThread.RunJobs();
            GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
            Assert.True(graph.FormatToolbarState.Bold);
            graph.ApplyFormat(WriterStyleChange.Toggle(WriterStyleKind.Bold));
            Assert.Contains("처음부터 굵게", session.StatusMessage, StringComparison.Ordinal);
            Assert.Empty(Card(session, "z").WriterMarks);

            // 기본값으로 — 설정은 돌아가고 카드는 그대로다.
            ScriptFont.Reset();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((false, (string?)null), AppSettingsService.LoadScriptLook());
            Assert.Equal(FontWeight.Bold, WritingBox(window, session, "z").FontWeight);

            window.Close();
        }
        finally
        {
            ScriptFont.Reset();
        }
    });

    /// <summary>
    /// 처음 글꼴 · 크기가 안 적힌 옛 카드(그 값이 생기기 전에 만든 프로젝트)는 열 때 그 PC의 지금 설정으로 굳는다(2026-11-23 소유자 — 「지금 설정으로 굳힘」).
    /// 연 것만으로는 「저장 안 한 변경」이 아니고, 저장하면 파일에 적혀 다시 열 때 그 PC의 설정과 상관없이 그 값이다.
    /// </summary>
    [Fact]
    public void 처음_값이_없는_옛_카드는_열_때_지금_설정으로_굳고_저장_안_한_변경이_아니다() => HeadlessUi.Run(() =>
    {
        try
        {
            ScriptFont.Set(null, 14, 140);
            (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);   // 처음 값 없이 지은 프로젝트

            Assert.All(session.Project.EnumerateNodes().OfType<DialogueNode>(), node => Assert.Equal(new WriterBase(string.Empty, 14, LineSpacing: 140), node.WriterBase));
            Assert.False(session.IsDirty);
            Assert.False(session.Editor.CanUndo);
            Assert.Equal(14, WritingBox(window, session).FontSize);

            // 설정을 바꿔도 그대로다. 저장하고 다시 열면 그 값이다.
            ScriptFont.Set(null, 20, 140);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(14, WritingBox(window, session).FontSize);
            session.Save();
            Assert.Contains("writerBase", File.ReadAllText(Directory.GetFiles(_directory, "*.json", SearchOption.AllDirectories)
                .Single(path => File.ReadAllText(path).Contains(Card(session).Id, StringComparison.Ordinal) && File.ReadAllText(path).Contains("\"writerBase\"", StringComparison.Ordinal))), StringComparison.Ordinal);

            session.Open(session.ProjectPath!);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new WriterBase(string.Empty, 14, LineSpacing: 140), Card(session).WriterBase);
            Assert.Equal(14, WritingBox(window, session).FontSize);

            window.Close();
        }
        finally
        {
            ScriptFont.Reset();
        }
    });

    [Fact]
    public void 범위_밖은_끝으로_당기고_같은_값은_알리지_않는다() => HeadlessUi.Run(() =>
    {
        try
        {
            ScriptFont.Reset();
            int told = 0;
            void Count() => told++;
            ScriptFont.Changed += Count;

            ScriptFont.Set(null, 100, 1000);
            Assert.Equal((ScriptFont.MaxSize, ScriptFont.MaxLineSpacing), (ScriptFont.Size, ScriptFont.LineSpacing));
            ScriptFont.Set("  ", 1, 10);
            Assert.Equal((string.Empty, ScriptFont.MinSize, ScriptFont.MinLineSpacing), (ScriptFont.Family, ScriptFont.Size, ScriptFont.LineSpacing));
            Assert.Equal(2, told);

            ScriptFont.Set(null, ScriptFont.MinSize, ScriptFont.MinLineSpacing);   // 같은 값
            Assert.Equal(2, told);

            ScriptFont.Changed -= Count;
        }
        finally
        {
            ScriptFont.Reset();
        }
    });

    /// <summary>
    /// 칸이 입을 줄 높이 — 줄 간격의 높이(0.1 단위)와 그 칸 글꼴 · 크기의 가장 낮은 줄 높이(<see cref="ScriptFont.MinLineHeight"/> · WF-D19 — 실제 배치로
    /// 맞는지는 <see cref="WriterLineHeightTests"/>가 본다) 가운데 큰 쪽. 가장 낮은 줄 높이는 그 PC의 한글 글꼴에 따라 다르다.
    /// </summary>
    private static double Expected(TextBox box, int lineSpacing) =>
        Math.Max(Math.Round(box.FontSize * lineSpacing / 100.0, 1), ScriptFont.MinLineHeight(new Typeface(box.FontFamily, box.FontStyle, box.FontWeight), box.FontSize));

    /// <summary>그 에피소드의 카드.</summary>
    private static DialogueNode Card(AuthoringSession session, string episodeId = "a") =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    /// <summary>[대본] 판에서 그 에피소드 카드의 글 칸.</summary>
    private static TextBox WritingBox(MainWindow window, AuthoringSession session, string episodeId = "a")
    {
        string cardId = EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;
        return window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(item => item.Tag as string == GraphEditorView.WritingTag + cardId);
    }
}
