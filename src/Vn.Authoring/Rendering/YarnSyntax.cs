using System.Text;

namespace Vn.Authoring.Rendering;

/// <summary>
/// Yarn 표기 조립 규칙 — 카드 글 칸의 시나리오 표기(조건 줄)와 노드 이름 규칙(<see cref="YarnNaming"/>) ·
/// 챕터 조건 번역이 지난다. 게임용 Yarn 파일은 이 툴이 내지 않는다(<c>ked-story-compositor</c>의 이미터).
/// (⛔ Yarn 미리보기 서식은 2026-10-29에 Script Preview 탭과 함께 걷혔다.)
/// </summary>
internal static class YarnSyntax
{
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
    /// ⛔ <b>변수가 아니라 함수인 것이 핵심이다.</b> <c>$trust</c>로 심어 주면 읽기와 함께
    /// <c>&lt;&lt;set $trust = 5&gt;&gt;</c>가 <b>문법적으로 유효해진다</b> — 그리고 대사
    /// 중의 스탯 쓰기는 세이브/로드 복귀와 도달성 증명이 <b>못 보는 뒷길</b>이다
    /// (2026-08-14에 그래서 J열을 폐지했다). 함수에는 왼쪽 변이 없으므로 <b>쓸 수가 없다</b> —
    /// 금지가 아니라 불가능이다.
    ///
    /// 2026-09-01(G0)에 걷힌 <c>PublishStats</c> 다리는 <b>양방향</b>이었다. 되살리는 것은
    /// 그 절반이다.
    ///
    /// ⚠ <b>런타임과의 접점이다.</b> 저쪽이 같은 이름의 Yarn 함수를 등록해야 하고, 없으면
    /// 번들이 <b>컴파일에서 멈춘다</b> — 조용히 틀리는 것보다 낫지만, 저쪽 반영 전에
    /// 내보내면 빌드가 선다. 이름을 고치는 것은 양쪽 합의 사항이다.
    ///
    /// 깃발(Bool 스탯)도 같은 표기다 — 0/1 정수로 살기 때문에(2026-08-19) 비교가 그대로 선다.
    /// </summary>
    public const string StatFunction = "stat";

    /// <summary>
    /// <inheritdoc cref="StatFunction"/>
    ///
    /// ⛔ <b>키를 정규화하지 않는다</b> (2026-09-17, 런타임 회신 §6.1이 잡았다).
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
    /// ⚠ 문자열을 깨뜨릴 수 있는 <c>"</c>와 <c>\</c>만 이스케이프한다.
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
