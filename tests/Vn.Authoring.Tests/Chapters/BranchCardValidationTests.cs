using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>분기 카드 검증</b> (2026-11-17 · <c>docs/work-orders/branch-card-orders.md</c> B2) — 분기 카드는 도달성 증명(런타임의 오라클 — 에피소드와
/// 간선만) 밖이라 따로 본다. ① 분기점이 에피소드에 이어져 있다 → 오류(옛 데이터), ② 아무도 부르지 않거나 도달할 수 없는 곳만 부른다 → 경고
/// (진행 JSON에 없어 게임을 깨지 않는다 — 소유자), ③ 줄이 없다 → 오류(Yarn이 줄 없는 노드를 빼 <c>&lt;&lt;detour&gt;&gt;</c>가 없는 노드를 부른다),
/// ④ 분기 카드 안에 분기가 있다 → 오류(그 전의 파일 — usability UX27).
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
    public void 분기점이_에피소드에_이어진_프로젝트는_받지_않는다()
    {
        // 편집 명령은 이렇게 잇지 않는다 — 그런 파일은 읽기 · 저장 · 되돌리기가 지나는 검증이 거절한다(2026-11-18 · old-shapes O2,
        // ⛔ 그 전에는 검증 보고의 오류 BranchIntoEpisode로 짚고 [구성]에 그렸다).
        (ProjectEditor editor, DialogueNode root) = World();
        editor.AddEpisode("ch01", "옆길", title: "옆길", 400, 0, sceneId: "옆길");
        DialogueNode side = EpisodeNaming.CardFor(editor.Project, "ch01", "옆길")!;
        root.RequireExtension(root.Script.ActiveLines.First().Id).DetourTargetNodeIds.Add(side.Id);

        InvalidDataException refused = Assert.Throws<InvalidDataException>(() => JsonSupport.ValidateProject(editor.Project));
        Assert.Contains("분기 카드", refused.Message, StringComparison.Ordinal);
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
    public void 도달할_수_없는_곳만_부르면_경고다()
    {
        // 외딴(들어오는 길 없음)이 부르는 분기 카드 — 재생되지 않는다.
        (ProjectEditor editor, _) = World();
        editor.AddEpisode("ch01", "외딴", title: "외딴", 0, 400, sceneId: "외딴");
        DialogueNode lonely = EpisodeNaming.CardFor(editor.Project, "ch01", "외딴")!;
        Filled(lonely);
        DialogueNode made = Filled(editor.AddBranchMarker(lonely.Id, lonely.Script.ActiveLines.First().Id));

        ChapterDiagnostic warning = Assert.Single(Branch(Validate(editor)));

        Assert.Equal((ChapterDiagnosticSeverity.Warning, ChapterDiagnosticCode.BranchCardUnreachable), (warning.Severity, warning.Code));
        Assert.Contains(made.Name, warning.Message, StringComparison.Ordinal);
        Assert.Contains("외딴", warning.Message, StringComparison.Ordinal);

        // 외딴으로 가는 길을 이으면 조용해진다.
        editor.AddEdge("ch01", "root", "외딴", optionLabel: "간다");
        Assert.Empty(Branch(Validate(editor)));
    }

    [Fact]
    public void 분기_카드_안의_분기점은_오류다()
    {
        // 편집기는 만들지 않는다(usability UX27) — 그 전의 파일에 남은 것을 그대로 지어 본다.
        (ProjectEditor editor, DialogueNode root) = World();
        string first = root.Script.ActiveLines.First().Id;
        DialogueNode outer = Filled(editor.AddBranchMarker(root.Id, first));
        DialogueNode inner = Filled(editor.AddBranchMarker(root.Id, first));
        outer.RequireExtension(outer.Script.ActiveLines.First().Id).DetourTargetNodeIds.Add(inner.Id);

        ChapterValidationResult result = Validate(editor);
        ChapterDiagnostic error = Assert.Single(Branch(result), item => item.Code == ChapterDiagnosticCode.BranchCardBranches);
        Assert.Equal((ChapterDiagnosticSeverity.Error, "root"), (error.Severity, error.EpisodeId));
        Assert.Contains(outer.Name, error.Message, StringComparison.Ordinal);
        Assert.True(result.HasErrors);   // 내보내지 않는다

        // 분기점을 지우면(줄의 ＋ → ✕) 조용해진다 — 지우기는 받는다.
        Assert.NotNull(editor.RemoveBranchMarker(outer.Id, outer.Script.ActiveLines.First().Id, inner.Id));
        Assert.DoesNotContain(Branch(Validate(editor)), item => item.Code == ChapterDiagnosticCode.BranchCardBranches);
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
        ChapterDiagnosticCode.BranchCardOrphaned,
        ChapterDiagnosticCode.BranchCardUnreachable,
        ChapterDiagnosticCode.BranchCardEmpty,
        ChapterDiagnosticCode.BranchCardNameTaken,
        ChapterDiagnosticCode.BranchCardBranches
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
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        DialogueNode root = Filled(EpisodeNaming.CardFor(editor.Project, "ch01", "root")!);

        return (editor, root);
    }
}
