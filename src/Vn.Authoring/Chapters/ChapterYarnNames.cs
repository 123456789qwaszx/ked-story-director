using Vn.Authoring.Model;
using Vn.Authoring.Rendering;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>Yarn 노드 이름은 프로젝트에서 하나다</b> (2026-11-17 소유자 · <c>docs/work-orders/auto-id-orders.md</c> I2). 게임은 모든 챕터의 Yarn을
/// 한 프로젝트로 컴파일하므로 챕터가 달라도 같은 이름의 노드가 둘이면 컴파일이 깨진다 — 툴의 내보내기는 챕터마다 따로 통과해도.
///
/// 이 챕터의 Yarn 노드(에피소드의 대사 노드 — <see cref="ChapterProgressionExporter.DialogueEntryIdOf"/>, 분기 카드)가 다른 챕터의 것과
/// 같은 이름이면 오류로 짚는다. 같은 챕터 안의 겹침은 <see cref="ChapterBranchCards"/>(분기 카드)와 판의 이름 검사가 본다.
/// </summary>
public static class ChapterYarnNames
{
    public static void Verify(ChapterGraphModel chapter, StoryProject project, List<ChapterDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var elsewhere = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ChapterDocument other in project.Chapters.Where(other =>
                     !string.Equals(other.ChapterId, chapter.ChapterId, StringComparison.Ordinal)))
        {
            foreach ((string title, string owner, _) in NodesOf(project, other.ChapterId, other.Episodes))
            {
                elsewhere.TryAdd(title, $"챕터 「{other.ShownName}」의 {owner}");
            }
        }

        foreach ((string title, string owner, string? episodeId) in NodesOf(project, chapter.ChapterId, chapter.Episodes))
        {
            if (elsewhere.TryGetValue(title, out string? taken))
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error,
                    ChapterDiagnosticCode.YarnNodeNameTaken,
                    chapter.SourcePath,
                    ChapterSheetNames.Episodes,
                    episodeId,
                    $"{owner}의 Yarn 노드 이름 '{title}'이(가) {taken}와 겹칩니다 — 게임은 모든 챕터의 Yarn을 한 프로젝트로 컴파일하므로 " +
                    "같은 이름의 노드가 둘이면 컴파일이 깨집니다. [구성] 탭에서 ID를 고쳐 주세요."));
            }
        }
    }

    /// <summary>그 챕터의 Yarn 노드 — 에피소드의 대사 노드와 판의 분기 카드. (이름, 사람이 읽는 주인, 짚을 에피소드)</summary>
    private static IEnumerable<(string Title, string Owner, string? EpisodeId)> NodesOf(
        StoryProject project, string chapterId, IReadOnlyList<ChapterEpisode> episodes)
    {
        ChapterBoard via = ChapterBoard.For(project, chapterId);

        foreach (ChapterEpisode episode in episodes)
        {
            yield return (ChapterProgressionExporter.DialogueEntryIdOf(episode, via), $"에피소드 「{episode.ShownTitle}」", episode.EpisodeId);
        }

        Dictionary<string, string> homes = ChapterBranchCards.HomesOf(project, chapterId)
            .ToDictionary(home => home.Card.Id, home => home.EpisodeId, StringComparer.Ordinal);

        foreach (DialogueNode card in EpisodeNaming.BoardOf(project, chapterId)?.Nodes.OfType<DialogueNode>().Where(node => node.IsBranchCard) ?? [])
        {
            yield return (YarnNaming.StoryNodeTitleOf(card.Name, card.Id), $"분기 카드 {EpisodeNaming.BranchLabelOf(card)}", homes.GetValueOrDefault(card.Id));
        }
    }
}
