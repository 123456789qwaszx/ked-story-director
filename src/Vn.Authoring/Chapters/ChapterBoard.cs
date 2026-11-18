using Vn.Authoring.Model;
using Vn.Authoring.Rendering;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 챕터의 에피소드가 <b>판의 어느 대사 노드로 재생되는가</b>를 잇는다.
///
/// <b>그 챕터의 판만 본다</b>. EpisodeId는 챕터 안에서만 유일하다(<c>EpisodeLibrary</c>) —
/// <b>프로젝트 전체</b>의 대사 노드를 EpisodeId 하나로 찾으면 다른 챕터의 같은 Id가 그 챕터의
/// 노드 대신 잡혀 진행 JSON이 엉뚱한 대사를 부른다. 판은 <see cref="EpisodeNaming.BoardOf"/>로
/// 찾는다 — 챕터 = 판 1:1(G-1 v2)이고, 판이 아직 없으면 노드도 없다.
///
/// <b>간선에 매달린 연출 씬</b>은 따로 찾지 않는다 — 같은 재생 순서를 에피소드 한 칸으로
/// 말할 수 있으므로(`A —문구→ 연출 —자동→ B`) 곁칸은 두 번째 표현 방법이 된다.
/// </summary>
internal sealed class ChapterBoard
{
    /// <summary>프로젝트 없이 부른 자리 — 판이 없어 대사 노드를 하나도 찾지 않는다.</summary>
    private static readonly ChapterBoard Empty = new(null, null);

    /// <summary>에피소드 → 그 에피소드의 카드(대사 노드). 배선을 찾는 출발점이다.</summary>
    private readonly Dictionary<string, DialogueNode> _excelNodes =
        new(StringComparer.Ordinal);

    private ChapterBoard(StoryProject? project, string? chapterId)
    {
        // 프로젝트 없이 부르거나 판이 아직 없으면 노드도 없다.
        if (project is null || chapterId is null || EpisodeNaming.BoardOf(project, chapterId) is not { } board)
        {
            return;
        }

        // 에피소드마다 카드가 하나고 카드는 표식을 든다 — 저장 검증이 지킨다(old-shapes O4).
        foreach (DialogueNode node in board.Nodes.OfType<DialogueNode>())
        {
            if (node.MarkedEpisodeId is { } episodeId)
            {
                _excelNodes.Add(episodeId, node);
            }
        }
    }

    /// <param name="chapterId">어느 챕터의 판을 볼 것인가. 그 판 밖의 노드는 보지 않는다.</param>
    public static ChapterBoard For(StoryProject? project, string chapterId) =>
        project is null ? Empty : new ChapterBoard(project, chapterId);

    /// <summary>
    /// 이 에피소드를 재생할 <b>대사 노드</b> — 표식(<c>MarkedEpisodeId</c>)으로 찾는다. 프로젝트 없이 부르면(모델만 보는 자리) <c>null</c>이다.
    /// ⛔ 표식 없는 구판 카드를 이름으로 찾던 뒷길은 2026-11-18에 걷혔다(old-shapes O4).
    /// </summary>
    public DialogueNode? EpisodeNodeFor(ChapterEpisode episode) =>
        _excelNodes.GetValueOrDefault(episode.EpisodeId);


}
