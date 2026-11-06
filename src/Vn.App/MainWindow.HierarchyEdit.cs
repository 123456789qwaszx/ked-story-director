using Avalonia.Controls;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App;

/// <summary>
/// <b>하이어아키에서 고치기</b> — 우클릭 차림표(챕터 · 장면 · 에피소드
/// 추가, 장면 풀기, 에피소드 지우기)와 두 번 눌러 이름 바꾸기. 두 탭의 하이어아키가 이 한 벌을 쓴다.
///
/// 주의: 규칙은 여기 없다 — 만들기는 <c>ProjectEditor</c>, 지우기는 <see cref="EpisodeDeleter"/>, 개명은
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
                // 장면은 에피소드가 들어야 있다 — 새 장면은 첫 에피소드와 함께 선다.
                // 그 에피소드에 간선을 저절로 잇지 않는다 — 새 장면이 어디서 들어오는지는 사람이 잇는다.
                CreateEpisode(chapter, after: null, NextSceneId(chapter.Model));
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

            case HierarchyCommand.DetachFromScene when row.EpisodeId is { } episodeId:
                DetachFromScene(chapterId, episodeId);
                break;

            case HierarchyCommand.DeleteEpisode when row.EpisodeId is { } episodeId:
            {
                EpisodeDeleter.Result result = EpisodeDeleter.Delete(_session.Editor, chapterId, episodeId);
                _session.SetStatus(result.Ok ? result.Describe(episodeId) : result.Failure!);
                break;
            }
        }
    }

    private void OnHierarchyRename(StoryHierarchy tree, HierarchyRow row, string name)
    {
        switch (row.Kind)
        {
            // [대본] 하이어아키의 에피소드 줄은 보이는 제목이다 — EpisodeId는 그대로 두고 제목만 고친다.
            case HierarchyKind.Episode when tree.ShowEpisodeTitles && row.ChapterId is { } chapterId && row.EpisodeId is { } episodeId:
                _session.Editor.UpdateEpisode(chapterId, episodeId, title: name);
                _session.SetStatus(name.Length == 0
                    ? $"제목을 비웠습니다 — 에피소드ID '{episodeId}'가 보입니다."
                    : $"제목을 '{name}'(으)로 바꿨습니다. 에피소드ID '{episodeId}'는 그대로입니다.");
                break;

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
                RenameEpisode(chapterId, oldId, name);
                break;
        }
    }

    /// <summary>
    /// 에피소드 개명 — [구성] 탭 하이어아키(두 번 눌러)의 길이다(규칙은
    /// <see cref="EpisodeRenamer"/>: 간선 · 픽스처 · 카드가 함께 간다, 되돌리기 한 칸). 못 바꾸면 사유를 상태줄에 말하고 거짓을 낸다.
    /// </summary>
    private bool RenameEpisode(string chapterId, string oldId, string newId)
    {
        newId = newId.Trim();
        EpisodeRenamer.Result result = EpisodeRenamer.Rename(_session.Editor, chapterId, oldId, newId);

        if (!result.Renamed)
        {
            _session.SetStatus(result.Failure!);
            return false;
        }

        // 카드는 같은 카드다(신원표만 바뀌었다) — 그것을 고르고 있었으면 챕터 그래프도 새 이름으로 고른다.
        if (EpisodeNaming.CardFor(_session.Project, chapterId, newId) is { } card &&
            string.Equals(_session.SelectedNodeId, card.Id, StringComparison.Ordinal))
        {
            ChapterGraph.ShowEpisode(chapterId, newId);
        }

        _session.SetStatus($"'{oldId}' → '{newId}' 개명했습니다. 간선·픽스처·대사 노드가 함께 따라갔습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
        return true;
    }

    /// <summary>
    /// 에피소드를 끌어 놓았다 — 다른 장면 줄이면 그 장면으로(장면ID가 그 장면의 것이 된다), 챕터 줄이면 장면에서 빼 챕터
    /// 직속으로. 되돌리기 한 칸.
    /// </summary>
    private void OnHierarchyDropped(HierarchyRow source, HierarchyRow target)
    {
        if (source.ChapterId is not { } chapterId || source.EpisodeId is not { } episodeId)
        {
            return;
        }

        if (target.Kind == HierarchyKind.Chapter)
        {
            DetachFromScene(chapterId, episodeId);
            return;
        }

        if (target.SceneId is not { } sceneId)
        {
            return;
        }

        _session.Editor.UpdateEpisode(chapterId, episodeId, sceneId: sceneId);
        _session.SetStatus($"'{episodeId}'를 장면 '{sceneId}'로 옮겼습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
    }

    /// <summary>
    /// 에피소드를 장면에서 뺀다 — 장면ID를 걷어 <b>챕터 직속</b>으로.런타임에서는 그 에피소드 하나가 저장 · 롤백
    /// 구간이 된다(장면ID가 없는 에피소드의 규칙 그대로). 되돌리기 한 칸.
    /// </summary>
    private void DetachFromScene(string chapterId, string episodeId)
    {
        _session.Editor.UpdateEpisode(chapterId, episodeId, sceneId: string.Empty);
        _session.SetStatus($"'{episodeId}'를 장면에서 뺐습니다 — 이제 챕터 직속입니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
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
