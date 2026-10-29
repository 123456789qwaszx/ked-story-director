using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.Authoring.Graph;

/// <summary>
/// <b>뿌리보다 왼쪽</b> — 연출 그래프에서 선택지 레일과 분기 포트가 어느 쪽으로 서는가 (2026-10-29 소유자:
/// *"뿌리 에피소드보다 왼쪽에 위치하면 왼쪽으로 생성되도록 해줄래?"*).
///
/// 뿌리는 그 판의 챕터 <b>시작 에피소드</b>(<see cref="ChapterGraphModel.StartEpisode"/>)의 카드다 — 챕터가 위에서 아래로
/// 자라는 줄기이고, 그보다 왼쪽에 둔 가지는 왼쪽으로 뻗는다. 그래서 그쪽 카드의 선택지는 왼쪽으로 나오고, 뿌리보다
/// 왼쪽에 선 분기로 가는 포트는 카드 왼쪽에 선다. 챕터가 아닌 판(낙서판)에는 뿌리가 없어 늘 오른쪽이다.
///
/// ⚠ 뿌리 자신은 왼쪽이 아니다 — 견주는 것은 카드의 왼쪽 X이고, 카드 폭이 같으므로 가운데를 견주는 것과 같다.
/// </summary>
public static class RootSide
{
    /// <summary>그 판의 뿌리 카드 — 챕터 판이 아니거나, 에피소드가 없거나, 시작 에피소드의 카드가 없으면 <c>null</c>.</summary>
    public static DialogueNode? RootCard(StoryProject project, StoryFile board)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(board);

        return EpisodeNaming.ChapterOfBoard(project, board) is { Episodes.Count: > 0 } chapter
            ? EpisodeNaming.CardFor(project, chapter.ChapterId, chapter.Episodes[0].EpisodeId)
            : null;
    }

    /// <summary>그 자리(카드의 왼쪽 X)가 뿌리 카드보다 왼쪽인가.</summary>
    public static bool IsLeftOfRoot(double cardX, double rootX) => cardX < rootX;
}
