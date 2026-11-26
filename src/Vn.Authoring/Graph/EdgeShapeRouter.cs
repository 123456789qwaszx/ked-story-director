using Vn.Authoring.Model;

namespace Vn.Authoring.Graph;

/// <summary>
/// 모양대로 그은 선 하나.
///
/// 직각 · 직선이면 <see cref="Points"/>는 꺾은선의 점들이다. 곡선이면 첫 점 다음에 3차 베지어 마디가
/// <c>(제어점1, 제어점2, 끝점)</c> 셋씩 이어진다.
/// </summary>
/// <param name="Handle">사람이 끄는 손잡이 자리 — 직선에는 없다(끌어 바꿀 굽이가 없다).</param>
/// <param name="FreeY">손잡이가 위아래로도 움직이는가. 실행 선의 직각은 가운데 세로 마디만 옆으로 민다.</param>
public sealed record ShapedEdge(
    EdgeShapeKind Kind,
    IReadOnlyList<GraphPosition> Points,
    GraphPosition? Handle,
    bool FreeY);

/// <summary>
/// <b>사람이 고친 모양대로 선을 긋는다</b> (G3 · 2026-10-29 소유자: *"곡선이나 그런 걸 조절할 수 있도록 해줘. 피그잼처럼"*).
///
/// 손잡이는 선 위에 있고, <see cref="EdgeShape.BendX"/> · <see cref="EdgeShape.BendY"/>는 그 손잡이를 자동 자리에서
/// 옮긴 양이다 — 끄는 동안 손잡이가 포인터를 그대로 따라간다.
/// <list type="bullet">
/// <item><b>직각</b> — 자동 경로의 꺾이는 마디를 민다. 선택지 레인은 내려가는 세로 줄(옆)과 도착 위를 건너는 가로 줄(위아래)을,
///   실행 선은 가운데 세로 마디(옆)만.</item>
/// <item><b>곡선</b> — 나가는 쪽으로 나가 드는 쪽으로 드는 자동 곡선을 가운데(t=½)에서 둘로 쪼개고, 가운데 점과 그 양옆
///   제어점을 함께 옮긴다. 가운데의 접선이 그대로라 굽이가 꺾이지 않는다.</item>
/// <item><b>직선</b> — 두 끝을 곧게 잇는다.</item>
/// </list>
/// </summary>
public static class EdgeShapeRouter
{
    /// <summary>곡선이 포트에서 곧게 뻗어 나가는 최소 길이.</summary>
    private const double CurveReach = 48;

    /// <summary>
    /// 실행 선(IF 갈래 · 분기) — 카드 옆 포트에서 나가 도착 카드의 옆으로 든다.
    /// <paramref name="leftward"/>면 왼쪽 포트에서 나가 도착 카드의 오른쪽으로 든다(G2).
    /// </summary>
    public static ShapedEdge Execution(GraphPosition from, GraphPosition to, bool leftward, EdgeShape? shape)
    {
        shape ??= EdgeShape.Default;
        double toward = leftward ? -1 : 1;

        switch (shape.Kind)
        {
            case EdgeShapeKind.Straight:
                return new ShapedEdge(EdgeShapeKind.Straight, [from, to], null, FreeY: false);

            case EdgeShapeKind.Curve:
                double reach = Math.Max(CurveReach, Math.Abs(to.X - from.X) / 2);
                return Curve(
                    from,
                    new GraphPosition(from.X + (toward * reach), from.Y),
                    new GraphPosition(to.X - (toward * reach), to.Y),
                    to,
                    shape);

            default:
                IReadOnlyList<GraphPosition> auto = OrthogonalEdgeRouter.Route(from, to, leftward: leftward);
                double x = auto[1].X + shape.BendX;

                return new ShapedEdge(
                    EdgeShapeKind.Elbow,
                    [from, new GraphPosition(x, from.Y), new GraphPosition(x, to.Y), to],
                    new GraphPosition(x, (from.Y + to.Y) / 2),
                    FreeY: false);
        }
    }

