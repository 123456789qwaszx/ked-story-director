using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>자동 모양 그대로인 ID는 자리를 따라간다</b> (2026-11-17 소유자 · <c>docs/work-orders/auto-id-orders.md</c> I3).
///
/// 자동 ID에는 챕터와 장면이 든다(<see cref="NewNames"/> — <c>ch01_scene01_new03</c> · 분기 카드 <c>ch01_scene01_branch01</c>). 에피소드를
/// 다른 장면으로 옮기거나 장면ID · 챕터ID를 바꾸면 ID 속 자리가 옛것으로 남는다 — 그래서 <b>아직 자동 모양 그대로인 ID만</b> 새 자리의
/// 이름으로 바꾼다. 사람이 고친 ID는 건드리지 않는다 — 바깥(compositor가 가져간 Yarn의 노드 이름, 게임 세이브)이 그 ID를 쥐고 있을 수 있다.
///
/// 에피소드 개명은 <see cref="EpisodeRenamer"/>(간선 · 픽스처 · 카드 이름 · 대사엔트리가 함께)를, 분기 카드는 이름만 바꾼다(부르는 쪽은 노드
/// Id로 잇는다). 분기 카드는 집(처음 부르는 에피소드)을 따른다. 옮기기와 따라가기가 <b>되돌리기 한 칸</b>이다.
/// </summary>
public static class AutoIdFollower
{
    /// <summary>
    /// 에피소드를 다른 장면으로 옮긴다 — 자동 모양이면 ID가 새 장면의 다음 번호로, 그 집의 분기 카드도 함께.
    /// </summary>
    /// <returns>옮긴 뒤의 EpisodeId(따라가지 않았으면 그대로).</returns>
    public static string MoveEpisodeToScene(ProjectEditor editor, string chapterId, string episodeId, string sceneId)
    {
        ArgumentNullException.ThrowIfNull(editor);

        string? oldSceneId = RequireEpisode(editor, chapterId, episodeId).SceneId;
        string current = episodeId;

        editor.Batch(() =>
        {
            List<string> branches = BranchesHomedAt(editor, chapterId, [episodeId]);
            editor.UpdateEpisode(chapterId, episodeId, sceneId: sceneId);

            current = FollowEpisode(
                editor, chapterId, episodeId,
                NewNames.EpisodePrefix(chapterId, oldSceneId), NewNames.EpisodePrefix(chapterId, sceneId), keepNumber: false);
            FollowBranches(
                editor, chapterId, branches,
                NewNames.BranchPrefix(chapterId, oldSceneId), NewNames.BranchPrefix(chapterId, sceneId), keepNumber: false);
        });

        return current;
    }

    /// <summary>
    /// 장면ID를 바꾼다(<see cref="ProjectEditor.RenameScene"/> — 장면 이름 · 메모가 따라간다) — 그 장면의 자동 모양 ID들이 번호를 지킨 채
    /// 새 장면ID로, 그 집의 분기 카드도 함께.
    /// </summary>
    /// <returns>장면ID가 바뀐 에피소드 수.</returns>
    public static int RenameScene(ProjectEditor editor, string chapterId, string oldSceneId, string newSceneId)
    {
        ArgumentNullException.ThrowIfNull(editor);

        ChapterDocument chapter = editor.FindChapter(chapterId)
            ?? throw new InvalidOperationException($"챕터 '{chapterId}'가 없습니다.");
        List<string> members = chapter.Episodes
            .Where(episode => string.Equals(episode.EffectiveSceneId, oldSceneId, StringComparison.Ordinal))
            .Select(episode => episode.EpisodeId)
            .ToList();
        int moved = 0;

        editor.Batch(() =>
        {
            List<string> branches = BranchesHomedAt(editor, chapterId, members);
            moved = editor.RenameScene(chapterId, oldSceneId, newSceneId);
            string wanted = newSceneId.Trim();

            foreach (string member in members)
            {
                FollowEpisode(
                    editor, chapterId, member,
                    NewNames.EpisodePrefix(chapterId, oldSceneId), NewNames.EpisodePrefix(chapterId, wanted), keepNumber: true);
            }

            FollowBranches(
                editor, chapterId, branches,
                NewNames.BranchPrefix(chapterId, oldSceneId), NewNames.BranchPrefix(chapterId, wanted), keepNumber: true);
        });

        return moved;
    }

