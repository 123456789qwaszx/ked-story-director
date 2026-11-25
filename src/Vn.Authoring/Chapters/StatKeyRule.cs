using System.Text.RegularExpressions;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>스탯 키는 영어로 적는다</b> — 영문자로 시작하고 영문자 · 숫자 · <c>_</c>만(2026-11-23 소유자 — 「스탯 역시, 이건 Yarn쪽에 바로 들어가는 statKey이므로
/// 한글을 허용하지 않고 영어로 작성해야 한다는 제한을 분명히 해주십시오」). 키는 대사의 <c>{stat("키")}</c> · 조건 식 · 진행 JSON · 세이브가 그대로
/// 쓰는 이름이다.
///
/// 새로 만들거나 바꾸는 키는 받지 않는다(<c>ProjectEditor.AddChapterStat</c> · <c>RenameChapterStat</c>). 이미 있는 키(옛 프로젝트 · 손으로 고친 파일)는
/// 검증 보고가 경고로 짚는다(<see cref="ChapterDiagnosticCode.StatKeyNotEnglish"/>) — 내보내기는 막지 않는다. 키 칸에서 고치면 간선 · 조건이 따라간다.
/// </summary>
public static partial class StatKeyRule
{
    /// <summary>예로 드는 키.</summary>
    public const string Examples = "courage · met_harin · trust2";

    /// <summary>규칙 한 줄 — 툴팁 · 안내가 쓴다.</summary>
    public static string Rule => string.Format(Vn.Authoring.Strings.Msg.StatKey_Rule, Examples);

    /// <summary>그 키를 쓸 수 있는가 — 쓸 수 없으면 까닭, 되면 null.</summary>
    public static string? Problem(string? key)
    {
        string target = (key ?? string.Empty).Trim();

        if (target.Length == 0 || Allowed().IsMatch(target))
        {
            return null;
        }

        return Hangul().IsMatch(target)
            ? string.Format(Msg.StatKey_StatKeyContainsHangulStat, target, Examples)
            : string.Format(Msg.StatKey_StatKeyCanTBe, target, Examples);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex Allowed();

    [GeneratedRegex("[\\u1100-\\u11FF\\u3130-\\u318F\\uAC00-\\uD7A3]")]
    private static partial Regex Hangul();
}
