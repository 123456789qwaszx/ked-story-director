using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터 개명은 이름을 지고 있는 것을 함께 옮긴다</b> (2026-08-24 소유자 보고:
/// "챕터의 이름을 바꾸니까, 에피소드들의 대화가 없다고 나오고, 엑셀도 새로운게 열리네.
/// 또 연출그래프에서도 조건노드가 이전의 챕터를 받고 있어").
///
/// 옮기는 셋: <c>Project.Chapters</c>의 Id · 판 이름 · 조건 공급 노드 <c>챕터 {Id} 조건</c>
/// (excel-boundary C2). 한 변경이라 되돌리기 한 칸이다. 에피소드의 카드와 원고는
/// 그 판 위에 서 있으므로 판과 함께 간다.
///
/// ⚠ <b>이 파일의 첫 두 테스트가 신고 그 자체다.</b> 나머지는 고치면서 새로 생긴 사고
/// 자리(이름 충돌)를 막는다.
///
/// (2026-10-29 · D2: 워크북·대본 폴더를 옮기던 일과 그 검사(잠김·폴더 충돌)는 엑셀 연동과 함께 걷혔다.)
/// </summary>
public sealed class ChapterRenamerTests : IDisposable
{
    private const string OldId = "ch05";
    private const string NewId = "ch05_다시";
    private const string EpisodeId = "main05.02";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "vn-chapter-rename", Guid.NewGuid().ToString("N"));

    public ChapterRenamerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string ProjectPath => Path.Combine(_root, "예제" + ProjectManifestJson.FileExtension);

    // ── 신고 ────────────────────────────────────────────────────────────────

    [Fact]
    public void 에피소드의_원고가_새_이름에서도_그대로다()
    {
        // ⛔ 이것이 "에피소드들의 대화가 없다"의 정체였다. 개명이 이름을 지고 있는 것 하나만
        //    옮기고 원고가 사는 자리는 옛 이름에 두었다 — 새 이름으로 찾으니 비어 있어 툴은
        //    대사가 없다고 말했다. 원고는 옛 자리에 그대로 살아 있는데도.
        ProjectEditor editor = Build();

        Assert.True(ChapterRenamer.Rename(editor, ProjectPath, OldId, NewId).Renamed);

        DialogueNode card = EpisodeNaming.CardFor(editor.Project, NewId, EpisodeId)!;
        Assert.Null(EpisodeNaming.CardFor(editor.Project, OldId, EpisodeId));

        // 원고가 그대로다 — 옮긴 것이지 새로 만든 것이 아니다.
        ScriptDocument script = card.Script;
        Assert.Equal("복도는 조용했다", script.Text(script.ActiveLines.First().Id, script.PrimaryLocale).Text);
    }

    [Fact]
    public void 개명_뒤에도_대사의_갈래가_그_챕터의_조건을_읽는다()
    {
        // ⛔ "연출그래프에서도 조건노드가 이전의 챕터를 받고 있어" — 공급 노드 시절에는 옛 이름의 배관이 판에 남으면 조건이 둘로 갈렸다.
        //    이제 카드는 제 판의 이름(= 챕터 Id)으로 챕터를 찾으므로 판 이름이 따라가면 끝이다(condition-supply C1).
        ProjectEditor editor = Build();
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, OldId, EpisodeId)!;
        editor.SetLineTransition(card.Id, card.Script.ActiveLines.First().Id, LineConditionTransition.BeginIf("신뢰높음"));

        ChapterRenamer.Result result = ChapterRenamer.Rename(editor, ProjectPath, OldId, NewId);
        Assert.True(result.Renamed, result.Failure);

        editor.UpdateChapterCondition(NewId, "신뢰높음", "trust >= 4");

        Assert.Equal(
            "stat(\"trust\") >= 4",
            Vn.Authoring.Flow.AvailableConditionResolver.Resolve(editor.Project, card.Id).Find("신뢰높음")!.Expression);
        Assert.Empty(Vn.Authoring.Flow.ConditionFlowResolver.Resolve(editor.Project.FindDialogue(card.Id)!, editor.Project).Problems);
        Assert.All(editor.Project.EnumerateNodes(), node => Assert.IsType<DialogueNode>(node));
    }

    [Fact]
    public void 판_이름도_따라간다()
    {
        // 챕터 = 판 1:1 (G-1 v2). 판 이름이 곧 챕터 Id라, 어긋나면 카드가 제 챕터(조건 목록)를 못 찾는다.
        ProjectEditor editor = Build();

        ChapterRenamer.Rename(editor, ProjectPath, OldId, NewId);

        Assert.Contains(editor.Project.Files, file => file.Name == NewId);
        Assert.DoesNotContain(editor.Project.Files, file => file.Name == OldId);
    }

    [Fact]
    public void 챕터_모델의_Id가_바뀐다()
    {
        // ⛔ 2026-10-29까지 여기가 빠져 있었다 — 판은 새 이름인데 Project.Chapters는
        //    옛 이름이라, 저장하고 다시 열면 챕터 목록은 옛 이름이고 판만 새 이름이었다.
        ProjectEditor editor = Build();

        Assert.True(ChapterRenamer.Rename(editor, ProjectPath, OldId, NewId).Renamed);

        Assert.NotNull(editor.FindChapter(NewId));
        Assert.Null(editor.FindChapter(OldId));
        Assert.Contains(editor.FindChapter(NewId)!.Episodes, episode => episode.EpisodeId == EpisodeId);
    }

    [Fact]
    public void 개명은_되돌리기_한_칸이다()
    {
        // 사용자 동작 하나 = 되돌리기 한 번. Id·판이 각각 한 칸이면 Ctrl+Z 한 번에 둘이
        // 어긋난 채 멈춘다.
        ProjectEditor editor = Build();

        ChapterRenamer.Rename(editor, ProjectPath, OldId, NewId);
        editor.Undo();

        Assert.NotNull(editor.FindChapter(OldId));
        Assert.Null(editor.FindChapter(NewId));
        Assert.Contains(editor.Project.Files, file => file.Name == OldId);
    }

    [Fact]
    public void 프로젝트에_없는_챕터는_개명하지_않는다()
    {
        ProjectEditor editor = Build();

        ChapterRenamer.Result result = ChapterRenamer.Rename(editor, ProjectPath, "없는챕터", NewId);

        Assert.False(result.Renamed);
        Assert.Contains("프로젝트에 없습니다", result.Failure);
    }

    // ── 고치면서 생긴 사고 자리 ─────────────────────────────────────────────

    [Fact]
    public void 새_이름의_챕터가_이미_있으면_아무것도_건드리지_않는다()
    {
        // 겹치는 이름으로 옮기면 두 챕터가 판 하나를 두고 다툰다 — 거절하고, 거절은 아무것도
        // 안 바꾼다.
        ProjectEditor editor = Build();
        editor.EnsureChapter(NewId);
        long revision = editor.Revision;

        ChapterRenamer.Result result = ChapterRenamer.Rename(editor, ProjectPath, OldId, NewId);

        Assert.False(result.Renamed);
        Assert.Contains("이미 있습니다", result.Failure);
        Assert.Equal(revision, editor.Revision);
        Assert.Contains(editor.Project.Files, file => file.Name == OldId);
    }

    [Fact]
    public void 개명은_옛_이름의_진행_JSON을_건드리지_않고_이름을_말한다()
    {
        // 개명은 모델만 바꾼다(되돌리기 한 칸). 새 이름의 파일은 바로 뒤의 내보내기가 쓰고 옛 이름의 파일은 같은
        // 내보내기가 걷는다(ChapterExportService). 화면은 이 이름으로 걷혔는지 말한다.
        ProjectEditor editor = Build();

        string export = ChapterExportService.ExportPathFor(ProjectPath, OldId);
        Directory.CreateDirectory(Path.GetDirectoryName(export)!);
        File.WriteAllText(export, "{}");

        ChapterRenamer.Result result = ChapterRenamer.Rename(editor, ProjectPath, OldId, NewId);

        Assert.True(result.Renamed, result.Failure);
        Assert.Equal(Path.GetFileName(export), result.StaleExport);
        Assert.True(File.Exists(export));
    }

    [Fact]
    public void 아직_저장하지_않은_프로젝트도_챕터를_개명한다()
    {
        // 원본은 프로젝트다 — 내보낸 파일을 볼 자리가 아직 없을 뿐, 개명을 막을 이유가 아니다.
        ProjectEditor editor = Build();

        ChapterRenamer.Result result = ChapterRenamer.Rename(editor, null, OldId, NewId);

        Assert.True(result.Renamed);
        Assert.Null(result.StaleExport);
        Assert.NotNull(editor.FindChapter(NewId));
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>챕터(스탯·조건·에피소드 하나) + 판 + 원고가 든 카드가 선 프로젝트.</summary>
    private static ProjectEditor Build()
    {
        var editor = new ProjectEditor(new StoryProject { Title = "개명" });

        ChapterDocument chapter = editor.EnsureChapter(OldId);
        chapter.Stats.Add(new ChapterStat("trust", 0, 0, 5));

        // 에피소드를 세우면 판과 카드가 함께 선다.
        editor.AddEpisode(OldId, EpisodeId, "조용한 복도", 0, 0, sceneId: EpisodeId);
        editor.AddChapterCondition(OldId, "신뢰높음", "trust >= 3", "라루를 신뢰");

        DialogueNode card = EpisodeNaming.CardFor(editor.Project, OldId, EpisodeId)!;
        ScriptDocument script = card.Script;
        editor.SetScriptLineText(card.Id, script.ActiveLines.First().Id, "윌로", "복도는 조용했다");

        return editor;
    }
}
