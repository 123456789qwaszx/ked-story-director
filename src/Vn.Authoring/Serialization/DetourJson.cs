using System.Text.Json.Nodes;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Serialization;

/// <summary>
/// 「조건 분기」 표식의 <c>detour</c> 칸 — 원고의 줄 항목과 발행 결과의 줄이 같은 모양을 쓴다.
///
/// 하나면 <b>문자열</b>, 여럿이면 <b>배열</b>이다 (2026-10-29 · 한 줄에 분기 여럿). 하나일 때 그전과 같은 바이트를
/// 쓰므로 분기가 하나뿐인 원고 · 결과는 다시 써도 그대로이고, 결과의 내용 해시도 바뀌지 않는다(새 버전이 생기지 않는다).
/// </summary>
internal static class DetourJson
{
    /// <summary>쓸 값. 다녀올 곳이 없으면 null — 칸을 쓰지 않는다.</summary>
    public static JsonNode? Write(IReadOnlyList<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        return targets.Count switch
        {
            0 => null,
            1 => JsonValue.Create(targets[0]),
            _ => new JsonArray(targets.Select(target => (JsonNode?)JsonValue.Create(target)).ToArray())
        };
    }

    /// <summary>
    /// 문자열(하나)이든 배열(둘 이상)이든 다녀올 곳들을 차례대로. 쓰는 모양 밖 — 빈 값 · 문자열 아닌 항목 · 같은 곳 둘 · 하나짜리 배열 —
    /// 은 받지 않는다(2026-11-18 · old-shapes O8 — ⛔ 그 전에는 버리고 읽었다).
    /// </summary>
    public static List<string> Read(JsonNode? node)
    {
        List<string> targets = node switch
        {
            null => [],
            JsonArray { Count: > 1 } array => array.Select(item => JsonSupport.RequiredText(item, Msg.File_TargetOfBranchDetour)).ToList(),
            JsonValue => [JsonSupport.RequiredText(node, Msg.File_TargetOfBranchDetour)],
            _ => throw new InvalidDataException(Msg.File_BranchDetourIsEitherOne)
        };

        return targets.Distinct(StringComparer.Ordinal).Count() == targets.Count
            ? targets
            : throw new InvalidDataException(Msg.File_BranchDetourHasSameTarget);
    }
}