    /// <summary>
    /// 챕터ID가 바뀌었다(<see cref="ChapterRenamer"/>가 같은 되돌리기 칸 안에서 부른다) — 그 챕터의 자동 모양 ID들이 번호를 지킨 채 새
    /// 챕터ID로. 분기 카드는 집이 있는 것만(집이 없으면 장면을 모른다).
    /// </summary>
    public static void FollowChapter(ProjectEditor editor, string oldChapterId, string newChapterId)
    {
        ArgumentNullException.ThrowIfNull(editor);

        ChapterDocument chapter = editor.FindChapter(newChapterId)
            ?? throw new InvalidOperationException($"챕터 '{newChapterId}'가 없습니다.");
        List<(string CardId, string? SceneId)> branches = ChapterBranchCards.HomesOf(editor.Project, newChapterId)
            .Select(home => (home.Card.Id, chapter.Episodes.FirstOrDefault(episode =>
                string.Equals(episode.EpisodeId, home.EpisodeId, StringComparison.Ordinal))?.SceneId))
            .ToList();

        foreach ((string episodeId, string? sceneId) in chapter.Episodes.Select(episode => (episode.EpisodeId, episode.SceneId)).ToList())
        {
            FollowEpisode(
                editor, newChapterId, episodeId,
                NewNames.EpisodePrefix(oldChapterId, sceneId), NewNames.EpisodePrefix(newChapterId, sceneId), keepNumber: true);
        }

        foreach ((string cardId, string? sceneId) in branches)
        {
            FollowBranches(
                editor, newChapterId, [cardId],
                NewNames.BranchPrefix(oldChapterId, sceneId), NewNames.BranchPrefix(newChapterId, sceneId), keepNumber: true);
        }
    }

    /// <summary>자동 모양이면 새 앞머리로 개명한다 — 번호를 지키거나(겹치면 다음 빈 수) 새 자리의 다음 번호로.</summary>
    private static string FollowEpisode(
        ProjectEditor editor, string chapterId, string episodeId, string from, string to, bool keepNumber)
    {
        if (string.Equals(from, to, StringComparison.Ordinal) || !NewNames.IsAuto(episodeId, from))
        {
            return episodeId;
        }

        string wanted = NewNames.NextFree(to, Taken(editor, chapterId), keepNumber ? NumberOf(episodeId, from) : 1);

        return EpisodeRenamer.Rename(editor, chapterId, episodeId, wanted).Renamed ? wanted : episodeId;
    }

    private static void FollowBranches(
        ProjectEditor editor, string chapterId, IEnumerable<string> cardIds, string from, string to, bool keepNumber)
    {
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return;
        }

        foreach (string cardId in cardIds)
        {
            if (editor.Project.FindDialogue(cardId) is { } card && NewNames.IsAuto(card.Name, from))
            {
                editor.RenameNode(card.Id, NewNames.NextFree(to, Taken(editor, chapterId), keepNumber ? NumberOf(card.Name, from) : 1));
            }
        }
    }

    /// <summary>그 에피소드들을 집으로 둔 분기 카드의 노드 Id — 옮기기 전에 잡는다(옮긴 뒤에는 에피소드ID가 바뀌어 있을 수 있다).</summary>
    private static List<string> BranchesHomedAt(ProjectEditor editor, string chapterId, IReadOnlyCollection<string> episodeIds) =>
        [.. ChapterBranchCards.HomesOf(editor.Project, chapterId)
            .Where(home => episodeIds.Contains(home.EpisodeId, StringComparer.Ordinal))
            .Select(home => home.Card.Id)];

    private static HashSet<string> Taken(ProjectEditor editor, string chapterId) =>
        NewNames.TakenNames(editor.Project, editor.FindChapter(chapterId)!);

    private static int NumberOf(string id, string prefix) =>
        int.TryParse(id[prefix.Length..], out int number) ? number : 1;

    private static ChapterEpisode RequireEpisode(ProjectEditor editor, string chapterId, string episodeId) =>
        editor.FindChapter(chapterId)?.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"챕터 '{chapterId}'에 에피소드 '{episodeId}'가 없습니다.");
}
