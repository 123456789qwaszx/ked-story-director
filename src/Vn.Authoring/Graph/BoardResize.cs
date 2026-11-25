using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.Authoring.Graph;

/// <summary>
/// 판 크기를 바꾼 결과 — 실제로 설 크기, 가로 · 세로 비율, 자리가 바뀌는 노드(Id → 새 자리), 움직이는 카드 수.
/// </summary>
/// <param name="Size">실제로 설 판 크기 — 비키다 넘치면 적은 값보다 크다.</param>
/// <param name="RatioX">카드 자리에 곱한 가로 비율. 줄이지 않으면 1.</param>
/// <param name="RatioY">카드 자리에 곱한 세로 비율. 줄이지 않으면 1.</param>
/// <param name="Positions">자리가 바뀌는 노드.</param>
/// <param name="MovedCards">자리가 바뀌는 카드 수 — 설정 창이 미리 말한다.</param>
public sealed record BoardResizePlan(
    BoardSize Size,
    double RatioX,
    double RatioY,
    IReadOnlyDictionary<string, (double X, double Y)> Positions,
    int MovedCards)
{
    /// <summary>카드 자리를 다시 적는가(줄여서 판 밖으로 나가는 카드가 있다).</summary>
    public bool Compresses => RatioX < 1 || RatioY < 1;
}

/// <summary>
/// <b>판 크기 바꾸기</b> (2026-11-17 소유자 · settings).
///
/// 늘리면 크기만 바뀐다. 줄여서 <b>새 판 밖으로 나가는 카드가 있을 때만</b> <b>카드 전체</b>의 자리를 같은 비율로 줄여 새 판에 들인다 —
/// 나간 카드만 끌어오면 가장자리에 몰려 겹치고 선이 엉킨다. 가로 · 세로는 따로 따진다. 카드 크기는 그대로다.
///
/// <list type="bullet">
/// <item>비율 r = min(1, (새 크기 − 카드 크기) / 카드 자리) — 모든 카드가 새 판 안에 드는 가장 큰 비율.</item>
/// <item>선 꺾임(<see cref="EdgeShape.BendX"/> · <see cref="EdgeShape.BendY"/> — 자동 자리에서 옮긴 양)도 같은 비율로 줄여 모양을 지킨다.</item>
/// <item>줄여서 <b>새로 겹친</b> 카드는 오른쪽으로 비킨다(원래 겹쳐 있던 둘은 그대로 둔다). 비키다 새 판을 넘으면 판을 그만큼 넓힌다 —
///   카드가 판 밖에 남는 일은 없다.</item>
/// </list>
///
/// 계산(<see cref="Plan"/>)은 순수 함수다 — 설정 창은 같은 계획으로 「카드 N장을 모읍니다」를 미리 말하고, 바꾸기는
/// <see cref="Editing.ProjectEditor.SetBoardSize"/>가 한 되돌리기 칸으로 한다.
/// </summary>
public static class BoardResize
{
    /// <summary>비킬 때 카드 사이에 두는 틈.</summary>
    public const double Gap = 20;

    public static BoardResizePlan Plan(StoryProject project, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(project);

        BoardSize wanted = new BoardSize(width, height).Clamped();
        List<StoryNode> cards = project.EnumerateNodes().ToList();

        double ratioX = Ratio(cards, wanted.Width, node => node.Layout.X, BoardSize.CardWidthOf);
        double ratioY = Ratio(cards, wanted.Height, node => node.Layout.Y, BoardSize.CardHeightOf);

        if (ratioX >= 1 && ratioY >= 1)
        {
            return new BoardResizePlan(wanted, 1, 1, new Dictionary<string, (double X, double Y)>(), 0);
        }

        // 모든 카드의 자리를 같은 비율로.
        var positions = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

        // 줄이는 축은 정수로 내린다 — 곱셈의 끝자리 오차(3760.0000000000005)로 카드 끝이 새 판을 머리카락만큼 넘으면 판이 1 넓어진다.
        // 내림이라 수학적으로 든 카드는 언제나 든다.
        foreach (StoryNode node in project.EnumerateNodes())
        {
            positions[node.Id] = (
                ratioX < 1 ? Math.Floor(node.Layout.X * ratioX) : node.Layout.X,
                ratioY < 1 ? Math.Floor(node.Layout.Y * ratioY) : node.Layout.Y);
        }

        Separate(cards, positions);

        double right = cards.Count == 0 ? 0 : cards.Max(card => positions[card.Id].X + BoardSize.CardWidthOf(card));
        double bottom = cards.Count == 0 ? 0 : cards.Max(card => positions[card.Id].Y + BoardSize.CardHeightOf(card));
        var size = new BoardSize(Math.Max(wanted.Width, Math.Ceiling(right)), Math.Max(wanted.Height, Math.Ceiling(bottom)));

        var moved = positions
            .Where(pair => project.FindNode(pair.Key) is { } node &&
                           (node.Layout.X != pair.Value.X || node.Layout.Y != pair.Value.Y))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return new BoardResizePlan(
            size,
            ratioX,
            ratioY,
            moved,
            cards.Count(card => moved.ContainsKey(card.Id)));
    }