    /// <summary>
    /// 선택지 레인 — 칩 끝(<paramref name="from"/>)에서 <paramref name="toward"/>(오른쪽 1 · 왼쪽 −1)로 나가
    /// 레인(<paramref name="laneX"/>)을 타고 내려가 도착 접점(<paramref name="junction"/>)에 위에서 든다.
    /// </summary>
    public static ShapedEdge Lane(
        GraphPosition from, double toward, double laneX, GraphPosition junction, EdgeShape? shape)
    {
        shape ??= EdgeShape.Default;

        switch (shape.Kind)
        {
            case EdgeShapeKind.Straight:
                return new ShapedEdge(EdgeShapeKind.Straight, [from, junction], null, FreeY: false);

            case EdgeShapeKind.Curve:
                return Curve(
                    from,
                    new GraphPosition(from.X + (toward * Math.Max(CurveReach, Math.Abs(laneX - from.X))), from.Y),
                    new GraphPosition(junction.X, junction.Y - Math.Max(CurveReach, Math.Abs(junction.Y - from.Y) / 2)),
                    junction,
                    shape);

            default:
                // 꺾이는 점 = 레인이 도착 위를 건너기 시작하는 모퉁이. 옆으로 끌면 레인이, 위아래로 끌면 건너는 줄이 움직인다.
                var bend = new GraphPosition(laneX + shape.BendX, junction.Y + shape.BendY);

                return new ShapedEdge(
                    EdgeShapeKind.Elbow,
                    [from, new GraphPosition(bend.X, from.Y), bend, new GraphPosition(junction.X, bend.Y), junction],
                    bend,
                    FreeY: true);
        }
    }

    /// <summary>
    /// 선택지의 길(2026-11-26 소유자 — 선택지 칸이 카드 아래에 가로로 나란히 서고, 선은 칸에서 <b>아래로</b> 나간다) — 칸의 점 바로 밑
    /// (<paramref name="from"/>)에서 아래로 나가 도착 접점(<paramref name="junction"/>)에 위에서 든다.
    ///
    /// 직각(자동): 조금 내려와(<see cref="DropStep"/> — 이웃 칸의 문구 아래) 세로 길을 타고 도착 접점의 높이에서 가로로 건넌다. 세로 길은 보통 칸 바로 밑
    /// (<c>from.X</c>)이고, 그 길을 다른 카드가 막으면 부르는 쪽이 비켜 갈 자리(<paramref name="detourX"/>)를 준다 — 그때는 내려온 자리에서 그 길까지 가로로 간다.
    /// <paramref name="rise"/>는 건너는 줄을 접점보다 그만큼 위에 둔다 — 한 카드의 여러 길이 같은 높이에서 겹쳐 한 줄로 보이지 않게 부르는 쪽이 길마다 달리 준다.
    /// 꺾이는 점(손잡이) = 세로 길이 건너기 시작하는 모퉁이. 옆으로 끌면 세로 길이, 위아래로 끌면 건너는 줄이 움직인다.
    /// 곡선: 아래로 나가 위에서 든다. 직선: 두 끝만 잇는다. 둘은 비켜 가지 않는다(사람이 고른 모양이다).
    /// </summary>
    public static ShapedEdge Drop(
        GraphPosition from, GraphPosition junction, EdgeShape? shape, double rise = 0, double? detourX = null)
    {
        shape ??= EdgeShape.Default;

        switch (shape.Kind)
        {
            case EdgeShapeKind.Straight:
                return new ShapedEdge(EdgeShapeKind.Straight, [from, junction], null, FreeY: false);

            case EdgeShapeKind.Curve:
                double reach = Math.Max(CurveReach, Math.Abs(junction.Y - from.Y) / 2);
                return Curve(
                    from,
                    new GraphPosition(from.X, from.Y + reach),
                    new GraphPosition(junction.X, junction.Y - reach),
                    junction,
                    shape);

            default:
                double stepY = from.Y + DropStep;
                var bend = new GraphPosition((detourX ?? from.X) + shape.BendX, junction.Y - rise + shape.BendY);

                return new ShapedEdge(
                    EdgeShapeKind.Elbow,
                    [
                        from,
                        new GraphPosition(from.X, stepY),
                        new GraphPosition(bend.X, stepY),
                        bend,
                        new GraphPosition(junction.X, bend.Y),
                        junction
                    ],
                    bend,
                    FreeY: true);
        }
    }

