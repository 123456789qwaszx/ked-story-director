using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>에피소드를 다른 장면으로 옮길 때 장면 입구가 늘어 둘 이상이 되게 하는 선만 끊는다</b> (2026-11-26 소유자 — 「깨지는 선만 끊고 옮기기」).
/// 끊어도 입구가 둘인 이동(챕터 시작을 시작점이 있는 장면으로)만 받지 않고 왜인지 말한다(2026-11-22 · usability UX37 — ⛔ 그날까지는 입구가 둘이
/// 되는 이동을 모두 거절했다).
///
/// 새 규칙이 아니다 — 계획대로 끊고 옮기면 <see cref="ChapterSceneEntryCheck"/>가 오류를 내지 않는지, 끊기 전 그대로 옮기면 내는지를 함께 잰다.
/// </summary>
public sealed class SceneMoveGuardTests
{
    [Fact]
    public void 시작점이_있는_장면으로_길을_받는_에피소드를_옮기면_그_길만_끊는다()
    {
        // opening: root → a(같은 장면), root → gate(hall의 시작점). a를 hall로 옮기면 root → a가 장면을 넘어 hall의 입구가 gate · a 둘이다 —
        // root → a만 끊는다. root → gate는 그대로다.
        ChapterDocument chapter = Chapter(("opening", "root"), ("opening", "a"), ("hall", "gate"));
        chapter.Edges.Add(new ChapterEdge("root", "a", "이쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("root", "gate", "저쪽", null, null));
        Assert.True(BreaksAfterMove(chapter, "a", "hall"));   // 끊지 않고 옮기면 오류다

        SceneMovePlan plan = SceneMoveGuard.Plan(chapter, "a", "hall");

        Assert.Null(plan.Refusal);
        Assert.Equal([("root", "a")], plan.Cuts.Select(cut => (cut.FromEpisodeId, cut.ToEpisodeId)));
        Assert.False(BreaksAfterMove(chapter, "a", "hall", plan.Cuts));

        string note = SceneMoveGuard.CutNote(chapter, plan.Cuts, done: false);
        Assert.Contains("선 1개를 끊습니다", note, StringComparison.Ordinal);
        Assert.Contains("'root'→'a' 「이쪽」", note, StringComparison.Ordinal);
        Assert.Contains("끊었습니다", SceneMoveGuard.CutNote(chapter, plan.Cuts, done: true), StringComparison.Ordinal);
    }

    [Fact]
    public void 옮긴_에피소드에서_길을_받던_옛_장면의_에피소드가_두_번째_입구가_되면_그_길만_끊는다()
    {
        // opening: root(챕터 시작) → a → b. a를 새 장면으로 옮기면 a → b가 장면을 넘어 opening의 입구가 root · b 둘이다 — a → b만 끊는다.
        // root → a는 남는다 — a가 hall의 입구(시작점)가 된다(hall에는 아직 입구가 없다).
        ChapterDocument chapter = Chapter(("opening", "root"), ("opening", "a"), ("opening", "b"), ("hall", "x"));
        chapter.Edges.Add(new ChapterEdge("root", "a", "하나", null, null));
        chapter.Edges.Add(new ChapterEdge("a", "b", "둘", null, null));
        Assert.True(BreaksAfterMove(chapter, "a", "hall"));

        SceneMovePlan plan = SceneMoveGuard.Plan(chapter, "a", "hall");

        Assert.Null(plan.Refusal);
        Assert.Equal([("a", "b")], plan.Cuts.Select(cut => (cut.FromEpisodeId, cut.ToEpisodeId)));
        Assert.False(BreaksAfterMove(chapter, "a", "hall", plan.Cuts));
    }

    [Fact]
    public void 시작점이_옮겨_갈_뿐이면_끊지_않고_새_입구가_둘_생기면_그_둘을_끊는다()
    {
        // hall: gate(root → gate로 시작점) → g1 · g2. gate를 annex로 옮기면 hall의 입구가 g1 · g2 둘이 된다 — 둘 다 끊는다(어느 쪽을 시작점으로 남길지
        // 고르지 않는다). g2가 없으면 g1이 hall의 하나뿐인 입구가 될 뿐이라 끊지 않는다.
        ChapterDocument chapter = Chapter(("opening", "root"), ("hall", "gate"), ("hall", "g1"), ("hall", "g2"));
        chapter.Edges.Add(new ChapterEdge("root", "gate", "간다", null, null));
        chapter.Edges.Add(new ChapterEdge("gate", "g1", "왼쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("gate", "g2", "오른쪽", null, null));

        SceneMovePlan plan = SceneMoveGuard.Plan(chapter, "gate", "annex");

        Assert.Null(plan.Refusal);
        Assert.Equal([("gate", "g1"), ("gate", "g2")], plan.Cuts.Select(cut => (cut.FromEpisodeId, cut.ToEpisodeId)));
        Assert.False(BreaksAfterMove(chapter, "gate", "annex", plan.Cuts));

        chapter.Edges.RemoveAt(2);
        Assert.Empty(SceneMoveGuard.Plan(chapter, "gate", "annex").Cuts);
        Assert.False(BreaksAfterMove(chapter, "gate", "annex"));
    }

    [Fact]
    public void 챕터_시작은_끊어도_입구가_둘이면_옮기지_않고_까닭을_말한다()
    {
        // 챕터 시작은 선과 상관없이 늘 입구다. hall에는 side → gate로 이미 입구가 있다 — root를 hall로 옮기면 끊을 선이 없는데도 입구가 둘이다.
        ChapterDocument chapter = Chapter(("opening", "root"), ("opening", "mid"), ("side", "s"), ("hall", "gate"));
        chapter.Edges.Add(new ChapterEdge("root", "mid", "곧장", null, null));
        chapter.Edges.Add(new ChapterEdge("root", "s", "옆길", null, null));
        chapter.Edges.Add(new ChapterEdge("s", "gate", "들어간다", null, null));

        SceneMovePlan plan = SceneMoveGuard.Plan(chapter, "root", "hall");

        Assert.NotNull(plan.Refusal);
        Assert.Empty(plan.Cuts);
        Assert.Contains("옮길 수 없습니다", plan.Refusal, StringComparison.Ordinal);
        Assert.Contains("'root'(챕터 시작)", plan.Refusal, StringComparison.Ordinal);
        Assert.Contains("챕터 시작 에피소드는 늘 그 장면의 입구", plan.Refusal, StringComparison.Ordinal);
        Assert.Equal(plan.Refusal, SceneMoveGuard.Refusal(chapter, "root", "hall"));
    }

    [Fact]
    public void 길이_없는_에피소드와_입구가_늘지_않는_이동은_받는다()
    {
        ChapterDocument chapter = Chapter(("opening", "root"), ("opening", "a"), ("opening", "tail"), ("hall", "gate"), ("hall", "loose"));
        chapter.Edges.Add(new ChapterEdge("root", "a", "이쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("a", "gate", "저쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("a", "tail", "끝", null, null));

        // 길이 없는 에피소드.
        Assert.Equal(SceneMovePlan.Nothing.Cuts, SceneMoveGuard.Plan(chapter, "loose", "opening").Cuts);
        Assert.Null(SceneMoveGuard.Refusal(chapter, "loose", "opening"));
        Assert.False(BreaksAfterMove(chapter, "loose", "opening"));

        // 장면의 끝 에피소드(들어오는 길만 있다)를 새 장면으로 — 그 장면의 하나뿐인 입구가 된다.
        Assert.Empty(SceneMoveGuard.Plan(chapter, "tail", "ending").Cuts);
        Assert.Null(SceneMoveGuard.Refusal(chapter, "tail", "ending"));
        Assert.False(BreaksAfterMove(chapter, "tail", "ending"));

        // 같은 장면으로는 바뀌는 것이 없다.
        Assert.Null(SceneMoveGuard.Refusal(chapter, "a", "opening"));
    }

    [Fact]
    public void 입구가_둘인_장면을_바로잡는_이동은_받는다()
    {
        // hall에 입구가 둘(gate · side — 옛 파일). side를 따로 떼어 새 장면으로 옮기면 hall은 gate 하나가 된다.
        ChapterDocument chapter = Chapter(("opening", "root"), ("hall", "gate"), ("hall", "side"));
        chapter.Edges.Add(new ChapterEdge("root", "gate", "이쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("root", "side", "저쪽", null, null));
        Assert.True(Breaks(chapter));

        Assert.Null(SceneMoveGuard.Refusal(chapter, "side", "annex"));
        Assert.Empty(SceneMoveGuard.Plan(chapter, "side", "annex").Cuts);
        Assert.False(BreaksAfterMove(chapter, "side", "annex"));
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static bool BreaksAfterMove(
        ChapterDocument chapter, string episodeId, string sceneId, IReadOnlyList<ChapterEdge>? cuts = null)
    {
        var moved = new ChapterDocument { ChapterId = chapter.ChapterId };
        moved.Episodes.AddRange(chapter.Episodes.Select(episode =>
            episode.EpisodeId == episodeId ? episode with { SceneId = sceneId } : episode));
        moved.Edges.AddRange(chapter.Edges.Where(edge => cuts?.Any(cut => ReferenceEquals(cut, edge)) != true));
        return Breaks(moved);
    }

    private static bool Breaks(ChapterDocument chapter) =>
        ChapterSceneEntryCheck.Of(chapter.Episodes, chapter.Edges, "ch01").Count > 0;

    private static ChapterDocument Chapter(params (string SceneId, string EpisodeId)[] episodes)
    {
        var chapter = new ChapterDocument { ChapterId = "ch01" };

        foreach ((string sceneId, string episodeId) in episodes)
        {
            chapter.Episodes.Add(new ChapterEpisode(episodeId, episodeId, string.Empty, 0, 0, null)
            {
                SceneId = sceneId
            });
        }

        return chapter;
    }
}
