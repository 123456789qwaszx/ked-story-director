using Vn.Authoring.Chapters;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>[구성] 탭 인스펙터 머리글 「장면 - 제목」</b> — 제목이 비면 EpisodeId다(에피소드는 늘 장면에 속한다 — ⛔ 장면ID가 없으면 제목만이던
/// 갈래는 2026-11-18에 걷혔다 · old-shapes O7).
/// </summary>
public sealed class EpisodeSceneAndTitleTests
{
    [Theory]
    [InlineData("간판 완성", "교실 - 간판 완성")]
    [InlineData(" 간판 완성 ", "교실 - 간판 완성")]
    [InlineData("", "교실 - ep01")]
    public void 장면_제목이고_제목이_비면_EpisodeId다(string title, string expected)
    {
        var episode = new ChapterEpisode("ep01", title, "01", 0, 0, Memo: null) { SceneId = "교실" };

        Assert.Equal(expected, episode.SceneAndTitle);
    }
}
