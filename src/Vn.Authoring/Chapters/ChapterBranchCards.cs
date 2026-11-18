using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>분기 카드 검증</b> (2026-11-17 · <c>docs/work-orders/branch-card-orders.md</c> B2).
///
/// 분기 카드(<see cref="DialogueNode.IsBranchCard"/>)는 에피소드가 아니라 도달성 증명(<see cref="ChapterReachabilityProver"/> — 런타임의 오라클,
/// 에피소드와 간선만)의 밖이다. 그래서 여기서 따로 본다:
/// <list type="number">
/// <item>아무도 부르지 않는 분기 카드 · 도달할 수 없는 곳(에피소드 또는 그런 분기 카드)만 부르는 분기 카드 → <b>경고</b>(소유자). 분기 카드는 진행
///   JSON에 없어 게임을 깨지 않는다 — 재생되지 않을 뿐이다. 분기 카드의 분기도 사슬로 따른다.</item>
/// <item>줄이 하나도 없는 분기 카드 → 오류. Yarn 컴파일러가 줄 없는 노드를 빼서 <c>&lt;&lt;detour&gt;&gt;</c>가 없는 노드를 부른다(에피소드의 「빈 씬」과 같은 까닭).</item>
/// <item>분기 카드의 Yarn 노드 이름이 에피소드나 다른 분기 카드와 겹친다 → 오류. 한 Yarn 프로젝트에 같은 이름의 노드가 둘이면 컴파일이 깨진다 —
///   분기 카드는 [대본] 탭에서 이름을 자유롭게 고친다.</item>
/// </list>
/// </summary>
/// <summary>분기 카드와 그 집(부르는 에피소드) — <see cref="ChapterBranchCards.HomesOf"/>.</summary>
public sealed record BranchCardHome(DialogueNode Card, string EpisodeId);

public static class ChapterBranchCards
{
    public static void Verify(
        ChapterGraphModel chapter,
        StoryProject project,
        IReadOnlySet<string> reachableEpisodes,
        List<ChapterDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(reachableEpisodes);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (EpisodeNaming.BoardOf(project, chapter.ChapterId) is not { } board)
        {
            return;
        }

        List<DialogueNode> cards = board.Nodes.OfType<DialogueNode>().ToList();
        var byId = cards.ToDictionary(card => card.Id, StringComparer.Ordinal);

        // 부르는 쪽 → 들르는 카드, 대본 차례로.
        var calls = new List<(DialogueNode Caller, DialogueNode Target)>();

        foreach (DialogueNode caller in cards)
        {
            foreach (ScriptLine line in caller.Script.ActiveLines)
            {
                foreach (string target in caller.FindExtension(line.Id)?.DetourTargetNodeIds ?? [])
                {
                    if (byId.TryGetValue(target, out DialogueNode? card))
                    {
                        calls.Add((caller, card));
                    }
                }
            }
        }

        // ⛔ 「분기점이 에피소드에 이어져 있다」(BranchIntoEpisode)는 2026-11-18에 걷혔다 — 잇는 길이 모두 분기 카드만 받고, 그런 파일은 읽을 때
        //    거절한다(JsonSupport.ValidateProject · old-shapes O2).

        // ① 재생될 수 있는 분기 카드 — 도달할 수 있는 에피소드가 부르거나, 그런 분기 카드가 부른다(사슬).
        var played = new HashSet<string>(StringComparer.Ordinal);
        bool grew = true;

        while (grew)
        {
            grew = false;

            foreach ((DialogueNode caller, DialogueNode target) in calls)
            {
                bool callerPlays = caller.IsBranchCard
                    ? played.Contains(caller.Id)
                    : EpisodeOf(chapter, caller) is { } episode && reachableEpisodes.Contains(episode);

                if (callerPlays && played.Add(target.Id))
                {
                    grew = true;
                }
            }
        }

        // 이미 쓴 Yarn 노드 이름 — 에피소드의 대사 노드(진행 JSON이 부르는 그 이름)부터.
        ChapterBoard via = ChapterBoard.For(project, chapter.ChapterId);
        var taken = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ChapterEpisode episode in chapter.Episodes)
        {
            taken.TryAdd(ChapterProgressionExporter.DialogueEntryIdOf(episode), $"에피소드 「{episode.ShownTitle}」");
        }

