using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>[구성] 탭 인스펙터 머리글 「장면 - 제목」</b> — 장면ID가 없으면 제목만, 제목이 비면 EpisodeId다.
/// </summary>
public sealed class EpisodeSceneAndTitleTests
{
    [Theory]
    [InlineData("교실", "간판 완성", "교실 - 간판 완성")]
    [InlineData(" 교실 ", " 간판 완성 ", "교실 - 간판 완성")]
    [InlineData("교실", "", "교실 - ep01")]
    [InlineData(null, "간판 완성", "간판 완성")]
    [InlineData("  ", "", "ep01")]
    public void 장면이_있으면_장면_제목이고_없으면_제목만이다(string? sceneId, string title, string expected)
    {
        var episode = new ChapterEpisode("ep01", title, "01", "ep01", 0, 0, Memo: null) { SceneId = sceneId };

        Assert.Equal(expected, episode.SceneAndTitle);
    }
}
