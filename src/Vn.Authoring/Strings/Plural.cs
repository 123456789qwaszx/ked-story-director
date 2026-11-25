using System.Globalization;
using System.Text.RegularExpressions;

namespace Vn.Authoring.Strings;

/// <summary>
/// <b>수에 맞춘 낱말</b> — 문구 표의 <c>{n:하나|여럿}</c> 꼴을 편다(english-ui E9). 영어 칸에만 쓴다 — 한국어는 수가 낱말을 바꾸지 않는다(「줄 {2}개」).
///
/// <c>{2:line|lines}</c>는 그 자리의 수와 낱말로 펴진다: 1이면 「1 line」, 그 밖(0 · 2 · 1,234)이면 「… lines」. 수의 꼴은 <c>string.Format</c>과 같다
/// (지금 문화). 동사까지 수를 따르는 문장(<i>1 line is</i> / <i>3 lines are</i>)은 이 꼴로 풀지 않고 수와 상관없는 문장으로 짓는다.
///
/// ⚠ 이 꼴이 든 키는 반드시 <see cref="Format"/>으로 편다 — <c>string.Format</c>은 <c>{2:line|lines}</c>를 수의 서식으로 읽어 「line|lines」를 그대로
/// 찍는다. 시험(<c>MessageTableTests</c> · <c>UiLanguageTests</c>)이 그런 키를 <c>Plural.Format</c> 밖에서 쓰는 자리를 짚는다.
/// ⛔ E8까지는 수 뒤 낱말이 늘 여럿이라 「1 lines」가 나왔다(검증 보고 — 2026-11-25 Windows 점검에서 찾았다).
/// </summary>
public static class Plural
{
    /// <summary><c>{n:하나|여럿}</c> — 두 낱말에 중괄호가 없고, 꼭 하나의 <c>|</c>로 가른다.</summary>
    public static Regex Token { get; } = new(@"\{(\d+):([^{}|]*)\|([^{}|]*)\}", RegexOptions.CultureInvariant);

    /// <summary><see cref="string.Format(string, object?[])"/>처럼 펴되, 수 낱말 자리는 그 자리의 수에 맞춘 낱말을 붙인다.</summary>
    public static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Expand(format, args), args);

    /// <summary>수 낱말 자리를 보통 자리 표시와 낱말로 바꾼다 — <c>{2:line|lines}</c> → <c>{2} line</c> 또는 <c>{2} lines</c>.</summary>
    public static string Expand(string format, IReadOnlyList<object?> args) =>
        Token.Replace(format, match =>
        {
            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string word = IsOne(index < args.Count ? args[index] : null) ? match.Groups[2].Value : match.Groups[3].Value;
            return "{" + match.Groups[1].Value + "} " + word;
        });

    /// <summary>하나인가 — 수 1, 또는 「1」로 적힌 글(미리 꼴을 갖춰 넘긴 수). 0과 그 밖은 여럿이다(영어: 0 lines).</summary>
    public static bool IsOne(object? value) => value switch
    {
        string text => text.Trim() == "1",
        sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToDecimal(value, CultureInfo.InvariantCulture) == 1m,
        decimal number => number == 1m,
        double number => number == 1d,
        float number => number == 1f,
        _ => false
    };
}
