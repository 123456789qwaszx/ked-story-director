using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터 그래프 카드의 오류 표식</b>은 진단이 짚는 에피소드를 Id로 찾는다 (2026-10-29 · L8).
///
/// 그 전에는 엑셀 행 번호(<c>SourceRow</c>)로 짝을 지었다. 툴에서 만든 에피소드는 행이 0이고 진단의
/// 행은 비어 있어(null) 짝이 맞는 일이 없었다 — 엑셀이 걷힌 뒤로 <b>어떤 에피소드도 붉게 표시되지 않았다</b>.
/// </summary>
public sealed class EpisodeErrorMarkTests
{
    [Fact]
    public void 툴에서_만든_에피소드의_오류가_카드에_표시된다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep_a", "가", 0, 0);
        editor.AddEpisode("ch01", "ep_b", "나", 0, 0);

        // 대사엔트리가 비면 런타임이 재생할 대상이 없다 — 에피소드 자리의 오류다.
        editor.UpdateEpisode("ch01", "ep_a", dialogueEntry: string.Empty);

        ChapterGraphModel model = editor.FindChapter("ch01")!.ToGraphModel("ch01");

        Assert.True(model.EpisodeHasError(model.FindEpisode("ep_a")!));
        Assert.False(model.EpisodeHasError(model.FindEpisode("ep_b")!));
    }

    [Fact]
    public void 간선의_오류는_에피소드에_번지지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep_a", "가", 0, 0);
        editor.AddEpisode("ch01", "ep_b", "나", 0, 0);
        editor.AddEdge("ch01", "ep_a", "ep_b", optionLabel: "간다", conditionLabel: "없는조건");

        ChapterGraphModel model = editor.FindChapter("ch01")!.ToGraphModel("ch01");

        Assert.Contains(model.Diagnostics, item =>
            item.Severity == ChapterDiagnosticSeverity.Error &&
            item.Sheet == ChapterSheetNames.Edges);
        Assert.False(model.EpisodeHasError(model.FindEpisode("ep_a")!));
    }
}
