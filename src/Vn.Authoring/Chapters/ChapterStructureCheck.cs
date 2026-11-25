using Vn.Authoring.Strings;
namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>에피소드 · 간선 · 깃발 스탯의 값 검사</b> — Id가 비었는지 · 겹치는지, 간선 끝점이 있는
/// 에피소드인지, 관문 라벨이 있는 조건인지, 문구가 비었는지, bool 스탯을 증감했는지를 짚는다.
/// 자동 간선 규칙은 <see cref="ChapterAutoEdgeCheck"/>가 따로 본다.
///
/// 대부분은 편집기가 애초에 막는다(같은 Id · 없는 끝점 · 모르는 스탯키는 명령이 거절한다). 그래도
/// 여기 두는 이유는 <b>값의 흠</b>이기 때문이다(소유자 2026-11-18 — 안전망으로 남긴다) — 손으로 고친 파일 · 앞으로 생길
/// 새 창구는 편집기의 문을 지나지 않는다. 문구가 비었거나 같은 문구가 갈리는 것은 편집기가 막지 않는다.
///
/// 깃발 스탯 규칙 넷은 코어의 내보내기 관문도 막는다 — 여기서는 그것을 <b>고치는 중에</b> 말한다.
/// </summary>
public static class ChapterStructureCheck
{
    /// <summary>이 챕터의 에피소드·간선·깃발 스탯을 본다. 깨진 것이 없으면 빈 목록이다.</summary>
    /// <param name="conditions">해석이 담긴 조건 — <see cref="ChapterDocument.ToGraphModel"/>이 다시 푼 것.</param>
    /// <param name="path">진단이 사람에게 짚어 줄 자리(챕터 Id).</param>
    public static IReadOnlyList<ChapterDiagnostic> Of(
        IReadOnlyList<ChapterEpisode> episodes,
        IReadOnlyList<ChapterEdge> edges,
        IReadOnlyList<ChapterCondition> conditions,
        IReadOnlyList<ChapterStat> stats,
        string path)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(stats);

        var diagnostics = new List<ChapterDiagnostic>();

        CheckEpisodes(episodes, path, diagnostics);
        CheckEdges(episodes, edges, conditions, stats, path, diagnostics);
        CheckBoolStats(edges, conditions, stats, path, diagnostics);

