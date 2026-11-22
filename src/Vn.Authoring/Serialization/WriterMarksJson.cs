using System.Text.Json.Nodes;
using Vn.Authoring.Model;

namespace Vn.Authoring.Serialization;

/// <summary>
/// <b>작가용 글 서식의 저장 · 읽기</b> — 대사 노드 JSON의 <c>writerMarks</c> 칸(writer-formatting 3.1).
/// <code>
/// "writerMarks": { "line_ab12": [ { "start": 3, "length": 4, "bold": true, "color": "#D32F2F", "fill": "#FFF59D", "font": "맑은 고딕", "grow": 2 } ] }
/// </code>
/// 줄은 LineId 차례, 구간은 자리 차례로 쓴다(결정적 출력). 모양의 칸은 켜진 것만 적는다.
/// <b>읽기는 너그럽다</b> — 모르는 색 · 숫자가 아닌 자리 · 글 밖의 구간 · 겹친 구간은 버리거나 잘라 정규형으로 만들고 계속한다. 글과 논리는 깨진 파일을
/// 거절하지만 서식은 아니다.
/// </summary>
internal static class WriterMarksJson
{
    public static JsonObject? Write(DialogueNode node)
    {
        var lines = new JsonObject();

        foreach ((string lineId, IReadOnlyList<WriterSpan> spans) in node.WriterMarks.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (spans.Count == 0)
            {
                continue;
            }

            var array = new JsonArray();

            foreach (WriterSpan span in spans.OrderBy(span => span.Start))
            {
                var item = new JsonObject { ["start"] = span.Start, ["length"] = span.Length };
                WriterLook look = span.Look;

                if (look.Bold)
                {
                    item["bold"] = true;
                }

                if (look.Italic)
                {
                    item["italic"] = true;
                }

                if (look.Strike)
                {
                    item["strike"] = true;
                }

                if (look.Color is { } color)
                {
                    item["color"] = color.Hex;
                }

                if (look.Fill is { } fill)
                {
                    item["fill"] = fill.Hex;
                }

                if (look.Font is { Length: > 0 } font)
                {
                    item["font"] = font;
                }

                if (look.Grow != 0)
                {
                    item["grow"] = look.Grow;
                }

                array.Add(item);
            }

            lines[lineId] = array;
        }

        return lines.Count > 0 ? lines : null;
    }

    /// <summary>읽어서 그 노드에 담는다 — 글 길이(기본 언어)로 잘라 정규형으로. 읽지 못한 것은 버린다.</summary>
    public static void Read(DialogueNode node, JsonNode? json)
    {
        if (json is not JsonObject lines)
        {
            return;
        }

        foreach ((string lineId, JsonNode? value) in lines)
        {
            if (string.IsNullOrWhiteSpace(lineId) || value is not JsonArray array)
            {
                continue;
            }

            var spans = new List<WriterSpan>();

            foreach (JsonNode? item in array)
            {
                if (item is JsonObject span &&
                    Int(span["start"]) is { } start &&
                    Int(span["length"]) is { } count &&
                    count > 0)
                {
                    spans.Add(new WriterSpan(start, count, new WriterLook(
                        Bold: Bool(span["bold"]),
                        Italic: Bool(span["italic"]),
                        Strike: Bool(span["strike"]),
                        Color: Color(span["color"], fill: false),
                        Fill: Color(span["fill"], fill: true),
                        Font: Text(span["font"]),
                        Grow: Math.Clamp(Int(span["grow"]) ?? 0, WriterMarkMath.MinGrow, WriterMarkMath.MaxGrow))));
                }
            }

            List<WriterSpan> normal = WriterMarkMath.Normalize(spans, node.Script.Text(lineId).Text.Length);

            if (normal.Count > 0)
            {
                node.WriterMarks[lineId] = normal;
            }
        }
    }

    public static WriterAlign ReadAlign(JsonNode? json) => Text(json) switch
    {
        "center" => WriterAlign.Center,
        "right" => WriterAlign.Right,
        _ => WriterAlign.Left
    };

    public static string AlignName(WriterAlign align) => align switch
    {
        WriterAlign.Center => "center",
        WriterAlign.Right => "right",
        _ => "left"
    };


    /// <summary>카드의 처음 글꼴 · 크기 — <c>{ "font": "맑은 고딕", "size": 11 }</c>(기본 글꼴이면 <c>font</c>는 빈 글).</summary>
    public static JsonObject WriteBase(WriterBase value) => new()
    {
        ["font"] = value.Font,
        ["size"] = value.Size
    };

    /// <summary>너그럽게 읽는다 — 크기가 숫자가 아니거나 0 이하면 안 적힌 카드로 둔다(열 때 그 PC의 설정으로 다시 적힌다).</summary>
    public static WriterBase? ReadBase(JsonNode? json)
    {
        if (json is not JsonObject value ||
            value["size"] is not JsonValue size ||
            !size.TryGetValue(out double points) ||
            !double.IsFinite(points) ||
            points <= 0)
        {
            return null;
        }

        string font = value["font"] is JsonValue name && name.TryGetValue(out string? text) ? text.Trim() : string.Empty;
        return new WriterBase(font, points);
    }

    /// <summary><c>"#RRGGBB"</c>. 옛 파일의 색 이름(<c>"red"</c> — WF-D14 전)은 정해 둔 열 가지의 그 색(글자색 · 바탕색)으로 읽는다. 모르는 것은 버린다.</summary>
    private static WriterRgb? Color(JsonNode? json, bool fill)
    {
        if (Text(json) is not { } text)
        {
            return null;
        }

        return WriterRgb.Parse(text) ??
               WriterSwatch.All.FirstOrDefault(swatch => string.Equals(swatch.Key, text, StringComparison.OrdinalIgnoreCase))?.Of(fill);
    }

    private static string? Text(JsonNode? json) =>
        json is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    private static bool Bool(JsonNode? json) => json is JsonValue value && value.TryGetValue(out bool flag) && flag;

    private static int? Int(JsonNode? json)
    {
        if (json is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue(out int number))
        {
            return number;
        }

        return value.TryGetValue(out double real) && double.IsFinite(real) && real >= int.MinValue && real <= int.MaxValue
            ? (int)Math.Round(real)
            : null;
    }
}
