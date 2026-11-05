using Vn.Authoring.Model;

namespace Vn.Authoring.Chapters;

/// <param name="Reachability">도달성 증명 결과. 챕터 모델만으로 돈다(증감은 간선의 것).</param>
public sealed record ChapterValidationResult(
    IReadOnlyList<ChapterDiagnostic> Diagnostics,
    ChapterReachabilityResult Reachability)
{
    public bool HasErrors =>
        Diagnostics.Any(item => item.Severity == ChapterDiagnosticSeverity.Error) ||
        Reachability.HasErrors;

    public IEnumerable<ChapterDiagnostic> All =>
        Diagnostics.Concat(Reachability.Diagnostics);
}

/// <summary>
/// 챕터를 검증한다. 구조 검증(모델이 이미 낸 것 + 판과의 교차 검증)과 도달성 증명을 묶어,
/// 내보내기가 <b>이 결과 하나만 보고 거부를 결정</b>하게 한다.
///
/// <b>프로젝트만 본다</b>. 스탯 증감은 챕터 모델(간선)에서 읽고 "줄이 없는 노드"는
/// 프로젝트(<see cref="VerifyScenesFilled"/>)로 본다 — 산출물 파일을 읽으면 원본이 같은데
/// 산출물의 상태(없음·손상·잠김)로 판정이 달라진다.
/// </summary>
public static class ChapterValidator
{
    /// <param name="project">
    /// 있으면 <b>`대사엔트리`가 실재하는 대사노드를 가리키는지</b>까지 본다
    /// (<see cref="VerifyDialogueEntriesOnBoard"/>). null이면 그 검사를
    /// 건너뛴다 — 판을 볼 수 없는 자리(콘솔·테스트)에서도 나머지 검증은 돌아야 한다.
    /// </param>
    public static ChapterValidationResult Validate(
        ChapterGraphModel chapter,
        StoryProject? project = null)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var diagnostics = new List<ChapterDiagnostic>(chapter.Diagnostics);

        VerifyDialogueEntriesOnBoard(chapter, project, diagnostics);
        VerifyScenesFilled(chapter, project, diagnostics);

        // 스탯 증감의 원천은 간선이다 — 증명기가 챕터 모델에서 직접 읽는다.
        ChapterReachabilityResult reachability = ChapterReachabilityProver.Prove(chapter);

