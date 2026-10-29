using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>`잠금시 숨김`이 폐지됐다</b> (2026-08-24 소유자: "이미 표시조건과, 해금조건이 있다보니
/// 기능적으로 제거하더라도 아무런 차이가 없어").
///
/// 같은 말을 두 번 하는 칸이었다 — <b>해금조건 + 숨김</b>은 그 식을 <b>표시조건</b>에 적은
/// 것과 결과가 같다. 개념이 둘이면 작가가 어느 쪽으로 적었는지에 따라 같은 이야기가
/// 다르게 보인다.
///
/// 2026-10-29 · D2: 옛 워크북의 그 열을 걷는 이행 테스트는 엑셀 연동과 함께 걷혔다.
/// </summary>
public sealed class HideWhenLockedRemovalTests
{
    [Fact]
    public void 내보내기에_그_칸이_아예_없다()
    {
        // 2026-08-25 — 저쪽 DTO에서도 사라졌다. 여기까지는 "안 채울 뿐"이었는데
        // (칸을 없애면 옛 진행 JSON이 안 읽히므로), 코어가 칸을 지우면서 이쪽도 뺀다.
        // 남겨 두면 조용히 버려지는 칸이 하나 더 생긴다.
        var editor = new ProjectEditor(new StoryProject());
        ChapterDocument chapter = editor.EnsureChapter("ch01");

        // 초기 3 — 간선의 해금조건이 `trust >= 3`이라, 0에서 시작하면 도달성 증명이
        // "어떤 경로로도 못 연다"며 내보내기를 막는다(그 판정은 옳다).
        chapter.Stats.Add(new ChapterStat("trust", "신뢰", 3, 0, 10));
        chapter.Stats.Add(new ChapterStat("fatigue", "피로", 0, 0, 10));

        editor.AddEpisode("ch01", "시작", "첫 화", 0, 0);
        editor.AddEpisode("ch01", "끝", "끝 화", 200, 0);
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        editor.AddEdge("ch01", "시작", "끝", conditionLabel: "신뢰높음", optionLabel: "계속");
        editor.UpdateEdge("ch01", "시작", "끝", lockedMessage: "신뢰가 부족하다", matchOptionLabel: "계속");

        ChapterExportResult export = ChapterProgressionExporter.Export(
            editor.FindChapter("ch01")!.ToGraphModel("ch01"));

        Assert.False(export.Refused, string.Join(
            " / ", export.Validation.All.Select(item => item.Message)));

        Assert.Contains("신뢰가 부족하다", export.Json);   // 잠긴 길 자체는 그대로 나간다
        Assert.DoesNotContain("HideWhenLocked", export.Json);
    }
}
