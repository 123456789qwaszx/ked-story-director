using Vn.Authoring.Editing;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 에피소드 하나를 걷는 <b>유일한 자리</b>.
///
/// 이 규율이 화면 안에 살면 다른 화면에 같은 기능을 붙일 때 <b>사본을 뜨는 수밖에</b> 없고,
/// 사본은 갈린다. 그래서 서비스로 둔다.
///
/// <b>두 자리를 함께 본다.</b>
///
/// <list type="number">
///   <item>챕터의 에피소드 행과 그것을 쓰는 간선·픽스처 참조 (<see cref="ProjectEditor.RemoveEpisode"/>)</item>
///   <item>판 위의 <b>카드</b> — 함께 걷는다</item>
/// </list>
///
/// 글이 든 카드도 이름만 바꿔 남기지 않고 지운다 — 판 편집에는 Ctrl+Z가 있으므로,
/// 남겨 두면 "지우라고 했는데 이름만 바뀐 카드가 남는" 혼란만 생긴다.
/// 되돌리기 한 번이면 카드가 제 대본과 함께 돌아온다.
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
                text += $" [대본] 탭의 카드 '{removed}'도 함께 지웠습니다.";
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

        string? cardRemoved = null;

        // <b>되돌리기 한 칸이다</b>. ①과 ②가 따로 적히면 Ctrl+Z 한 번은 카드만 돌려놓고 챕터의 에피소드 행은
        //   지운 채로 둔다 — 챕터에 없는 카드가 판에 섰다가 둘째 Ctrl+Z에야 제자리를 찾는다.
        editor.Batch(() =>
        {
            // ① 행과 그것을 쓰는 것들.
            editor.RemoveEpisode(chapterId, episodeId);

            // ② 카드.
            cardRemoved = TakeCard(editor, chapterId, episodeId);
        });

        return new Result(true, null, cardRemoved);
    }

    /// <summary>
    /// 그 에피소드의 카드를 <b>거둔다</b>.
    ///
    /// 글이 든 카드도 지운다 — 판 편집은 Ctrl+Z로 되돌릴 수 있다.
    ///
    /// 카드의 대본도 함께 걷힌다(남겨 두면 주인 없는 대본이 프로젝트에 쌓인다). 되돌리기 한 번이면
    /// 카드와 대본이 함께 돌아온다(스냅샷).
    ///
    /// <b>이 챕터의 판에서만</b> 찾는다. 프로젝트 전체를 이름으로 훑으면 다른 챕터에 같은
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
