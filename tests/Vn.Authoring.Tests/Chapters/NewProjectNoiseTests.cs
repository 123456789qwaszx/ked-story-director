using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 소유자 보고 — "완전 새 프로젝트인데 검증 보고가 동기화 거부·경고를 계속 띄운다."
/// 갓 만든 챕터·에피소드는 <b>아직 아무것도 잘못하지 않았다</b>. 그 상태에서 뜨는 것은
/// 사람이 고칠 것이 아니라 소음이다.
///
/// (2026-10-29 · D2: 대본 워크북의 파일 이름(분해형·.xlsm)·템플릿 행 검사는 엑셀 연동과 함께 걷혔다.)
/// </summary>
public sealed class NewProjectNoiseTests
{
    [Fact]
    public void 갓_만든_챕터와_빈_에피소드는_고칠_것이_없다()
    {
        // 툴이 하는 그대로: [＋ 챕터](스탯을 깔고 판을 세운다) → [＋ 대사 노드](새 장면과 함께 — 2026-11-17).
        var editor = new ProjectEditor(new StoryProject());

        editor.EnsureChapter("ch01");
        editor.AddChapterStat("ch01", "trust", initial: 0, minimum: 0, maximum: 10);
        editor.AddChapterStat("ch01", "anger", initial: 0, minimum: 0, maximum: 10);
        editor.EnsureChapterBoard("ch01");

        editor.AddEpisode("ch01", "ep01", title: string.Empty, 0, 0, sceneId: "scene01");

        ChapterGraphModel model = editor.FindChapter("ch01")!.ToGraphModel("ch01");
        ChapterValidationResult validation = ChapterValidator.Validate(model, editor.Project);

        string[] shown = validation.All
            .Where(item => item.Severity != ChapterDiagnosticSeverity.Info)
            .Select(item => $"[{item.Severity}] {item.Code}: {item.Message}")
            .ToArray();

        Assert.Empty(shown);
    }

    [Fact]
    public void 장면_없는_에피소드는_경고고_내보내기를_막지_않는다()
    {
        // 2026-11-17 소유자 — 에피소드는 늘 장면에 속한다. 툴은 장면 없는 에피소드를 만들지 않으니 옛 데이터에서 온다 — 경고로 짚는다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "ep01", title: "첫 화", 0, 0, sceneId: "scene01");
        editor.AddEpisode("ch01", "ep02", title: "장면 없음", 0, 300);
        editor.AddEdge("ch01", "ep01", "ep02", optionLabel: "간다");

        ChapterGraphModel model = editor.FindChapter("ch01")!.ToGraphModel("ch01");
        ChapterValidationResult validation = ChapterValidator.Validate(model, editor.Project);

        ChapterDiagnostic warning = Assert.Single(validation.All, item => item.Code == ChapterDiagnosticCode.EpisodeWithoutScene);
        Assert.Equal((ChapterDiagnosticSeverity.Warning, "ep02"), (warning.Severity, warning.EpisodeId));
        Assert.Contains("장면 없음", warning.Message, StringComparison.Ordinal);
        Assert.False(validation.HasErrors);
    }
}
