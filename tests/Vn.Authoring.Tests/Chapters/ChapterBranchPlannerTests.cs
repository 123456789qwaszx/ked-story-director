using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 깊이 기반 분기 배치 (챕터 v2 8단계 · 2026-10-29 D3부터 깊이가 아래로 간다). 소유자 피드백 그대로의 사례를 고정한다:
/// "root에서 뻗어나온 두 에피소드 둘이서 다시 연결되는" 합류가 터무니없어 보이지 않아야 하고,
/// 서로 다른 부모의 자식이 겹치지 않아야 한다.
/// </summary>
public sealed class ChapterBranchPlannerTests
{
    private static ChapterEpisode Episode(string id, double x, double y) =>
        new(id, id, "10", $"Story_{id}", x, y,
            Memo: null, AllowUnreachable: false);

    private static ChapterEdge Edge(string from, string to) =>
        new(from, to, OptionLabel: null, ConditionLabel: null,
            LockedMessage: null);

    private static ChapterGraphModel Model(
        IReadOnlyList<ChapterEpisode> episodes, IReadOnlyList<ChapterEdge> edges) =>
        new("ch", "ch", episodes, edges, [], [], [], []);

    // ── 연출 그래프를 본뜬 배치 (2026-10-30) ───────────────────────────────────

    [Fact]
    public void 연출_그래프의_자리를_챕터_간격으로_줄여_본뜬다()
    {
        // 소유자: "연출 그래프에 배치된 노드들을 참고해서 그와 거의 같은 형태로". 연출 그래프의 열 하나(320)는 챕터의
        // 옆 칸(220), 줄 하나(400)는 깊이 한 칸(170)이다. 왼위가 (0,0)이다.
        ChapterGraphModel model = Model(
            [Episode("root", 0, 0), Episode("a", 0, 0), Episode("b", 0, 0), Episode("c", 0, 0)],
            [Edge("root", "a"), Edge("root", "b")]);

        IReadOnlyDictionary<string, (double X, double Y)> placed = ChapterBranchPlanner.LayoutLikeBoard(model, new Dictionary<string, (double X, double Y)>
        {
            ["root"] = (100, 100), ["a"] = (420, 500), ["b"] = (-220, 500), ["c"] = (740, 100)
        });

        Assert.Equal((220, 0), placed["root"]);
        Assert.Equal((440, 170), placed["a"]);
        Assert.Equal((0, 170), placed["b"]);
        Assert.Equal((660, 0), placed["c"]);
    }

    [Fact]
    public void 바짝_붙은_카드는_겹치지_않게_오른쪽으로_비킨다()
    {
        // 연출 카드(폭 210)를 붙여 놓으면 줄인 자리에서 챕터 카드(폭 190)가 겹친다 — 왼쪽 이웃의 오른쪽으로 비키고,
        // 그 오른쪽 이웃도 따라 비킨다. 좌우 순서는 연출 그래프 그대로다.
        ChapterGraphModel model = Model(
            [Episode("root", 0, 0), Episode("a", 0, 0), Episode("b", 0, 0), Episode("under", 0, 0)],
            []);

        IReadOnlyDictionary<string, (double X, double Y)> placed = ChapterBranchPlanner.LayoutLikeBoard(model, new Dictionary<string, (double X, double Y)>
        {
            ["root"] = (0, 0), ["a"] = (230, 10), ["b"] = (460, 0), ["under"] = (230, 400)
        });

        Assert.Equal((0, 0), placed["root"]);
        Assert.Equal(220, placed["a"].X);
        Assert.Equal(440, placed["b"].X);
        Assert.Equal(10 * 170 / 400.0, placed["a"].Y, 3);   // 세로 자리는 비키지 않는다
        Assert.Equal(230 * 220 / 320.0, placed["under"].X, 3);   // 아래 줄은 부딪치지 않아 그대로
        Assert.Equal(170, placed["under"].Y);
    }

    [Fact]
    public void 연출_그래프에_카드가_없는_에피소드는_맨_아래_줄에_선다()
    {
        ChapterGraphModel model = Model(
            [Episode("root", 0, 0), Episode("a", 0, 0), Episode("stray", 0, 0), Episode("stray2", 0, 0)],
            [Edge("root", "a")]);

        IReadOnlyDictionary<string, (double X, double Y)> placed = ChapterBranchPlanner.LayoutLikeBoard(model, new Dictionary<string, (double X, double Y)>
        {
            ["root"] = (0, 0), ["a"] = (0, 400)
        });

        Assert.Equal((0, 170 + 340), placed["stray"]);
        Assert.Equal((220, 170 + 340), placed["stray2"]);
    }

