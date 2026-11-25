using Vn.Authoring.Model;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Flow;

/// <summary>
/// 카드가 볼 수 있는 조건 하나의 읽기 모델 — 그 판의 챕터 조건 공급 노드가 나른 챕터 조건이다. 편집 화면과 흐름 해석기는 이 중립적인 형태만 본다.
/// ⛔ 조건의 출처(<c>SourceKind</c> — 옛 「X 설정」 노드의 작가 조건 · 게임 전역 조건)를 가르던 칸은 걷혔다 — 조건의 자리는 챕터 하나다
/// (<c>docs/work-orders/old-shapes-orders.md</c> O6 · <c>definition-split-orders.md</c> S2).
/// </summary>
public sealed record AvailableCondition(
    string Id,
    string Name,
    string Expression,
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

        foreach (ConnectedSetNode connected in ConnectedSetNodeResolver.Resolve(project, dialogueNodeId))
        {
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
                    setNode.Id);
            }
        }

        return null;
    }

    public static string UnavailableLabel(AvailableCondition? known, string conditionId)
    {
        string name = known?.DisplayName ?? conditionId;
        return string.Format(Msg.Diag_Unavailable, name);
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
