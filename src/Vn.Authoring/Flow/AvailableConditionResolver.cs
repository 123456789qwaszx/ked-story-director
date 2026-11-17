using Vn.Authoring.Model;

namespace Vn.Authoring.Flow;

/// <summary>
/// 조건이 어디서 왔나. ⛔ 게임 전역 조건(<c>GameGlobal</c> — <c>game.definition.json</c>의 <c>conditions</c>)은 2026-11-17에 걷혔다 —
/// 쓰는 창구가 없었고 조건의 자리는 챕터의 [조건] 하나다(<c>docs/work-orders/definition-split-orders.md</c> S2).
/// </summary>
public enum AvailableConditionSourceKind
{
    SetNode,

    /// <summary>
    /// <b>A계층(기획자) 조건</b> — 챕터 `조건` 시트가 주인이고, 동기화가 공급 설정노드로
    /// 날라 온 것이다 (2026-08-17 소유자: "둘은 서로 완전히 다른 계층이야").
    ///
    /// <b>작가가 고르는 목록에는 나오지 않는다</b> — 스탯 식은 작가에게 노출되면 안 되는
    /// 자료이고, 여기서 고른다 해도 값의 주인은 챕터라 고칠 수 없다. 다만 <b>이미 쓰인
    /// 것은 읽힌다</b>: 엑셀노드의 조건라벨이 이 조건을 가리키면 그 이름이 그대로 보인다
    /// (숨긴다고 "알 수 없는 조건"이 되면 그게 더 나쁘다).
    /// </summary>
    ChapterLayer
}

/// <summary>
/// DialogueNode가 현재 선택할 수 있는 조건 하나의 읽기 모델. 편집 화면과 흐름 해석기는 이 중립적인 형태만 본다.
/// </summary>
public sealed record AvailableCondition(
    string Id,
    string Name,
    string Expression,
    AvailableConditionSourceKind SourceKind,
    string? SourceNodeId)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

public sealed class AvailableConditionCatalog
{
    private readonly Dictionary<string, AvailableCondition> _byId;

    public AvailableConditionCatalog(IReadOnlyList<AvailableCondition> conditions)
    {
        Conditions = conditions;
        _byId = conditions.ToDictionary(condition => condition.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<AvailableCondition> Conditions { get; }

    /// <summary>
    /// 작가가 <b>고를 수 있는</b> 조건만 (2026-08-17) — A계층은 빠진다. 목록을 세우는 화면은
    /// 이쪽을, 이미 쓰인 조건의 이름을 읽는 화면은 <see cref="Find"/>를 쓴다.
    /// </summary>
    public IEnumerable<AvailableCondition> Selectable =>
        Conditions.Where(condition => condition.SourceKind != AvailableConditionSourceKind.ChapterLayer);

    public AvailableCondition? Find(string? conditionId)
    {
        return conditionId is not null && _byId.TryGetValue(conditionId, out AvailableCondition? condition)
            ? condition
            : null;
    }
}

/// <summary>
/// DialogueNode가 사용할 수 있는 조건의 범위를 계산한다.
///
/// 조건은 <b>그 대사 노드와 같은 판</b>의 SetNode 것만 들어온다(<see cref="ConnectedSetNodeResolver"/> — 범위는 챕터다). 같은 Id가
/// 여러 곳에서 공급되면 먼저 나온 하나만 사용한다. 순서는 판 안의 노드 순서 → SetNode 내부 순서다.
/// </summary>
public static class AvailableConditionResolver
{
    public static AvailableConditionCatalog Resolve(
        StoryProject project,
        string dialogueNodeId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.FindDialogue(dialogueNodeId) is null)
        {
            return new AvailableConditionCatalog(Array.Empty<AvailableCondition>());
        }

        var conditions = new List<AvailableCondition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // A계층 공급 노드가 나른 조건은 종류를 달리 매긴다 — 작가의 목록에서 빠지고,
        // 이미 쓰인 것을 읽을 때만 이름이 나온다.
        HashSet<string> chapterSupplyIds = Chapters.ChapterBoardSupply.ConditionSupplyNodeIds(project);

        foreach (ConnectedSetNode connected in ConnectedSetNodeResolver.Resolve(project, dialogueNodeId))
        {
            AvailableConditionSourceKind kind = chapterSupplyIds.Contains(connected.Node.Id)
                ? AvailableConditionSourceKind.ChapterLayer
                : AvailableConditionSourceKind.SetNode;

            foreach (ConditionDefinition condition in connected.Node.Conditions)
            {
                if (string.IsNullOrWhiteSpace(condition.Id) || !seen.Add(condition.Id))
                {
                    continue;
                }

                conditions.Add(new AvailableCondition(
                    condition.Id,
                    condition.Name,
                    condition.Expression,
                    kind,
                    connected.Node.Id));
            }
        }

        return new AvailableConditionCatalog(conditions);
    }

    /// <summary>
    /// 현재 Dialogue 범위와 무관하게 조건 Id가 어디엔가 정의되어 있는지 찾는다.
    /// 범위 밖(다른 판)에 있는 조건과 완전히 삭제된 조건을 구분하는 데 사용한다.
    /// </summary>
    public static AvailableCondition? FindKnown(
        StoryProject project,
        string? conditionId)
    {
        if (string.IsNullOrEmpty(conditionId))
        {
            return null;
        }

        HashSet<string> chapterSupplyIds = Chapters.ChapterBoardSupply.ConditionSupplyNodeIds(project);

        foreach (SetNode setNode in project.EnumerateNodes().OfType<SetNode>())
        {
            ConditionDefinition? local = setNode.Conditions.FirstOrDefault(
                condition => string.Equals(condition.Id, conditionId, StringComparison.Ordinal));

            if (local is not null)
            {
                return new AvailableCondition(
                    local.Id,
                    local.Name,
                    local.Expression,
                    chapterSupplyIds.Contains(setNode.Id)
                        ? AvailableConditionSourceKind.ChapterLayer
                        : AvailableConditionSourceKind.SetNode,
                    setNode.Id);
            }
        }

        return null;
    }

    public static string UnavailableLabel(AvailableCondition? known, string conditionId)
    {
        string name = known?.DisplayName ?? conditionId;
        return $"사용할 수 없음 · {name}";
    }

    /// <summary>
    /// 화면에 세울 이름 — 조건의 이름 그대로다.
    ///
    /// ⛔ 2026-11-17 소유자: 챕터 조건 앞의 「[기획]」을 걷었다. 2026-08-17에는 작가가 고칠 수 없는 조건이 왜 보이는지를 이름으로
    /// 말하려고 붙였지만, 이제 조건 이름은 작가가 [대본] 탭에서 짓고 분기에 직접 건다(two-graphs 7절 Q · S) — 작가의 것이다.
    /// </summary>
    public static string LayeredLabel(AvailableCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return condition.DisplayName;
    }
}
