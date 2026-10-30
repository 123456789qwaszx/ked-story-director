using Avalonia.Controls;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App;

/// <summary>
/// <b>하이어아키에서 고치기</b> (2026-10-30 HY4 — <c>docs/plans/hierarchy.md</c>) — 우클릭 차림표(챕터 · 장면 · 에피소드
/// 추가, 장면 풀기, 에피소드 지우기)와 두 번 눌러 이름 바꾸기. 두 탭의 하이어아키가 이 한 벌을 쓴다.
///
/// ⚠ 규칙은 여기 없다 — 만들기는 <c>ProjectEditor</c>, 지우기는 <see cref="EpisodeDeleter"/>, 개명은
/// <see cref="EpisodeRenamer"/> · <see cref="ChapterRenamer"/>. 이 자리는 <b>누른 줄이 무엇을 어디에 하자는지</b>를 풀고
/// 결과를 말한다.
/// </summary>
public partial class MainWindow
{
    private void OnHierarchyCommand(HierarchyCommand command, HierarchyRow row, Control anchor)
    {
        if (command == HierarchyCommand.AddChapter)
        {
            ShowAddChapterFlyout(anchor);
            return;
        }

        if (row.ChapterId is not { } chapterId ||
            _chapters.FirstOrDefault(entry => string.Equals(entry.ChapterId, chapterId, StringComparison.Ordinal))
                is not { } chapter)
        {
            return;
        }

        IReadOnlyList<ChapterScene> scenes = ChapterSceneGrouping.Of(chapter.Model);
        ChapterScene? scene = scenes.FirstOrDefault(item =>
            string.Equals(item.SceneId, row.SceneId, StringComparison.Ordinal));

        switch (command)
        {
            case HierarchyCommand.ChapterMenu:
                ShowChapterContextFlyout(anchor, chapterId);
                break;

            case HierarchyCommand.AddScene:
            {
                // 장면은 에피소드가 들어야 있다 — 새 장면은 첫 에피소드와 함께 선다. 그 에피소드는 누른 에피소드(없으면
                // 챕터의 마지막 에피소드)에서 잇는다: 떨어진 섬을 만들지 않는다(그 간선이 곧 장면의 들어오는 자리다).
                string sceneId = NextSceneId(chapter.Model);
                CreateEpisode(chapter, after: row.EpisodeId ?? LastEpisodeOf(chapter.Model), sceneId);
                break;
            }

            case HierarchyCommand.AddEpisode:
            {
                (string? after, string? sceneId) = row.Kind switch
                {
                    HierarchyKind.Episode => (row.EpisodeId, RealSceneId(chapter.Model.FindEpisode(row.EpisodeId!)?.SceneId)),
                    HierarchyKind.Scene when scene is not null => (scene.Episodes[^1].EpisodeId,
                        scene.IsDefault ? null : scene.SceneId),
                    _ => (LastEpisodeOf(chapter.Model), null)
                };

                CreateEpisode(chapter, after, sceneId);
                break;
            }

            case HierarchyCommand.DeleteScene when scene is not null && !scene.IsDefault:
            {
                string[] episodeIds = scene.Episodes.Select(episode => episode.EpisodeId).ToArray();
                _session.Editor.UpdateEpisodeScenes(chapterId, episodeIds, string.Empty);
                _session.SetStatus(
                    $"장면 '{scene.SceneId}'를 풀었습니다 — 에피소드 {episodeIds.Length}개는 남고 저마다 제 장면입니다. " +
                    "되돌리기(Ctrl+Z)로 돌아옵니다.");
                break;
            }

            case HierarchyCommand.DeleteEpisode when row.EpisodeId is { } episodeId:
            {
                EpisodeDeleter.Result result = EpisodeDeleter.Delete(_session.Editor, chapterId, episodeId);
                _session.SetStatus(result.Ok ? result.Describe(episodeId) : result.Failure!);
                break;
            }
        }
    }

