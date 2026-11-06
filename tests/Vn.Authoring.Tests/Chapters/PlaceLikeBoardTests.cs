using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>[대본] 판에 새로 선 카드의 챕터 그래프 자리</b> — 같은 챕터의 가장 가까운 카드를 기준으로, 판 위의 거리를 자동 정렬과 같은
/// 배율(옆 320 → 220 · 줄 400 → 170)로 줄여 그 에피소드 곁에 놓는다. 기준이 없으면 원점이고, 부딪치면 오른쪽으로 비킨다.
/// </summary>
public sealed class PlaceLikeBoardTests
{
    [Fact]
    public void 챕터의_첫_카드는_원점이다() =>
        Assert.Equal((0, 0), ChapterBranchPlanner.PlaceLikeBoard((2600, 1800), []));

    [Fact]
    public void 가장_가까운_카드의_에피소드_곁에_판의_거리를_줄여_놓는다()
    {
        ((double, double), (double, double))[] neighbors =
        [
            ((0, 0), (500, 300)),        // 먼 카드
            ((2600, 1800), (40, 0))      // 가까운 카드 — 이 에피소드가 기준이다
        ];

        // 판에서 기준 카드의 한 줄 아래 · 한 열 오른쪽.
        Assert.Equal((260, 170), ChapterBranchPlanner.PlaceLikeBoard((2920, 2200), neighbors));
    }

    [Fact]
    public void 기준_에피소드와_부딪치면_오른쪽으로_비킨다()
    {
        ((double, double), (double, double))[] neighbors = [((1000, 1000), (0, 0))];

        // 판에서 거의 같은 자리 — 그대로면 기준 에피소드에 포개진다.
        Assert.Equal((220, 0), ChapterBranchPlanner.PlaceLikeBoard((1010, 1000), neighbors));
    }
}
