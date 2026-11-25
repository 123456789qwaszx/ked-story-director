namespace Vn.Authoring.Text;

/// <summary>
/// <b>이름 뒤에 붙는 조사를 고른다</b> — 「'new01'을(를)」처럼 두 꼴을 함께 적던 글을 「'new01'을」로(2026-11-18 소유자 — 어색한 문구 교정 ·
/// finishing FX2). 끝 글자의 받침으로 고른다 — 한글은 받침 그대로, 숫자는 읽는 소리(1 일 · 2 이 · 0 영 …),
/// 영문은 끝소리(…ng · …n · …m · …l · …k · …p는 받침, 읽지 않는 e — name 「네임」 · line 「라인」 · title 「타이틀」, …ut — layout 「레이아웃」,
/// 대문자 약어는 글자 이름 — JSON 「제이슨」 · ID 「아이디」). 따옴표 · 괄호 같은 닫는 글자는 건너뛴다.
/// 알 수 없으면 받침이 없는 꼴이다.
/// </summary>
public static class Josa
{
    /// <summary>은 · 는</summary>
    public static string 은는(object? word) => Final(word) == Sound.None ? "는" : "은";

    /// <summary>이 · 가</summary>
    public static string 이가(object? word) => Final(word) == Sound.None ? "가" : "이";

    /// <summary>을 · 를</summary>
    public static string 을를(object? word) => Final(word) == Sound.None ? "를" : "을";

    /// <summary>과 · 와</summary>
    public static string 과와(object? word) => Final(word) == Sound.None ? "와" : "과";

    /// <summary>으로 · 로 — 받침이 ㄹ이면 「로」다.</summary>
    public static string 으로(object? word) => Final(word) == Sound.Batchim ? "으로" : "로";

    private enum Sound
    {
        None,
        Batchim,
        Rieul
    }

    /// <summary>끝 글자의 받침 — 닫는 글자는 건너뛴다.</summary>
    private static Sound Final(object? word)
    {
        string text = (word as string ?? Convert.ToString(word, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            .TrimEnd(' ', '\'', '"', '’', '”', '」', '』', ')', ']', '}', '»', '.');

        if (text.Length == 0)
        {
            return Sound.None;
        }

        char last = text[^1];

        if (last is >= '가' and <= '힣')
        {
            int jong = (last - '가') % 28;
            return jong == 0 ? Sound.None : jong == 8 ? Sound.Rieul : Sound.Batchim;
        }

        if (char.IsAsciiDigit(last))
        {
            // 0은 영 · 십 · 백 · 천처럼 늘 받침이 있다. 1 일 · 7 칠 · 8 팔은 ㄹ.
            return last switch
            {
                '1' or '7' or '8' => Sound.Rieul,
                '0' or '3' or '6' => Sound.Batchim,
                _ => Sound.None
            };
        }

        if (char.IsAsciiLetter(last))
        {
            return Latin(text);
        }

        return Sound.None;
    }

    /// <summary>영문 끝소리 — 대문자 약어(4자까지)는 글자 이름(엘 · 엠 · 엔 · 알), 그 밖은 끝 몇 글자.</summary>
    private static Sound Latin(string text)
    {
        int start = text.Length;

        while (start > 0 && char.IsAsciiLetter(text[start - 1]))
        {
            start--;
        }

        string tail = text[start..];

        if (tail.Length <= 4 && tail.All(char.IsAsciiLetterUpper))
        {
            return char.ToLowerInvariant(tail[^1]) switch
            {
                'l' or 'r' => Sound.Rieul,
                'm' or 'n' => Sound.Batchim,
                _ => Sound.None
            };
        }

        string lower = tail.ToLowerInvariant();

        if (lower.EndsWith("le", StringComparison.Ordinal))
        {
            return Sound.Rieul;   // title · file · table
        }

        if (lower.EndsWith("ng", StringComparison.Ordinal) || lower.EndsWith("me", StringComparison.Ordinal) ||
            lower.EndsWith("ne", StringComparison.Ordinal) || lower.EndsWith("ut", StringComparison.Ordinal))
        {
            return Sound.Batchim;   // opening · name · line · layout
        }

        return lower[^1] switch
        {
            'l' => Sound.Rieul,
            'm' or 'n' or 'k' or 'p' => Sound.Batchim,
            _ => Sound.None
        };
    }
}
