using Avalonia.Controls;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App;

/// <summary>
/// <b>하이어아키에서 고치기</b> — 우클릭 차림표(챕터 · 장면 · 에피소드 추가, 에피소드 지우기)와 두 번 눌러 이름 바꾸기, 에피소드를 다른
/// 장면 줄로 끌어 옮기기. 두 탭의 하이어아키가 이 한 벌을 쓴다(고치는 것은 [대본] 탭뿐 — [구성] 탭 하이어아키는 읽기 전용이다).
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
            CreateChapterNow();
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
            case HierarchyCommand.DeleteChapter:
                ConfirmChapterRemoval(anchor, chapterId);
                break;

            case HierarchyCommand.AddScene:
            {
                // 장면은 에피소드가 들어야 있다 — 새 장면은 첫 에피소드와 함께 선다.
                // 그 에피소드에 간선을 저절로 잇지 않는다 — 새 장면이 어디서 들어오는지는 사람이 잇는다.
                // 장면ID는 자동이고(작가는 보지 않는다) 이름은 「새 장면」이다 — 한 되돌리기 칸(display-names N2).
                CreateEpisode(chapter, NewNames.NextSceneId(_session.Editor.FindChapter(chapter.ChapterId)!), sceneName: NewNames.Scene);
                break;
            }

            // 장면 줄에서만 — 그 장면에 길 없이 선다(2026-11-18 소유자 · ⛔ 에피소드 줄에서 그 뒤에 「계속」 길로 잇던 것은 걷었다).
            case HierarchyCommand.AddEpisode when row.Kind == HierarchyKind.Scene && scene is not null:
                CreateEpisode(chapter, scene.SceneId);
                break;

            case HierarchyCommand.DeleteEpisode when row.EpisodeId is { } episodeId:
            {
                EpisodeDeleter.Result result = EpisodeDeleter.Delete(_session.Editor, chapterId, episodeId);
                _session.SetStatus(result.Ok ? result.Describe(episodeId) : result.Failure!);
                break;
            }
        }
    }

    /// <summary>
    /// 줄 이름 고치기 — [대본] 탭 하이어아키만 연다(보이는 이름만 고친다). [구성] 탭 하이어아키는 읽기 전용이고, ID는 [구성] 탭
    /// 인스펙터의 ID 칸에서 고친다(⛔ 2026-11-17 — ID로 세운 줄의 개명 갈래 · 「장면ID를 비워 장면 풀기」는 걷었다: 열리지 않는 길이었다).
    /// </summary>
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

            // [대본] 탭은 이름만 고친다 — 챕터는 표시 이름, 장면은 장면 이름. ID는 그대로다(display-names N3).
            case HierarchyKind.Chapter when tree.ShowEpisodeTitles && row.ChapterId is { } chapterId:
                _session.Editor.UpdateChapterDisplayName(chapterId, name);
                _session.SetStatus(name.Length == 0
                    ? "챕터 이름을 비웠습니다 — 챕터 ID가 보입니다."
                    : $"챕터 이름을 「{name}」(으)로 바꿨습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
                break;

            case HierarchyKind.Scene when tree.ShowEpisodeTitles && row.ChapterId is { } chapterId && row.SceneId is { } sceneId:
                _session.Editor.SetSceneName(chapterId, sceneId, name);
                _session.SetStatus(name.Length == 0
                    ? "장면 이름을 비웠습니다 — 장면 ID가 보입니다."
                    : $"장면 이름을 「{name}」(으)로 바꿨습니다. 같은 이름의 장면이 있어도 합쳐지지 않습니다.");
                break;
        }
    }

    /// <summary>
    /// 에피소드를 끌어 놓았다 — 다른 장면 줄이면 그 장면으로(장면ID가 그 장면의 것이 된다). 되돌리기 한 칸. 장면 줄에만 놓는다
    /// (<see cref="StoryHierarchy.Accepts"/>).
    /// </summary>
    private void OnHierarchyDropped(HierarchyRow source, HierarchyRow target)
    {
        if (source.ChapterId is not { } chapterId || source.EpisodeId is not { } episodeId)
        {
            return;
        }

        if (target.SceneId is not { } sceneId)
        {
            return;
        }

        // 자동 모양 그대로인 ID는 새 장면의 이름으로 따라간다(분기 카드도 — AutoIdFollower, 같은 되돌리기 칸).
        string moved = AutoIdFollower.MoveEpisodeToScene(_session.Editor, chapterId, episodeId, sceneId);
        _session.SetStatus($"'{episodeId}'를 장면 '{sceneId}'로 옮겼습니다" +
            (moved == episodeId ? "" : $"(에피소드ID는 '{moved}'로 따라갔습니다)") + ". 되돌리기(Ctrl+Z)로 돌아옵니다.");
    }

    /// <summary>
    /// 에피소드를 하나 더한다 — <paramref name="sceneId"/>의 장면에, 길 없이(<see cref="EpisodeAdder"/>).
    /// 되돌리기 한 칸(<c>Batch</c>). 더한 에피소드를 두 탭에서 고른다.
    /// </summary>
    private void CreateEpisode(ChapterEntry chapter, string sceneId, string? sceneName = null)
    {
        string chapterId = chapter.ChapterId;
        string episodeId = string.Empty;

        _session.Editor.Batch(() =>
        {
            episodeId = EpisodeAdder.Add(_session, chapterId, sceneId);

            if (sceneName is not null)
            {
                _session.Editor.SetSceneName(chapterId, sceneId, sceneName);
            }
        });

        if (EpisodeNaming.CardFor(_session.Project, chapterId, episodeId) is { } card)
        {
            _session.Select(card.Id);
        }

        _session.SetStatus(
            (sceneName is null ? $"「{NewNames.Episode}」를 더했습니다" : $"「{sceneName}」 장면을 더했습니다") +
            ". 이름은 하이어아키에서 두 번 눌러 고칩니다. 되돌리기(Ctrl+Z)로 돌아옵니다.");
    }

}
