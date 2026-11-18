using Vn.Authoring.Text;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>이름 뒤의 조사는 받침으로 고른다</b> — 「'new01'을(를)」 같은 두 꼴 글을 걷고(2026-11-18 소유자 — 어색한 문구 교정 ·
/// <c>docs/work-orders/finishing-orders.md</c> FX2). 한글 · 숫자 · 영문 · 닫는 글자를 못 박는다.
/// </summary>
public sealed class JosaTests
{
    [Theory]
    [InlineData("교실", "을")]          // 받침 ㄹ — 을 · 으로는 「로」
    [InlineData("축제 전날", "을")]
    [InlineData("장면", "을")]
    [InlineData("나비", "를")]
    [InlineData("ch01", "을")]          // 일
    [InlineData("ch02", "를")]          // 이
    [InlineData("new10", "을")]         // 십
    [InlineData("a", "를")]             // 에이
    [InlineData("opening", "을")]       // 오프닝
    [InlineData("name", "을")]          // 네임
    [InlineData("title", "을")]         // 타이틀
    [InlineData("layout", "을")]        // 레이아웃
    [InlineData("text", "를")]          // 텍스트
    [InlineData("JSON", "을")]          // 제이슨
    [InlineData("ID", "를")]            // 아이디
    [InlineData("'교실'", "을")]        // 닫는 글자는 건너뛴다
    [InlineData("「나비」", "를")]
    [InlineData("", "를")]              // 알 수 없으면 받침 없는 꼴
    [InlineData(null, "를")]
    public void 을를(string? word, string expected) => Assert.Equal(expected, Josa.을를(word));

    [Fact]
    public void 짝마다_받침으로_고른다()
    {
        Assert.Equal("은", Josa.은는("분기점"));
        Assert.Equal("는", Josa.은는("br02"));
        Assert.Equal("이", Josa.이가("진행 JSON"));
        Assert.Equal("가", Josa.이가("대사"));
        Assert.Equal("이", Josa.이가("대사 Yarn"));
        Assert.Equal("과", Josa.과와("에피소드 'b1'"));
        Assert.Equal("와", Josa.과와("분기 카드 'br02'"));
        Assert.Equal("은", Josa.은는(50000.0));   // 수도 받는다
    }

    [Theory]
    [InlineData("교실", "로")]          // ㄹ 받침은 「로」
    [InlineData("ch01", "로")]          // 일
    [InlineData("ch07", "로")]          // 칠
    [InlineData("ch03", "으로")]        // 삼
    [InlineData("축제 전날", "로")]
    [InlineData("장면", "으로")]
    [InlineData("나비", "로")]
    [InlineData("label", "로")]         // 레이블
    [InlineData("name", "으로")]
    public void 으로(string word, string expected) => Assert.Equal(expected, Josa.으로(word));
}
