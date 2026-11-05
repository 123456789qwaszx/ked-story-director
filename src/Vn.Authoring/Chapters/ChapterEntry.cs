namespace Vn.Authoring.Chapters;

/// <summary>
/// 화면이 쓰는 챕터 한 줄 — 프로젝트의 챕터를 그래프 모델로 편 것.
/// </summary>
public sealed record ChapterEntry(string ChapterId, ChapterGraphModel Model)
{
    /// <summary>모델 진단에 오류가 있는가. 왼쪽 목록이 붉게 표시한다.</summary>
    public bool HasErrors => Model.HasErrors;
}
