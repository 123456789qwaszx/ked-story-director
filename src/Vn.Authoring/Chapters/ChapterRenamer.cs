using Vn.Authoring.Editing;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 챕터 개명의 <b>유일한 자리</b>. 이름이 한 곳만 바뀌면 에피소드들의 대화가 없다고 나온다.
///
/// 개명은 <see cref="ProjectEditor.RenameChapter"/> 한 번이다: <c>Project.Chapters</c>의 Id · 판 이름이 한 변경으로 바뀌고
/// 되돌리기 한 번에 돌아온다.
///
/// 따라 바뀌어야 할 파일은 진행 JSON 하나이고, 여기서는 옮기지 않고 이름만 말한다 — 새 이름의 파일은 바로 뒤의
/// 내보내기가 쓰고, 옛 이름의 파일은 같은 내보내기가 걷는다(<see cref="ChapterExportService"/>).
/// </summary>
public static class ChapterRenamer
{
    /// <param name="StaleExport">
    /// 옛 이름으로 내보낸 진행 JSON이 남아 있으면 그 파일 이름. 여기서는 지우지 않는다 — 개명은 모델만 바꾸고(되돌리기
    /// 한 칸), 파일은 바로 뒤의 내보내기가 걷는다. 화면은 이 이름으로 걷혔는지 말한다. <see cref="ChapterDeleter"/>도 같다.
    /// </param>
    public sealed record Result(
        bool Renamed,
        string? Failure,
        string? StaleExport = null)
    {
        public static Result Fail(string reason) => new(false, reason);
    }

    /// <param name="projectManifestPath">
    /// 프로젝트 파일 경로 — 옛 이름의 진행 JSON이 남았는지 보는 데만 쓴다. 아직 저장 안 한
    /// 프로젝트(null)면 볼 것이 없다.
    /// </param>
    public static Result Rename(
        ProjectEditor editor, string? projectManifestPath, string oldId, string newId)
    {
        ArgumentNullException.ThrowIfNull(editor);

        oldId = (oldId ?? string.Empty).Trim();
        newId = (newId ?? string.Empty).Trim();

        if (oldId.Length == 0 || newId.Length == 0)
        {
            return Result.Fail(Msg.Edit_ChapterNameIsEmpty);
        }

        if (string.Equals(oldId, newId, StringComparison.Ordinal))
        {
            return Result.Fail(Msg.Edit_NameIsSame);
        }

        if (editor.FindChapter(oldId) is null)
        {
            return Result.Fail(string.Format(Msg.Edit_ChapterIsnTInProject, oldId));
        }

        if (editor.FindChapter(newId) is not null)
        {
            return Result.Fail(string.Format(Msg.Edit_ChapterAlreadyExists, newId));
        }

        // 챕터 Id · 판이 한 변경으로 — 자동 모양 그대로인 에피소드ID · 분기 카드 이름도 같은 칸에서 따라간다(2026-11-17).
        editor.Batch(() =>
        {
            editor.RenameChapter(oldId, newId);
            AutoIdFollower.FollowChapter(editor, oldId, newId);
        });

        string? stale = projectManifestPath is null
            ? null
            : ChapterExportService.ExportPathFor(projectManifestPath, oldId);

        return new Result(
            true, null,
            stale is not null && File.Exists(stale) ? Path.GetFileName(stale) : null);
    }
}
