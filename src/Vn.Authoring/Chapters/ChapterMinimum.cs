namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>챕터에는 장면 하나 · 에피소드 하나가 늘 남는다</b>(2026-11-18 소유자 — 「그 최소 1개 장면과 에피소드는 삭제가 되지 않음으로써 챕터내 1개의
/// 장면과 에피소드는 챕터 자체를 지우지 않는 한 유지되도록」). 「챕터 추가」가 그 둘을 함께 세우고, 지우기는 마지막 것을 받지 않는다 — 챕터째
/// 지우기(<see cref="ChapterDeleter"/>)는 그대로다. 에피소드는 늘 장면에 속하므로 에피소드가 하나 남으면 장면도 하나 남는다.
///
/// 읽기 규칙은 아니다 — 그 전에 만든 빈 챕터는 그대로 열리고(진행 JSON을 내지 않을 뿐이다), 되돌리기는 지난 상태로 그대로 돌아간다.
/// 규칙은 여기 한 곳이다 — 에피소드 지우기(<see cref="EpisodeDeleter"/>), 장면 지우기(<c>ProjectEditor.RemoveScene</c>), 화면의 [삭제] · Delete 키 ·
/// 하이어아키 차림표가 모두 이것을 묻는다.
/// </summary>
public static class ChapterMinimum
{
    /// <summary>마지막 에피소드를 지우려 할 때의 까닭.</summary>
    public const string LastEpisodeReason =
        "챕터의 마지막 에피소드는 지울 수 없습니다 — 챕터에는 장면 하나 · 에피소드 하나가 늘 남습니다. 챕터째 지우려면 챕터를 고르고 [챕터 삭제]를 누르세요.";

    /// <summary>마지막 장면을 지우려 할 때의 까닭.</summary>
    public const string LastSceneReason =
        "챕터의 마지막 장면은 지울 수 없습니다 — 챕터에는 장면 하나 · 에피소드 하나가 늘 남습니다. 챕터째 지우려면 챕터를 고르고 [챕터 삭제]를 누르세요.";

    /// <summary>그 에피소드가 챕터에 남은 하나뿐인 에피소드인가.</summary>
    public static bool IsLastEpisode(IReadOnlyList<ChapterEpisode> episodes, string episodeId)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        return episodes.Count == 1 && string.Equals(episodes[0].EpisodeId, episodeId, StringComparison.Ordinal);
    }

    /// <summary>그 장면이 챕터에 남은 하나뿐인 장면인가 — 챕터의 에피소드가 모두 그 장면에 있다.</summary>
    public static bool IsLastScene(IReadOnlyList<ChapterEpisode> episodes, string sceneId)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        return episodes.Count > 0 && episodes.All(episode => string.Equals(episode.SceneId, sceneId, StringComparison.Ordinal));
    }
}