    private void OnHierarchyRename(HierarchyRow row, string name)
    {
        switch (row.Kind)
        {
            case HierarchyKind.Chapter when row.ChapterId is { } chapterId && name.Length > 0:
                RenameChapter(chapterId, name);
                break;

            case HierarchyKind.Scene when row.ChapterId is { } chapterId:
            {
                ChapterEntry? chapter = _chapters.FirstOrDefault(entry =>
                    string.Equals(entry.ChapterId, chapterId, StringComparison.Ordinal));
                string[] episodeIds = chapter is null
                    ? []
                    : chapter.Model.Episodes
                        .Where(episode => string.Equals(episode.EffectiveSceneId, row.SceneId, StringComparison.Ordinal))
                        .Select(episode => episode.EpisodeId)
                        .ToArray();

                if (episodeIds.Length == 0)
                {
                    return;
                }

                _session.Editor.UpdateEpisodeScenes(chapterId, episodeIds, name);
                _session.SetStatus(name.Length == 0
                    ? $"장면ID를 걷었습니다 — 에피소드 {episodeIds.Length}개가 저마다 제 장면입니다."
                    : $"장면 이름을 '{name}'로 바꿨습니다 — 에피소드 {episodeIds.Length}개.");
                break;
            }

            case HierarchyKind.Episode when row.ChapterId is { } chapterId && row.EpisodeId is { } oldId && name.Length > 0:
            {
                EpisodeRenamer.Result result = EpisodeRenamer.Rename(_session.Editor, chapterId, oldId, name);

                if (!result.Renamed)
                {
                    _session.SetStatus(result.Failure!);
                    return;
                }

                // 카드는 같은 카드다(신원표만 바뀌었다) — 그것을 고르고 있었으면 챕터 그래프도 새 이름으로 고른다.
                if (EpisodeNaming.CardFor(_session.Project, chapterId, name) is { } card &&
                    string.Equals(_session.SelectedNodeId, card.Id, StringComparison.Ordinal))
                {
                    ChapterGraph.ShowEpisode(chapterId, name);
                }

                _session.SetStatus($"'{oldId}' → '{name}' 개명했습니다. 간선·픽스처·대사 노드가 함께 따라갔습니다.");
                break;
            }
        }
    }

    /// <summary>
    /// 에피소드를 하나 더한다 — <paramref name="after"/>에서 간선을 달고(없으면 홀로), <paramref name="sceneId"/>가 있으면 그
    /// 장면에. 되돌리기 한 칸(<c>Batch</c>). 더한 에피소드를 두 탭에서 고른다.
    /// </summary>
    private void CreateEpisode(ChapterEntry chapter, string? after, string? sceneId)
    {
        string chapterId = chapter.ChapterId;
        string episodeId = _session.Editor.NextEpisodeId(chapterId);
        ChapterGraphModel model = chapter.Model;

        _session.Editor.Batch(() =>
        {
            if (after is not null && model.FindEpisode(after) is not null)
            {
                (double x, double y) = ChapterBranchPlanner.SuggestPlacement(model, after);
                _session.Editor.AddNextEpisode(
                    chapterId, after, episodeId, title: string.Empty, x, y, optionLabel: ChapterGraphView.DefaultOptionLabel);
            }
            else
            {
                double x = model.Episodes.Count == 0 ? 0 : model.Episodes.Max(episode => episode.X) + ChapterBranchPlanner.SiblingSpacing;
                _session.Editor.AddEpisode(chapterId, episodeId, title: string.Empty, x, 0);
            }

            if (sceneId is not null)
            {
                _session.Editor.UpdateEpisode(chapterId, episodeId, sceneId: sceneId);
            }
        });

        if (EpisodeNaming.CardFor(_session.Project, chapterId, episodeId) is { } card)
        {
            _session.Select(card.Id);
        }

        _session.SetStatus(
            $"'{episodeId}'을 더했습니다" + (sceneId is null ? string.Empty : $"(장면 '{sceneId}')") +
            ". 이름은 하이어아키에서 두 번 눌러 고칩니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
    }

    private static string? LastEpisodeOf(ChapterGraphModel model) =>
        model.Episodes.Count > 0 ? model.Episodes[^1].EpisodeId : null;

    /// <summary>적어 둔 장면ID — 비었으면 null(미지정).</summary>
    private static string? RealSceneId(string? sceneId) => string.IsNullOrWhiteSpace(sceneId) ? null : sceneId.Trim();

    /// <summary>그 챕터에서 아직 안 쓰인 장면 자리표시 — <c>scene01</c>, <c>scene02</c>… 사람이 두 번 눌러 고친다.</summary>
    private static string NextSceneId(ChapterGraphModel model)
    {
        var taken = new HashSet<string>(model.Episodes.Select(episode => episode.EffectiveSceneId), StringComparer.Ordinal);
        int number = 1;

        while (taken.Contains($"scene{number:D2}"))
        {
            number++;
        }

        return $"scene{number:D2}";
    }
}
