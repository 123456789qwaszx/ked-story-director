using System.Text.Json.Nodes;

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

    /// <summary>문자열이든 배열이든 다녀올 곳들을 차례대로. 빈 값과 같은 곳의 두 번째는 버린다.</summary>
    public static List<string> Read(JsonNode? node)
    {
        IEnumerable<string?> values = node switch
        {
            JsonArray array => array.Select(item => item is JsonValue value && value.TryGetValue(out string? text)
                ? text
                : null),
            JsonValue value when value.TryGetValue(out string? text) => [text],
            _ => []
        };

        return values
            .OfType<string>()
            .Where(target => target.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