        return diagnostics;
    }

    // ── 에피소드 ────────────────────────────────────────────────────────────

    private static void CheckEpisodes(
        IReadOnlyList<ChapterEpisode> episodes, string path, List<ChapterDiagnostic> diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (ChapterEpisode episode in episodes)
        {
            if (string.IsNullOrWhiteSpace(episode.EpisodeId))
            {
                diagnostics.Add(Episode(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.EpisodeIdBlank,
                    path, episode, Msg.Diag_EpisodeIdIsEmpty));
                continue;
            }

            if (!seen.Add(episode.EpisodeId))
            {
                diagnostics.Add(Episode(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.EpisodeIdDuplicated,
                    path, episode, string.Format(Msg.Diag_EpisodeIdIsDuplicated, episode.EpisodeId)));
            }
        }
    }

    // ── 간선 ────────────────────────────────────────────────────────────────

    private static void CheckEdges(
        IReadOnlyList<ChapterEpisode> episodes,
        IReadOnlyList<ChapterEdge> edges,
        IReadOnlyList<ChapterCondition> conditions,
        IReadOnlyList<ChapterStat> stats,
        string path,
        List<ChapterDiagnostic> diagnostics)
    {
        var episodeIds = episodes.Select(episode => episode.EpisodeId).ToHashSet(StringComparer.Ordinal);
        var labels = conditions.Select(condition => condition.Label).ToHashSet(StringComparer.Ordinal);
        var statKeys = stats.Select(stat => stat.Key).ToHashSet(StringComparer.Ordinal);

        foreach (ChapterEdge edge in edges)
        {
            if (string.IsNullOrWhiteSpace(edge.FromEpisodeId) || string.IsNullOrWhiteSpace(edge.ToEpisodeId))
            {
                diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.EdgeEndpointBlank,
                    path, Msg.Diag_PathNeedsBothSourceAnd));
                continue;
            }

            RequireEpisode(edge, edge.FromEpisodeId, Msg.Diag_Source, episodeIds, path, diagnostics);
            RequireEpisode(edge, edge.ToEpisodeId, Msg.Diag_Target, episodeIds, path, diagnostics);
            RequireCondition(edge, edge.VisibleConditionLabel, Msg.Edit_ShowCondition, labels, path, diagnostics);
            RequireCondition(edge, edge.ConditionLabel, Msg.Edit_UnlockCondition, labels, path, diagnostics);

            foreach (StatDelta delta in edge.StatChanges.Where(delta => !statKeys.Contains(delta.Key)))
            {
                diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatKeyUnknown,
                    path, string.Format(Msg.Diag_StatChangeOnPathIsn, edge.FromEpisodeId, edge.ToEpisodeId, delta.Key)));
            }

            // 문구는 반드시 있다 — "보이지 않는 기본"은 없다.
            //    문구 없는 길은 명시한 자동 진행뿐이다(그 규칙은 ChapterAutoEdgeCheck가 본다).
            if (edge.HasNoOptionLabel && !edge.Auto)
            {
                diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.OptionLabelBlank,
                    path, string.Format(Msg.Diag_PathHasNoChoiceText, edge.FromEpisodeId, edge.ToEpisodeId)));
            }
        }

        // 간선 신원 = (출발, 도착, 문구) — v9. 셋이 다 같으면 어느 쪽이 참인지 모호하다.
        foreach (IGrouping<(string, string, string), ChapterEdge> duplicated in edges
                     .GroupBy(edge => (edge.FromEpisodeId, edge.ToEpisodeId, edge.OptionLabel ?? string.Empty))
                     .Where(group => group.Count() > 1))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.OptionEdgeMismatch,
                path, string.Format(Msg.Diag_ThereAreSeveralPathsWith, duplicated.Key.Item1, duplicated.Key.Item2)));
        }

        // 같은 에피소드에서 같은 문구가 서로 다른 곳으로 간다 — 플레이어에게는 같은 버튼 둘이다.
        foreach (IGrouping<(string, string), ChapterEdge> collided in edges
                     .Where(edge => !edge.HasNoOptionLabel)
                     .GroupBy(edge => (edge.FromEpisodeId, edge.OptionLabel!))
                     .Where(group => group.Select(edge => edge.ToEpisodeId).Distinct(StringComparer.Ordinal).Count() > 1))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Warning, ChapterDiagnosticCode.OptionEdgeMismatch,
                path, string.Format(Msg.Diag_InChoiceGoesSeveralWays, collided.Key.Item1, collided.Key.Item2)));
        }
    }

    private static void RequireEpisode(
        ChapterEdge edge, string episodeId, string field, HashSet<string> known,
        string path, List<ChapterDiagnostic> diagnostics)
    {
        if (!known.Contains(episodeId))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.EdgeEndpointUnknown,
                path, string.Format(Msg.Diag_OfPathIsnTEpisode, edge.FromEpisodeId, edge.ToEpisodeId, field, episodeId)));
        }
    }

    private static void RequireCondition(
        ChapterEdge edge, string? label, string field, HashSet<string> known,
        string path, List<ChapterDiagnostic> diagnostics)
    {
        if (!string.IsNullOrEmpty(label) && !known.Contains(label))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.ConditionLabelUndefined,
                path, string.Format(Msg.Diag_OfPathIsnTAmong, edge.FromEpisodeId, edge.ToEpisodeId, field, label)));
        }
    }

    // ── 깃발(bool) 스탯 ─────────────────────────────────────────────────────

    /// <summary>
    /// bool 스탯의 어휘 — 값 공간이 0/1 하나이므로 범위는 0~1, 조건은 <c>== true/false</c>
    /// 뿐이고, 간선의 증감은 뜻이 없다. 지정(true/false)은 bool에만 열린다.
    /// </summary>
    private static void CheckBoolStats(
        IReadOnlyList<ChapterEdge> edges,
        IReadOnlyList<ChapterCondition> conditions,
        IReadOnlyList<ChapterStat> stats,
        string path,
        List<ChapterDiagnostic> diagnostics)
    {
        foreach (ChapterStat stat in stats.Where(stat =>
                     stat.Type == ChapterStatType.Bool &&
                     (stat.Minimum != 0 || stat.Maximum != 1 || stat.Initial is not (0 or 1))))
        {
            diagnostics.Add(new ChapterDiagnostic(
                ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatRangeInvalid, path,
                ChapterSheetNames.Stats, null,
                string.Format(Msg.Diag_IsOnOffStatSo, stat.Key, stat.Minimum, stat.Maximum, stat.Initial)));
        }

        HashSet<string> boolKeys = stats
            .Where(stat => stat.Type == ChapterStatType.Bool)
            .Select(stat => stat.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (ChapterCondition condition in conditions)
        {
            foreach (ConditionTerm term in condition.Parsed.Where(term =>
                         term.Kind == ConditionTermKind.StatComparison &&
                         boolKeys.Contains(term.Key) &&
                         (term.Comparison != ConditionComparison.Exactly || term.Value is not (0 or 1))))
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.ConditionExpressionMalformed, path,
                    ChapterSheetNames.Conditions, null,
                    string.Format(Msg.Diag_ConditionIsOnOffStat, condition.Label, term.Key)));
            }
        }

        foreach (ChapterEdge edge in edges)
        {
            foreach (StatDelta delta in edge.StatChanges)
            {
                bool isBool = boolKeys.Contains(delta.Key);

                if (isBool && !delta.IsSet)
                {
                    diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatValueNotInteger,
                        path, string.Format(Msg.Diag_PathIsOnOffStat, edge.FromEpisodeId, edge.ToEpisodeId, delta.Key)));
                }

                if (!isBool && delta.IsSet)
                {
                    diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatValueNotInteger,
                        path, string.Format(Msg.Diag_PathIsIntegerStatSo, edge.FromEpisodeId, edge.ToEpisodeId, delta.Key)));
                }
            }

            // 한 간선이 같은 깃발을 두 번 정하면 어느 쪽이 이기는지 아무도 모른다.
            foreach (IGrouping<string, StatDelta> repeated in edge.StatChanges
                         .Where(delta => delta.IsSet)
                         .GroupBy(delta => delta.Key, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatValueNotInteger,
                    path, string.Format(Msg.Diag_PathSetsTwiceWriteIt, edge.FromEpisodeId, edge.ToEpisodeId, repeated.Key)));
            }
        }
    }

    // ── 진단 모양 ───────────────────────────────────────────────────────────

    private static ChapterDiagnostic Episode(
        ChapterDiagnosticSeverity severity, ChapterDiagnosticCode code, string path,
        ChapterEpisode episode, string message) =>
        new(severity, code, path, ChapterSheetNames.Episodes, episode.EpisodeId, message);

    private static ChapterDiagnostic Edge(
        ChapterDiagnosticSeverity severity, ChapterDiagnosticCode code, string path,
        string message) =>
        new(severity, code, path, ChapterSheetNames.Edges, EpisodeId: null, message);
}
