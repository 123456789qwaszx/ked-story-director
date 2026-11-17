namespace Vn.Authoring.Editing;

/// <summary>
/// 「분기 지우기」 한 번의 결과 (<see cref="ProjectEditor.RemoveBranchMarker"/>). 분기 지우기는 분기점과 선만 걷는다 — 다녀가던
/// 카드는 늘 남는다(2026-11-17).
/// </summary>
/// <param name="KeptNodeName">판에 남은 카드(분기가 다녀가던 에피소드)의 보이는 이름. 카드가 없었으면 <c>null</c>.</param>
public sealed record BranchMarkerRemoval(string? KeptNodeName)
{
    /// <summary>상태줄에 할 말.</summary>
    public string Describe() => KeptNodeName is { } kept
        ? $"분기를 지웠습니다 — 카드 '{kept}'는 판에 남아 있습니다(없애려면 그 카드를 고르고 [노드 삭제]). 되돌리기(Ctrl+Z)로 돌아옵니다."
        : "분기를 지웠습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.";
}