    [Fact]
    public void 연출_그래프에_카드가_하나도_없으면_흐름_순서_배치다()
    {
        ChapterGraphModel model = Model(
            [Episode("root", 0, 0), Episode("a", 0, 0), Episode("b", 0, 0)],
            [Edge("root", "a"), Edge("a", "b")]);

        Assert.Equal(
            ChapterBranchPlanner.Layout(model).OrderBy(pair => pair.Key, StringComparer.Ordinal),
            ChapterBranchPlanner.LayoutLikeBoard(model, new Dictionary<string, (double X, double Y)>())
                .OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void 합류_노드의_깊이는_가장_깊은_부모_기준이다()
    {
        // root → a → c,  root → b → (b2) → c  — c는 depth 2가 아니라 3이어야
        // b2 → c 간선이 뒤로 꺾이지 않는다.
        ChapterGraphModel model = Model(
            [Episode("root", 0, 0), Episode("a", 220, 0), Episode("b", 220, 110),
             Episode("b2", 440, 110), Episode("c", 440, 0)],
            [Edge("root", "a"), Edge("root", "b"), Edge("b", "b2"),
             Edge("a", "c"), Edge("b2", "c")]);

        IReadOnlyDictionary<string, int> depths = ChapterBranchPlanner.Depths(model);

        Assert.Equal(0, depths["root"]);
        Assert.Equal(1, depths["a"]);
        Assert.Equal(1, depths["b"]);
        Assert.Equal(2, depths["b2"]);
        Assert.Equal(3, depths["c"]);
    }

    [Fact]
    public void 서로_다른_부모의_자식이_같은_줄에서_겹치지_않는다()
    {
        // 옛 결함 그대로의 사례: 같은 깊이의 두 부모(a, b)가 각자 자식을 만들면
        // 옛 규칙(부모 자리 + 형제 수 × 간격)으로는 정확히 같은 자리에 겹쳤다.
        var episodes = new List<ChapterEpisode>
        {
            Episode("root", 0, 0), Episode("a", 0, 170), Episode("b", 220, 170)
        };
        var edges = new List<ChapterEdge> { Edge("root", "a"), Edge("root", "b") };

        (double ax, double ay) = ChapterBranchPlanner.SuggestPlacement(
            Model(episodes, edges), "a");
        episodes.Add(Episode("a1", ax, ay));
        edges.Add(Edge("a", "a1"));

        (double bx, double by) = ChapterBranchPlanner.SuggestPlacement(
            Model(episodes, edges), "b");

        Assert.Equal(ay, by);                                  // 같은 깊이 → 같은 줄
        Assert.True(Math.Abs(ax - bx) >= ChapterBranchPlanner.SiblingSpacing,
            $"두 자식이 겹쳤다: a1의 X={ax}, b1의 X={bx}");
    }

    [Fact]
    public void 제안_줄은_깊이가_정한다_부모가_위에_있어도()
    {
        // 부모가 손으로 위로 옮겨져 있어도 새 노드는 자기 깊이 줄로 간다 —
        // depth 구분이 배치에서 무너지지 않는다.
        ChapterGraphModel model = Model(
            [Episode("root", 0, 0), Episode("mid", 110, 40)],   // mid를 root 근처로 끌어다 둠
            [Edge("root", "mid")]);

        (double x, double y) = ChapterBranchPlanner.SuggestPlacement(model, "mid");

        Assert.Equal(2 * ChapterBranchPlanner.DepthSpacing, y);  // depth(mid)=1 → 줄 2
        Assert.Equal(110d, x);                                   // 부모 바로 아래에서 시작한다
    }

    [Fact]
    public void 순환이_있어도_깊이_계산이_멈춘다()
    {
        ChapterGraphModel model = Model(
            [Episode("root", 0, 0), Episode("a", 220, 0), Episode("b", 440, 0)],
            [Edge("root", "a"), Edge("a", "b"), Edge("b", "a")]);

        IReadOnlyDictionary<string, int> depths = ChapterBranchPlanner.Depths(model);

        Assert.True(depths["a"] < model.Episodes.Count);
        Assert.True(depths["b"] < model.Episodes.Count);
    }

    [Fact]
    public void 화면_배치는_줄이_깊이고_고아는_맨_아래_줄에_선다()
    {
        // 이 배치는 저장된 X·Y와 무관하게 흐름이 자리를 정한다(v3 — 지금은 처음 심을 때만 쓴다).
        ChapterGraphModel model = Model(
            [Episode("root", 10, 20), Episode("a", 900, -50), Episode("b", 30, 400),
             Episode("c", 500, 0), Episode("orphan", 777, 888)],
            [Edge("root", "a"), Edge("root", "b"), Edge("a", "c"), Edge("b", "c")]);

        IReadOnlyDictionary<string, (double X, double Y)> placed =
            ChapterBranchPlanner.Layout(model);

        // 줄 = 깊이 (원점 기준). 합류 c는 줄 2.
        Assert.Equal((0d, 0d), placed["root"]);
        Assert.Equal(ChapterBranchPlanner.DepthSpacing, placed["a"].Y);
        Assert.Equal(ChapterBranchPlanner.DepthSpacing, placed["b"].Y);
        Assert.Equal(2 * ChapterBranchPlanner.DepthSpacing, placed["c"].Y);

        // 같은 줄 안 순서 = 에피소드를 적은 순서 (a가 b보다 먼저 적힘).
        Assert.Equal(0d, placed["a"].X);
        Assert.Equal(ChapterBranchPlanner.SiblingSpacing, placed["b"].X);

        // 간선 없는 고아는 그래프 아래 줄에 따로 선다 — 겹쳐 숨기지 않는다.
        Assert.True(placed["orphan"].Y > placed["c"].Y + ChapterBranchPlanner.DepthSpacing,
            $"고아가 그래프와 겹친다: Y={placed["orphan"].Y}");
    }
}
