using Vn.Authoring.Strings;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>수에 맞춘 낱말</b>(english-ui E9 — <see cref="Plural"/>) — 영어 문구 표의 <c>{n:하나|여럿}</c>가 그 자리의 수로 단수 · 복수를 고른다.
/// ⛔ E8까지는 「1 lines of dialogue」처럼 늘 복수였다.
/// </summary>
public sealed class PluralTests
{
    [Theory]
    [InlineData(1, "1 line")]
    [InlineData(0, "0 lines")]
    [InlineData(2, "2 lines")]
    [InlineData(1L, "1 line")]
    [InlineData(1.5, "1.5 lines")]
    public void 하나면_단수_그_밖은_복수다(object count, string expected) =>
        Assert.Equal(expected, Plural.Format("{0:line|lines}", count));

    [Fact]
    public void 자리마다_따로_고르고_보통_자리와_수의_서식은_그대로다()
    {
        Assert.Equal("'a' — 1 path · 2 conditions · 12pt", Plural.Format("'{0}' — {1:path|paths} · {2:condition|conditions} · {3:0}pt", "a", 1, 2, 12.0));

        // 미리 꼴을 갖춘 수(「1,234」)도 받는다 — 글 「1」만 하나다.
        Assert.Equal("1,234 characters without spaces · 1 line", Plural.Format("{0:character|characters} without spaces · {1:line|lines}", "1,234", "1"));

        Assert.Equal("{x} 1 card", Plural.Format("{{x}} {0:card|cards}", 1));   // 중괄호 글자는 그대로
        Assert.Equal("줄 1개", Plural.Format("줄 {0}개", 1));   // 꼴이 없으면(한국어) string.Format 그대로
    }
}
