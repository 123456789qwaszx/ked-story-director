using System.Text;

namespace Vn.Authoring.Rendering;

/// <summary>
/// Yarn 표기 조립 규칙 — 카드 글 칸의 시나리오 표기(조건 줄)와 노드 이름 규칙(<see cref="YarnNaming"/>) ·
/// 챕터 조건 번역 · <b>대사만 있는 Yarn</b>(<see cref="Chapters.ChapterYarnExporter"/> — 2026-11-17)이 지난다.
/// 연출 든 게임용 Yarn은 이 툴이 내지 않는다.
/// (Yarn 미리보기 서식은 2026-10-29에 Script Preview 탭과 함께 걷혔다.)
/// </summary>
internal static class YarnSyntax
{
    public const string Indent = "    ";

    public static string IndentOf(int level) =>
        string.Concat(Enumerable.Repeat(Indent, Math.Max(0, level)));

    public static void AppendJump(StringBuilder builder, string target)
    {
        builder.Append("<<jump ").Append(target).Append(">>");
    }

    /// <summary>
    /// <c>&lt;&lt;detour 노드&gt;&gt;</c> — 대상 노드를 재생하고 이 자리로 돌아온다.
    /// 조건 갈래의 출구가 쓴다 (YarnSpinner 3.x).
    /// </summary>
    public static void AppendDetour(StringBuilder builder, string target)
    {
        builder.Append("<<detour ").Append(target).Append(">>");
    }

    /// <summary>
    /// <b>대사 글을 Yarn 줄에 그대로 실을 수 있게 막는다</b> (2026-11-17 · 컴파일러 3.2.1로 하나씩 돌려 봤다).
    ///
    /// 작가는 Yarn 문법을 모르고 글을 쓴다. 그대로 실으면: <c>#</c>는 태그로 읽혀 그 뒤가 문법 오류가 되고,
    /// <c>//</c> 뒤는 주석으로 <b>조용히 잘리고</b>(오류도 없다 — <c>#line:</c>까지 함께 사라진다), <c>&lt;&lt;</c>는 명령으로,
    /// <c>\</c>는 이스케이프의 머리로 읽힌다. 넷 다 <c>\</c>를 앞에 세우면 컴파일러가 원래 글자로 되돌려 문자열 표에 싣는다.
    ///
    /// <b><c>{ }</c>와 <c>[ ]</c>는 건드리지 않는다</b> — <c>{stat("키")}</c>는 작가가 일부러 적는 스탯 읽기이고
    /// (<see cref="StatRead"/>), 대괄호는 마크업이다. 홑 <c>&lt;</c> · <c>&gt;</c> · <c>/</c>도 그대로 통과한다.
    /// </summary>
    public static string EscapeLineText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length + 8);

        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';

            if (c == '\\' || c == '#')
            {
                builder.Append('\\').Append(c);
            }
            else if ((c == '<' || c == '>' || c == '/') && next == c)
            {
                builder.Append('\\').Append(c).Append('\\').Append(c);
                index++;
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    public static void AppendCondition(StringBuilder builder, string keyword, string? expression)
    {
        builder.Append("<<")
            .Append(keyword)
            .Append(' ')
            .Append(string.IsNullOrWhiteSpace(expression) ? "false" : expression)
            .Append(">>");
    }

    /// <summary>
    /// <c>stat("키")</c> — <b>진행 스탯을 대사 안에서 읽는 유일한 표기</b>
    /// (2026-09-17 소유자 · 계약서 §D1).
    ///
    /// <b>변수가 아니라 함수인 것이 핵심이다.</b> <c>$trust</c>로 심어 주면 읽기와 함께
    /// <c>&lt;&lt;set $trust = 5&gt;&gt;</c>가 <b>문법적으로 유효해진다</b> — 그리고 대사
    /// 중의 스탯 쓰기는 세이브/로드 복귀와 도달성 증명이 <b>못 보는 뒷길</b>이다
    /// (2026-08-14에 그래서 J열을 폐지했다). 함수에는 왼쪽 변이 없으므로 <b>쓸 수가 없다</b> —
    /// 금지가 아니라 불가능이다.
    ///
    /// 2026-09-01(G0)에 걷힌 <c>PublishStats</c> 다리는 <b>양방향</b>이었다. 되살리는 것은
    /// 그 절반이다.
    ///
    /// <b>런타임과의 접점이다.</b> 저쪽이 같은 이름의 Yarn 함수를 등록해야 하고, 없으면
    /// 번들이 <b>컴파일에서 멈춘다</b> — 조용히 틀리는 것보다 낫지만, 저쪽 반영 전에
    /// 내보내면 빌드가 선다. 이름을 고치는 것은 양쪽 합의 사항이다.
    ///
    /// 깃발(Bool 스탯)도 같은 표기다 — 0/1 정수로 살기 때문에(2026-08-19) 비교가 그대로 선다.
    /// </summary>
    public const string StatFunction = "stat";

    /// <summary>
    /// <inheritdoc cref="StatFunction"/>
    ///
    /// <b>키를 정규화하지 않는다</b> (2026-09-17, 런타임 회신 §6.1이 잡았다).
    /// 한때 <c>SanitizeVariableName</c>(2026-10-29 L5에 걷혔다)을 지났는데 <b>틀렸다</b>:
    ///
    /// <code>
    /// 스탯 이름   호감도 (윌로우)
    /// JSON Key    "호감도 (윌로우)"        ← 진행 JSON은 원본 그대로 낸다
    /// 옛 대사     stat("호감도__윌로우_")  ← 정규화돼 갈렸다
    /// 런타임      KeyNotFoundException     ← 그 대사가 재생될 때 처음 터진다
    /// </code>
    ///
    /// <b>산출도 컴파일도 통과한다</b> — 그 대사가 나올 때까지 아무도 모른다.
    ///
    /// 정규화하던 이유는 <c>$변수</c> 시절의 것이다(*"Yarn 식별자에는 공백이 못 들어간다"*).
    /// 함수의 인자는 식별자가 아니라 <b>문자열 리터럴</b>이라 공백도 괄호도 그냥 들어간다 —
    /// 규칙을 더하는 게 아니라 <b>빼는</b> 것이 맞다. 이름이 한 개가 되고 유니코드 정규화
    /// 위험도 함께 사라진다(같은 실행에서 같은 원본이 두 자리로 나가므로).
    ///
    /// 문자열을 깨뜨릴 수 있는 <c>"</c>와 <c>\</c>만 이스케이프한다.
    /// </summary>
    public static string StatRead(string? key) =>
        $"{StatFunction}(\"{EscapeStringLiteral(key)}\")";

    /// <summary>Yarn 문자열 리터럴 안에서 깨지는 두 글자만 막는다 — <c>\</c>가 먼저다.</summary>
    private static string EscapeStringLiteral(string? value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary>
    /// 노드 타이틀에 쓸 수 있는 이름으로 다듬는다. 문자·숫자·밑줄만 남긴다.
    /// 타이틀은 세이브 키다(계약서 C2) — 같은 입력은 언제나 같은 이름이 되어야 한다.
    /// </summary>
    public static string SanitizeNodeName(string? name)
    {
        string source = string.IsNullOrWhiteSpace(name) ? "unnamed" : name.Trim();
        var builder = new StringBuilder(source.Length);

        foreach (char c in source)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return builder.ToString();
    }
}
