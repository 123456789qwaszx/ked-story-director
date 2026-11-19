using System.Text;

namespace Vn.Authoring.Text;

/// <summary>
/// <b>한글 이름을 로마자로</b> (2026-11-19 소유자 · usability UX29 — 화자를 더할 때 임시 캐릭터 키를 짓는다 · <see cref="Model.CharacterKey"/>).
/// 국어의 로마자 표기법(2000)의 글자 표를 따르고, 소리가 바뀌는 규칙은 둘만 본다 — 받침이 다음 「ㅇ」 자리로 넘어간다(석우 → seogu),
/// 「ㄹㄹ」은 ll이다(윌로 → willo). 그 밖의 동화(신라 → silla 따위)는 보지 않는다 — 임시 키라 사람이 고친다.
///
/// 결과는 영문 소문자 · 숫자 · <c>_</c>뿐이다. 영문 · 숫자는 소문자로 그대로 두고, 그 밖의 글자(공백 · 문장부호 · 가나 · 한자 · 낱자모)는
/// 사이를 가르는 <c>_</c> 하나가 된다(앞뒤의 <c>_</c>는 걷는다). 남는 것이 없으면 빈 글이다.
/// </summary>
public static class HangulRomanizer
{
    private const int SyllableFirst = 0xAC00;

    private const int SyllableLast = 0xD7A3;

    private static readonly string[] Initials =
        ["g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h"];

    private static readonly string[] Medials =
        ["a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i"];

    /// <summary>받침(0 = 없음) — 낱말 끝이나 자음 앞에서.</summary>
    private static readonly string[] Finals =
        ["", "k", "k", "k", "n", "n", "n", "t", "l", "k", "m", "l", "l", "l", "p", "l", "m", "p", "p", "t", "t", "ng", "t", "t", "k", "t", "p", "t"];

    /// <summary>받침이 다음 「ㅇ」 자리로 넘어갈 때 — (남는 것, 넘어가는 것). ㅇ 받침은 넘어가지 않는다.</summary>
    private static readonly (string Stay, string Move)[] Liaisons =
    [
        ("", ""), ("", "g"), ("", "kk"), ("k", "s"), ("", "n"), ("n", "j"), ("n", ""), ("", "d"), ("", "r"), ("l", "g"),
        ("l", "m"), ("l", "b"), ("l", "s"), ("l", "t"), ("l", "p"), ("l", ""), ("", "m"), ("", "b"), ("p", "s"), ("", "s"),
        ("", "ss"), ("ng", ""), ("", "j"), ("", "ch"), ("", "k"), ("", "t"), ("", "p"), ("", "")
    ];

    private const int SilentInitial = 11;   // ㅇ

    private const int RieulInitial = 5;     // ㄹ

    private const int RieulFinal = 8;       // ㄹ

    private const int IeungFinal = 21;      // ㅇ

    /// <summary>그 이름의 로마자 — 영문 소문자 · 숫자 · <c>_</c>. 바꿀 것이 없으면 빈 글.</summary>
    public static string Romanize(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        bool gap = false;

        for (int index = 0; index < name.Length; index++)
        {
            char letter = name[index];

            if (Syllable(letter) is { } syllable)
            {
                if (gap)
                {
                    Separate(text);
                    gap = false;
                }

                (int? nextInitial, bool nextIsSyllable) = index + 1 < name.Length && Syllable(name[index + 1]) is { } next
                    ? (next.Initial, true)
                    : ((int?)null, false);

                // 앞 글자가 받침 ㄹ이고 이 글자의 초성이 ㄹ이면 r이 아니라 l이다(윌로 → willo).
                bool afterRieul = index > 0 && Syllable(name[index - 1]) is { Final: RieulFinal };
                text.Append(afterRieul && syllable.Initial == RieulInitial ? "l" : Initials[syllable.Initial]);
                text.Append(Medials[syllable.Medial]);

                if (nextIsSyllable && nextInitial == SilentInitial && syllable.Final != IeungFinal)
                {
                    (string stay, string move) = Liaisons[syllable.Final];
                    text.Append(stay).Append(move);
                }
                else
                {
                    text.Append(Finals[syllable.Final]);
                }

                continue;
            }

            if (char.IsAsciiLetterOrDigit(letter))
            {
                if (gap)
                {
                    Separate(text);
                    gap = false;
                }

                text.Append(char.ToLowerInvariant(letter));
                continue;
            }

            gap = true;
        }

        return text.ToString();
    }

    /// <summary>앞에 글이 있을 때만 가른다 — 머리의 <c>_</c>는 두지 않는다.</summary>
    private static void Separate(StringBuilder text)
    {
        if (text.Length > 0)
        {
            text.Append('_');
        }
    }

    private static (int Initial, int Medial, int Final)? Syllable(char letter)
    {
        if (letter < SyllableFirst || letter > SyllableLast)
        {
            return null;
        }

        int offset = letter - SyllableFirst;
        return (offset / (21 * 28), offset % (21 * 28) / 28, offset % 28);
    }
}