    /// <summary>
    /// 선택지의 길이 <b>접은 장면 상자의 줄 입구</b>(<paramref name="entry"/> — 줄의 왼쪽 점)로 든다(2026-11-27 소유자 — 「다른 장면에서 Root로 연결되는 것
    /// 역시 … 입구로 연결되는 게 맞는 것 같은데, 왼쪽에 있을 경우에는 출구로 연결되어버리는」). 칸의 점 바로 밑(<paramref name="from"/>)에서 아래로 나가
    /// 건너는 줄(<paramref name="crossY"/>)에서 상자 왼쪽 바깥의 세로 길(<paramref name="laneX"/>)까지 가로로 건너고, 줄 높이까지 가서 입구로 옆에서 든다.
    /// 건너는 줄은 부르는 쪽이 상자 위(또는 아래)에 준다 — 상자가 길 위에 그려지므로 상자를 가로지르면 길이 상자 뒤로 사라진다. 건널 것이 없으면(세로 길이 곧
    /// 칸 밑) 부르는 쪽이 줄 높이를 준다 — 곧게 내려가 든다.
    /// 꺾이는 점(손잡이) = 건너는 줄이 세로 길을 만나는 모퉁이. 옆으로 끌면 세로 길이, 위아래로 끌면 건너는 줄이 움직인다.
    /// 곡선 · 직선은 사람이 고른 모양이라 비켜 가지 않는다.
    /// </summary>
    public static ShapedEdge IntoSide(GraphPosition from, GraphPosition entry, double laneX, double crossY, EdgeShape? shape)
    {
        shape ??= EdgeShape.Default;

        switch (shape.Kind)
        {
            case EdgeShapeKind.Straight:
                return new ShapedEdge(EdgeShapeKind.Straight, [from, entry], null, FreeY: false);

            case EdgeShapeKind.Curve:
                return Curve(
                    from,
                    new GraphPosition(from.X, from.Y + Math.Max(CurveReach, Math.Abs(entry.Y - from.Y) / 2)),
                    new GraphPosition(entry.X - Math.Max(CurveReach, Math.Abs(entry.X - from.X) / 2), entry.Y),
                    entry,
                    shape);

            default:
                var bend = new GraphPosition(laneX + shape.BendX, crossY + shape.BendY);

                return new ShapedEdge(
                    EdgeShapeKind.Elbow,
                    [from, new GraphPosition(from.X, bend.Y), bend, new GraphPosition(bend.X, entry.Y), entry],
                    bend,
                    FreeY: true);
        }
    }

    /// <summary>선택지의 길이 칸의 점 밑에서 곧게 내려오는 거리 — 여기서 세로 길로 옮겨 탄다(이웃 칸의 문구 아래다).</summary>
    public const double DropStep = 20;

    /// <summary>자동 곡선 P0..P3을 t=½에서 쪼개(드 카스텔조) 가운데 마디를 손잡이만큼 옮긴다.</summary>
    private static ShapedEdge Curve(
        GraphPosition p0, GraphPosition p1, GraphPosition p2, GraphPosition p3, EdgeShape shape)
    {
        GraphPosition q0 = Mid(p0, p1);
        GraphPosition q1 = Mid(p1, p2);
        GraphPosition q2 = Mid(p2, p3);
        GraphPosition r0 = Mid(q0, q1);
        GraphPosition r1 = Mid(q1, q2);
        GraphPosition middle = Mid(r0, r1);

        GraphPosition Moved(GraphPosition point) => new(point.X + shape.BendX, point.Y + shape.BendY);

        return new ShapedEdge(
            EdgeShapeKind.Curve,
            [p0, q0, Moved(r0), Moved(middle), Moved(r1), q2, p3],
            Moved(middle),
            FreeY: true);
    }

    private static GraphPosition Mid(GraphPosition a, GraphPosition b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
}
