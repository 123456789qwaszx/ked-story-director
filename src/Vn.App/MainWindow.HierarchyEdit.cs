using Avalonia.Controls;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Text;
using Vn.App.Strings;
using Vn.Authoring.Strings;

namespace Vn.App;

/// <summary>
/// <b>하이어아키에서 고치기</b> — 우클릭 차림표(챕터 · 장면 · 에피소드 추가, 에피소드 지우기)와 두 번 눌러 이름 바꾸기, 에피소드를 다른
/// 장면 줄로 끌어 옮기기, 고른 챕터 · 장면을 [대본] 툴바의 [삭제]로 지우기. 두 탭의 하이어아키가 이 한 벌을 쓴다(고치는 것은 [대본] 탭뿐 — [구성] 탭 하이어아키는 읽기 전용이다).
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
            CreateChapterNow(at: null);
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
    /// [대본] 툴바의 [삭제]를 고른 챕터 · 장면에 눌렀다(2026-11-18 소유자 — ⛔ 하이어아키 차림표의 「챕터 지우기」 · 「장면 지우기」를 갈음한다). 단추
    /// 옆에 확인 칸이 선다. 에피소드 · 분기 카드는 단추가 스스로 지운다(<see cref="GraphEditorView"/>).
    /// </summary>
    private void OnDirectorRemoveRequested(HierarchyPick pick, Control anchor)
    {
        if (pick.ChapterId is not { } chapterId ||
            _chapters.FirstOrDefault(entry => string.Equals(entry.ChapterId, chapterId, StringComparison.Ordinal))
                is not { } chapter)
        {
            return;
        }

        if (pick.Kind == HierarchyKind.Chapter)
        {
            ConfirmChapterRemoval(anchor, chapterId);
        }
        else if (pick.Kind == HierarchyKind.Scene &&
                 ChapterSceneGrouping.Of(chapter.Model).FirstOrDefault(item =>
                     string.Equals(item.SceneId, pick.SceneId, StringComparison.Ordinal)) is { } scene)
        {
            ConfirmSceneRemoval(anchor, chapter, scene);
        }
    }

    /// <summary>
    /// 고른 장면을 지운다([삭제] → 「장면 삭제」) — 확인 한 번 뒤에 그 장면의 에피소드(카드 · 간선째)와 그 에피소드들이 부르던 분기 카드, 장면 항목
    /// (이름 · 메모)을 걷는다(<see cref="Vn.Authoring.Editing.ProjectEditor.RemoveScene"/> — 되돌리기 한 칸). 에피소드 여럿이 함께 사라지므로 챕터
    /// 지우기와 같이 묻는다. 분기 카드 수는 확인 문구에 미리 센다(<see cref="ChapterBranchCards.GoingWithScene"/>).
    /// </summary>
    private void ConfirmSceneRemoval(Control anchor, ChapterEntry chapter, ChapterScene scene)
    {
        string chapterId = chapter.ChapterId;
        string sceneId = scene.SceneId;
        int branchCards = ChapterBranchCards.GoingWithScene(_session.Project, chapterId, sceneId).Count;

        RemoveConfirmButton = ConfirmFlyout.Show(
            anchor,
            _session,
            branchCards > 0
                ? Plural.Format(Ui.Side_EpisodesOfSceneAndBranch, scene.DisplayName, scene.Episodes.Count, branchCards)
                : Plural.Format(Ui.Side_EpisodesOfSceneWillBe, scene.DisplayName, scene.Episodes.Count),
            Ui.Side_DeleteScene,
            () =>
            {
                Vn.Authoring.Editing.ProjectEditor.RemovedScene removed = _session.Editor.RemoveScene(chapterId, sceneId);
                _session.SetStatus(removed.BranchCards > 0
                    ? Plural.Format(Ui.Side_DeletedSceneEpisodesBranchCards, scene.DisplayName, removed.Episodes, removed.BranchCards)
                    : Plural.Format(Ui.Side_DeletedSceneEpisodesUndoCtrl, scene.DisplayName, removed.Episodes));
                ForgetRemovedPick();
            },
            closed: () => RemoveConfirmButton = null);
    }

    /// <summary>
    /// 지운 챕터 · 장면을 고른 채로 두지 않는다 — 인스펙터는 빈 안내로, [삭제]는 다시 「삭제」(꺼짐)로 돌아간다.
    /// </summary>
    private void ForgetRemovedPick()
    {
        _directorSummary = null;
        ShowSelectedNode();
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
                    ? string.Format(Ui.Side_ClearedTitleEpisodeIDIs, episodeId, Josa.이가(episodeId))
                    : string.Format(Ui.Side_RenamedTitleToEpisodeID, name, Josa.으로(name), episodeId, Josa.은는(episodeId)));
                break;

            // [대본] 탭은 이름만 고친다 — 챕터는 표시 이름, 장면은 장면 이름. ID는 그대로다(display-names N3).
            case HierarchyKind.Chapter when tree.ShowEpisodeTitles && row.ChapterId is { } chapterId:
                RenameChapter(chapterId, name);
                break;

            case HierarchyKind.Scene when tree.ShowEpisodeTitles && row.ChapterId is { } chapterId && row.SceneId is { } sceneId:
                RenameScene(chapterId, sceneId, name);
                break;
        }
    }

    /// <summary>챕터의 보이는 이름을 고친다 — 하이어아키 이름 칸과 [대본] 「조건 및 스탯」 탭 맨 위의 챕터 이름 칸이 이 한 벌을 쓴다. 비우면 챕터 ID가 보인다.</summary>
    /// <returns>이제 보이는 이름(비웠으면 챕터 ID).</returns>
    private string RenameChapter(string chapterId, string name)
    {
        _session.Editor.UpdateChapterDisplayName(chapterId, name);
        _session.SetStatus(name.Length == 0
            ? Ui.Side_ClearedChapterNameChapterID
            : string.Format(Ui.Side_RenamedChapterToUndoCtrl, name, Josa.으로(name)));
        return _session.Editor.FindChapter(chapterId)?.ShownName ?? chapterId;
    }

    /// <summary>장면 이름을 고친다 — 하이어아키 이름 칸과 [대본] 인스펙터 머리의 이름 칸이 이 한 벌을 쓴다. 비우면 장면 ID가 보인다.</summary>
    /// <returns>이제 보이는 이름(비웠으면 장면 ID).</returns>
    private string RenameScene(string chapterId, string sceneId, string name)
    {
        _session.Editor.SetSceneName(chapterId, sceneId, name);
        _session.SetStatus(name.Length == 0
            ? Ui.Side_ClearedSceneNameSceneID
            : string.Format(Ui.Side_RenamedSceneToScenesWith, name, Josa.으로(name)));
        return _session.Editor.FindChapter(chapterId) is { } chapter &&
               ChapterSceneNotes.NameOf(chapter.SceneNotes, sceneId) is { Length: > 0 } named
            ? named
            : sceneId;
    }

    /// <summary>
    /// 그 에피소드를 그 장면 줄로 옮기지 못하는 까닭 — 장면 입구가 둘이 되게 하는 선을 끊어도 입구가 둘인 이동(챕터 시작을 시작점이 있는 장면으로 ·
    /// <see cref="SceneMoveGuard"/>). 옮겨도 되면(끊을 선이 있어도) null.
    /// </summary>
    private string? HierarchyMoveRefusal(HierarchyRow source, HierarchyRow target) =>
        HierarchyMovePlan(source, target)?.Refusal;

    /// <summary>옮기면 끊을 선을 미리 말한다 — 끊을 선이 없거나 옮기지 못하면 null(2026-11-26).</summary>
    private string? HierarchyMoveNote(HierarchyRow source, HierarchyRow target) =>
        HierarchyMovePlan(source, target) is { Refusal: null, Cuts.Count: > 0 } plan &&
        _session.Editor.FindChapter(source.ChapterId!) is { } chapter
            ? SceneMoveGuard.CutNote(chapter, plan.Cuts, done: false)
            : null;

    private SceneMovePlan? HierarchyMovePlan(HierarchyRow source, HierarchyRow target) =>
        source.ChapterId is { } chapterId && source.EpisodeId is { } episodeId && target.SceneId is { } sceneId &&
        _session.Editor.FindChapter(chapterId) is { } chapter
            ? SceneMoveGuard.Plan(chapter, episodeId, sceneId)
            : null;

    /// <summary>
    /// 에피소드를 끌어 놓았다 — 다른 장면 줄이면 그 장면으로(장면ID가 그 장면의 것이 된다). 장면 입구가 둘이 되게 하는 선은 끊고(2026-11-26 ·
    /// <see cref="SceneMoveGuard.Plan"/>) 그 문구는 출발 카드 칸에 적힌 채 남는다. 되돌리기 한 칸. 장면 줄에만 놓는다(<see cref="StoryHierarchy.Accepts"/>).
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

        if (_session.Editor.FindChapter(chapterId) is not { } chapter)
        {
            return;
        }

        SceneMovePlan plan = SceneMoveGuard.Plan(chapter, episodeId, sceneId);

        if (plan.Refusal is { } refusal)
        {
            _session.SetStatus(refusal);
            return;
        }

        // 끊을 선의 문구는 출발 카드 칸에 적힌 채 남긴다 — 선을 걷기 전에(빈 곳에 놓아 끊기와 같다).
        string note = SceneMoveGuard.CutNote(chapter, plan.Cuts, done: true);
        Graph.KeepCutChoiceTexts(chapterId, plan.Cuts);

        // 자동 모양 그대로인 ID는 새 장면의 이름으로 따라간다(분기 카드도 — AutoIdFollower, 같은 되돌리기 칸).
        string moved = AutoIdFollower.MoveEpisodeToScene(_session.Editor, chapterId, episodeId, sceneId).EpisodeId;
        Graph.FollowChoiceRows(chapterId, episodeId, moved);
        _session.SetStatus(note.Length == 0
            ? moved == episodeId
                ? string.Format(Ui.Side_MovedToSceneUndoCtrl, episodeId, sceneId)
                : string.Format(Ui.Side_MovedToSceneItsEpisode, episodeId, sceneId, moved)
            : moved == episodeId
                ? string.Format(Ui.Side_MovedToSceneCut, episodeId, sceneId, note)
                : string.Format(Ui.Side_MovedToSceneItsEpisodeCut, episodeId, sceneId, moved, note));
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

        _session.SetStatus(sceneName is null
            ? string.Format(Ui.Side_AddedRenameItByDouble, NewNames.Episode)
            : string.Format(Ui.Side_AddedSceneRenameItBy, sceneName));
    }

}
