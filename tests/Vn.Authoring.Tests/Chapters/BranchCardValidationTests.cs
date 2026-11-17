using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>분기 카드 검증</b> (2026-11-17 · <c>docs/work-orders/branch-card-orders.md</c> B2) — 분기 카드는 도달성 증명(런타임의 오라클 — 에피소드와
/// 간선만) 밖이라 따로 본다. ① 분기점이 에피소드에 이어져 있다 → 오류(옛 데이터), ② 아무도 부르지 않거나 도달할 수 없는 곳만 부른다 → 경고
/// (진행 JSON에 없어 게임을 깨지 않는다 — 소유자), ③ 줄이 없다 → 오류(Yarn이 줄 없는 노드를 빼 <c>&lt;&lt;detour&gt;&gt;</c>가 없는 노드를 부른다).
/// </summary>
public sealed class BranchCardValidationTests
{
    [Fact]
    public void 도달할_수_있는_에피소드가_부르는_분기_카드는_말이_없다()
    {
        (ProjectEditor editor, DialogueNode root) = World();
        Filled(editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id));

        ChapterValidationResult result = Validate(editor);

        Assert.False(result.HasErrors);
        Assert.Empty(Branch(result));
    }

    [Fact]
    public void 분기점이_에피소드에_이어져_있으면_오류다()
    {
        // 편집 명령은 이렇게 잇지 않는다 — 옛 프로젝트의 것이다(옮기지 않는다 — 소유자). 들렀다 돌아오는 동안 그 에피소드의 선택지는
        // 게임에 나오지 않는다.
        (ProjectEditor editor, DialogueNode root) = World();
        editor.AddEpisode("ch01", "옆길", title: "옆길", 400, 0);
        DialogueNode side = EpisodeNaming.CardFor(editor.Project, "ch01", "옆길")!;
        root.RequireExtension(root.Script.ActiveLines.First().Id).DetourTargetNodeIds.Add(side.Id);

        ChapterDiagnostic error = Assert.Single(Branch(Validate(editor)));

        Assert.Equal((ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.BranchIntoEpisode, "root"),
            (error.Severity, error.Code, error.EpisodeId));
        Assert.Contains("분기 카드", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 아무도_부르지_않는_분기_카드는_경고고_내보내기를_막지_않는다()
    {
        (ProjectEditor editor, DialogueNode root) = World();
        string lineId = root.Script.ActiveLines.First().Id;
        DialogueNode made = Filled(editor.AddBranchMarker(root.Id, lineId));
        editor.RemoveBranchMarker(root.Id, lineId, made.Id);   // 분기점만 걷힌다 — 카드는 남는다

        ChapterValidationResult result = Validate(editor);
        ChapterDiagnostic warning = Assert.Single(Branch(result));

        Assert.Equal((ChapterDiagnosticSeverity.Warning, ChapterDiagnosticCode.BranchCardOrphaned),
            (warning.Severity, warning.Code));
        Assert.Contains(made.Name, warning.Message, StringComparison.Ordinal);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void 도달할_수_없는_곳만_부르면_경고고_사슬도_따른다()
    {
        // 외딴(들어오는 길 없음)이 부르는 분기 카드, 그 분기 카드가 또 부르는 분기 카드 — 둘 다 재생되지 않는다.
        (ProjectEditor editor, _) = World();
        editor.AddEpisode("ch01", "외딴", title: "외딴", 0, 400);
        DialogueNode lonely = EpisodeNaming.CardFor(editor.Project, "ch01", "외딴")!;
        Filled(lonely);
        DialogueNode first = Filled(editor.AddBranchMarker(lonely.Id, lonely.Script.ActiveLines.First().Id));
        DialogueNode second = Filled(editor.AddBranchMarker(first.Id, first.Script.ActiveLines.First().Id));

        List<ChapterDiagnostic> warnings = Branch(Validate(editor)).ToList();

        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(
            (ChapterDiagnosticSeverity.Warning, ChapterDiagnosticCode.BranchCardUnreachable),
            (warning.Severity, warning.Code)));
        Assert.Contains(warnings, warning => warning.Message.Contains(first.Name, StringComparison.Ordinal) &&
                                             warning.Message.Contains("외딴", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Message.Contains(second.Name, StringComparison.Ordinal));

        // 외딴으로 가는 길을 이으면 둘 다 조용해진다.
        editor.AddEdge("ch01", "root", "외딴", optionLabel: "간다");
        Assert.Empty(Branch(Validate(editor)));
    }

    [Fact]
    public void 줄이_없는_분기_카드는_오류다()
    {
        (ProjectEditor editor, DialogueNode root) = World();
        DialogueNode made = editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);

        foreach (ScriptLine line in made.Script.ActiveLines.ToList())
        {
            editor.RetireScriptLine(made.Id, line.Id);
        }

        ChapterDiagnostic error = Assert.Single(Branch(Validate(editor)));

        Assert.Equal((ChapterDiagnosticSeverity.Error, ChapterDiagnosticCode.BranchCardEmpty, "root"),
            (error.Severity, error.Code, error.EpisodeId));
    }

    [Fact]
    public void 분기_카드의_Yarn_이름이_에피소드나_다른_분기_카드와_겹치면_오류다()
    {
        // 분기 카드는 [대본] 탭에서 이름을 자유롭게 고친다 — 같은 이름의 Yarn 노드가 둘이면 컴파일이 깨진다.
        (ProjectEditor editor, DialogueNode root) = World();
        string lineId = root.Script.ActiveLines.First().Id;
        DialogueNode first = Filled(editor.AddBranchMarker(root.Id, lineId));
        DialogueNode second = Filled(editor.AddBranchMarker(root.Id, lineId));

        editor.RenameNode(first.Id, "root");
        editor.RenameNode(second.Id, "root");

        List<ChapterDiagnostic> errors = Branch(Validate(editor)).ToList();

        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Equal(ChapterDiagnosticCode.BranchCardNameTaken, error.Code));
        Assert.Contains(errors, error => error.Message.Contains("에피소드 「root」", StringComparison.Ordinal));

        editor.RenameNode(first.Id, "들름 1");
        editor.RenameNode(second.Id, "들름 2");
        Assert.Empty(Branch(Validate(editor)));
    }

    [Fact]
    public void 분기_카드는_진행_JSON에_실리지_않는다()
    {
        (ProjectEditor editor, DialogueNode root) = World();
        DialogueNode made = Filled(editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id));
        ChapterDocument chapter = editor.Project.Chapters.Single();
        ChapterGraphModel model = chapter.ToGraphModel("ch01");

        ChapterExportResult export = ChapterProgressionExporter.ExportValidated(model, ChapterValidator.Validate(model, editor.Project), editor.Project);

        Assert.NotNull(export.Json);
        Assert.DoesNotContain(made.Name, export.Json!, StringComparison.Ordinal);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static readonly ChapterDiagnosticCode[] BranchCodes =
    [
        ChapterDiagnosticCode.BranchIntoEpisode,
        ChapterDiagnosticCode.BranchCardOrphaned,
        ChapterDiagnosticCode.BranchCardUnreachable,
        ChapterDiagnosticCode.BranchCardEmpty,
        ChapterDiagnosticCode.BranchCardNameTaken
    ];

    private static IEnumerable<ChapterDiagnostic> Branch(ChapterValidationResult result) =>
        result.Diagnostics.Where(item => BranchCodes.Contains(item.Code));

    private static ChapterValidationResult Validate(ProjectEditor editor)
    {
        ChapterDocument chapter = editor.Project.Chapters.Single();
        return ChapterValidator.Validate(chapter.ToGraphModel(chapter.ChapterId), editor.Project);
    }

    /// <summary>카드의 첫 줄에 대사를 적는다 — 빈 줄도 줄이지만 사람이 읽는 시험이 되게.</summary>
    private static DialogueNode Filled(DialogueNode card)
    {
        card.Script.RequireLocale(card.Script.PrimaryLocale).Entries[card.Script.ActiveLines.First().Id] = new LocalizedLine("하린", "한 줄");
        return card;
    }

    /// <summary>ch01 — 시작 에피소드 root(대사 한 줄).</summary>
    private static (ProjectEditor Editor, DialogueNode Root) World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0);
        DialogueNode root = Filled(EpisodeNaming.CardFor(editor.Project, "ch01", "root")!);

        return (editor, root);
    }
}
