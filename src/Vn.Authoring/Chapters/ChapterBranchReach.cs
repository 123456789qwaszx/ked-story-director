using Vn.Authoring.Model;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>분기로 들어오는 에피소드를 실제 연결로 알아본다</b> (2026-11-17 소유자).
///
/// 에피소드로 들어가는 길은 둘이다 — 선택지 · 자동 길(챕터 간선 — 게임이 「지금 에피소드」를 옮긴다)과 분기(대본 줄의
/// <c>&lt;&lt;detour&gt;&gt;</c> — 지금 에피소드를 트는 도중에 그 카드를 들렀다 돌아온다). 도달성 증명(<see cref="ChapterReachabilityProver"/>)은
/// 런타임 진행 코어의 오라클이라 간선만 따른다 — 증명은 그대로 두고, 그 결과를 여기서 분기 연결과 함께 읽는다.
///
/// <b>규칙: 도달할 수 있는 에피소드의 분기점이 들르는 에피소드는 도달할 수 있다</b>(분기 카드의 분기도 사슬로 따른다). 그런 에피소드의 「경로 없음」
/// 진단은 「분기로 들어온다」 알림으로 바뀐다. 분기점이 있는 에피소드 자체가 도달할 수 없으면 들르는 에피소드도 도달할 수 없다 — 오류 그대로다.
///
/// ⛔ 전에는 「분기 추가」가 세운 카드에만 `도달불가 허용` 깃발을 몰래 달아 오류를 낮췄다. 깃발은 만들 때 붙는 표시라 실제 연결과 따로 놀았다 —
/// 다른 카드를 분기점에 이으면 깃발이 없어 거짓 「경로 없음」 오류가 났고, 분기를 끊어도 깃발이 남아 고아 카드가 알림으로만 섰고, 도달할 수 없는
/// 에피소드의 분기점이 들르는 카드도 경고 없이 지나갔다.
/// </summary>
public static class ChapterBranchReach
{
    /// <summary>
    /// 증명 결과에 분기 연결을 더한다 — 분기로 들어오는 에피소드를 도달할 수 있는 것에 넣고, 그 에피소드의 「경로 없음」 진단을 알림으로 바꾼다.
    /// 분기가 없거나 바뀔 것이 없으면 받은 것을 그대로 돌려준다.
    /// </summary>
    public static ChapterReachabilityResult Apply(
        ChapterGraphModel chapter, StoryProject project, ChapterReachabilityResult proven)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(proven);

        IReadOnlyList<ChapterBranchMarkers.Marker> markers = ChapterBranchMarkers.For(project, chapter.ChapterId);

        if (markers.Count == 0)
        {
            return proven;
        }

        var reached = new HashSet<string>(proven.ReachableEpisodeIds, StringComparer.Ordinal);
        var via = new Dictionary<string, string>(StringComparer.Ordinal);   // 분기로만 들어오는 에피소드 → 그 분기점의 에피소드
        bool grew = true;

        while (grew)
        {
            grew = false;

            foreach (ChapterBranchMarkers.Marker marker in markers)
            {
                if (reached.Contains(marker.FromEpisodeId) && reached.Add(marker.ToEpisodeId))
                {
                    via[marker.ToEpisodeId] = marker.FromEpisodeId;
                    grew = true;
                }
            }
        }

        if (via.Count == 0)
        {
            return proven;
        }

        List<ChapterDiagnostic> diagnostics = proven.Diagnostics
            .Select(diagnostic =>
                diagnostic.Code == ChapterDiagnosticCode.EpisodeUnreachable &&
                diagnostic.EpisodeId is { } episodeId &&
                via.TryGetValue(episodeId, out string? from)
                    ? new ChapterDiagnostic(
                        ChapterDiagnosticSeverity.Info,
                        ChapterDiagnosticCode.EpisodeEnteredByBranch,
                        diagnostic.FilePath,
                        diagnostic.Sheet,
                        episodeId,
                        $"'{episodeId}'는 「{chapter.FindEpisode(from)?.ShownTitle ?? from}」의 분기로 들어옵니다 — 선택지 · 자동 길로 들어오는 길은 없습니다.")
                    : diagnostic)
            .ToList();

        return proven with { ReachableEpisodeIds = reached, Diagnostics = diagnostics };
    }
}