        foreach (DialogueNode branch in cards.Where(card => card.IsBranchCard))
        {
            List<DialogueNode> callers = calls.Where(call => call.Target.Id == branch.Id).Select(call => call.Caller).Distinct().ToList();
            string? at = callers.Select(caller => EpisodeOf(chapter, caller)).FirstOrDefault(episode => episode is not null);

            if (callers.Count == 0)
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Warning,
                    ChapterDiagnosticCode.BranchCardOrphaned,
                    chapter.SourcePath,
                    ChapterSheetNames.Episodes,
                    null,
                    $"분기 카드 {EpisodeNaming.BranchLabelOf(branch)}를 부르는 분기점이 없습니다 — 게임에서 재생되지 않습니다. 쓰지 않으면 지우고, 쓰려면 분기점을 이 카드에 이으세요."));
            }
            else if (!played.Contains(branch.Id))
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Warning,
                    ChapterDiagnosticCode.BranchCardUnreachable,
                    chapter.SourcePath,
                    ChapterSheetNames.Episodes,
                    at,
                    $"분기 카드 {EpisodeNaming.BranchLabelOf(branch)}를 부르는 「{string.Join("」 · 「", callers.Select(caller => Shown(chapter, caller)))}」에 " +
                    "도달할 수 없습니다 — 이 분기 카드도 게임에서 재생되지 않습니다."));
            }

            // ③ Yarn 노드 이름이 겹친다 — 에피소드의 대사 노드 · 다른 분기 카드.
            string title = YarnNaming.StoryNodeTitleOf(branch.Name, branch.Id);

            if (taken.TryGetValue(title, out string? owner))
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error,
                    ChapterDiagnosticCode.BranchCardNameTaken,
                    chapter.SourcePath,
                    ChapterSheetNames.Episodes,
                    at,
                    $"분기 카드 {EpisodeNaming.BranchLabelOf(branch)}의 Yarn 노드 이름 '{title}'이(가) {owner}와 겹칩니다 — 한 Yarn 프로젝트에 같은 이름의 노드가 둘이면 " +
                    "컴파일이 깨집니다. [구성] 탭에서 분기 카드의 ID를 고쳐 주세요."));
            }
            else
            {
                taken[title] = $"분기 카드 {EpisodeNaming.BranchLabelOf(branch)}";
            }

            // ② 줄이 없는 분기 카드.
            if (!branch.Script.ActiveLines.Any())
            {
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error,
                    ChapterDiagnosticCode.BranchCardEmpty,
                    chapter.SourcePath,
                    ChapterSheetNames.Episodes,
                    at,
                    $"분기 카드 {EpisodeNaming.BranchLabelOf(branch)}에 재생할 줄이 하나도 없습니다. [대본] 탭의 그 카드에 대사를 한 줄이라도 적어 주세요."));
            }
        }
    }

    /// <summary>
    /// <b>분기 카드의 집</b> — 그 분기 카드를 부르는 에피소드(설계 3절 3). 분기 카드의 장면은 저장하지 않고 이것에서 읽는다 — [대본] 판의 장면
    /// 테두리 · 장면 접기, 대사 Yarn의 장면 파일, 장면 파일(<c>scenes.json</c>), 하이어아키가 같은 규칙을 쓴다.
    ///
    /// 판의 카드 차례로 에피소드를 돌며 대본 차례로 분기점을 따른다 — 여러 곳에서 불리면 <b>먼저 만난 에피소드</b>가 집이다. 분기 카드가 부르는
    /// 분기 카드는 같은 집에 든다(사슬). 도달할 수 있는 에피소드에서 이어지지 않는 분기 카드(아무도 안 부르거나 집 없는 분기 카드만 부른다)는
    /// 목록에 없다 — 검증이 경고로 짚는다.
    /// </summary>
    /// <returns>집이 정해진 분기 카드들 — 에피소드마다 대본 차례(분기 카드의 분기는 그 카드 바로 뒤).</returns>
    public static IReadOnlyList<BranchCardHome> HomesOf(StoryProject project, string chapterId)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Homes(project, chapterId, _ => true);
    }

    /// <summary>
    /// <b>장면째 지울 때 함께 갈 분기 카드</b>(2026-11-18 소유자 — 「장면 지우기시에 분기카드까지 함께」) — 집이 그 장면의 에피소드인 분기 카드(사슬
    /// 포함 — 하이어아키에서 그 장면 밑에 보이던 것) 가운데 <b>장면을 걷은 뒤 집을 잃는 것</b>. 다른 장면의 에피소드도 부르는 카드는 그쪽으로 집을
    /// 옮겨 남는다(그쪽 대사가 부르고 있다).
    /// </summary>
    public static IReadOnlyList<DialogueNode> GoingWithScene(StoryProject project, string chapterId, string sceneId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.Chapters.FirstOrDefault(item => string.Equals(item.ChapterId, chapterId, StringComparison.Ordinal)) is not { } chapter)
        {
            return [];
        }

        HashSet<string> inScene = chapter.Episodes
            .Where(episode => string.Equals(episode.SceneId, sceneId, StringComparison.Ordinal))
            .Select(episode => episode.EpisodeId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> kept = Homes(project, chapterId, episodeId => !inScene.Contains(episodeId))
            .Select(home => home.Card.Id)
            .ToHashSet(StringComparer.Ordinal);

        return HomesOf(project, chapterId)
            .Where(home => inScene.Contains(home.EpisodeId) && !kept.Contains(home.Card.Id))
            .Select(home => home.Card)
            .ToList();
    }

    /// <summary><see cref="HomesOf"/>의 알맹이 — <paramref name="fromEpisode"/>가 참인 에피소드에서만 분기를 따른다.</summary>
    private static List<BranchCardHome> Homes(StoryProject project, string chapterId, Func<string, bool> fromEpisode)
    {
        if (EpisodeNaming.BoardOf(project, chapterId) is not { } board ||
            project.Chapters.FirstOrDefault(item => string.Equals(item.ChapterId, chapterId, StringComparison.Ordinal)) is not { } chapter)
        {
            return [];
        }

        var byId = board.Nodes.OfType<DialogueNode>().ToDictionary(card => card.Id, StringComparer.Ordinal);
        var homes = new List<BranchCardHome>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Visit(DialogueNode caller, string episodeId)
        {
            foreach (ScriptLine line in caller.Script.ActiveLines)
            {
                foreach (string target in caller.FindExtension(line.Id)?.DetourTargetNodeIds ?? [])
                {
                    if (byId.TryGetValue(target, out DialogueNode? card) && card.IsBranchCard && seen.Add(card.Id))
                    {
                        homes.Add(new BranchCardHome(card, episodeId));
                        Visit(card, episodeId);
                    }
                }
            }
        }

        foreach (DialogueNode card in byId.Values)
        {
            if (EpisodeNaming.EpisodeFor(chapter, card) is { } episode && fromEpisode(episode.EpisodeId))
            {
                Visit(card, episode.EpisodeId);
            }
        }

        return homes;
    }

    /// <summary>그 카드가 이 챕터의 에피소드면 그 Id — 분기 카드 · 챕터에 없는 카드면 null.</summary>
    private static string? EpisodeOf(ChapterGraphModel chapter, DialogueNode card) =>
        card.IsBranchCard ? null : chapter.FindEpisode(EpisodeNaming.EpisodeIdOf(card))?.EpisodeId;

    /// <summary>사람에게 보일 이름 — 에피소드면 제목, 아니면 카드 이름.</summary>
    private static string Shown(ChapterGraphModel chapter, DialogueNode card) =>
        EpisodeOf(chapter, card) is { } episode ? chapter.FindEpisode(episode)!.ShownTitle : EpisodeNaming.BranchTitleOf(card);
}
