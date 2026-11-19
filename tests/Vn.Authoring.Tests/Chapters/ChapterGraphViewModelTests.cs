using Vn.Authoring.Chapters;
using Vn.Authoring.Serialization;
using Vn.Authoring.Tests.Fixtures;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// G4 수용 기준 중 뷰가 아니라 <b>데이터가 지는 부분</b> — 챕터를 그래프 모델로 펴고 배치를
/// 계산하는 일이 정말 읽기뿐인가. 배치 자체의 규칙(깊이 열)은 <c>ChapterBranchPlannerTests</c>가 진다.
///
/// (2026-10-29 · D2: 챕터 폴더를 훑던 검사(폴더 자리·잠금 파일·못 읽는 워크북)는 엑셀 연동과 함께 걷혔다.)
/// </summary>
public sealed class ChapterGraphViewModelTests
{
    // ── 읽기전용 (Gate A 4번) ───────────────────────────────────────────────

    [Fact]
    public void 펴고_배치를_계산해도_챕터_값이_바뀌지_않는다()
    {
        ChapterDocument chapter = SampleChapters.Load(SampleChapters.Corridor);
        string before = Snapshot(chapter);

        // 펴기 → 배치 계산 → 다시 펴기. 뷰가 한 프레임에 하는 일 전부다.
        for (int pass = 0; pass < 3; pass++)
        {
            ChapterGraphModel model = chapter.ToGraphModel(chapter.ChapterId);
            _ = ChapterBranchPlanner.Layout(model);
        }

        Assert.Equal(before, Snapshot(chapter));
    }

    [Fact]
    public void 같은_챕터를_두_번_펴면_같은_위치가_나온다()
    {
        ChapterGraphModel first = SampleChapters.Model(SampleChapters.Corridor);
        ChapterGraphModel second = SampleChapters.Model(SampleChapters.Corridor);

        Assert.Equal(
            first.Episodes.Select(episode => (episode.EpisodeId, episode.X, episode.Y)),
            second.Episodes.Select(episode => (episode.EpisodeId, episode.X, episode.Y)));

        Assert.Equal(
            ChapterBranchPlanner.Layout(first).OrderBy(pair => pair.Key, StringComparer.Ordinal),
            ChapterBranchPlanner.Layout(second).OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>챕터의 저작 값 전부 — 프로젝트에 저장되는 모양 그대로.</summary>
    private static string Snapshot(ChapterDocument chapter) =>
        ChapterDocumentJson.Write([chapter])!.ToJsonString();
}
