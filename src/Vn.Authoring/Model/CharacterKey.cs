using Vn.Authoring.Text;

namespace Vn.Authoring.Model;

/// <summary>
/// <b>임시 캐릭터 키</b> (2026-11-19 소유자 · usability UX29 — 「캐릭터 추가할 때 캐릭터 키도 자동으로 · 한글 이름을 로마자로 하되 앞에
/// temp_를 붙여 임시라는 걸로」). 화자를 더하면 키가 빈 채가 아니라 <c>temp_</c> + 이름의 로마자로 선다(하린 → <c>temp_harin</c> ·
/// <see cref="HangulRomanizer"/>). 개발자가 [구성] 탭에서 그림과 맞는 키로 바꾼다 — 그때까지는 검증 보고가 「임시 키」로 짚는다
/// (<see cref="Chapters.ChapterSpeakerKeys"/>).
///
/// <b>이름을 고쳐도 따라가지 않는다</b>(소유자 — 「직관을 깨뜨릴 수 있다」). 키는 더할 때 한 번 짓는다.
/// </summary>
public static class CharacterKey
{
    /// <summary>임시 키의 앞머리.</summary>
    public const string TempPrefix = "temp_";

    /// <summary>로마자로 바꿀 것이 없는 이름(문장부호 · 한자 · 가나뿐)의 몸 — 번호를 붙인다(<c>temp_char01</c>).</summary>
    private const string Fallback = "char";

    /// <summary>그 키가 임시 키인가 — 앞머리가 <see cref="TempPrefix"/>다.</summary>
    public static bool IsTemp(string? key) => key is not null && key.StartsWith(TempPrefix, StringComparison.Ordinal);

    /// <summary>
    /// 새 화자의 임시 키 — <c>temp_</c> + 이름의 로마자. 이미 쓰인 키(<paramref name="taken"/>)와 겹치면 뒤에 2부터 수를 붙인다
    /// (<c>temp_harin2</c>). 로마자로 바꿀 것이 없으면 <c>temp_char01</c>부터 빈 번호다.
    /// </summary>
    public static string NewTemp(string name, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var used = new HashSet<string>(taken, StringComparer.Ordinal);
        string body = HangulRomanizer.Romanize(name);

        if (body.Length == 0)
        {
            for (int number = 1; ; number++)
            {
                string candidate = $"{TempPrefix}{Fallback}{number:00}";

                if (!used.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        string key = TempPrefix + body;

        for (int number = 2; used.Contains(key); number++)
        {
            key = $"{TempPrefix}{body}{number}";
        }

        return key;
    }
}
