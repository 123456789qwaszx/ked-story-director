using System.Text.Json.Nodes;
using Vn.Authoring.Model;

namespace Vn.Authoring.Serialization;

/// <summary>
/// <see cref="EdgeShape"/> ↔ <c>{"kind":"curve","bendX":12,"bendY":-30}</c> (G3 · 2026-10-29).
/// 기본값은 안 쓴다 — 고친 것이 없는 선은 칸째 없다.
/// </summary>
internal static class EdgeShapeJson
{
    internal static JsonObject? Write(EdgeShape? shape)
    {
        if (shape is null || shape.IsDefault)
        {
            return null;
        }

        var json = new JsonObject();

        if (shape.Kind != EdgeShapeKind.Elbow)
        {
            json["kind"] = shape.Kind.ToString().ToLowerInvariant();
        }

        if (shape.BendX != 0)
        {
            json["bendX"] = Math.Round(shape.BendX, 2);
        }

        if (shape.BendY != 0)
        {
            json["bendY"] = Math.Round(shape.BendY, 2);
        }

        return json;
    }

    /// <summary>
    /// 없으면 null(고친 것이 없는 선). 쓰는 꼴(<c>curve</c> · <c>straight</c>) 밖의 이름이나 수가 아닌 굽이는 받지 않는다(2026-11-18 · old-shapes
    /// O8 — ⛔ 그 전에는 모르는 꼴을 직각으로 읽었다).
    /// </summary>
    internal static EdgeShape? Read(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        JsonObject json = JsonSupport.RequiredObject(node, "선 모양");
        var shape = new EdgeShape(
            json["kind"] is null ? EdgeShapeKind.Elbow : KindOf(JsonSupport.RequiredText(json["kind"], "선 모양의 kind")),
            JsonSupport.OptionalNumber(json["bendX"], "선 모양의 bendX") ?? 0,
            JsonSupport.OptionalNumber(json["bendY"], "선 모양의 bendY") ?? 0);

        return shape.IsDefault ? null : shape;
    }

    /// <summary>쓰는 이름 — 직각은 기본이라 쓰지 않는다.</summary>
    private static EdgeShapeKind KindOf(string name) => name switch
    {
        "curve" => EdgeShapeKind.Curve,
        "straight" => EdgeShapeKind.Straight,
        _ => throw new InvalidDataException($"선 모양의 kind '{name}'를 모릅니다.")
    };
}