        return new ChapterValidationResult(diagnostics, reachability);
    }

    /// <summary>
    /// 재생할 줄이 하나도 없는 노드를 <b>오류로</b> 짚는다 — 에피소드의 것이든, 간선에
    /// 매단 자유 씬이든.
    ///
    /// <b>경고가 아니라 오류다.</b> 빈 씬이라도 들어갔다 곧장 나오면 그만일 것 같지만,
    /// <b>Yarn 컴파일러가 줄 없는 노드를 YarnProject에 아예 넣지 않는다</b>(실측). 그러면 진행 JSON이 부르는 이름이 저쪽에 없어 유니티의 사전 대조가
    /// <b>재생을 통째로 막는다</b> — 빈 씬 하나가 챕터 전체를 못 돌게 한다.
    ///
    /// 커맨드만 있고 <b>대사가 빈 줄</b>은 여기서 세지 않는다 — 그것이 순수 연출 씬의
    /// 정상 모양이고, 이미터가 보이지 않는 글자로 채워 내므로 노드가 살아남는다.
    /// 여기가 보는 것은 <b>줄 자체가 없는</b> 노드다.
    /// </summary>
    private static void VerifyScenesFilled(
        ChapterGraphModel chapter,
        StoryProject? project,
        List<ChapterDiagnostic> diagnostics)
    {
        if (project is null)
        {
            return;
        }

        ChapterBoard board = ChapterBoard.For(project, chapter.ChapterId);

        foreach (ChapterEpisode episode in chapter.Episodes)
        {
            // ① 에피소드 자신의 대사 노드. 없는 것은 `VerifyDialogueEntriesOnBoard`가 짚으므로
            //    여기서는 <b>있는데 비었을 때</b>만 본다 — 같은 말을 두 번 하지 않는다.
            if (board.EpisodeNodeFor(episode) is { } own && !HasPlayableLine(own))
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error,
                    ChapterDiagnosticCode.EpisodeSceneEmpty,
                    chapter.SourcePath,
                    ChapterSheetNames.Episodes,
                    episode.EpisodeId,
                    // Yarn이 줄 없는 노드를 안 싣는다는 사정은 위 주석의 것이다 —
                    // 기획자에게는 고치는 법 한 문장이면 된다.
                    $"'{episode.EpisodeId}'의 대사 노드 '{own.Name}'에 재생할 줄이 하나도 " +
                    "없습니다. 연출 그래프의 그 카드에 대사를 한 줄이라도 적어 주세요."));
            }

        }
    }

    /// <summary>재생될 줄이 하나라도 있는가 — 본문이 비어도 줄은 줄이다(연출이 매달린다).</summary>
    private static bool HasPlayableLine(DialogueNode scene) =>
        scene.Script.ActiveLines.Any();

    /// <summary>
    /// <b>에피소드가 부를 대사 노드가 그 판에 실제로 있는가</b>.
    ///
    /// 노드가 없으면 그 .yarn이 아예 안 나가고 진행 JSON만 <b>없는 노드를 부른다</b> —
    /// 로드·검증·도달성 증명은 전부 통과하고 재생만 안 된다. 유니티의 사전 대조가 잡는
    /// 그 모양이고, <b>여기서 잡으면 유니티까지 안 간다.</b>
    ///
    /// <b>안 연 챕터도 봐주지 않는다.</b> 판이 없거나 비어 있을 때 이 관문을 건너뛰면
    /// 진행 JSON이 그대로 나가고 게임에서만 죽는다. 판과 노드는 <b>프로젝트 파일에 남으므로</b>
    /// (세션 상태가 아니다) 한 번 연 챕터는 계속 통과한다.
    /// </summary>
    private static void VerifyDialogueEntriesOnBoard(
        ChapterGraphModel chapter,
        StoryProject? project,
        List<ChapterDiagnostic> diagnostics)
    {
        if (project is null || chapter.Episodes.Count == 0)
        {
            return;
        }

        // <b>판이 없어도 건너뛰지 않는다</b>. 판이 없거나 비어 있으면
        //    그 챕터의 <b>모든</b> 에피소드가 부를 노드를 잃는데, 조용히 통과시키면
        //    내보내기가 나가고 유니티 사전 대조가 재생을 통째로 막는다.
        //    한 번도 안 연 챕터도 <b>내보내는 순간</b>에는 같은 사고다 — 안 열었다는 것이
        //    면제 사유가 될 수 없다. 대신 무엇을 하면 되는지를 말한다.
        ChapterBoard board = ChapterBoard.For(project, chapter.ChapterId);

        List<ChapterEpisode> missing = chapter.Episodes
            .Where(episode => board.EpisodeNodeFor(episode) is null)
            .ToList();

        if (missing.Count == chapter.Episodes.Count)
        {
            diagnostics.Add(new ChapterDiagnostic(
                ChapterDiagnosticSeverity.Error,
                ChapterDiagnosticCode.DialogueEntryNodeMissing,
                chapter.SourcePath,
                ChapterSheetNames.Episodes,
                null,
                $"'{chapter.ChapterId}' 판에 대사 노드가 하나도 없습니다 — 이대로 내보내면 " +
                "진행 JSON이 부르는 노드가 전부 없어 게임이 재생을 시작하지 못합니다. " +
                "프로젝트를 저장하고 다시 열면 에피소드마다 빈 카드가 섭니다."));

            return;   // 에피소드마다 같은 말을 반복하지 않는다.
        }

        foreach (ChapterEpisode episode in missing)
        {
            diagnostics.Add(new ChapterDiagnostic(
                ChapterDiagnosticSeverity.Error,
                ChapterDiagnosticCode.DialogueEntryNodeMissing,
                chapter.SourcePath,
                ChapterSheetNames.Episodes,
                episode.EpisodeId,
                $"'{episode.EpisodeId}'의 대사엔트리 '{episode.DialogueEntry}'에 해당하는 " +
                $"대사노드가 '{chapter.ChapterId}' 판에 없습니다. 이대로 내보내면 진행 JSON이 " +
                "존재하지 않는 대사 노드를 부르고, 로드는 통과하는데 재생만 안 됩니다. " +
                "프로젝트를 저장하고 다시 열면 에피소드마다 빈 카드가 섭니다."));
        }
    }

}
