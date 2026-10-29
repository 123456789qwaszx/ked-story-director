namespace Vn.Authoring.Rendering;

/// <summary>
/// <b>대사 노드 하나가 게임의 Yarn에서 가질 노드 타이틀</b> — 그 규칙의 단 한 자리.
///
/// Yarn 자체는 이 툴이 내지 않는다(2026-10-29 — 게임용 Yarn은 <c>ked-story-compositor</c>만 낸다).
/// 그래도 규칙은 여기 남는다: <b>진행 JSON의 <c>DialogueEntryId</c></b>와 챕터 판의 대사엔트리가
/// 이 글자로 노드를 부르고, 런타임은 같은 글자로 <c>YarnProject</c>에서 노드를 찾는다. compositor의
/// 이미터와 이 함수가 갈리면 진행 JSON이 없는 노드를 부른다 — 2026-08-23에 진행 JSON은 `new01`,
/// yarn은 `Story_new01`로 갈렸던 것이 그 모양이다(`docs/work-orders/dialogue-entry-naming-orders.md`).
///
/// ⚠ <b>규칙은 접두가 아니라 정규화다</b> — <see cref="YarnSyntax.SanitizeNodeName"/>가
/// 공백·점·하이픈을 바꾼다(`장면 1` → `장면_1`). 바깥에서 이름을 그대로 쓰면 그런 이름에서 갈린다.
/// <c>Story_</c> 접두는 2026-08-24에 폐지됐다(기획자가 대사엔트리에 이미 적어 두 번 붙고 있었다).
/// </summary>
public static class YarnNaming
{
    /// <summary>이름이 비었으면 Id를, 그것도 없으면 <c>missing_target</c>을 정규화한다.</summary>
    public static string StoryNodeTitleOf(string? nodeName, string? nodeId = null)
    {
        string source = string.IsNullOrWhiteSpace(nodeName)
            ? (string.IsNullOrWhiteSpace(nodeId) ? "missing_target" : nodeId)
            : nodeName;

        return YarnSyntax.SanitizeNodeName(source);
    }
}
