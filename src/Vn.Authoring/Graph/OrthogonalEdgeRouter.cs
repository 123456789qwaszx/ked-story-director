namespace Vn.Authoring.Graph;

/// <summary>두 endpoint 사이를 수평·수직 선분만으로 잇는 ㄱ자 경로 계산.</summary>
public static class OrthogonalEdgeRouter
{
    /// <param name="leftward">
    /// 카드 <b>왼쪽</b> 포트에서 나가 도착 카드의 오른쪽으로 드는 선 — 오른쪽 선을 거울에 비춘 모양이다
    /// (2026-10-29 소유자: 왼쪽으로 가는 분기는 박스 왼쪽에서 — <see cref="EdgeSide"/>).
    /// </param>
    public static IReadOnlyList<GraphPosition> Route(
        GraphPosition from,
        GraphPosition to,
        double detour = 48,
        bool leftward = false)
    {
        if (detour < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(detour));
        }

        // 대상이 나가는 쪽에 충분히 있으면 둘 사이 중앙을 사용한다.
        // 대상이 반대쪽이거나 너무 가까우면 두 항목 바깥(나가는 쪽)으로 우회하여 카드 위를 가로지르지 않는다.
        double middleX = leftward
            ? to.X <= from.X - detour
                ? (from.X + to.X) / 2
                : Math.Min(from.X, to.X) - detour
            : to.X >= from.X + detour
                ? (from.X + to.X) / 2
                : Math.Max(from.X, to.X) + detour;

        return new[]
        {
            from,
            new GraphPosition(middleX, from.Y),
            new GraphPosition(middleX, to.Y),
            to
        };
    }
}
