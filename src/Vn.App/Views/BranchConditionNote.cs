using Vn.Authoring.Chapters;

namespace Vn.App.Views;

/// <summary>
/// 분기 조건을 사람에게 말하는 글 — 연출 그래프 카드의 표와 인스펙터의 분기 카드가 같은 말을 쓴다.
///
/// 조건은 다녀오는 에피소드의 첫머리에 서고(<see cref="BranchEntryCondition"/>) 정하는 곳은 챕터 그래프의 분기 선 하나다.
/// 연출 그래프는 조건을 고치지 않는 대신 지금 걸린 조건을 보이고 그 자리로 데려간다 — 안 보이면 조건 없는 분기와 조건 걸린
/// 분기가 판에서 똑같아 보인다.
/// </summary>
internal static class BranchConditionNote
{
    /// <summary>어디서 정하는가.</summary>
    internal const string Where = "조건은 챕터 그래프에서 이 분기 선(점선)을 눌러 정합니다.";

    /// <summary>카드 머리에 다는 짧은 표.</summary>
    internal static string Chip(BranchEntryCondition.State state) =>
        state.Label is { } label
            ? $"⑂ 조건: {label}"
            : state.Blocked is not null ? "⑂ 조건: 직접 지음" : "⑂ 조건 없음";

    /// <summary>지금 이 분기가 어떻게 도는가 — 한 문장.</summary>
    internal static string Sentence(BranchEntryCondition.State state) =>
        state.Label is { } label
            ? $"조건 「{label}」일 때만 재생합니다 — 아니면 들렀다가 바로 돌아옵니다."
            : state.Blocked is not null
                ? "대사 편집기에서 직접 지은 조건이 있습니다 — 챕터 그래프에서는 읽기만 합니다."
                : "지금은 조건 없이 늘 다녀옵니다.";
}
