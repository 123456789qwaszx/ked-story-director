using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App.Tests;

/// <summary>
/// <b>설정 창 「대본 글 칸」</b> — [대본] 판 카드 글 칸의 글꼴 · 글자 크기 · 줄 간격(2026-11-18 소유자 — 「글꼴이나, 줄 사이의 간격, 폰트 크기
/// 같은 것들을 조정할 수 있다면 작가들이 좋아하겠습니다. 이런 것들을 설정에서」 · <c>docs/work-orders/writer-comfort-orders.md</c> WC5).
///
/// 이 PC의 앱 설정이고 고치는 즉시 반영된다(「기타」의 체크와 같은 규칙). 서 있는 글 칸은 판을 다시 짓지 않고 따른다 — 같은 칸이 새 글꼴을 입는다.
/// 설정은 이 시험 프로세스가 함께 쓰므로 끝에 기본값으로 돌린다.
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
        Assert.Equal(15.4, box.LineHeight);
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

    [Fact]
    public void 고치면_서_있는_글_칸이_판을_다시_짓지_않고_바로_따르고_기억한다() => HeadlessUi.Run(() =>
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

            // 같은 칸이다 — 판을 다시 짓지 않았다.
            Assert.Same(box, WritingBox(window, session));
            Assert.Equal((16d, 28.8, font), (box.FontSize, box.LineHeight, box.FontFamily.Name));
            Assert.Equal((16d, 28.8), (settings.ScriptPreview.FontSize, settings.ScriptPreview.LineHeight));   // 미리 보기도
            Assert.Equal((font, 16d, 180), AppSettingsService.LoadScriptFont());
            Assert.False(session.Editor.CanUndo);   // 프로젝트는 바뀌지 않는다

            // [취소]로 돌아가지 않고, 새 판의 글 칸도 그 설정으로 선다.
            settings.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(16, box.FontSize);
            session.Editor.AddEpisode("ch01", "z", title: "z", 0, 0, sceneId: "a");   // 판이 다시 선다
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(16, WritingBox(window, session, "z").FontSize);
            box = WritingBox(window, session);

            // 기본값으로 — 테마의 글꼴로 돌아간다.
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(font, settings.ScriptFontCombo.SelectedItem);
            settings.ScriptFontDefaultButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((11d, 15.4), (box.FontSize, box.LineHeight));
            Assert.False(box.IsSet(TextBox.FontFamilyProperty));
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

    /// <summary>[대본] 판에서 그 에피소드 카드의 글 칸.</summary>
    private static TextBox WritingBox(MainWindow window, AuthoringSession session, string episodeId = "a")
    {
        string cardId = EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;
        return window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(item => item.Tag as string == GraphEditorView.WritingTag + cardId);
    }
}
