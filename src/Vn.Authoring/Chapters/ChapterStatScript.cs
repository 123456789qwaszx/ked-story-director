using Vn.Authoring.Rendering;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 대본(대사)에 스탯을 쓰는 표기 — 작가가 [대본] 탭 챕터 인스펙터의 표에서 그대로 복사해 붙인다.
/// </summary>
public static class ChapterStatScript
{
    /// <summary>
    /// 대사 안에 그 스탯의 지금 값을 넣는 표기 — <c>{stat("성실")}</c>. 인자는 스탯의 <b>키</b> 그대로다
    /// (<see cref="YarnSyntax.StatRead"/> — 정규화하지 않는다). 표시 이름이 키와 달라도 대본에는 키가 들어간다.
    /// </summary>
    public static string InlineRead(ChapterStat stat)
    {
        ArgumentNullException.ThrowIfNull(stat);

        return "{" + YarnSyntax.StatRead(stat.Key) + "}";
    }
}
