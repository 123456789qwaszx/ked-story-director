using System.Text.RegularExpressions;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>대사 글이 읽는 스탯</b> — 작가가 대사에 붙인 <c>{stat("키")}</c>(<see cref="ChapterStatScript.InlineRead"/>). 2026-11-23 소유자 — 「키를 바꿀
/// 때 대사 글의 같은 스탯도 바꾸는게 맞고」 · <c>docs/work-orders/stat-read-rename-orders.md</c> S1–S3. 키 바꾸기(<c>RenameChapterStat</c>)가 이것으로
/// 대사 글을 끌고 가고, 지우기의 「쓰는 곳」이 이것으로 대사 줄을 찾고, 검증 보고가 이것으로 없는 키를 읽는 대사를 짚는다.
///
/// <b>중괄호 안만 본다</b> — Yarn이 식으로 푸는 자리가 <c>{…}</c>다. 그 밖의 <c>stat("…")</c>는 화면에 그대로 나오는 글이라 읽기가 아니다.
/// 키는 문자열 리터럴이라 <c>\"</c> · <c>\\</c>를 풀어서 견준다(<see cref="YarnSyntax.StatRead"/>가 그렇게 적는다). 이름이 닮은 다른 함수
/// (<c>mystat("…")</c>)는 읽기가 아니다.
///
/// <b>그 챕터의 대본만</b>(<see cref="ChapterScripts.PlacesOf"/> — 에피소드 카드 · 분기 카드) — 스탯은 챕터의 것이라 다른 챕터에 같은 키가 있어도
/// 그 챕터의 스탯이다.
/// </summary>
public static partial class ChapterStatReads
{
    /// <summary>대사 한 줄이 그 스탯을 읽는 자리 — 카드 · 사람이 읽는 곳 이름 · 줄 · 카드 안에서 몇째 줄(1부터).</summary>
    public sealed record Line(DialogueNode Node, string Place, ScriptLine ScriptLine, int Number);

    /// <summary>없는 스탯을 읽는 대사 — 키 · 그 키를 읽는 줄 수 · 처음 나오는 곳.</summary>
    public sealed record Missing(string Key, int Lines, string FirstPlace);

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Inline();

    [GeneratedRegex(@"(?<![\w.])" + YarnSyntax.StatFunction + @"\s*\(\s*""((?:[^""\\]|\\.)*)""\s*\)")]
    private static partial Regex Read();

    [GeneratedRegex(@"\\(.)")]
    private static partial Regex Escaped();

    /// <summary>글이 읽는 스탯 키들 — 나오는 차례로, 겹치면 한 번.</summary>
    public static IReadOnlyList<string> KeysIn(string? text)
    {
        var keys = new List<string>();

        if (string.IsNullOrEmpty(text))
        {
            return keys;
        }

        foreach (Match inline in Inline().Matches(text))
        {
            foreach (Match read in Read().Matches(inline.Value))
            {
                string key = Unescape(read.Groups[1].Value);

                if (!keys.Contains(key, StringComparer.Ordinal))
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    /// <summary>글이 그 스탯을 읽는가.</summary>
    public static bool Reads(string? text, string key) => KeysIn(text).Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// 글의 <c>{…}</c> 안에서 <paramref name="from"/>을 읽는 <c>stat("…")</c>를 <paramref name="to"/>로 바꾼다 — 나머지 글자는 한 글자도 안 건드린다.
    /// 바꿀 것이 없으면 같은 글을 돌려준다.
    /// </summary>
    public static string Replace(string? text, string from, string to)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return Inline().Replace(text, inline => Read().Replace(inline.Value, read =>
            string.Equals(Unescape(read.Groups[1].Value), from, StringComparison.Ordinal)
                ? YarnSyntax.StatRead(to)
                : read.Value));
    }

    /// <summary>
    /// 그 챕터 대본에서 그 스탯을 읽는 <b>살아 있는</b> 줄 — 언어마다 본다(키는 언어와 상관없다 · 번역 칸에만 든 읽기도 짚는다).
    /// 에피소드 차례, 그다음 분기 카드 · 카드 안에서는 줄 차례.
    /// </summary>
    public static IReadOnlyList<Line> LinesReading(ChapterGraphModel chapter, StoryProject project, string key)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);

        var lines = new List<Line>();

        foreach ((DialogueNode node, string place) in ChapterScripts.PlacesOf(chapter, project))
        {
            int number = 0;

            foreach (ScriptLine line in node.Script.ActiveLines)
            {
                number++;

                if (node.Script.Locales.Any(locale => Reads(locale.Find(line.Id).Text, key)))
                {
                    lines.Add(new Line(node, place, line, number));
                }
            }
        }

        return lines;
    }

    /// <summary>
    /// 그 챕터의 스탯 표에 없는 키를 읽는 대사 — 키마다 하나, 처음 나오는 차례. 검증 캐시의 지문도 이것을 싣는다 — 대사 글은 챕터 모델
    /// 밖이라 싣지 않으면 대사를 고쳐도 옛 경고가 남는다.
    /// </summary>
    public static IReadOnlyList<Missing> MissingOf(ChapterGraphModel chapter, StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);

        var known = new HashSet<string>(chapter.Stats.Select(stat => stat.Key), StringComparer.Ordinal);
        var found = new Dictionary<string, (int Lines, string FirstPlace)>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach ((DialogueNode node, string place) in ChapterScripts.PlacesOf(chapter, project))
        {
            foreach (ScriptLine line in node.Script.ActiveLines)
            {
                IEnumerable<string> keys = node.Script.Locales
                    .SelectMany(locale => KeysIn(locale.Find(line.Id).Text))
                    .Distinct(StringComparer.Ordinal)
                    .Where(key => !known.Contains(key));

                foreach (string key in keys)
                {
                    if (found.TryGetValue(key, out (int Lines, string FirstPlace) seen))
                    {
                        found[key] = (seen.Lines + 1, seen.FirstPlace);
                    }
                    else
                    {
                        found[key] = (1, place);
                        order.Add(key);
                    }
                }
            }
        }

        return order.Select(key => new Missing(key, found[key].Lines, found[key].FirstPlace)).ToList();
    }

    /// <summary>
    /// 없는 스탯을 읽는 대사마다 <b>경고</b> 한 줄(2026-11-25 소유자 — 「🟡 경고로 짚기」). 진행 JSON을 막지 않는다 — 읽기는 Yarn 함수라 컴파일은
    /// 되고, 게임에서 그 줄이 나올 때 없는 스탯을 읽는다. 옛 프로젝트 · 손으로 고친 글 · 지운 스탯(「모두 비우고 지우기」는 대사의 읽기를 남긴다)이
    /// 여기 걸린다.
    /// </summary>
    public static void Verify(ChapterGraphModel chapter, StoryProject project, List<ChapterDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        foreach (Missing missing in MissingOf(chapter, project))
        {
            diagnostics.Add(new ChapterDiagnostic(
                ChapterDiagnosticSeverity.Warning,
                ChapterDiagnosticCode.StatReadUnknown,
                chapter.SourcePath,
                ChapterSheetNames.Stats,
                null,
                string.Format(Msg.Diag_InDialogueStatIsnT, YarnSyntax.StatRead(missing.Key), missing.Key, missing.Lines, missing.FirstPlace)));
        }
    }

    private static string Unescape(string literal) => Escaped().Replace(literal, "$1");
}
