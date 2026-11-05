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
    /// 이 노드가 <b>A계층(기획자) 조건을 나르는 배관</b>인가. 챕터 = 판 1:1이므로 판 이름이
    /// 곧 챕터 Id다. <b>계층을 가르는 규칙은 이 하나뿐이다</b>(사본 금지) — 그래프 프로젝션의
    /// 카드 숨김도, 작가 조건 목록의 배제도 같은 것을 부른다.
    /// </summary>
    public static bool IsConditionSupplyNode(StoryNode node, StoryFile file)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(file);

        // 판 이름이 챕터 Id와 어긋난 프로젝트(구판·수기 개명)에서도 배관은 배관이다 —
        // 이름 규약만으로도 알아본다. 이 판정이 이미터의 네임스페이스에도 쓰이므로
        // (스탯에는 접두를 붙이면 안 된다) 놓치면 게임이 깨진다.
        return node is SetNode &&
               (string.Equals(node.Name, ConditionSupplyNodeName(file.Name), StringComparison.Ordinal) ||
                IsConditionSupplyNodeName(node.Name));
    }

    /// <summary>이름만으로 A계층 공급 노드인지 — <c>챕터 … 조건</c>.</summary>
    public static bool IsConditionSupplyNodeName(string? name) =>
        name is not null &&
        name.StartsWith("챕터 ", StringComparison.Ordinal) &&
        name.EndsWith(" 조건", StringComparison.Ordinal);

    /// <summary>프로젝트 전체에서 A계층 공급 노드의 Id들. 작가 화면이 이 집합을 걸러 낸다.</summary>
    public static HashSet<string> ConditionSupplyNodeIds(StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (StoryFile file in project.Files)
        {
            foreach (StoryNode node in file.Nodes)
            {
                if (IsConditionSupplyNode(node, file))
                {
                    ids.Add(node.Id);
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// 라벨이 바뀐 조건은 <b>이름만 갈아 끼운다</b> — 조건 이름을 고쳐도 연결이 이어져야 한다.
    ///
    /// 줄에 매달린 전환은 <see cref="Model.LineConditionTransition.ConditionId"/>로 잇는다.
    /// 그래서 이름이 바뀌었다고 조건을 <b>새로 만들면</b> Id가 달라져 이미 매달린 갈래가 전부
    /// 고아가 된다 — 같은 식이면 같은 조건이므로, 있는 것의 이름만 고쳐 Id를 지킨다.
    /// 반대로 이름을 그냥 두면 판에는 <b>사라진 옛 라벨</b>이 계속 보인다.
    ///
    /// 고치는 것은 <b>공급 노드가 소유한 조건</b>뿐이다. 같은 식을 전역 정의나 작가의
    /// 설정노드가 이미 주고 있어도 그쪽 이름은 남의 것이라 건드리지 않는다.
    /// </summary>
    /// <returns>공급 노드가 이미 그 식을 갖고 있어 새로 만들 필요가 없으면 true.</returns>
    private static bool RenameSuppliedCondition(
        ProjectEditor editor,
        SetNode supply,
        string label,
        string yarn)
    {
        ConditionDefinition? existing = supply.Conditions.FirstOrDefault(condition =>
            string.Equals(condition.Expression.Trim(), yarn, StringComparison.Ordinal));

        if (existing is null)
        {
            return false;
        }

        if (!string.Equals(existing.Name, label, StringComparison.Ordinal))
        {
            editor.UpdateCondition(existing.Id, label, existing.Expression);
        }

        return true;
    }

    /// <summary>
    /// 챕터의 조건 <b>전부</b>를 그 판의 모든 대사 노드가 쓸 수 있게 공급한다 (2단계 4번).
    /// 작가의 자유 노드가 조건 드롭다운에서 챕터 라벨(A 계층)을 바로 고르게 하는 자리다 —
    /// 설정노드를 손으로 찾아 잇는 절차가 없어야 "개발자를 안 부르고" 조건을 건다.
    /// 링크·조건 모두 멱등이다(있으면 다시 만들지 않는다).
    /// </summary>
    public static void SupplyChapterConditionsToBoard(
        ProjectEditor editor,
        string fileId,
        ChapterGraphModel chapter)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(chapter);

        List<(string Label, string Yarn)> translatable = chapter.Conditions
            .Select(condition => (condition.Label, Translated: ConditionYarnTranslator.Translate(condition)))
            .Where(pair => pair.Translated.IsTranslatable)
            .Select(pair => (pair.Label, Yarn: pair.Translated.Yarn!))
            .DistinctBy(pair => pair.Yarn, StringComparer.Ordinal)
            .ToList();

        if (translatable.Count == 0)
        {
            return;
        }

        string supplyName = ConditionSupplyNodeName(chapter.ChapterId);

        SetNode? supply = editor.Project.EnumerateNodes().OfType<SetNode>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, supplyName, StringComparison.Ordinal));

        supply ??= editor.AddSetNode(fileId, name: supplyName);

        foreach ((string label, string yarn) in translatable)
        {
            // 있으면 이름만 따라가고(Id 보존 = 매달린 갈래 보존), 없으면 만든다.
            if (!RenameSuppliedCondition(editor, supply, label, yarn))
            {
                editor.AddCondition(supply.Id, label, yarn);
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

            Check(dialogue.DefaultExitTargetNodeId, "기본 출구");

            foreach ((_, string targetId) in dialogue.BranchExits)
            {
                Check(targetId, "갈래 출구");
            }

            foreach ((_, string targetId) in dialogue.ChoiceExits)
            {
                Check(targetId, "선택지 출구");
            }
        }

        return warnings;
    }
}
