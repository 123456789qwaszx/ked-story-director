using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Strings;
using Vn.Authoring.Text;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>에피소드를 다른 챕터의 장면으로 옮기는 계획</b> (2026-11-26 소유자 — 「에피소드를 다른 챕터의 다른 장면으로 옮기고 싶어질 때가 있다」 ·
/// 「선택지연결은 모두 끊고」). 옮기는 것은 <see cref="AutoIdFollower.MoveEpisodeToChapter"/>가 한 되돌리기 칸에 한다.
///
/// <list type="bullet">
/// <item><b>선은 모두 끊는다</b> — 챕터끼리는 잇지 못한다. 그 에피소드에 닿은 옛 챕터의 간선이 모두 걷힌다(<see cref="ChapterMovePlan.Cuts"/>).</item>
/// <item><b>함께 가는 것</b> — 대사 카드(글 · 화자 · 서식 · 메모 · 크기)와, 그 에피소드만 부르는 분기 카드. 다른 에피소드도 부르는 분기 카드는 옛
///   챕터에 남고, 옮기는 카드의 그 분기점은 이어지지 않은 채 남는다(<see cref="ChapterMovePlan.SharedDetours"/>).</item>
/// <item><b>ID</b> — 자동 모양(<c>ch01_scene01_new01</c>)이면 새 챕터 · 장면의 다음 번호로 따라간다. 사람이 지은 ID는 그대로다 — 새 챕터에
///   같은 ID가 있으면 옮기지 않는다.</item>
/// <item><b>챕터에 묶인 것</b> — 옮기는 카드들의 줄 조건이 쓰는 조건 가운데 새 챕터에 없는 것은 식째 함께 적고, 그 식이 쓰는 스탯 · 대사의
///   <c>{stat("키")}</c>가 읽는 스탯 가운데 새 챕터에 없는 것도 함께 적는다(<see cref="ChapterMovePlan.CopiedConditions"/> ·
///   <see cref="ChapterMovePlan.CopiedStats"/>). 새 챕터에 없는 조건은 경고가 아니라 그 장면의 대사 Yarn을 막는 문제이기 때문이다. 같은 이름이
///   이미 있으면 새 챕터의 것을 쓴다(덮어쓰지 않는다 · <see cref="ChapterMovePlan.SameNames"/>).</item>
/// <item><b>옮기지 않는 것</b> — 챕터 시작 에피소드(챕터 시작은 목록의 첫 에피소드라, 옮기면 남은 아무 에피소드나 시작이 된다) · 출시 기준선이
///   있는 챕터의 에피소드(플레이어 세이브가 그 에피소드를 가리키고, 끊는 선이 선택지 순서를 바꾼다).</item>
/// </list>
/// </summary>
public static class ChapterMoveGuard
{
    /// <param name="sourceReleased">옛 챕터에 출시 기준선이 있는가(<see cref="ChapterReleaseBaseline.PathFor"/> — 파일은 부르는 쪽이 본다).</param>
    public static ChapterMovePlan Plan(
        StoryProject project, string fromChapterId, string episodeId, string toChapterId, string sceneId, bool sourceReleased = false)
    {
        ArgumentNullException.ThrowIfNull(project);

        ChapterDocument? from = Find(project, fromChapterId);
        ChapterDocument? to = Find(project, toChapterId);
        ChapterEpisode? moving = from?.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal));

        if (from is null || to is null || moving is null || ReferenceEquals(from, to))
        {
            return ChapterMovePlan.Nothing(episodeId);
        }

        string name = moving.ShownTitle;

        if (ReferenceEquals(from.Episodes[0], moving))
        {
            return ChapterMovePlan.Refused(episodeId, string.Format(
                Msg.Edit_ChapterStartCanTLeave, name, Josa.을를(name), from.ShownName));
        }

        if (sourceReleased)
        {
            return ChapterMovePlan.Refused(episodeId, string.Format(
                Msg.Edit_ReleasedChapterKeepsEpisode, name, Josa.을를(name), from.ShownName));
        }

        // ID — 자동 모양이면 새 자리의 다음 번호로. 사람이 지은 ID는 그대로이고, 새 챕터에 있으면 옮기지 않는다.
        string newId = NewNames.IsAuto(episodeId, NewNames.EpisodePrefix(fromChapterId, moving.SceneId))
            ? NewNames.NextFree(NewNames.EpisodePrefix(toChapterId, sceneId), NewNames.TakenNames(project, to))
            : episodeId;

        if (to.Episodes.Any(episode => string.Equals(episode.EpisodeId, newId, StringComparison.Ordinal)))
        {
            return ChapterMovePlan.Refused(episodeId, string.Format(
                Msg.Edit_TargetChapterHasEpisodeId, name, Josa.을를(name), to.ShownName, newId));
        }

        List<ChapterEdge> cuts = from.Edges
            .Where(edge =>
                string.Equals(edge.FromEpisodeId, episodeId, StringComparison.Ordinal) ||
                string.Equals(edge.ToEpisodeId, episodeId, StringComparison.Ordinal))
            .ToList();

        // 카드와 분기 카드 — 그 에피소드만 부르는 분기 카드는 함께, 다른 카드도 부르는 것은 남는다.
        DialogueNode? card = EpisodeNaming.CardFor(project, fromChapterId, episodeId);
        List<DialogueNode> board = EpisodeNaming.BoardOf(project, fromChapterId)?.Nodes.OfType<DialogueNode>().ToList() ?? [];
        var going = new List<DialogueNode>();
        var shared = new List<ChapterMoveDetour>();

        if (card is not null)
        {
            foreach (ScriptLine line in card.Script.ActiveLines)
            {
                foreach (string target in card.FindExtension(line.Id)?.DetourTargetNodeIds ?? [])
                {
                    if (board.FirstOrDefault(node => string.Equals(node.Id, target, StringComparison.Ordinal)) is not { IsBranchCard: true } branch)
                    {
                        continue;
                    }

                    bool calledElsewhere = board.Any(other =>
                        !ReferenceEquals(other, card) &&
                        other.LineExtensions.Any(extension => extension.DetourTargetNodeIds.Contains(branch.Id, StringComparer.Ordinal)));

                    if (calledElsewhere)
                    {
                        shared.Add(new ChapterMoveDetour(card.Id, line.Id, branch.Id, EpisodeNaming.BranchLabelOf(branch)));
                    }
                    else if (!going.Contains(branch))
                    {
                        going.Add(branch);
                    }
                }
            }
        }

        // 챕터에 묶인 것 — 옮기는 카드들이 쓰는 조건 · 스탯.
        List<DialogueNode> carried = card is null ? [] : [card, .. going];
        var usedConditions = new List<string>();
        var readStats = new List<string>();

        foreach (DialogueNode node in carried)
        {
            foreach (LineConditionTransition transition in node.LineExtensions.SelectMany(extension => extension.Transitions).Concat(node.TrailingTransitions))
            {
                if (transition.ConditionId is { Length: > 0 } label && !usedConditions.Contains(label, StringComparer.Ordinal))
                {
                    usedConditions.Add(label);
                }
            }

            foreach (ScriptLine line in node.Script.ActiveLines)
            {
                foreach (string key in ChapterStatReads.KeysIn(node.Script.Text(line.Id).Text))
                {
                    if (!readStats.Contains(key, StringComparer.Ordinal))
                    {
                        readStats.Add(key);
                    }
                }
            }
        }

        var copiedConditions = new List<ChapterCondition>();
        var sameNames = new List<string>();

        foreach (string label in usedConditions)
        {
            if (to.Conditions.Any(condition => string.Equals(condition.Label, label, StringComparison.Ordinal)))
            {
                sameNames.Add(string.Format(Msg.Edit_ConditionItem, label));
            }
            else if (from.Conditions.FirstOrDefault(condition => string.Equals(condition.Label, label, StringComparison.Ordinal)) is { } source)
            {
                copiedConditions.Add(source);
            }
        }

        // 스탯 — 함께 적는 조건의 식이 쓰는 것과 대사가 읽는 것. 옛 챕터에 있는 것만(없던 것은 옮겨도 없다 — 경고가 짚는다), 영어 키만
        // (새로 세우는 키는 영어로만 — StatKeyRule · 옛 한글 키는 경고에 맡긴다).
        var wantedStats = new List<string>();

        foreach (ChapterStat stat in from.Stats)
        {
            bool inCondition = copiedConditions.Any(condition => !string.Equals(
                ConditionExpressionParser.ReplaceStatKey(condition.Expression, stat.Key, "\u0000"), condition.Expression, StringComparison.Ordinal));

            if (inCondition || readStats.Contains(stat.Key, StringComparer.Ordinal))
            {
                wantedStats.Add(stat.Key);
            }
        }

        var copiedStats = new List<ChapterStat>();

        foreach (string key in wantedStats)
        {
            if (to.Stats.Any(stat => string.Equals(stat.Key, key, StringComparison.Ordinal)))
            {
                sameNames.Add(string.Format(Msg.Edit_StatItem, key));
            }
            else if (StatKeyRule.Problem(key) is null)
            {
                copiedStats.Add(from.Stats.First(stat => string.Equals(stat.Key, key, StringComparison.Ordinal)));
            }
        }

        return new ChapterMovePlan(
            null, newId, cuts, [.. going.Select(branch => branch.Id)], shared, copiedConditions, copiedStats, sameNames);
    }

    /// <summary>
    /// 옮기면 함께 일어나는 일을 사람 말로 — 끄는 동안의 풍선(<paramref name="done"/>이 거짓)과 옮긴 뒤의 상태줄(참)이 쓴다. 아무 일도 없으면 빈 글이다.
    /// 이름은 옮기기 전의 것이다.
    /// </summary>
    public static string Note(StoryProject project, string fromChapterId, string toChapterId, ChapterMovePlan plan, bool done)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(plan);

        ChapterDocument? from = Find(project, fromChapterId);
        string target = Find(project, toChapterId)?.ShownName ?? toChapterId;
        var parts = new List<string>();

        if (plan.Cuts.Count > 0)
        {
            string list = string.Join(" · ", plan.Cuts.Select(cut =>
            {
                string start = Title(from, cut.FromEpisodeId);
                string end = Title(from, cut.ToEpisodeId);
                return cut.HasNoOptionLabel
                    ? string.Format(Msg.Edit_CutPathItem, start, end)
                    : string.Format(Msg.Edit_CutPathItemWithText, start, end, cut.OptionLabel);
            }));
            parts.Add(done
                ? Plural.Format(Msg.Edit_ChapterMoveCutPaths, plan.Cuts.Count, list)
                : Plural.Format(Msg.Edit_ChapterMoveCutsPaths, plan.Cuts.Count, list));
        }

        if (plan.CopiedConditions.Count + plan.CopiedStats.Count > 0)
        {
            string list = string.Join(" · ",
                plan.CopiedConditions.Select(condition => string.Format(Msg.Edit_ConditionItem, condition.Label))
                    .Concat(plan.CopiedStats.Select(stat => string.Format(Msg.Edit_StatItem, stat.Key))));
            parts.Add(string.Format(done ? Msg.Edit_ChapterMoveCopied : Msg.Edit_ChapterMoveCopies, target, list));
        }

        if (plan.SameNames.Count > 0)
        {
            parts.Add(string.Format(Msg.Edit_ChapterMoveSameNames, target, string.Join(" · ", plan.SameNames)));
        }

        if (plan.SharedDetours.Count > 0)
        {
            string list = string.Join(" · ", plan.SharedDetours.Select(detour => detour.BranchLabel).Distinct(StringComparer.Ordinal));
            parts.Add(string.Format(done ? Msg.Edit_ChapterMoveLeftBranches : Msg.Edit_ChapterMoveLeavesBranches, list));
        }

        return string.Join(" ", parts);
    }

    private static string Title(ChapterDocument? chapter, string episodeId) =>
        chapter?.Episodes.FirstOrDefault(episode => string.Equals(episode.EpisodeId, episodeId, StringComparison.Ordinal))?.ShownTitle ?? episodeId;

    private static ChapterDocument? Find(StoryProject project, string chapterId) =>
        project.Chapters.FirstOrDefault(chapter => string.Equals(chapter.ChapterId, chapterId, StringComparison.Ordinal));
}

