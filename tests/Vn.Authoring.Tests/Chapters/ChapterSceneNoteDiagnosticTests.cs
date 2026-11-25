using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 주인 없는 장면 메모 (2026-11-16 · scene-notes S3). 메모를 조용히 지우지 않는 대신 검증이 알린다 —
/// 경고이고 내보내기를 막지 않는다.
/// </summary>
public sealed class ChapterSceneNoteDiagnosticTests
{
    private const string Ch = "ch01";

    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter(Ch);
        editor.AddEpisode(Ch, "ep01", "", 0, 0, sceneId: "교실");
        editor.AddEpisode(Ch, "ep02", "", 0, 0, sceneId: "강당");
        return editor;
    }

    private static IReadOnlyList<ChapterDiagnostic> Orphaned(ProjectEditor editor) =>
        editor.FindChapter(Ch)!.ToGraphModel(Ch).Diagnostics
            .Where(item => item.Code == ChapterDiagnosticCode.SceneNoteOrphaned)
            .ToList();

    [Fact]
    public void 주인_있는_메모는_말하지_않는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "교실", "교실");
        editor.SetSceneNote(Ch, "강당", "혼자");

        Assert.Empty(Orphaned(editor));
    }

    [Fact]
    public void 주인_없는_메모마다_경고_하나이고_장면과_첫머리를_짚는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneNote(Ch, "교실", "마감 전날, 다시 시작할지 고른다. 가볍게 열고 결심으로 맺는다\n둘째 줄");
        editor.SetSceneNote(Ch, "강당", "혼자");

        editor.UpdateEpisode(Ch, "ep01", sceneId: "복도");
        editor.RemoveEpisode(Ch, "ep02");

        IReadOnlyList<ChapterDiagnostic> orphaned = Orphaned(editor);

        Assert.Equal(2, orphaned.Count);
        Assert.All(orphaned, item =>
        {
            Assert.Equal(ChapterDiagnosticSeverity.Warning, item.Severity);
            Assert.Equal(ChapterSheetNames.SceneNotes, item.Sheet);
            Assert.Null(item.EpisodeId);
        });
        Assert.Contains("'교실'", orphaned[1].Message);
        Assert.Contains("「마감 전날, 다시 시작할지 고른다. 가볍게…」", orphaned[1].Message);
        Assert.Contains("'강당'", orphaned[0].Message);
    }

    [Fact]
    public void 주인_없는_메모는_검증_오류가_아니고_내보내기를_막지_않는다()
    {
        // 검증 오류 없이 내보내지는 견본(축제 전날)에 주인 없는 메모 하나를 얹는다.
        string manifest = Path.GetFullPath("../../../../../tests/fixtures/festival-eve/festival_eve.vnproject.json");
        var editor = new ProjectEditor(ProjectStore.Load(manifest).Project);
        editor.SetSceneNote("festival_eve", "옥상", "주인 없음");

        ChapterGraphModel model = editor.FindChapter("festival_eve")!.ToGraphModel("festival_eve");
        ChapterValidationResult validation = ChapterValidator.Validate(model, editor.Project);

        Assert.Single(model.Diagnostics, item => item.Code == ChapterDiagnosticCode.SceneNoteOrphaned);
        Assert.False(validation.HasErrors, string.Join("\n", validation.Diagnostics.Select(item => item.Message)));
        Assert.NotNull(ChapterProgressionExporter.ExportValidated(model, validation, editor.Project).Json);
    }
}
