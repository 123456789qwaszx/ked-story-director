using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Vn.Authoring.Chapters;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>Vn.Authoring 문구 표</b>(english-ui E5 — <c>src/Vn.Authoring/Strings/Msg.resx</c> · <c>Msg.en.resx</c>) — 두 표의 키가 같고, 영어 칸이 비지 않고,
/// 영어가 한국어에 없는 자리 표시(<c>{0}</c>)를 쓰지 않는다. 영어 표가 함께 구워져 꺼내진다. 시험은 한국어로 돈다(<see cref="TestLanguage"/>).
/// </summary>
public sealed class MessageTableTests
{
    private static readonly string StringsFolder = Path.GetFullPath("../../../../../src/Vn.Authoring/Strings");

    [Fact]
    public void 시험은_한국어로_돈다()
    {
        Assert.Equal("ko-KR", Msg.Culture?.Name);
    }

    [Fact]
    public void 두_표는_키가_같고_영어_칸이_비지_않고_자리_표시가_맞는다()
    {
        Dictionary<string, string> korean = Table("Msg.resx");
        Dictionary<string, string> english = Table("Msg.en.resx");

        Assert.NotEmpty(korean);
        Assert.Equal(korean.Keys.Order(StringComparer.Ordinal), english.Keys.Order(StringComparer.Ordinal));

        foreach ((string key, string value) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"{key}: 영어 칸이 비었습니다");
            Assert.True(Holes(value).IsSubsetOf(Holes(korean[key])), $"{key}: 영어가 한국어에 없는 자리를 씁니다 — {value}");
        }
    }

    [Fact]
    public void 영어_표가_함께_구워져_영어로_꺼낼_수_있다()
    {
        string key = Table("Msg.resx").Keys.First();

        Assert.Equal(Table("Msg.en.resx")[key], Msg.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en")));
        Assert.Equal(Table("Msg.resx")[key], Msg.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("ko-KR")));
    }

    [Fact]
    public void 조건_진단은_두_언어_모두_간선_패널이_고르는_머리로_시작한다()
    {
        // [구성] 간선 패널은 「조건 '이름' — 」 머리로 그 조건의 진단을 고른다(Diag_ConditionHead · ChapterGraphView) — 영어 문장도 같은 머리여야 한다.
        Dictionary<string, string> korean = Table("Msg.resx");
        Dictionary<string, string> english = Table("Msg.en.resx");
        string[] heads = korean.Where(pair => pair.Key != "Diag_ConditionHead" && pair.Value.StartsWith(korean["Diag_ConditionHead"], StringComparison.Ordinal))
            .Select(pair => pair.Key).ToArray();

        Assert.NotEmpty(heads);
        Assert.All(heads, key => Assert.StartsWith(english["Diag_ConditionHead"], english[key], StringComparison.Ordinal));
    }

    [Fact]
    public void 영어_표에는_한글이_없다()
    {
        // 2026-11-25 소유자 — 만들어지는 글(새 이름 · Yarn 주석 · 설정 파일의 안내 · 검증 보고의 자리 이름)도 영어 화면이면 영어다(english-ui E8).
        //   ⛔ E7까지 영어 칸에 옛 시트 이름(`간선` · `스탯` · `조건`)이 남아 있었다.
        Assert.Empty(Table("Msg.en.resx").Where(pair => Hangul.IsMatch(pair.Value)).Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    [Fact]
    public void 만들어지는_글은_화면_언어를_따르고_한국어로는_전과_같다()
    {
        // 처음 이름 · 자리 이름은 표에서 온다 — 한국어 화면에서는 E7까지와 같은 글이다. 자리를 가르는 열쇠(ChapterSheetNames의 상수)는 그대로다.
        Assert.Equal(("새 에피소드", "새 장면", "새 챕터", "새 분기"), (NewNames.Episode, NewNames.Scene, NewNames.Chapter, NewNames.Branch));
        Assert.Equal("간선", ChapterSheetNames.Edges);
        Assert.Equal("간선", ChapterSheetNames.Display(ChapterSheetNames.Edges));
        Assert.Equal("ch01.json", ChapterSheetNames.Display("ch01.json"));   // 모르는 자리는 그대로
        Assert.Null(ChapterSheetNames.Display(null));

        CultureInfo english = CultureInfo.GetCultureInfo("en");
        Assert.Equal(
            ["New episode", "New scene", "New chapter", "New branch", "Paths", "// Episode: {0}", "_note"],
            new[] { "Name_NewEpisode", "Name_NewScene", "Name_NewChapter", "Name_NewBranch", "Place_Edges", "Yarn_EpisodeComment", "Export_NoteKey" }
                .Select(key => Msg.ResourceManager.GetString(key, english)));
    }

    private static readonly Regex Hangul = new("[가-힣ㄱ-ㅎㅏ-ㅣ]");

    private static Dictionary<string, string> Table(string file) =>
        XDocument.Load(Path.Combine(StringsFolder, file)).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static HashSet<string> Holes(string format) =>
        Regex.Matches(format.Replace("{{", string.Empty, StringComparison.Ordinal), @"\{(\d+)").Select(match => match.Groups[1].Value).ToHashSet();
}
