using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>자동 길 다섯 규칙이 툴 소유 챕터에서도 돈다</b> (V1 · `docs/plans/R6.md` §9).
///
/// ⛔ 이 다섯은 옛 워크북 리더(<c>ChapterWorkbookReader</c>) 안에만 있었다. R-F로 판의 주인이
/// 워크북에서 프로젝트로 넘어가자 읽는 길이 사라졌고, 그와 함께 <b>조용히 안 돌게 됐다</b>.
/// 내보내기 관문은 코어가 여전히 거부하니 잘못된 것이 나가지는 않았지만, 기획자는
/// <b>고치는 중에</b> 못 봤다 — 관문에서야 알면 어디를 고칠지 되짚어야 한다.
///
/// ⚠ 그러니 이 파일이 재는 것은 <see cref="ChapterDocument"/>다.
/// </summary>
public sealed class ChapterAutoEdgeCheckTests
{
    [Fact]
    public void 자동_길이_장면을_건너면_저작_중에_뜬다()
    {
        // ⛔ 회귀의 본체. 장면은 확정·롤백의 경계라, 입력 없이 그 경계를 넘으면 되돌아갈
        //    자리가 사라진다 — 코어의 `VerifyAuto`와 같은 규칙이다.
        ChapterDocument chapter = Chapter(("opening", "ep01"), ("classroom", "ep02"));
        chapter.Edges.Add(Auto("ep01", "ep02"));

        Assert.Contains(
            Errors(chapter),
            item => item.Code == ChapterDiagnosticCode.AutoEdgeCrossesScene);
    }

    [Fact]
    public void 같은_장면_안의_외길이면_아무_말도_없다()
    {
        ChapterDocument chapter = Chapter(("opening", "ep01"), ("opening", "ep02"));
        chapter.Edges.Add(Auto("ep01", "ep02"));

        Assert.DoesNotContain(
            Errors(chapter),
            item => item.Code.ToString().StartsWith("AutoEdge", StringComparison.Ordinal));
    }

    [Fact]
    public void 갈림길이면_자동일_수_없다()
    {
        ChapterDocument chapter = Chapter(("opening", "ep01"), ("opening", "ep02"), ("opening", "ep03"));
        chapter.Edges.Add(Auto("ep01", "ep02"));
        chapter.Edges.Add(new ChapterEdge("ep01", "ep03", "이쪽으로", null, null));

        Assert.Contains(
            Errors(chapter),
            item => item.Code == ChapterDiagnosticCode.AutoEdgeHasSiblings);
    }

    [Fact]
    public void 관문과_스탯변화와_문구는_자동과_섞이지_않는다()
    {
        // 셋을 한 간선에 몰아 둔다 — 하나만 걸리고 나머지가 조용하면 그것도 회귀다.
        ChapterDocument chapter = Chapter(("opening", "ep01"), ("opening", "ep02"));

        chapter.Edges.Add(Auto("ep01", "ep02") with
        {
            OptionLabel = "따라간다",
            ConditionLabel = "믿음충분",
            StatChanges = [new StatDelta("trust", 1)]
        });

        List<ChapterDiagnosticCode> codes = Errors(chapter).Select(item => item.Code).ToList();

        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasConditions, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasStatChanges, codes);
        Assert.Contains(ChapterDiagnosticCode.AutoEdgeHasChoiceLabel, codes);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static List<ChapterDiagnostic> Errors(ChapterDocument chapter) =>
        [.. chapter.ToGraphModel("ch01").Errors];

    private static ChapterEdge Auto(string from, string to) =>
        new(from, to, null, null, null) { Auto = true };

    private static ChapterDocument Chapter(params (string SceneId, string EpisodeId)[] episodes)
    {
        var chapter = new ChapterDocument { ChapterId = "ch01" };

        foreach ((string sceneId, string episodeId) in episodes)
        {
            chapter.Episodes.Add(new ChapterEpisode(
                episodeId, episodeId, string.Empty, episodeId, 0, 0, null)
            {
                SceneId = sceneId
            });
        }

        return chapter;
    }
}
