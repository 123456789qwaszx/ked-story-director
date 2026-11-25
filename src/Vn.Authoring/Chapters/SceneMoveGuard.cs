using Vn.Authoring.Text;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>에피소드를 다른 장면으로 옮기면 장면 입구가 늘어 둘 이상이 되는가</b> (2026-11-22 소유자 · usability UX37).
///
/// 장면은 한 자리에서만 시작한다(<see cref="ChapterSceneEntryCheck"/> — 검증 오류 · 내보내기를 막는다). 에피소드를 옮기면 그 에피소드에
/// 닿은 길들이 장면을 넘거나 넘지 않게 되어 <b>옛 장면과 새 장면이 함께</b> 바뀐다 — 옮긴 에피소드가 새 장면의 두 번째 입구가 되거나, 옮긴
/// 에피소드에서 길을 받던 옛 장면의 에피소드가 그 장면의 두 번째 입구가 된다. 소유자: 「옮기면 열에 아홉은 오류다 — 그냥 안 되게 하기보다 왜 안
/// 되는지 설명하자」. 그래서 여기는 <b>까닭을 문장으로</b> 낸다 — 어느 장면에 어느 입구가 왜 생기는지, 그리고 어떻게 하면 옮길 수 있는지.
///
/// 새 규칙이 아니다 — 옮긴 뒤의 모습에 <see cref="ChapterSceneEntryCheck.IsEntry"/>를 그대로 부른다. <b>늘었고 둘 이상</b>일 때만 거절한다:
/// 입구가 줄거나 그대로인 이동(둘인 장면을 바로잡는 이동 포함), 시작점이 옮겨 갈 뿐인 이동, 길이 없는 에피소드의 이동은 받는다.
/// </summary>
public static class SceneMoveGuard
{
    /// <summary>옮기지 못하는 까닭 — 옮겨도 되면 null.</summary>
    public static string? Refusal(ChapterDocument chapter, string episodeId, string newSceneId)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        ChapterEpisode? moving = chapter.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal));

        if (moving is null || string.Equals(moving.SceneId, newSceneId, StringComparison.Ordinal))
        {
            return null;
        }

        string oldSceneId = moving.SceneId;

        // 옮긴 뒤의 에피소드 목록 — 차례는 그대로다(첫 에피소드가 챕터 시작이다).
        List<ChapterEpisode> after = chapter.Episodes
            .Select(episode => ReferenceEquals(episode, moving) ? episode with { SceneId = newSceneId } : episode)
            .ToList();

        var reasons = new List<string>();

        foreach (string sceneId in new[] { newSceneId, oldSceneId })
        {
            List<ChapterEpisode> was = Entries(chapter.Episodes, chapter.Edges, sceneId);
            List<ChapterEpisode> now = Entries(after, chapter.Edges, sceneId);

            if (now.Count <= Math.Max(was.Count, 1))
            {
                continue;
            }

            string scene = SceneName(chapter, sceneId);
            string list = string.Join(", ", now.Select(entry => $"'{entry.ShownTitle}'({Why(after, chapter.Edges, entry)})"));

            reasons.Add(string.Format(Msg.Edit_SceneWouldHaveEntrancesFrom, scene, now.Count, list));
        }

        if (reasons.Count == 0)
        {
            return null;
        }

        string name = moving.ShownTitle;

        return string.Format(Msg.Edit_CanTMoveToScene, name, Josa.을를(name), SceneName(chapter, newSceneId), Josa.으로(SceneName(chapter, newSceneId))) +
               string.Join(" ", reasons) +
               Msg.Edit_SceneMustStartInOne;
    }

    private static List<ChapterEpisode> Entries(
        IReadOnlyList<ChapterEpisode> episodes, IReadOnlyList<ChapterEdge> edges, string sceneId) =>
        episodes
            .Where(episode => string.Equals(episode.SceneId, sceneId, StringComparison.Ordinal) &&
                              ChapterSceneEntryCheck.IsEntry(episodes, edges, episode))
            .ToList();

    /// <summary>그 에피소드가 왜 입구인가 — 챕터 시작이거나, 다른 장면의 어느 에피소드에서 길이 온다.</summary>
    private static string Why(IReadOnlyList<ChapterEpisode> episodes, IReadOnlyList<ChapterEdge> edges, ChapterEpisode entry)
    {
        if (episodes.Count > 0 && ReferenceEquals(episodes[0], entry))
        {
            return Msg.Edit_ChapterStart;
        }

        List<string> from = edges
            .Where(edge => string.Equals(edge.ToEpisodeId, entry.EpisodeId, StringComparison.Ordinal))
            .Select(edge => episodes.FirstOrDefault(episode =>
                string.Equals(episode.EpisodeId, edge.FromEpisodeId, StringComparison.Ordinal)))
            .Where(source => source is not null && !string.Equals(source.SceneId, entry.SceneId, StringComparison.Ordinal))
            .Select(source => $"'{source!.ShownTitle}'")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return from.Count == 0 ? Msg.Edit_PathFromOutside : string.Format(Msg.Edit_PathFromInAnotherScene, string.Join(" · ", from));
    }

    /// <summary>장면의 보이는 이름 — 이름을 붙였으면 그것, 아니면 장면ID.</summary>
    private static string SceneName(ChapterDocument chapter, string sceneId) =>
        ChapterSceneNotes.NameOf(chapter.SceneNotes, sceneId) ?? sceneId;
}
