using Vn.Authoring.Editing;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 에피소드 하나를 걷는 <b>유일한 자리</b> (2026-09-18).
///
/// ⛔ <b>2026-09-18까지 이 규율은 [챕터 그래프] 화면 안에 살았다.</b> 그래서 다른 화면에
/// 같은 기능을 붙이려면 <b>사본을 뜨는 수밖에</b> 없었고, 사본은 갈린다 — 바로 앞의
/// 「에피소드를 만드는 길」이 정확히 그렇게 갈려 있었다(화면 하나가 카드 만들기를 잊었다).
/// 그 일을 다시 하지 않으려고 먼저 꺼낸다.
///
/// <b>두 자리를 함께 본다.</b>
///
/// <list type="number">
///   <item>챕터의 에피소드 행과 그것을 쓰는 간선·픽스처 참조 (<see cref="ProjectEditor.RemoveEpisode"/>)</item>
///   <item>판 위의 <b>카드</b> — 함께 걷는다</item>
/// </list>
///
/// (2026-10-29 · D2: 셋째 자리였던 대본 워크북 <c>.bak</c> 밀기는 엑셀 연동과 함께 걷혔다.)
///
/// ⛔ <b>2026-09-18에 「떼어내기」가 없어졌다</b> (소유자가 짚었다). 글이 든 카드는 지우지
/// 않고 <c>(떼어냄)</c>으로 이름만 바꿔 남겼었는데, 그 근거가 *"툴이 임의로 지우면 되돌릴
/// 자리가 없다(판 편집에는 <b>Ctrl+Z가 없다</b>)"*였다. <b>같은 날 Ctrl+Z가 붙어 전제가
/// 없어졌고</b>, 남는 것은 "지우라고 했는데 이름만 바뀐 카드가 남는" 혼란뿐이었다.
///
/// ⚠ 그래도 <b>글은 안 사라진다</b> — 대본은 원래 안 지운다. 되돌리기 한 번이면 카드가
/// 제 대본을 다시 찾는다.
/// </summary>
public static class EpisodeDeleter
{
    /// <param name="CardRemoved">함께 걷힌 카드의 이름(카드가 없었으면 null).</param>
    public sealed record Result(
        bool Ok,
        string? Failure,
        string? CardRemoved = null)
    {
        public static Result Fail(string reason) => new(false, reason);

        /// <summary>사람에게 할 말 — 화면이 제 사정(내보내기 결과)을 뒤에 붙인다.</summary>
        public string Describe(string episodeId)
        {
            string text = $"'{episodeId}'과 그 간선·픽스처 참조를 지웠습니다.";

            if (CardRemoved is { } removed)
            {
                text += $" 연출 그래프의 노드 '{removed}'도 함께 지웠습니다.";
            }

            return text + " 되돌리기(Ctrl+Z)로 돌아옵니다.";
        }
    }

    public static Result Delete(ProjectEditor editor, string chapterId, string episodeId)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (string.IsNullOrWhiteSpace(episodeId))
        {
            return Result.Fail("지울 에피소드 이름이 비어 있습니다.");
        }

        if (editor.FindChapter(chapterId) is null)
        {
            return Result.Fail($"챕터 '{chapterId}'를 찾지 못했습니다.");
        }

        // ① 행과 그것을 쓰는 것들.
        editor.RemoveEpisode(chapterId, episodeId);

        // ② 카드.
        return new Result(true, null, TakeCard(editor, chapterId, episodeId));
    }

    /// <summary>
    /// 그 에피소드의 카드를 <b>거둔다</b>.
    ///
    /// ⛔ <b>2026-09-18까지는 글이 든 카드를 지우지 않고 「떼어내기」만 했다</b> — 이름을
    /// <c>(떼어냄)</c>으로 바꾸고 표식을 비워 판에 남겼다. 근거는 코드에 이렇게 적혀 있었다:
    /// *"툴이 임의로 지우면 되돌릴 자리가 없다(<b>판 편집에는 Ctrl+Z가 없다</b>)"*.
    ///
    /// <b>그 전제가 같은 날 없어졌다</b> — Ctrl+Z가 붙었다. 지킬 것이 없어진 뒤로 남는 것은
    /// <i>"지우라고 했는데 이름만 바뀐 카드가 남는"</i> 혼란뿐이라, 소유자가 그것을 짚었다.
    ///
    /// ⚠ <b>글은 그래도 안 사라진다.</b> 대본(<see cref="Model.StoryProject.Scripts"/>)은
    /// 원래 지우지 않으므로 카드만 걷히고, 되돌리기 한 번이면 카드가 제 대본을 다시 찾는다.
    ///
    /// ⚠ <b>이 챕터의 판에서만</b> 찾는다. 프로젝트 전체를 이름으로 훑으면 다른 챕터에 같은
    /// Id가 있을 때 남의 카드를 건드린다.
    /// </summary>
    private static string? TakeCard(ProjectEditor editor, string chapterId, string episodeId)
    {
        if (EpisodeNaming.CardFor(editor.Project, chapterId, episodeId) is not { } card)
        {
            return null;
        }

        string name = card.Name;
        editor.RemoveNode(card.Id);

        return name;
    }
}
