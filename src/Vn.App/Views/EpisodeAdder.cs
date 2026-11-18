using Vn.App.Services;
using Vn.Authoring.Chapters;

namespace Vn.App.Views;

/// <summary>
/// <b>에피소드를 하나 더한다</b> — [대본] 탭 하이어아키의 장면 줄 우클릭 「에피소드 추가」와 「장면 추가」가 부른다. 창구는 이것 하나다
/// (두 벌이면 어느 쪽으로 더했느냐에 따라 자리가 달라진다).
///
/// 에피소드는 <paramref name="sceneId"/>의 장면에 <b>길 없이</b> 선다(챕터 그래프에서는 맨 오른쪽) — 에피소드는 서로 떨어진 내용이고, 잇기는
/// 사람이 [대본] 판에서 선택지 칸을 끌어 정한다(2026-11-18 소유자). 에피소드 · 카드 · 대본이 한 번의 변경이다(되돌리기 한 칸).
/// ⛔ 그 전에는 앞 에피소드(누른 에피소드 줄, 장면 줄이면 그 장면의 마지막 에피소드)에서 문구 「계속」 길을 이어 붙였다 — 같은 에피소드에서
/// 여러 번 더하면 선택지가 셋을 넘어 늘어났다.
/// </summary>
internal static class EpisodeAdder
{
    /// <returns>더한 에피소드의 Id — 자리표시 Id(<c>ch01_scene01_new01</c>…)이고, 사람이 [구성] 탭 인스펙터의 에피소드ID 칸에서 고친다.</returns>
    public static string Add(AuthoringSession session, string chapterId, string sceneId)
    {
        ArgumentNullException.ThrowIfNull(session);

        ChapterDocument chapter = session.Editor.FindChapter(chapterId)
            ?? throw new InvalidOperationException($"챕터 '{chapterId}'가 없습니다.");
        string episodeId = session.Editor.NextEpisodeId(chapterId, sceneId);
        double x = chapter.Episodes.Count == 0
            ? 0
            : chapter.Episodes.Max(episode => episode.X) + ChapterBranchPlanner.SiblingSpacing;

        session.Editor.AddEpisode(chapterId, episodeId, title: NewNames.Episode, x, 0, sceneId);
        return episodeId;
    }
}
