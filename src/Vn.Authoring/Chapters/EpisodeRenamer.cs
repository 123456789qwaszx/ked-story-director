using Vn.Authoring.Editing;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>에피소드 Id를 바꾸고 그 이름을 지고 있는 것들을 함께 옮긴다</b>
/// (<see cref="ChapterRenamer"/>·<see cref="ChapterDeleter"/>와 같은 결).
///
/// <b>왜 서비스인가</b>: 이름을 고치는 화면이 여럿이다. 화면마다 사본을
/// 뜨면 <b>한쪽만 노드를 따라가게</b> 되고, 그 어긋남은 카드가 고아가 되는 모양으로 나타난다.
///
/// 함께 따라가는 것 — <b>간선·픽스처</b>와 <b>카드</b>(표식과 이름 · 새로 만들지 않으므로 줄·연출이 전부 보존된다). 둘 다
/// <see cref="ProjectEditor.RenameEpisode"/>의 한 칸이다.
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
            return Result.Fail(Msg.Edit_NoNameWasChanged);
        }

        bool nodeRenamed = EpisodeNaming.CardFor(editor.Project, chapterId, oldId) is not null;

        try
        {
            editor.RenameEpisode(chapterId, oldId, newId);
        }
        catch (InvalidOperationException exception)
        {
            // 챕터 쪽이 막혔다(같은 Id).
            return Result.Fail(exception.Message);
        }

        return new Result(true, null, nodeRenamed);
    }
}
