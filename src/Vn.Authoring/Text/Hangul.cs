namespace Vn.Authoring.Text;

/// <summary>
/// <b>이름을 고를 때의 한글 맞추기</b>(2026-11-18 소유자 — 「자음만 입력하거나, 혹은 처음 한 글자만 입력하더라도 바로 자동완성이 시작되도록」 ·
/// writer-comfort WC3). 화자 이름 같은 짧은 목록에서 친 글이 그 이름에 맞는지 본다 —
/// <list type="bullet">
/// <item>글로 들어 있으면 맞는다(대소문자는 가리지 않는다) — 「통」은 샤통에.</item>
/// <item>자음(호환 자모 ㄱ–ㅎ)은 그 자리 글자의 <b>초성</b>과 견준다 — 「ㅅ」 · 「ㅅㅌ」 · 「샤ㅌ」은 샤통에.</item>
/// <item>마지막 글자가 받침 없는 음절이면 <b>앞부분만</b> 맞아도 된다 — 조합 중인 「사」가 다음 자음을 받으면 「삭」 · 「산」이 될 수 있으니,
///   「사」는 「사」 · 「삭」 · 「산」에 맞는다(초성 · 중성이 같다).</item>
/// </list>
/// 이름 어디서부터든 이어서 맞으면 맞는다(글로 맞추기와 같은 「들어 있다」 규칙).
/// </summary>
public static class Hangul
{
    private const char First = '가';

    private const char Last = '힣';

    /// <summary>초성 19개 — 호환 자모.</summary>
    private const string Initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    /// <summary>친 글(<paramref name="query"/>)이 이름에 맞는가 — 비었으면 늘 맞는다.</summary>
    public static bool Matches(string? name, string? query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (int start = 0; start + query.Length <= name.Length; start++)
        {
            bool all = true;

            for (int index = 0; index < query.Length && all; index++)
            {
                all = CharMatches(name[start + index], query[index], last: index == query.Length - 1);
            }

            if (all)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>그 음절의 초성(호환 자모). 한글 음절이 아니면 null.</summary>
    public static char? InitialOf(char syllable) =>
        syllable is >= First and <= Last ? Initials[(syllable - First) / 588] : null;

    private static bool CharMatches(char named, char typed, bool last)
    {
        if (char.ToUpperInvariant(named) == char.ToUpperInvariant(typed))
        {
            return true;
        }

        // 자음만 쳤다 — 그 자리 글자의 초성과.
        if (Initials.Contains(typed))
        {
            return InitialOf(named) == typed;
        }

        // 조합 중인 마지막 음절 — 받침이 없으면 초성 · 중성이 같은 글자에.
        if (last && typed is >= First and <= Last && named is >= First and <= Last && (typed - First) % 28 == 0)
        {
            return (named - First) / 28 == (typed - First) / 28;
        }

        return false;
    }
}
