using Vn.Authoring.Text;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>에피소드를 다른 장면으로 옮길 때, 장면 입구가 늘어 둘 이상이 되게 하는 선만 끊는다</b> (2026-11-26 소유자 — 「하이어아키에서 특정 장면 속의
/// 에피소드를 다른 장면으로 옮기고 싶어질 때가 있다」 · 「깨지는 선만 끊고 옮기기」).
///
/// 장면은 한 자리에서만 시작한다(<see cref="ChapterSceneEntryCheck"/> — 검증 오류 · 내보내기를 막는다). 에피소드를 옮기면 그 에피소드에
/// 닿은 길들이 장면을 넘거나 넘지 않게 되어 <b>옛 장면과 새 장면이 함께</b> 바뀐다 — 옮긴 에피소드가 새 장면의 두 번째 입구가 되거나, 옮긴
/// 에피소드에서 길을 받던 옛 장면의 에피소드가 그 장면의 두 번째 입구가 된다.
///
/// 규칙(<see cref="Plan"/>): 옮긴 뒤의 모습에 <see cref="ChapterSceneEntryCheck.IsEntry"/>를 그대로 부른다. 장면의 입구가 <b>늘었고 둘 이상</b>이면,
/// 옮긴 에피소드에 닿은 선 가운데 <b>옮긴 뒤 장면을 넘어 그 장면의 (전에는 입구가 아니던) 에피소드로 들어가는 선</b>을 모두 끊는다. 그 밖의 선
/// (같은 장면 안이 된 선 · 그 장면의 시작점으로 들어가는 선 · 바뀌지 않는 선)은 남는다. 입구가 줄거나 그대로인 이동 · 시작점이 옮겨 갈 뿐인
/// 이동 · 길이 없는 에피소드의 이동은 아무것도 끊지 않는다. 끊어도 입구가 둘인 경우(챕터 시작 에피소드를 이미 시작점이 있는 장면으로 — 챕터
/// 시작은 늘 입구다)만 옮기지 않고 까닭을 말한다(<see cref="SceneMovePlan.Refusal"/>).
///
/// ⛔ 2026-11-22(usability UX37)부터 그날까지는 입구가 둘이 되는 이동을 모두 거절하고 까닭만 말했다 — 소유자 말대로 「옮기면 열에 아홉은 오류」라
/// 선이 이어진 에피소드는 사실상 옮길 수 없었다.
/// </summary>
public static class SceneMoveGuard
{
    /// <summary>옮기지 못하는 까닭 — 옮길 수 있으면(끊을 선이 있어도) null.</summary>
    public static string? Refusal(ChapterDocument chapter, string episodeId, string newSceneId) =>
        Plan(chapter, episodeId, newSceneId).Refusal;

    /// <summary>그 에피소드를 그 장면으로 옮기면 끊을 선과, 옮기지 못하면 그 까닭.</summary>
    public static SceneMovePlan Plan(ChapterDocument chapter, string episodeId, string newSceneId)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        ChapterEpisode? moving = chapter.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal));

        if (moving is null || string.Equals(moving.SceneId, newSceneId, StringComparison.Ordinal))
        {
            return SceneMovePlan.Nothing;
        }

        string oldSceneId = moving.SceneId;
        string[] scenes = [newSceneId, oldSceneId];

        // 옮긴 뒤의 에피소드 목록 — 차례는 그대로다(첫 에피소드가 챕터 시작이다).
        List<ChapterEpisode> after = chapter.Episodes
            .Select(episode => ReferenceEquals(episode, moving) ? episode with { SceneId = newSceneId } : episode)
            .ToList();

        var cuts = new List<ChapterEdge>();

        foreach (string sceneId in scenes)
        {
            List<ChapterEpisode> was = Entries(chapter.Episodes, chapter.Edges, sceneId);

            if (Entries(after, chapter.Edges, sceneId).Count <= Math.Max(was.Count, 1))
            {
                continue;
            }

            foreach (ChapterEdge edge in chapter.Edges)
            {
                if (!Touches(edge, episodeId) ||
                    Find(after, edge.FromEpisodeId) is not { } from ||
                    Find(after, edge.ToEpisodeId) is not { } to ||
                    !string.Equals(to.SceneId, sceneId, StringComparison.Ordinal) ||
                    string.Equals(from.SceneId, sceneId, StringComparison.Ordinal) ||
                    was.Any(entry => string.Equals(entry.EpisodeId, to.EpisodeId, StringComparison.Ordinal)) ||
                    cuts.Any(cut => ReferenceEquals(cut, edge)))
                {
                    continue;
                }

                cuts.Add(edge);
            }
        }

        // 끊고 남는 선으로 다시 센다 — 그래도 늘어 둘 이상이면(챕터 시작은 선과 상관없이 입구다) 옮기지 않는다.
        List<ChapterEdge> kept = chapter.Edges.Where(edge => !cuts.Any(cut => ReferenceEquals(cut, edge))).ToList();
        var reasons = new List<string>();

        foreach (string sceneId in scenes)
        {
            List<ChapterEpisode> was = Entries(chapter.Episodes, chapter.Edges, sceneId);
            List<ChapterEpisode> now = Entries(after, kept, sceneId);

            if (now.Count <= Math.Max(was.Count, 1))
            {
                continue;
            }

            string scene = SceneName(chapter, sceneId);
            string list = string.Join(", ", now.Select(entry => $"'{entry.ShownTitle}'({Why(after, kept, entry)})"));

            reasons.Add(Plural.Format(Msg.Edit_SceneWouldHaveEntrancesFrom, scene, now.Count, list));
        }

        if (reasons.Count == 0)
        {
            return new SceneMovePlan(null, cuts);
        }

        string name = moving.ShownTitle;

        return new SceneMovePlan(
            string.Format(Msg.Edit_CanTMoveToScene, name, Josa.을를(name), SceneName(chapter, newSceneId), Josa.으로(SceneName(chapter, newSceneId))) +
            string.Join(" ", reasons) +
            Msg.Edit_ChapterStartIsAlwaysEntrance,
            []);
    }

    /// <summary>
    /// 끊는 선을 사람 말로 — 끄는 동안의 풍선(<paramref name="done"/>이 거짓 — 「끊습니다」)과 옮긴 뒤의 상태줄(참 — 「끊었습니다」)이 쓴다.
    /// 끊을 선이 없으면 빈 글이다. 이름은 옮기기 전의 것이다.
    /// </summary>
    public static string CutNote(ChapterDocument chapter, IReadOnlyList<ChapterEdge> cuts, bool done)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(cuts);

        if (cuts.Count == 0)
        {
            return string.Empty;
        }

        string list = string.Join(" · ", cuts.Select(cut =>
        {
            string from = Find(chapter.Episodes, cut.FromEpisodeId)?.ShownTitle ?? cut.FromEpisodeId;
            string to = Find(chapter.Episodes, cut.ToEpisodeId)?.ShownTitle ?? cut.ToEpisodeId;
            return cut.HasNoOptionLabel
                ? string.Format(Msg.Edit_CutPathItem, from, to)
                : string.Format(Msg.Edit_CutPathItemWithText, from, to, cut.OptionLabel);
        }));

        return done
            ? Plural.Format(Msg.Edit_CutPaths, cuts.Count, list)
            : Plural.Format(Msg.Edit_MoveCutsPaths, cuts.Count, list);
    }

    private static bool Touches(ChapterEdge edge, string episodeId) =>
        string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal) ||
        string.Equals(edge.ToEpisodeId, episodeId, StringComparison.Ordinal);

    private static ChapterEpisode? Find(IReadOnlyList<ChapterEpisode> episodes, string episodeId) =>
        episodes.FirstOrDefault(episode => string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal));

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

/// <summary>
/// 장면 옮기기의 계획(<see cref="SceneMoveGuard.Plan"/>) — 끊을 선(옮기는 것과 한 되돌리기 칸에 걷는다)과, 옮기지 못하면 그 까닭(그때 끊을 선은 비었다).
/// </summary>
public sealed record SceneMovePlan(string? Refusal, IReadOnlyList<ChapterEdge> Cuts)
{
    /// <summary>바뀌는 것이 없다 — 같은 장면이거나 없는 에피소드.</summary>
    public static SceneMovePlan Nothing { get; } = new(null, []);
}
