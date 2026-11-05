using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 챕터 제거의 <b>유일한 자리</b>. 챕터를 지우면 연출 그래프에 있던 것도 모두 함께 지워진다.
///
/// 걷는 것은 둘이고 <b>한 변경</b>이다(<see cref="ProjectEditor.RemoveChapterWithBoard"/>):
///
/// <list type="number">
///   <item>챕터 자체 — <c>Project.Chapters</c>의 그 항목</item>
///   <item>판(<c>StoryFile</c>)과 그 위의 노드 전부 — 에피소드 카드도, 조건 공급 노드(<c>챕터 {Id} 조건</c>)도
///     이 판에 서 있다.</item>
/// </list>
///
/// 되돌리기 한 번에 둘이 함께 돌아온다.
/// </summary>
public static class ChapterDeleter
{
    /// <param name="NodesRemoved">판과 함께 걷힌 노드 수 — 연출 그래프에서 사라지는 것이다.</param>
    /// <param name="StaleExport">
    /// 그 챕터로 내보낸 진행 JSON이 남아 있으면 그 파일 이름. <b>지우지 않는다</b> —
    /// 산출물이라도 남의 폴더(개발자가 가져가는 자리)에 있는 파일이다. 대신 <b>이름을 말해</b>
    /// 사람이 지우게 한다: 그냥 두면 런타임이 <b>없는 챕터를 계속 싣는다</b>.
    /// </param>
    public sealed record Result(
        bool Deleted,
        string? Failure,
        int NodesRemoved = 0,
        string? StaleExport = null)
    {
        public static Result Fail(string reason) => new(false, reason);
    }

    /// <summary>챕터 하나와 그 판을 걷는다.</summary>
    /// <param name="projectManifestPath">
    /// 프로젝트 파일 경로 — 그 챕터의 진행 JSON이 남았는지 보는 데만 쓴다.
    /// </param>
    public static Result Delete(
        ProjectEditor editor, string? projectManifestPath, string chapterId)
    {
        ArgumentNullException.ThrowIfNull(editor);

        chapterId = (chapterId ?? string.Empty).Trim();

        if (chapterId.Length == 0)
        {
            return Result.Fail("챕터 이름이 비어 있습니다.");
        }

        StoryFile? board = EpisodeNaming.BoardOf(editor.Project, chapterId);

        if (editor.FindChapter(chapterId) is null && board is null)
        {
            return Result.Fail($"챕터 '{chapterId}'를 찾지 못했습니다.");
        }

        if (board is not null && editor.Project.Files.Count <= 1)
        {
            return Result.Fail(
                $"'{chapterId}'는 마지막 판이라 지울 수 없습니다 — 새 노드가 갈 자리가 " +
                "없어집니다. 다른 챕터를 하나 만든 뒤에 지워 주세요.");
        }

        int nodesRemoved = editor.RemoveChapterWithBoard(chapterId);

        string? stale = projectManifestPath is null
            ? null
            : ChapterExportService.ExportPathFor(projectManifestPath, chapterId);

        return new Result(
            true,
            null,
            nodesRemoved,
            stale is not null && File.Exists(stale) ? Path.GetFileName(stale) : null);
    }
}
