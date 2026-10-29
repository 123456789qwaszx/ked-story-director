namespace Vn.Authoring.Editing;

/// <summary>
/// 「분기 지우기」 한 번의 결과 (<see cref="ProjectEditor.RemoveBranchMarker"/>).
/// </summary>
/// <param name="TakenNodeName">표식과 함께 걷은 카드(분기가 다녀가던 에피소드)의 이름. 남겼으면 <c>null</c>.</param>
/// <param name="KeptNodeName">다른 곳이 쓰고 있어 판에 남긴 카드의 이름. 걷었거나 카드가 없었으면 <c>null</c>.</param>
public sealed record BranchMarkerRemoval(string? TakenNodeName, string? KeptNodeName)
{
    /// <summary>상태줄에 할 말.</summary>
    public string Describe() => (TakenNodeName, KeptNodeName) switch
    {
        ({ } taken, _) => $"분기를 지웠습니다 — 다녀오던 '{taken}'도 함께 지웠습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.",
        (_, { } kept) => $"분기를 지웠습니다. '{kept}'은 다른 곳에서도 이어 쓰고 있어 판에 남겼습니다. 되돌리기(Ctrl+Z)로 돌아옵니다.",
        _ => "분기를 지웠습니다. 되돌리기(Ctrl+Z)로 돌아옵니다."
    };
}