    /// <summary>
    /// 계획을 프로젝트에 적는다 — 크기, 자리, (줄일 때) 선 꺾임. 되돌리기 칸은 부르는 쪽(<see cref="Editing.ProjectEditor"/>)이 연다.
    /// </summary>
    public static void Apply(StoryProject project, BoardResizePlan plan)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(plan);

        project.Board = plan.Size;

        foreach ((string nodeId, (double x, double y)) in plan.Positions)
        {
            if (project.FindNode(nodeId) is { } node)
            {
                node.Layout.X = x;
                node.Layout.Y = y;
            }
        }

        if (!plan.Compresses)
        {
            return;
        }

        // 실행 선의 꺾임 — 노드가 진다.
        foreach (StoryNode node in project.EnumerateNodes())
        {
            foreach (string key in node.EdgeShapes.Keys.ToList())
            {
                node.EdgeShapes[key] = Scale(node.EdgeShapes[key], plan);
            }
        }

        // 선택지 레인의 꺾임 — 챕터 간선이 진다.
        foreach (ChapterDocument chapter in project.Chapters)
        {
            for (int index = 0; index < chapter.Edges.Count; index++)
            {
                if (chapter.Edges[index].Shape is { } shape)
                {
                    chapter.Edges[index] = chapter.Edges[index] with { Shape = Scale(shape, plan) };
                }
            }
        }
    }

    private static EdgeShape Scale(EdgeShape shape, BoardResizePlan plan) =>
        shape with { BendX = shape.BendX * plan.RatioX, BendY = shape.BendY * plan.RatioY };

    /// <summary>모든 카드가 새 변 안에 드는 가장 큰 비율 — 이미 다 들면 1.</summary>
    private static double Ratio(
        IEnumerable<StoryNode> cards, double side, Func<StoryNode, double> start, Func<StoryNode, double> extent)
    {
        double ratio = 1;

        foreach (StoryNode card in cards)
        {
            double at = start(card);

            if (at > 0)
            {
                ratio = Math.Min(ratio, (side - extent(card)) / at);
            }
        }

        return Math.Max(0, ratio);
    }

    /// <summary>
    /// 줄여서 새로 겹친 카드를 오른쪽으로 비킨다 — 위에서 아래, 왼쪽에서 오른쪽 차례로 놓으며, 앞서 놓은 카드와 겹치면(원래는 안 겹쳤던
    /// 사이만) 그 카드의 오른쪽 끝 + 틈으로 옮긴다.
    /// </summary>
    private static void Separate(IReadOnlyList<StoryNode> cards, Dictionary<string, (double X, double Y)> positions)
    {
        var placed = new List<StoryNode>();

        foreach (StoryNode card in cards
                     .OrderBy(card => positions[card.Id].Y)
                     .ThenBy(card => positions[card.Id].X)
                     .ThenBy(card => card.Id, StringComparer.Ordinal))
        {
            bool moved = true;

            while (moved)
            {
                moved = false;

                foreach (StoryNode other in placed)
                {
                    if (Overlaps(Now(card), Now(other)) && !Overlaps(Before(card), Before(other)))
                    {
                        (double _, double y) = positions[card.Id];
                        positions[card.Id] = (positions[other.Id].X + BoardSize.CardWidthOf(other) + Gap, y);
                        moved = true;
                    }
                }
            }

            placed.Add(card);
        }

        (double X, double Y, double W, double H) Now(StoryNode node) =>
            (positions[node.Id].X, positions[node.Id].Y, BoardSize.CardWidthOf(node), BoardSize.CardHeightOf(node));

        static (double X, double Y, double W, double H) Before(StoryNode node) =>
            (node.Layout.X, node.Layout.Y, BoardSize.CardWidthOf(node), BoardSize.CardHeightOf(node));
    }

    private static bool Overlaps((double X, double Y, double W, double H) a, (double X, double Y, double W, double H) b) =>
        a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;
}
