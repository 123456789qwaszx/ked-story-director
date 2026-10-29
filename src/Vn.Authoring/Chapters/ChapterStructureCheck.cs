namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>에피소드 · 간선 · 깃발 스탯의 값 검사</b> (2026-10-29 · D2).
///
/// ⛔ <b>이 검사들은 옛 워크북 리더 안에만 있었다.</b> 리더가 시트를 읽으며 Id가 비었는지 · 겹치는지,
/// 간선 끝점이 있는 에피소드인지, 관문 라벨이 있는 조건인지, 문구가 비었는지, bool 스탯을 증감했는지를
/// 짚었다. R-F 뒤로 판의 주인이 프로젝트가 되자 그 검사는 <b>들여오는 순간에만</b> 돌았고, 엑셀 연동을
/// 걷으며 리더째 사라졌다 — <see cref="ChapterAutoEdgeCheck"/>(V1)와 조건 검사가 이미 겪은 그 사고다.
///
/// ⚠ 대부분은 편집기가 애초에 막는다(같은 Id · 없는 끝점 · 모르는 스탯키는 명령이 거절한다). 그래도
/// 여기 두는 이유는 <b>값의 흠</b>이기 때문이다 — 들여온 옛 프로젝트 · 손으로 고친 파일 · 앞으로 생길
/// 새 창구는 편집기의 문을 지나지 않는다. 문구가 비었거나 같은 문구가 갈리는 것은 편집기가 막지 않는다.
///
/// ⚠ <b>새 규칙을 만들지 않는다.</b> 옛 리더의 규칙과 문구를 그대로 옮기되, 엑셀 좌표(시트 열)는 뺐다.
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
                    path, episode, "EpisodeId가 비어 있습니다."));
                continue;
            }

            if (!seen.Add(episode.EpisodeId))
            {
                diagnostics.Add(Episode(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.EpisodeIdDuplicated,
                    path, episode, $"EpisodeId '{episode.EpisodeId}'가 겹칩니다."));
            }

            if (string.IsNullOrWhiteSpace(episode.DialogueEntry))
            {
                diagnostics.Add(Episode(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.DialogueEntryBlank,
                    path, episode, $"'{episode.EpisodeId}'의 대사엔트리가 비어 있습니다. 런타임이 재생할 대상이 없습니다."));
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
                    path, "간선의 출발·도착은 둘 다 있어야 합니다."));
                continue;
            }

            RequireEpisode(edge, edge.FromEpisodeId, "출발", episodeIds, path, diagnostics);
            RequireEpisode(edge, edge.ToEpisodeId, "도착", episodeIds, path, diagnostics);
            RequireCondition(edge, edge.VisibleConditionLabel, "표시조건", labels, path, diagnostics);
            RequireCondition(edge, edge.ConditionLabel, "해금조건", labels, path, diagnostics);

            foreach (StatDelta delta in edge.StatChanges.Where(delta => !statKeys.Contains(delta.Key)))
            {
                diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatKeyUnknown,
                    path, $"'{edge.FromEpisodeId}'→'{edge.ToEpisodeId}' 길의 스탯변화 '{delta.Key}'가 " +
                    "이 챕터의 스탯이 아닙니다."));
            }

            // ⛔ 문구는 반드시 있다 (2026-08-24 소유자 결정) — "보이지 않는 기본"은 폐지됐다.
            //    문구 없는 길은 명시한 자동 진행뿐이다(그 규칙은 ChapterAutoEdgeCheck가 본다).
            if (edge.HasNoOptionLabel && !edge.Auto)
            {
                diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.OptionLabelBlank,
                    path, $"'{edge.FromEpisodeId}'→'{edge.ToEpisodeId}' 길에 선택지 문구가 없습니다. " +
                    "일반 선택지에는 문구를 적으세요."));
            }
        }

        // 간선 신원 = (출발, 도착, 문구) — v9. 셋이 다 같으면 어느 쪽이 참인지 모호하다.
        foreach (IGrouping<(string, string, string), ChapterEdge> duplicated in edges
                     .GroupBy(edge => (edge.FromEpisodeId, edge.ToEpisodeId, edge.OptionLabel ?? string.Empty))
                     .Where(group => group.Count() > 1))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.OptionEdgeMismatch,
                path, $"{duplicated.Key.Item1}→{duplicated.Key.Item2} 간선이 같은 선택지 문구로 " +
                "여럿 있습니다 — 문구를 다르게 하거나 하나를 지워 주세요."));
        }

        // 같은 에피소드에서 같은 문구가 서로 다른 곳으로 간다 — 플레이어에게는 같은 버튼 둘이고,
        // 문구를 열쇠로 매다는 연출(`DialogueNode.ChoiceExits`)도 한 벌뿐이라 두 길이 같은 연출을 탄다.
        foreach (IGrouping<(string, string), ChapterEdge> collided in edges
                     .Where(edge => !edge.HasNoOptionLabel)
                     .GroupBy(edge => (edge.FromEpisodeId, edge.OptionLabel!))
                     .Where(group => group.Select(edge => edge.ToEpisodeId).Distinct(StringComparer.Ordinal).Count() > 1))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Warning, ChapterDiagnosticCode.OptionEdgeMismatch,
                path, $"'{collided.Key.Item1}'에서 '{collided.Key.Item2}' 선택지가 여러 갈래로 " +
                "갑니다 — 플레이어에게는 같은 버튼 둘로 보이고, 이 문구에 매단 연출도 " +
                "한 벌뿐이라 두 길이 같은 연출을 탑니다."));
        }
    }

    private static void RequireEpisode(
        ChapterEdge edge, string episodeId, string field, HashSet<string> known,
        string path, List<ChapterDiagnostic> diagnostics)
    {
        if (!known.Contains(episodeId))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.EdgeEndpointUnknown,
                path, $"'{edge.FromEpisodeId}'→'{edge.ToEpisodeId}' 길의 {field} '{episodeId}'가 이 챕터의 에피소드가 아닙니다."));
        }
    }

    private static void RequireCondition(
        ChapterEdge edge, string? label, string field, HashSet<string> known,
        string path, List<ChapterDiagnostic> diagnostics)
    {
        if (!string.IsNullOrEmpty(label) && !known.Contains(label))
        {
            diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.ConditionLabelUndefined,
                path, $"'{edge.FromEpisodeId}'→'{edge.ToEpisodeId}' 길의 {field} '{label}'이 이 챕터의 " +
                "조건에 없습니다. 라벨↔식의 원천은 챕터 조건입니다(G-7)."));
        }
    }

    // ── 깃발(bool) 스탯 ─────────────────────────────────────────────────────

    /// <summary>
    /// bool 스탯(2026-08-16)의 어휘 — 값 공간이 0/1 하나이므로 범위는 0~1, 조건은 <c>== true/false</c>
    /// 뿐이고, 간선의 증감은 뜻이 없다. 지정(true/false)은 bool에만 열린다(2026-08-19 소유자).
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
                $"'{stat.Key}'는 bool 스탯이라 범위가 0~1이고 처음값은 0이나 1이어야 합니다 " +
                $"(지금 {stat.Minimum}~{stat.Maximum}, 처음값 {stat.Initial})."));
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
                    $"조건 '{condition.Label}': '{term.Key}'는 bool 스탯입니다 — " +
                    $"'{term.Key} == true' 또는 '{term.Key} == false'로만 적을 수 있습니다."));
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
                        path, $"'{edge.FromEpisodeId}'→'{edge.ToEpisodeId}' 길: '{delta.Key}'는 bool 스탯이라 증감을 쓸 수 없습니다 — " +
                        $"켜고 끄려면 '{delta.Key} true' 또는 '{delta.Key} false'로 적어 주세요."));
                }

                if (!isBool && delta.IsSet)
                {
                    diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatValueNotInteger,
                        path, $"'{edge.FromEpisodeId}'→'{edge.ToEpisodeId}' 길: '{delta.Key}'는 정수 스탯이라 true/false로 정할 수 없습니다 — " +
                        $"'{delta.Key} +1'처럼 증감으로 적어 주세요."));
                }
            }

            // 한 간선이 같은 깃발을 두 번 정하면 어느 쪽이 이기는지 아무도 모른다.
            foreach (IGrouping<string, StatDelta> repeated in edge.StatChanges
                         .Where(delta => delta.IsSet)
                         .GroupBy(delta => delta.Key, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Edge(ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.StatValueNotInteger,
                    path, $"'{edge.FromEpisodeId}'→'{edge.ToEpisodeId}' 길이 '{repeated.Key}'를 두 번 정합니다 — " +
                    "한 번만 적어 주세요."));
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
