using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>대사에 스탯의 지금 값을 넣는 표기 — [대본] 탭 챕터 인스펙터가 복사해 주는 글.</summary>
public sealed class ChapterStatScriptTests
{
    [Theory]
    [InlineData("성실", "{stat(\"성실\")}")]
    [InlineData("호감도 (윌로우)", "{stat(\"호감도 (윌로우)\")}")]   // 키를 정규화하지 않는다 — 진행 JSON의 키 그대로
    [InlineData("따옴\"표", "{stat(\"따옴\\\"표\")}")]               // 문자열을 깨뜨리는 글자만 막는다
    public void 키를_그대로_stat_함수에_싣는다(string key, string expected) =>
        Assert.Equal(expected, ChapterStatScript.InlineRead(new ChapterStat(key, "보이는 이름", 0, 0, 5)));
}
