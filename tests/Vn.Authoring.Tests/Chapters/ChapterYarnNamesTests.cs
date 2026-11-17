using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>Yarn 노드 이름은 프로젝트에서 하나다</b> (2026-11-17 · <c>docs/work-orders/auto-id-orders.md</c> I2) — 게임은 모든 챕터의 Yarn을 한
/// 프로젝트로 컴파일하므로 다른 챕터의 노드와 이름이 겹치면 오류다(<see cref="ChapterYarnNames"/>).
/// </summary>
public sealed class ChapterYarnNamesTests
{
    [Fact]
    public void 다른_챕터의_에피소드와_이름이_겹치면_두_챕터_모두_오류다()
    {
        ProjectEditor editor = new(new StoryProject());
        editor.AddDialogueNode(TestChapter.Board(editor, "ch01").Id, 0, 0, name: "intro", sceneId: "scene01");
        editor.AddDialogueNode(TestChapter.Board(editor, "ch02").Id, 0, 0, name: "intro", sceneId: "scene01");
        editor.UpdateChapterDisplayName("ch02", "둘째 날");

        ChapterDiagnostic one = Assert.Single(Taken(editor, "ch01"));
        Assert.Single(Taken(editor, "ch02"));

        Assert.Equal(ChapterDiagnosticSeverity.Error, one.Severity);
        Assert.Equal("intro", one.EpisodeId);
        Assert.Contains("'intro'", one.Message, StringComparison.Ordinal);
        Assert.Contains("챕터 「둘째 날」", one.Message, StringComparison.Ordinal);   // 상대가 어느 챕터인지 말한다
    }

    [Fact]
    public void 분기_카드도_다른_챕터의_노드와_겹치면_오류다()
    {
        ProjectEditor editor = new(new StoryProject());
        editor.AddDialogueNode(TestChapter.Board(editor, "ch01").Id, 0, 0, name: "side", sceneId: "scene01");
        DialogueNode home = editor.AddDialogueNode(TestChapter.Board(editor, "ch02").Id, 0, 0, sceneId: "scene01");
        editor.AddBranchMarker(home.Id, home.Script.ActiveLines.First().Id, name: "side");

        ChapterDiagnostic branch = Assert.Single(Taken(editor, "ch02"));
        Assert.Contains("분기 카드 「side」", branch.Message, StringComparison.Ordinal);
        Assert.Equal(EpisodeNaming.EpisodeIdOf(home), branch.EpisodeId);   // 집 에피소드를 짚는다
    }

    [Fact]
    public void 자동_ID로_세우면_챕터가_달라도_겹치지_않는다()
    {
        ProjectEditor editor = new(new StoryProject());

        foreach (string chapterId in new[] { "ch01", "ch02" })
        {
            DialogueNode episode = editor.AddDialogueNode(TestChapter.Board(editor, chapterId).Id, 0, 0, sceneId: "scene01");
            editor.AddBranchMarker(episode.Id, episode.Script.ActiveLines.First().Id);
        }

        Assert.Empty(Taken(editor, "ch01"));
        Assert.Empty(Taken(editor, "ch02"));
    }

    private static IEnumerable<ChapterDiagnostic> Taken(ProjectEditor editor, string chapterId) =>
        ChapterValidator.Validate(editor.FindChapter(chapterId)!.ToGraphModel(chapterId), editor.Project).All
            .Where(item => item.Code == ChapterDiagnosticCode.YarnNodeNameTaken);
}
