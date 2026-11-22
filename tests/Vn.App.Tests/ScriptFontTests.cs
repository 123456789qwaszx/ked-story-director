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
/// 이 PC의 앱 설정이고 고치는 즉시 앱 설정에 들어간다(「기타」의 체크와 같은 규칙). 글꼴 · 글자 크기는 새로 만드는 카드의 처음 값이고(카드에 적힌다 ·
/// writer-formatting WF-D13), 줄 간격은 서 있는 글 칸이 판을 다시 짓지 않고 따른다 — 같은 칸이 새 줄 간격을 입는다.
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

    /// <summary>
    /// 2026-11-23 소유자 — 「설정 내부에 대본 글칸속의 글꼴과 글자 크기는 이제 처음 생성시에 기본값으로만 사용하는 게 맞겠습니다. 지금처럼 즉각 갱신되는게
    /// 아니라요」(writer-formatting WF-D13). 서 있는 카드는 처음 글꼴 · 크기 그대로이고 줄 간격만 바로 따른다. 새로 만든 카드가 그 글꼴 · 크기로 서고
    /// 카드에 적힌다. ⛔ 그 전에는 모든 칸이 셋 다 바로 따랐다.
    /// </summary>
    [Fact]
    public void 고치면_서_있는_칸은_줄_간격만_따르고_새_카드가_그_글꼴_크기로_선다() => HeadlessUi.Run(() =>
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

            // 같은 칸이다 — 판을 다시 짓지 않았다. 글꼴 · 크기는 그 카드의 처음 값 그대로이고 줄 간격만 따른다.
            Assert.Same(box, WritingBox(window, session));
            Assert.Equal((11d, 19.8), (box.FontSize, box.LineHeight));
            Assert.False(box.IsSet(TextBox.FontFamilyProperty));
            Assert.Equal((16d, 28.8), (settings.ScriptPreview.FontSize, settings.ScriptPreview.LineHeight));   // 미리 보기는 새 카드의 모습
            Assert.Equal((font, 16d, 180), AppSettingsService.LoadScriptFont());
            Assert.False(session.Editor.CanUndo);   // 프로젝트는 바뀌지 않는다
            Assert.Equal(new WriterBase(string.Empty, 11), Card(session).WriterBase);

            // [취소]로 돌아가지 않고, 새로 만든 카드는 그 글꼴 · 크기로 선다 — 카드에 적힌다.
            settings.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(11, box.FontSize);
            session.Editor.AddEpisode("ch01", "z", title: "z", 0, 0, sceneId: "a");   // 판이 다시 선다
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new WriterBase(font, 16), Card(session, "z").WriterBase);
            Assert.Equal((16d, 28.8, font), (WritingBox(window, session, "z").FontSize, WritingBox(window, session, "z").LineHeight, WritingBox(window, session, "z").FontFamily.Name));
            Assert.Equal(11, WritingBox(window, session).FontSize);

            // 기본값으로 — 줄 간격만 모든 칸이 따른다. 카드의 글꼴 · 크기는 그대로다.
            settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(font, settings.ScriptFontCombo.SelectedItem);
            settings.ScriptFontDefaultButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((11d, 15.4), (WritingBox(window, session).FontSize, WritingBox(window, session).LineHeight));
            Assert.False(WritingBox(window, session).IsSet(TextBox.FontFamilyProperty));
            Assert.Equal((16d, 22.4, font), (WritingBox(window, session, "z").FontSize, WritingBox(window, session, "z").LineHeight, WritingBox(window, session, "z").FontFamily.Name));
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

            Assert.All(session.Project.EnumerateNodes().OfType<DialogueNode>(), node => Assert.Equal(new WriterBase(string.Empty, 14), node.WriterBase));
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
            Assert.Equal(new WriterBase(string.Empty, 14), Card(session).WriterBase);
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
