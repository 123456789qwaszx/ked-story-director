using Vn.Authoring.Editing;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>에피소드 Id를 바꾸고 그 이름을 지고 있는 것들을 함께 옮긴다</b>
/// (<see cref="ChapterRenamer"/>·<see cref="ChapterDeleter"/>와 같은 결).
///
/// ⛔ <b>왜 서비스로 나왔나</b> (2026-09-16): 이 규율이 <c>ChapterGraphView</c> 안에만
/// 있었는데 다른 화면에서도 이름을 고치게 되면서 부르는 자리가 둘이 됐다. 사본을
/// 뜨면 <b>한쪽만 노드를 따라가게</b> 되고, 그 어긋남은 카드가 고아가 되는 모양으로 나타난다.
///
/// 함께 따라가는 것 셋:
/// <list type="bullet">
/// <item><b>간선·픽스처</b> — <see cref="ProjectEditor.RenameEpisode"/>가 한다.</item>
/// <item><b>대사 노드</b> — 이름과 표식. 새로 만들지 않고 이름만 바꾸므로 줄·연출이 전부 보존된다.</item>
/// <item><b>대사엔트리</b> — 규약(= Id)을 따르던 것만.</item>
/// </list>
///
/// (2026-10-29 · D2: 넷째였던 대본 워크북 <c>episodes/{챕터}/{Id}.xlsx</c> 옮기기는 엑셀 연동과 함께 걷혔다.)
/// </summary>
public static class EpisodeRenamer
{
    /// <param name="NodeRenamed">연출 그래프의 대사 노드가 따라갔으면 참.</param>
    public sealed record Result(
        bool Renamed,
        string? Failure,
        bool NodeRenamed = false)
    {
        public static Result Fail(string reason) => new(false, reason);
    }

    public static Result Rename(
        ProjectEditor editor,
        string chapterId,
        string oldId,
        string newId)
    {
        ArgumentNullException.ThrowIfNull(editor);

        oldId = (oldId ?? string.Empty).Trim();
        newId = (newId ?? string.Empty).Trim();

        if (oldId.Length == 0 || newId.Length == 0 ||
            string.Equals(oldId, newId, StringComparison.Ordinal))
        {
            return Result.Fail("바뀐 이름이 없습니다.");
        }

        bool nodeRenamed = false;

        try
        {
            // ⚠ <b>되돌리기 한 칸이다</b> (2026-10-30) — 챕터 쪽 개명과 카드 따라가기가 둘로 나뉘어 있어서 Ctrl+Z 한 번은
            //   카드 이름만 되돌리고 에피소드는 새 이름으로 남겼다(사람 눈에는 "반쯤 돌아갔다").
            editor.Batch(() =>
            {
                editor.RenameEpisode(chapterId, oldId, newId);
                nodeRenamed = RenameNode(editor, chapterId, oldId, newId);
            });
        }
        catch (InvalidOperationException exception)
        {
            // 챕터 쪽이 막혔다(같은 Id·폐지된 cleared: 참조).
            return Result.Fail(exception.Message);
        }

        return new Result(true, null, nodeRenamed);
    }

    /// <summary>
    /// 대사 노드도 따라간다 — 규약(대사엔트리 = Id)을 따르던 노드만.
    ///
    /// ⚠ <b>이 챕터의 판에서만</b> 찾는다 (2026-08-25). 프로젝트 전체를 이름으로 훑으면
    /// 다른 챕터에 같은 Id가 있을 때 남의 노드를 개명한다 — 그쪽 판에서는 에피소드와
    /// 이름이 갈려 유령이 되고, 그 유령이 이름 중복으로 내보내기를 막는다.
    /// </summary>
    private static bool RenameNode(
        ProjectEditor editor, string chapterId, string oldId, string newId)
    {
        if (EpisodeNaming.CardFor(editor.Project, chapterId, oldId) is not { } node)
        {
            return false;
        }

        // 표식도 함께 간다 — 옛 Id로 남으면 연출 그래프가 챕터 밖 노드로 보고 레일을 끊는다.
        if (node.MarkedEpisodeId is not null)
        {
            node.MarkedEpisodeId = newId;
        }

        editor.RenameNode(node.Id, newId);
        return true;
    }
}
