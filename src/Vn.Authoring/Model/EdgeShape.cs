using Vn.Authoring.Flow;

namespace Vn.Authoring.Model;

/// <summary>선의 꼴 — 꺾인 직각 · 부드러운 곡선 · 곧은 직선.</summary>
public enum EdgeShapeKind
{
    Elbow,
    Curve,
    Straight
}

/// <summary>
/// <b>사람이 정한 선 모양</b> (G3 · 2026-10-29 소유자: *"선이 생기는 모양새가 종종 이상할 때가 있는데, 원한다면 저것도
/// 곡선이나 그런 걸 조절할 수 있도록 해줘. 피그잼처럼"*).
///
/// 꼴(<see cref="Kind"/>)과 <b>손잡이를 자동 자리에서 얼마나 옮겼는가</b>(<see cref="BendX"/> · <see cref="BendY"/>)다.
/// 절대 좌표가 아니라 옮긴 양이라, 카드를 끌어도 사람이 준 굽이가 카드를 따라간다. 손잡이의 자동 자리와
/// 선을 긋는 법은 <see cref="Graph.EdgeShapeRouter"/>가 정한다.
///
/// 아무것도 안 고친 선(<see cref="IsDefault"/>)은 저장하지 않는다 — 기본값을 적어 두면 기본이 바뀔 때 옛 파일이
/// 옛 기본을 굳힌다.
/// </summary>
public sealed record EdgeShape(EdgeShapeKind Kind = EdgeShapeKind.Elbow, double BendX = 0, double BendY = 0)
{
    public static EdgeShape Default { get; } = new();

    public bool IsDefault => Kind == EdgeShapeKind.Elbow && BendX == 0 && BendY == 0;

    /// <summary>
    /// 노드가 진 실행 선(<see cref="StoryNode.EdgeShapes"/>)의 열쇠 — 출구 종류와 그 출구의 열쇠
    /// (<see cref="ExitPort.ExitKey"/>: 갈래는 LineId, 분기는 LineId와 다녀갈 카드, 선택지는 문구). 기본 출구는 노드에 하나라 종류만이다.
    /// </summary>
    public static string KeyOf(ExitPortKind kind, string? exitKey) =>
        exitKey is null
            ? kind.ToString().ToLowerInvariant()
            : $"{kind.ToString().ToLowerInvariant()}:{exitKey}";

    /// <inheritdoc cref="KeyOf(ExitPortKind, string?)"/>
    public static string KeyOf(ExitPort exit)
    {
        ArgumentNullException.ThrowIfNull(exit);
        return KeyOf(exit.Kind, exit.ExitKey);
    }
}
