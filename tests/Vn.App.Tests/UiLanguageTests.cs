using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Strings;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>화면 언어의 틀</b>(2026-11-25 소유자 · <c>docs/work-orders/english-ui-orders.md</c> E1) — 처음은 PC 언어(한국어 PC면 한국어, 그 밖은 영어),
/// 설정 창 「기타」의 언어 칸에서 고르면 다시 켤 때 바뀐다. 문구는 표(<c>Strings/Ui.resx</c> · <c>Ui.en.resx</c>)에서 꺼낸다 — 두 표의 키가 같고,
/// 영어 칸이 비지 않고, 영어가 한국어에 없는 자리 표시(<c>{0}</c>)를 쓰지 않는다. 시험은 한국어로 돈다(<see cref="TestProcessIsolation"/>).
/// </summary>
public sealed class UiLanguageTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-ui-language", Guid.NewGuid().ToString("N"));

    private static readonly string StringsFolder = Path.GetFullPath("../../../../../src/Vn.App/Strings");

    public void Dispose()
    {
        AppSettingsService.SaveLanguage(null);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, "ko-KR", "ko")]
    [InlineData(null, "ko", "ko")]
    [InlineData(null, "en-US", "en")]
    [InlineData(null, "ja-JP", "en")]   // 한국어가 아니면 영어
    [InlineData("", "ko-KR", "ko")]
    [InlineData("fr", "ko-KR", "ko")]   // 모르는 값은 PC 언어 따라
    [InlineData("en", "ko-KR", "en")]   // 고른 것이 PC 언어보다 먼저다
    [InlineData(" KO ", "en-US", "ko")]
    public void 처음은_PC_언어를_따르고_고른_언어가_있으면_그것이다(string? setting, string os, string expected)
    {
        Assert.Equal(expected, UiLanguage.Resolve(setting, CultureInfo.GetCultureInfo(os)));
    }

    [Fact]
    public void 시험은_한국어_화면으로_돈다()
    {
        Assert.Equal(UiLanguage.Korean, UiLanguage.Current);
        Assert.Equal("언어 (Language)", Ui.Settings_LanguageLabel);
    }

    [Fact]
    public void 영어_표가_함께_구워져_영어로_꺼낼_수_있다()
    {
        // 영어 표는 따로 구운 어셈블리(en/StoryDirector.resources.dll)다 — 꾸러미에 함께 가야 영어 화면이 선다.
        Assert.Equal("Language (언어)", Ui.ResourceManager.GetString(nameof(Ui.Settings_LanguageLabel), CultureInfo.GetCultureInfo("en")));
        Assert.Equal("Language (언어)", Ui.ResourceManager.GetString(nameof(Ui.Settings_LanguageLabel), CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("언어 (Language)", Ui.ResourceManager.GetString(nameof(Ui.Settings_LanguageLabel), CultureInfo.GetCultureInfo("ko-KR")));
    }

    [Fact]
    public void 두_표는_키가_같고_영어_칸이_비지_않고_자리_표시가_맞는다()
    {
        Dictionary<string, string> korean = Table("Ui.resx");
        Dictionary<string, string> english = Table("Ui.en.resx");

        Assert.NotEmpty(korean);
        Assert.Equal(korean.Keys.Order(StringComparer.Ordinal), english.Keys.Order(StringComparer.Ordinal));

        foreach ((string key, string value) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"{key}: 영어 칸이 비었습니다");

            // 영어가 한국어에 없는 자리를 쓰면 string.Format이 터진다. 영어가 덜 쓰는 것은 된다 — 조사({1} = 은/는)처럼 한국어에만 드는 자리가 있다.
            Assert.True(Holes(value).IsSubsetOf(Holes(korean[key])), $"{key}: 영어가 한국어에 없는 자리를 씁니다 — {value}");
        }
    }

    [Fact]
    public void 설정_창의_언어_칸은_고르면_앱_설정에_들고_화면은_다시_켤_때_바뀐다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["PC 언어 따라 — 지금 한국어", "한국어", "English"], settings.LanguageCombo.Items.Cast<string>());
        Assert.Equal(0, settings.LanguageCombo.SelectedIndex);
        Assert.False(settings.LanguageNote.IsVisible);

        settings.LanguageCombo.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(UiLanguage.English, AppSettingsService.LoadLanguage());
        Assert.True(settings.LanguageNote.IsVisible);
        Assert.Equal("다시 켜면 English로 바뀝니다.", settings.LanguageNote.Text);
        Assert.Equal("다시 켜면 English로 바뀝니다.", session.StatusMessage);
        Assert.Equal(UiLanguage.Korean, UiLanguage.Current);   // 지금 화면은 그대로
        Assert.Equal("언어 (Language)", Ui.Settings_LanguageLabel);

        // 다시 열면 고른 것이 보인다 — 지금 화면과 같은 언어로 되돌리면 알림이 걷힌다.
        settings.Close();
        settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, settings.LanguageCombo.SelectedIndex);

        settings.LanguageCombo.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.Null(AppSettingsService.LoadLanguage());
        Assert.False(settings.LanguageNote.IsVisible);

        settings.Close();
        window.Close();
    });

    [Fact]
    public void 영어_표에는_언어_칸의_이름_말고_한글이_없다()
    {
        // 언어 칸의 이름(「Language (언어)」)은 두 언어로 적는다 — 읽지 못하는 언어로 켜졌어도 찾게(E1). 하이어아키 찾기의 「ㅅㅌ」는 한국어 이름을
        // 자음으로 찾는 예다. 그 밖에 영어 칸에 남은 한글은 영어 화면에 한국어로 보인다(english-ui E8 — ⛔ E7까지 「새 챕터」 · 「새 장면」이 남아 있었다).
        Assert.Equal(
            ["Hierarchy_FindsEpisodesScenesChaptersBy", "Settings_LanguageLabel"],
            Table("Ui.en.resx").Where(pair => Regex.IsMatch(pair.Value, "[가-힣ㄱ-ㅎㅏ-ㅣ]")).Select(pair => pair.Key).Order(StringComparer.Ordinal));
    }

    private static Dictionary<string, string> Table(string file) =>
        XDocument.Load(Path.Combine(StringsFolder, file)).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static HashSet<string> Holes(string format) =>
        Regex.Matches(format.Replace("{{", string.Empty, StringComparison.Ordinal), @"\{(\d+)").Select(match => match.Groups[1].Value).ToHashSet();
}
