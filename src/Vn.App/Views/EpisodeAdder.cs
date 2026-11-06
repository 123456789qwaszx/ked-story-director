using Vn.App.Services;
using Vn.Authoring.Chapters;

namespace Vn.App.Views;

/// <summary>
/// <b>에피소드를 하나 더한다</b> — [대본] 탭 하이어아키의 우클릭 「에피소드 추가」 · 「장면 추가」가 부른다. 창구는 이것 하나다
/// (두 벌이면 어느 쪽으로 더했느냐에 따라 간선 · 자리가 달라진다).
///
/// <paramref name="after"/>가 있으면 그 다음으로 간선(문구 <see cref="DefaultOptionLabel"/>)까지 잇고, 자리는 그 곁이다
/// (<see cref="ChapterBranchPlanner.SuggestPlacement"/>). 없으면 홀로 맨 오른쪽에 선다. <paramref name="sceneId"/>가 있으면 그
/// 장면에. 에피소드 · 카드 · 대본 · 간선 · 장면이 한 번의 변경이다(되돌리기 한 칸).
/// </summary>
internal static class EpisodeAdder
{
    /// <summary>
    /// 툴이 길을 놓을 때 넣어 주는 문구. 빈 문구는 오류이므로 무언가는 있어야 하고, 기획자가 바로 고쳐 쓸 수 있는 가장 짧은 말이다.
    /// </summary>
    internal const string DefaultOptionLabel = "계속";

    /// <returns>더한 에피소드의 Id — 자리표시 Id(<c>new01</c>…)이고, 사람이 [구성] 탭 인스펙터의 에피소드ID 칸에서 고친다.</returns>
    public static string Add(AuthoringSession session, string chapterId, string? after, string? sceneId)
    {
        ArgumentNullException.ThrowIfNull(session);

        ChapterDocument chapter = session.Editor.FindChapter(chapterId)
            ?? throw new InvalidOperationException($"챕터 '{chapterId}'가 없습니다.");
        ChapterGraphModel model = chapter.ToGraphModel(chapterId, session.Definition);
        string episodeId = session.Editor.NextEpisodeId(chapterId);

        session.Editor.Batch(() =>
        {
            if (after is not null && model.FindEpisode(after) is not null)
            {
                (double x, double y) = ChapterBranchPlanner.SuggestPlacement(model, after);
                session.Editor.AddNextEpisode(
                    chapterId, after, episodeId, title: string.Empty, x, y, optionLabel: DefaultOptionLabel);
            }
            else
            {
                double x = model.Episodes.Count == 0
                    ? 0
                    : model.Episodes.Max(episode => episode.X) + ChapterBranchPlanner.SiblingSpacing;
                session.Editor.AddEpisode(chapterId, episodeId, title: string.Empty, x, 0);
            }

            if (sceneId is not null)
            {
                session.Editor.UpdateEpisode(chapterId, episodeId, sceneId: sceneId);
            }
        });

        return episodeId;
    }
}
