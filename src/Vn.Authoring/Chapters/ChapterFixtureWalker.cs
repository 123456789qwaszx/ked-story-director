using Vn.Authoring.Strings;
namespace Vn.Authoring.Chapters;

/// <param name="EpisodeIds">지나간 에피소드, 순서대로. 첫 항목은 언제나 시작 에피소드다.</param>
/// <param name="StoppedBecause">멈춘 이유. 끝까지 갔으면 null이다.</param>
public sealed record FixtureWalkResult(
    IReadOnlyList<string> EpisodeIds,
    string? StoppedBecause);

/// <summary>
/// 픽스처("이 조건에서 어디로 가나")의 실제 경로를 계산한다.
///
/// 도달성 증명("어떤 조건에서든 갈 수 있나")과는 다른 도구다: 증명은 모든 가능성을 탐색하고,
/// 픽스처는 <b>시작 스탯과 고정된 선택</b>으로 한 판을 걸어 본다. 픽스처 스탯은 시작값이고,
/// 걷는 동안 간선의 `스탯변화`가 그대로 커밋된다 — 증감이 정확값이라 결정론적으로 걸을 수 있다.
/// </summary>
public static class ChapterFixtureWalker
{
    public static FixtureWalkResult Walk(ChapterGraphModel chapter, ChapterFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(fixture);

        var path = new List<string>();

        if (chapter.StartEpisode is not { } start)
        {
            return new FixtureWalkResult(path, Msg.Diag_ThereAreNoEpisodes);
        }

        int[] stats = chapter.Stats
            .Select(stat => fixture.Stats.GetValueOrDefault(stat.Key, stat.Initial))
            .ToArray();

        var fixedChoices = fixture.Choices.ToDictionary(
            choice => choice.From, choice => choice.To, StringComparer.Ordinal);

        string current = start.EpisodeId;

        while (true)
        {
            if (path.Contains(current))
            {
                return new FixtureWalkResult(path, string.Format(Msg.Diag_MetAgainAndStoppedAt, current));
            }

            path.Add(current);

            List<ChapterEdge> passable = chapter.Edges
                .Where(edge => string.Equals(edge.FromEpisodeId, current, StringComparison.Ordinal))
                .Where(edge =>
                    chapter.FindEpisode(edge.ToEpisodeId) is not null &&
                    Satisfied(chapter, edge.VisibleConditionLabel, stats) &&
                    Satisfied(chapter, edge.ConditionLabel, stats))
                .ToList();

            if (passable.Count == 0)
            {
                return new FixtureWalkResult(path, null); // 자연 종료 — 여기가 이 픽스처의 끝이다.
            }

            ChapterEdge? next = passable.Count == 1
                ? passable[0]
                : PickByFixture(passable, fixedChoices, current);

            if (next is null)
            {
                return new FixtureWalkResult(path,
                    string.Format(Msg.Diag_HasWaysForwardButTest, current, passable.Count));
            }

            stats = ChapterReachabilityProver.ApplyDeltas(chapter, stats, next.StatChanges);
            current = next.ToEpisodeId;
        }
    }

    private static ChapterEdge? PickByFixture(
        IReadOnlyList<ChapterEdge> passable,
        IReadOnlyDictionary<string, string> fixedChoices,
        string current)
    {
        if (!fixedChoices.TryGetValue(current, out string? target))
        {
            // 고정 선택이 없어도 갈 수 있는 길이 하나뿐이면 그걸 탄다.
            return passable.Count == 1 ? passable[0] : null;
        }

        return passable.FirstOrDefault(edge =>
            string.Equals(edge.ToEpisodeId, target, StringComparison.Ordinal));
    }

    // 판정의 단일 구현은 ChapterGateJudge다 — 증명기와 한 벌.
    // 항의 종류가 스탯 비교 하나뿐이라 깃발도 같은 길로 판정된다.
    private static bool Satisfied(
        ChapterGraphModel chapter, string? label, int[] stats) =>
        ChapterGateJudge.Judge(chapter, label, stats) == ChapterGateVerdict.Open;
}
