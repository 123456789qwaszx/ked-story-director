using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Chapters;
/// <summary>
/// <b>챕터가 판에 공급하는 것</b> — 조건 배관과 가드레일.
///
/// 하는 일은 둘이다: 챕터 조건을 설정노드로 공급하는 배관과, 판을 훑어 위험을 말하는 경고.
/// 공급은 조건을 고치는 명령과 판이 서는 순간에 <c>ProjectEditor</c>가 부른다.
/// </summary>
public static class ChapterBoardSupply
{
    /// <summary>
    /// 챕터 조건 공급 설정노드의 이름 규약 — 이 이름이 곧 신원이다. 동기화(생성·dedupe)와
    /// 그래프 프로젝션(작가 화면에서 숨김)이 같은 규칙 하나를 쓴다.
    /// </summary>
    public static string ConditionSupplyNodeName(string chapterId) => $"챕터 {chapterId} 조건";

    /// <summary>
    /// <b>식 미정</b>인 조건이 대사 카드에서 서는 식 — 항상 통과한다(writer-fields W1의 규칙: 식이 빈 조건은 관문이 없는 것과 같다).
    /// 빈 식을 그대로 두면 이미터가 <c>&lt;&lt;if false&gt;&gt;</c>로 내어 분기가 영영 안 탄다 — 그래서 사본에는 이 글자를 적는다.
    /// </summary>
    public const string UndecidedYarn = "true";

    /// <summary>
    /// 챕터의 조건 <b>전부</b>를 그 판의 모든 대사 노드가 쓸 수 있게 공급한다 (2단계 4번). 분기의 조건(다녀갈 카드 첫머리의
    /// <c>&lt;&lt;if&gt;&gt;</c>)이 이 사본을 Id로 붙든다. 멱등이다(바뀔 것이 없으면 안 건드린다).
    ///
    /// <b>사본의 짝은 이름(라벨)이다</b> (2026-11-17 — 전에는 식이었다). 챕터 조건 하나에 사본 하나:
    /// <list type="bullet">
    /// <item>같은 이름의 사본이 있으면 <b>Id를 지킨 채</b> 식만 맞춘다 — 식을 고쳐도, 식 미정이었다가 채워져도 매달린 분기가 그대로 따라온다.</item>
    /// <item>없으면 만든다.</item>
    /// <item><b>식 미정</b>(식이 빈) 조건도 공급한다 — <see cref="UndecidedYarn"/>(항상 통과). 식 미정인 조건이 여럿이어도 이름으로
    /// 갈리므로 섞이지 않는다. 식이 같은 조건 둘도 이제 사본이 따로다.</item>
    /// <item>식이 <b>틀린</b> 조건(문법 · 모르는 스탯)은 건드리지 않는다 — 있던 사본은 옛 식 그대로 남고, 검증이 오류로 짚는다.</item>
    /// </list>
    /// 이름 바꾸기는 <c>ProjectEditor.RenameChapterCondition</c>이 사본의 이름을 먼저 옮긴 뒤 이것을 부른다.
    /// </summary>
    public static void SupplyChapterConditionsToBoard(
        ProjectEditor editor,
        string fileId,
        ChapterGraphModel chapter)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(chapter);

        var wanted = new List<(string Label, string Yarn)>();

