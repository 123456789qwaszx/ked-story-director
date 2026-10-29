using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.Authoring.Graph;

/// <summary>
/// <b>새 카드가 연출 그래프에 설 자리</b> — 위에서 아래로 (D3 · 2026-10-29, <c>docs/design/two-graphs.md</c> §7 D3).
///
/// 소유자: *"챕터 그래프처럼 위에서 아래로 진행시키도록 합니다 … 분기는 기존처럼 오른쪽으로 빠지도록."*
///
/// <list type="bullet">
/// <item><b>선택지로 가는 다음 에피소드</b>는 부모 카드의 <b>아래</b>에 선다. 그 자리가 차 있으면 오른쪽으로 비켜 선다.</item>
/// <item><b>분기로 다녀갈 카드</b>는 원본의 <b>오른쪽</b>에 선다(<see cref="BesideOf"/>).</item>
/// <item>부모가 없으면(시작 에피소드 · 들어오는 길이 없는 카드) 판의 맨 아래 카드 밑에, 판이 비었으면 원점에 선다.</item>
/// </list>
///
/// ⛔ 2026-10-29까지는 왼→오였다 — 장면마다 한 줄을 받고 장면 안의 진행은 그 줄의 오른쪽으로 갔다(R7 P-4).
/// 그리고 에피소드를 만드는 길이 자리를 <b>에피소드를 넣기 전에</b> 물어서, 챕터 그래프에서 만든 새 카드는
/// 언제나 판 오른쪽 끝에 섰다(§6-1). 이제 부르는 쪽이 부모를 함께 준다.
///
/// ⚠ <b>이미 선 카드는 안 옮긴다.</b> 자리는 사람이 끌어 정하는 것이다 — 여기서 정하는 것은 새로 서는
/// 카드의 첫 자리뿐이다. (⛔ 판 전체를 다시 세우던 연출 그래프의 [자동 정렬]은 2026-10-29에 걷혔다 — 소유자:
/// *"자동 정렬과 필터가 있는데, 둘다 필요없으니 지워주십시오."*)
/// </summary>
public static class NodePlacement
{
    /// <summary>옆 카드까지의 거리 — 카드 너비(210)에, 칩 오른쪽으로 내려가는 레인의 자리를 더한 값.</summary>
    public const double Column = 320;

    /// <summary>
    /// 아래 카드까지의 거리 — 카드(글 칸을 품어 대개 150~220, D4) + 그 아래 선택지 칩 셋(약 140) + 도착 접점과 여백.
    /// </summary>
    public const double Row = 400;

    /// <summary>
    /// 그 에피소드의 카드가 설 자리.
    /// </summary>
    /// <param name="parentEpisodeId">
    /// 이 에피소드로 이어 줄 부모. 주지 않으면 챕터의 간선에서 찾는다(들어오는 길의 출발 중 카드가 선 가장 아래 것).
    /// ⚠ 에피소드와 간선을 <b>한 변경으로</b> 만드는 길(<c>AddNextEpisode</c>)은 자리를 물을 때 간선이 아직
    /// 없다 — 그래서 부모를 직접 준다.
    /// </param>
    public static (double X, double Y) For(
        StoryProject project, string chapterId, string episodeId, string? parentEpisodeId = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (EpisodeNaming.BoardOf(project, chapterId) is not { } board)
        {
            return (0, 0);
        }

        if (ParentCard(project, chapterId, episodeId, parentEpisodeId) is { } parent)
        {
            return FirstFree(board, parent.Layout.X, parent.Layout.Y + Row);
        }

        List<DialogueNode> cards = board.Nodes.OfType<DialogueNode>().ToList();

        return cards.Count == 0
            ? (0, 0)
            : FirstFree(board, cards.Min(card => card.Layout.X), cards.Max(card => card.Layout.Y) + Row);
    }

    /// <summary>원본의 오른쪽 — 분기로 다녀갈 카드의 자리. 차 있으면 더 오른쪽으로 비켜 선다.</summary>
    public static (double X, double Y) BesideOf(StoryFile board, StoryNode source)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(source);

        return FirstFree(board, source.Layout.X + Column, source.Layout.Y);
    }

    /// <summary>
    /// 부모 카드 — 준 것이 먼저고, 없으면 들어오는 간선의 출발 중 카드가 선 것 가운데 <b>가장 아래</b> 것이다.
    /// 합류하는 에피소드는 가장 깊은 부모의 밑에 서야 그 부모의 레인이 위로 거슬러 오르지 않는다
    /// (챕터 그래프의 깊이 규칙과 같다 — <see cref="ChapterBranchPlanner.Depths"/>).
    /// </summary>
    private static DialogueNode? ParentCard(
        StoryProject project, string chapterId, string episodeId, string? parentEpisodeId)
    {
        if (parentEpisodeId is not null)
        {
            return EpisodeNaming.CardFor(project, chapterId, parentEpisodeId);
        }

        ChapterDocument? chapter = project.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, chapterId, StringComparison.Ordinal));

        return chapter?.Edges
            .Where(edge => string.Equals(edge.ToEpisodeId, episodeId, StringComparison.Ordinal) &&
                           !string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal))
            .Select(edge => EpisodeNaming.CardFor(project, chapterId, edge.FromEpisodeId))
            .Where(card => card is not null)
            .MaxBy(card => card!.Layout.Y);
    }

    /// <summary>그 자리에 카드가 있으면 한 칸씩 오른쪽으로 — 겹치지만 않으면 된다.</summary>
    private static (double X, double Y) FirstFree(StoryFile board, double x, double y)
    {
        List<DialogueNode> cards = board.Nodes.OfType<DialogueNode>().ToList();

        while (cards.Any(card =>
                   Math.Abs(card.Layout.X - x) < Column / 2 &&
                   Math.Abs(card.Layout.Y - y) < Row / 2))
        {
            x += Column;
        }

        return (x, y);
    }
}
