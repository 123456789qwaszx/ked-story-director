using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>분기로 들어오는 에피소드는 실제 연결로 알아본다</b> (2026-11-17 소유자 · <see cref="ChapterBranchReach"/>).
///
/// 도달할 수 있는 에피소드의 분기점이 들르는 에피소드는 「경로 없음」 오류가 아니라 「분기로 들어온다」 알림이다. 분기점이 있는 에피소드가 도달할 수
/// 없으면 들르는 쪽도 오류다. 「분기 추가」는 더 이상 `도달불가 허용` 깃발을 달지 않는다 — 분기를 끊으면 고아 카드가 오류로 다시 잡힌다.
/// </summary>
public sealed class BranchReachTests
{
    /// <summary>root → a(선택지 「가자」). root가 시작이다.</summary>
    private static ProjectEditor Chapter()
    {
        var editor = new ProjectEditor(new StoryProject { Title = "분기 도달" });
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "시작", 0, 0);
        editor.AddEpisode("ch01", "a", title: "다음", 220, 0);
        editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        return editor;
    }

    private static DialogueNode Card(ProjectEditor editor, string episodeId) =>
        EpisodeNaming.CardFor(editor.Project, "ch01", episodeId)!;

    /// <summary>그 카드의 첫 줄 분기점을 다른 카드에 잇는다 — 판에서 분기점을 끌어 카드에 놓은 것과 같은 모델.</summary>
    private static void Branch(ProjectEditor editor, string fromEpisodeId, string toEpisodeId)
    {
        DialogueNode from = Card(editor, fromEpisodeId);
        from.RequireExtension(from.Script.ActiveLines.First().Id).DetourTargetNodeIds.Add(Card(editor, toEpisodeId).Id);
    }

    private static ChapterValidationResult Validate(ProjectEditor editor) =>
        ChapterValidator.Validate(editor.FindChapter("ch01")!.ToGraphModel("ch01"), editor.Project);

    private static ChapterDiagnostic? About(ChapterValidationResult result, string episodeId, ChapterDiagnosticCode code) =>
        result.All.FirstOrDefault(item => item.EpisodeId == episodeId && item.Code == code);

    [Fact]
    public void 새로_만든_에피소드를_분기점에_이으면_경로_없음_오류가_아니다()
    {
        // 소유자 보고 — 분기 카드를 지우고 [+]로 새 에피소드를 만들어 분기점에 이었더니 「들어오는 간선이 없다」 오류가 났다.
        ProjectEditor editor = Chapter();
        editor.AddEpisode("ch01", "x", title: "곁가지", 440, 0);
        Assert.Equal(ChapterDiagnosticSeverity.Error, About(Validate(editor), "x", ChapterDiagnosticCode.EpisodeUnreachable)?.Severity);

        Branch(editor, "a", "x");
        ChapterValidationResult result = Validate(editor);

        Assert.Null(About(result, "x", ChapterDiagnosticCode.EpisodeUnreachable));
        ChapterDiagnostic note = About(result, "x", ChapterDiagnosticCode.EpisodeEnteredByBranch)!;
        Assert.Equal(ChapterDiagnosticSeverity.Info, note.Severity);
        Assert.Contains("「다음」의 분기로 들어옵니다", note.Message, StringComparison.Ordinal);
        Assert.Contains("x", result.Reachability.ReachableEpisodeIds);
        Assert.False(result.HasErrors);

        // 진행 JSON도 막히지 않는다.
        ChapterGraphModel model = editor.FindChapter("ch01")!.ToGraphModel("ch01");
        Assert.False(ChapterProgressionExporter.ExportValidated(model, result, editor.Project).Refused);
    }

    [Fact]
    public void 분기_추가가_세운_카드는_깃발이_없고_분기를_끊으면_고아로_다시_잡힌다()
    {
        ProjectEditor editor = Chapter();
        DialogueNode root = Card(editor, "root");
        DialogueNode created = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        string branchEpisode = EpisodeNaming.EpisodeIdOf(created);

        Assert.False(editor.FindChapter("ch01")!.Episodes.Single(episode => episode.EpisodeId == branchEpisode).AllowUnreachable);
        Assert.Equal(ChapterDiagnosticSeverity.Info,
            About(Validate(editor), branchEpisode, ChapterDiagnosticCode.EpisodeEnteredByBranch)?.Severity);

        // 분기를 지우면 카드는 남는다(7절 T) — 이제 아무도 안 들르는 고아다. 깃발로 숨지 않고 오류로 선다.
        Assert.NotNull(editor.RemoveBranchMarker(root.Id, root.Script.ActiveLines.First().Id, created.Id));
        ChapterValidationResult after = Validate(editor);

        Assert.Equal(ChapterDiagnosticSeverity.Error, About(after, branchEpisode, ChapterDiagnosticCode.EpisodeUnreachable)?.Severity);
        Assert.Null(About(after, branchEpisode, ChapterDiagnosticCode.EpisodeEnteredByBranch));
    }

    [Fact]
    public void 도달할_수_없는_에피소드의_분기점이_들르는_카드도_오류다()
    {
        // 소유자: 분기점도 도달할 수 없으면 경고해야 한다 — 예외를 두지 않는다.
        ProjectEditor editor = Chapter();
        editor.AddEpisode("ch01", "lonely", title: "외딴", 0, 400);   // 들어오는 길이 없다
        editor.AddEpisode("ch01", "y", title: "외딴 곁가지", 220, 400);
        Branch(editor, "lonely", "y");

        ChapterValidationResult result = Validate(editor);

        Assert.Equal(ChapterDiagnosticSeverity.Error, About(result, "lonely", ChapterDiagnosticCode.EpisodeUnreachable)?.Severity);
        Assert.Equal(ChapterDiagnosticSeverity.Error, About(result, "y", ChapterDiagnosticCode.EpisodeUnreachable)?.Severity);
        Assert.DoesNotContain("y", result.Reachability.ReachableEpisodeIds);
    }

    [Fact]
    public void 분기_카드의_분기도_사슬로_따른다()
    {
        ProjectEditor editor = Chapter();
        editor.AddEpisode("ch01", "b1", title: "곁가지 하나", 440, 0);
        editor.AddEpisode("ch01", "b2", title: "곁가지 둘", 660, 0);
        Branch(editor, "b1", "b2");   // 차례를 일부러 뒤집는다 — 사슬은 순서와 상관없이 닿는다
        Branch(editor, "root", "b1");

        ChapterValidationResult result = Validate(editor);

        Assert.Equal(ChapterDiagnosticSeverity.Info, About(result, "b1", ChapterDiagnosticCode.EpisodeEnteredByBranch)?.Severity);
        Assert.Contains("「곁가지 하나」의 분기로", About(result, "b2", ChapterDiagnosticCode.EpisodeEnteredByBranch)!.Message, StringComparison.Ordinal);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void 선택지로도_들어오는_카드는_분기_알림을_달지_않는다()
    {
        // 간선으로 이미 도달하면 증명이 도달 가능이라 했다 — 분기로 「만」 들어오는 것이 아니다.
        ProjectEditor editor = Chapter();
        Branch(editor, "root", "a");

        ChapterValidationResult result = Validate(editor);

        Assert.Null(About(result, "a", ChapterDiagnosticCode.EpisodeEnteredByBranch));
        Assert.Null(About(result, "a", ChapterDiagnosticCode.EpisodeUnreachable));
    }
}
