namespace Vn.Authoring.Chapters;

/// <summary>
/// 화면이 쓰는 챕터 한 줄 — 프로젝트의 챕터를 그래프 모델로 편 것.
/// </summary>
/// <param name="Branches">
/// 분기 카드와 그 집(부르는 에피소드 — <see cref="ChapterBranchCards.HomesOf"/>) — 하이어아키가 부르는 에피소드 밑에 「⑂ 이름」 줄로 세운다
/// (2026-11-17). 분기 카드는 에피소드가 아니라 <see cref="Model"/>에 없다. 판이 없으면 빈 목록이다.
/// </param>
public sealed record ChapterEntry(string ChapterId, ChapterGraphModel Model, IReadOnlyList<BranchCardHome>? Branches = null)
{
    /// <summary>모델 진단에 오류가 있는가. 왼쪽 목록이 붉게 표시한다.</summary>
    public bool HasErrors => Model.HasErrors;

    /// <summary>분기 카드들 — 없으면 빈 목록.</summary>
    public IReadOnlyList<BranchCardHome> BranchCards => Branches ?? [];
}
