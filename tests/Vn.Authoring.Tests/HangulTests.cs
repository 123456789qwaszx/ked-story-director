using Vn.Authoring.Text;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>이름 고르기의 한글 맞추기</b> — 화자 자동완성이 첫 글자 · 자음부터 걸러 낸다(2026-11-18 소유자 · <c>docs/work-orders/writer-comfort-orders.md</c>
/// WC3). 글로 들어 있음 · 초성 · 조합 중인 마지막 음절 · 맞지 않는 것을 못 박는다.
/// </summary>
public sealed class HangulTests
{
    [Theory]
    [InlineData("샤통", "", true)]
    [InlineData("샤통", "샤", true)]
    [InlineData("샤통", "통", true)]            // 들어 있다
    [InlineData("샤통", "ㅅ", true)]            // 초성
    [InlineData("샤통", "ㅅㅌ", true)]
    [InlineData("샤통", "샤ㅌ", true)]
    [InlineData("샤통", "ㅌ", true)]            // 어디서부터든
    [InlineData("샤통", "사", false)]           // 중성이 다르다
    [InlineData("샤통", "샥", false)]           // 받침까지 친 글자는 그 글자여야
    [InlineData("시마오", "시", true)]
    [InlineData("카시미라", "ㅋㅅ", true)]
    [InlineData("카시미라", "칵", false)]
    [InlineData("돌아온 백묘 단원", "ㅂㅁ", true)]
    [InlineData("베니토", "ㅅ", false)]
    [InlineData("AN", "an", true)]              // 영문은 대소문자를 가리지 않는다
    [InlineData("???", "?", true)]
    public void 친_글이_이름에_맞는가(string name, string query, bool expected) =>
        Assert.Equal(expected, Hangul.Matches(name, query));

    [Fact]
    public void 조합_중인_받침_없는_마지막_음절은_받침이_붙을_글자에도_맞는다()
    {
        // 「통」을 치는 중에는 「토」가 먼저 보인다 — 받침을 받을 자리다.
        Assert.True(Hangul.Matches("샤통", "샤토"));
        Assert.True(Hangul.Matches("미르사", "미르사"));
        Assert.False(Hangul.Matches("샤통", "토샤"));   // 마지막이 아닌 글자는 그대로 견준다
    }

    [Fact]
    public void 초성()
    {
        Assert.Equal('ㅅ', Hangul.InitialOf('샤'));
        Assert.Equal('ㄲ', Hangul.InitialOf('까'));
        Assert.Null(Hangul.InitialOf('A'));
    }
}