/// <summary>옮기는 카드의 분기점 가운데, 다른 카드도 부르는 분기 카드(옛 챕터에 남는다)로 가는 것 — 이어지지 않은 채 남긴다.</summary>
public sealed record ChapterMoveDetour(string NodeId, string LineId, string TargetNodeId, string BranchLabel);

/// <summary>
/// 다른 챕터로 옮기기의 계획(<see cref="ChapterMoveGuard.Plan"/>). 옮기지 못하면 <see cref="Refusal"/>에 까닭이 있고 나머지는 비었다.
/// </summary>
/// <param name="EpisodeId">옮긴 뒤의 에피소드ID — 자동 모양이면 새 자리의 번호, 아니면 그대로.</param>
/// <param name="Cuts">걷을 옛 챕터의 간선 — 그 에피소드에 닿은 것 모두.</param>
/// <param name="BranchCardIds">함께 가는 분기 카드(노드 Id) — 그 에피소드만 부르는 것.</param>
/// <param name="SameNames">새 챕터에 같은 이름이 있어 새 챕터의 것을 쓰는 조건 · 스탯(사람 말 — 「조건 'x'」).</param>
public sealed record ChapterMovePlan(
    string? Refusal,
    string EpisodeId,
    IReadOnlyList<ChapterEdge> Cuts,
    IReadOnlyList<string> BranchCardIds,
    IReadOnlyList<ChapterMoveDetour> SharedDetours,
    IReadOnlyList<ChapterCondition> CopiedConditions,
    IReadOnlyList<ChapterStat> CopiedStats,
    IReadOnlyList<string> SameNames)
{
    /// <summary>바뀌는 것이 없다 — 같은 챕터 · 없는 챕터나 에피소드.</summary>
    public static ChapterMovePlan Nothing(string episodeId) => new(null, episodeId, [], [], [], [], [], []);

    /// <summary>옮기지 못한다 — 그 까닭.</summary>
    public static ChapterMovePlan Refused(string episodeId, string reason) => new(reason, episodeId, [], [], [], [], [], []);
}
