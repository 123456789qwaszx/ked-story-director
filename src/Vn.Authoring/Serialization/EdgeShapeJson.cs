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

    /// <summary>모르는 꼴은 직각으로 읽는다 — 모양 하나 때문에 프로젝트를 못 여는 일은 없다.</summary>
    internal static EdgeShape? Read(JsonNode? node)
    {
        if (node is not JsonObject json)
        {
            return null;
        }

        var shape = new EdgeShape(
            Enum.TryParse((string?)json["kind"], ignoreCase: true, out EdgeShapeKind kind) && Enum.IsDefined(kind)
                ? kind
                : EdgeShapeKind.Elbow,
            (double?)json["bendX"] ?? 0,
            (double?)json["bendY"] ?? 0);

        return shape.IsDefault ? null : shape;
    }
}