        foreach (ChapterCondition condition in chapter.Conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.Label) ||
                wanted.Any(item => string.Equals(item.Label, condition.Label, StringComparison.Ordinal)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(condition.Expression))
            {
                wanted.Add((condition.Label, UndecidedYarn));
            }
            else if (ConditionYarnTranslator.Translate(condition) is { Yarn: { } yarn })
            {
                wanted.Add((condition.Label, yarn));
            }
        }

        if (wanted.Count == 0)
        {
            return;
        }

        string supplyName = ConditionSupplyNodeName(chapter.ChapterId);

        SetNode? supply = editor.Project.EnumerateNodes().OfType<SetNode>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, supplyName, StringComparison.Ordinal));

        supply ??= editor.AddSetNode(fileId, name: supplyName);

        foreach ((string label, string yarn) in wanted)
        {
            ConditionDefinition? held = supply.Conditions.FirstOrDefault(condition =>
                string.Equals(condition.Name, label, StringComparison.Ordinal));

            if (held is null)
            {
                editor.AddCondition(supply.Id, label, yarn);
            }
            else if (!string.Equals(held.Name, label, StringComparison.Ordinal) ||
                     !string.Equals(held.Expression.Trim(), yarn, StringComparison.Ordinal))
            {
                editor.UpdateCondition(held.Id, label, yarn);   // Id 보존 = 매달린 분기 보존
            }
        }

        // 공급 범위가 판(챕터) 전체다 — 노드를 새로 만들 때마다 배관을 다시 잇지 않아도 된다.
    }

    /// <summary>
    /// 가드레일 — 판 위 노드의 출구(기본·갈래)가 <b>진행이 착지하는 카드</b>를 가리키면 경고.
    /// 에피소드 사이의 흐름은 챕터 간선이 소유하므로, 이 점프는 챕터 장부
    /// (표시/해금 검사·에피소드 끝 스탯 환산·cleared 기록)를 전부 지나친다. 게다가 Yarn
    /// 점프는 노드 처음부터 다시 재생하므로 같은 에피소드로의 "복귀"도 함정이다.
    /// 편집기는 후보에서 빼지만, 이미 있는 연결(옛 프로젝트)은 막지 않고 크게 말한다.
    /// </summary>
    public static IReadOnlyList<ChapterDiagnostic> WarnExitsIntoLandingCards(
        ProjectEditor editor,
        string fileId,
        ChapterGraphModel chapter)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(chapter);

        // 판의 대사 노드는 <b>전부</b> 에피소드라, <c>MarkedEpisodeId</c>가 있는 노드를
        //    가리키는지로 가르면 <b>모든 배선이 운다</b>.
        //
        // 기준은 <b>진행이 그 카드에 착지하는가</b>다 — 규칙은 <see cref="ChapterSpine"/> 한 벌이다.
        HashSet<string> landed = ChapterSpine.LandedOn(chapter.Episodes, chapter.Edges);

        Dictionary<string, DialogueNode> excelNodes = editor.Project.EnumerateNodes()
            .OfType<DialogueNode>()
            .Where(node => node.MarkedEpisodeId is { } episodeId && landed.Contains(episodeId))
            .ToDictionary(node => node.Id, node => node, StringComparer.Ordinal);

        if (excelNodes.Count == 0)
        {
            return Array.Empty<ChapterDiagnostic>();
        }

        StoryFile? file = editor.Project.Files.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, fileId, StringComparison.Ordinal));

        var warnings = new List<ChapterDiagnostic>();

        foreach (DialogueNode dialogue in (file?.Nodes ?? []).OfType<DialogueNode>())
        {
            void Check(string? targetId, string exitKind)
            {
                if (targetId is not null && excelNodes.TryGetValue(targetId, out DialogueNode? target))
                {
                    warnings.Add(new ChapterDiagnostic(
                        ChapterDiagnosticSeverity.Warning,
                        ChapterDiagnosticCode.ExitIntoLandingCard,
                        chapter.SourcePath, null, null,
                        $"노드 '{dialogue.Name}'의 {exitKind}가 진행이 착지하는 에피소드 " +
                        $"'{target.Name}'을 가리킵니다 — 에피소드 사이의 흐름은 챕터 간선(기획자)이 " +
                        "정합니다. 이 점프는 표시/해금·cleared 기록을 지나치고, 도착 노드를 처음부터 " +
                        "다시 재생합니다. 출구를 비우고 카드 아래 선택지 칸으로 이어 주세요."));
                }
            }

            foreach ((_, string targetId) in dialogue.BranchExits)
            {
                Check(targetId, "갈래 출구");
            }
        }

        return warnings;
    }
}
