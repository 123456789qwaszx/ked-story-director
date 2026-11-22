using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>에피소드를 다른 장면으로 옮기면 장면 입구가 늘어 둘 이상이 되는 이동은 받지 않고, 왜인지 말한다</b>
/// (2026-11-22 소유자 — 「옮기면 열에 아홉은 오류다. 그냥 안 되게 하기보다 왜 안 되는지 설명을」 · usability UX37).
///
/// 새 규칙이 아니다 — 거절한 이동을 그대로 하면 <see cref="ChapterSceneEntryCheck"/>가 오류를 내고, 받은 이동은 내지 않는지를 함께 잰다.
/// </summary>
public sealed class SceneMoveGuardTests
{
    [Fact]
    public void 시작점이_있는_장면으로_길을_받는_에피소드를_옮기면_거절하고_두_입구를_이름_댄다()
    {
        // opening: root → a(같은 장면), root → gate(hall의 시작점). a를 hall로 옮기면 root → a가 장면을 넘어 hall의 입구가 gate · a 둘이다.
        ChapterDocument chapter = Chapter(("opening", "root"), ("opening", "a"), ("hall", "gate"));
        chapter.Edges.Add(new ChapterEdge("root", "a", "이쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("root", "gate", "저쪽", null, null));

        string? refusal = SceneMoveGuard.Refusal(chapter, "a", "hall");

        Assert.NotNull(refusal);
        Assert.Contains("장면 'hall'에 밖에서 들어오는 자리가 2개", refusal, StringComparison.Ordinal);
        Assert.Contains("'gate'", refusal, StringComparison.Ordinal);
        Assert.Contains("'a'(다른 장면의 'root'에서 오는 길)", refusal, StringComparison.Ordinal);
        Assert.True(BreaksAfterMove(chapter, "a", "hall"));
    }

    [Fact]
    public void 옮긴_에피소드에서_길을_받던_옛_장면의_에피소드가_두_번째_입구가_되면_거절한다()
    {
        // opening: root(챕터 시작) → a → b. a를 새 장면으로 옮기면 a → b가 장면을 넘어 opening의 입구가 root · b 둘이다.
        ChapterDocument chapter = Chapter(("opening", "root"), ("opening", "a"), ("opening", "b"), ("hall", "x"));
        chapter.Edges.Add(new ChapterEdge("root", "a", "하나", null, null));
        chapter.Edges.Add(new ChapterEdge("a", "b", "둘", null, null));

        string? refusal = SceneMoveGuard.Refusal(chapter, "a", "hall");

        Assert.NotNull(refusal);
        Assert.Contains("장면 'opening'에 밖에서 들어오는 자리가 2개", refusal, StringComparison.Ordinal);
        Assert.Contains("'root'(챕터 시작)", refusal, StringComparison.Ordinal);
        Assert.Contains("'b'(다른 장면의 'a'에서 오는 길)", refusal, StringComparison.Ordinal);
        Assert.True(BreaksAfterMove(chapter, "a", "hall"));
    }

    [Fact]
    public void 길이_없는_에피소드와_입구가_늘지_않는_이동은_받는다()
    {
        ChapterDocument chapter = Chapter(("opening", "root"), ("opening", "a"), ("opening", "tail"), ("hall", "gate"), ("hall", "loose"));
        chapter.Edges.Add(new ChapterEdge("root", "a", "이쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("a", "gate", "저쪽", null, null));
        chapter.Edges.Add(new ChapterEdge("a", "tail", "끝", null, null));

        // 길이 없는 에피소드.
        Assert.Null(SceneMoveGuard.Refusal(chapter, "loose", "opening"));
        Assert.False(BreaksAfterMove(chapter, "loose", "opening"));

        // 장면의 끝 에피소드(들어오는 길만 있다)를 새 장면으로 — 그 장면의 하나뿐인 입구가 된다.
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
        Assert.False(BreaksAfterMove(chapter, "side", "annex"));
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static bool BreaksAfterMove(ChapterDocument chapter, string episodeId, string sceneId)
    {
        var moved = new ChapterDocument { ChapterId = chapter.ChapterId };
        moved.Episodes.AddRange(chapter.Episodes.Select(episode =>
            episode.EpisodeId == episodeId ? episode with { SceneId = sceneId } : episode));
        moved.Edges.AddRange(chapter.Edges);
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
