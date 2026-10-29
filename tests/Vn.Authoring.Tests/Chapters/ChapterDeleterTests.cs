using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터 제거는 그 챕터와 판을 함께 걷는다</b> (2026-08-25 소유자: "챕터를
/// 만들 수는 있는데, 삭제할 방법이 없어 … 당연하지만, 그렇게 하면 연출그래프에 있던 것도
/// 모두 자동으로 제거되도록").
///
/// ⛔ 그전에는 "폴더에서 사람이 지우세요"였다. 그 길은 <b>절반만 지운다</b> — 파일은
/// 사라지는데 판과 노드는 프로젝트에 남아, 이름이 겹치는 번들과 "부르는 노드가
/// YarnProject에 없다"로 뒤늦게 터졌다. <b>사람이 손으로 못 지우는 쪽이 남는 것</b>이
/// 문제였으므로 여기서 재는 것도 그 남는 쪽이다.
///
/// (2026-10-29 · D2: 워크북·대본 폴더를 <c>.bak</c>으로 밀던 일과 그 검사는 엑셀 연동과 함께 걷혔다.)
/// </summary>
public sealed class ChapterDeleterTests : IDisposable
{
    private const string Doomed = "ch05";
    private const string Kept = "ch06";
    private const string EpisodeId = "main05.02";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "vn-chapter-delete", Guid.NewGuid().ToString("N"));

    public ChapterDeleterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string ProjectPath => Path.Combine(_root, "예제" + ProjectManifestJson.FileExtension);

    // ── 요청 그 자체 ────────────────────────────────────────────────────────

    [Fact]
    public void 챕터_자체도_프로젝트에서_걷힌다()
    {
        // ⛔ R-F가 남긴 구멍 (2026-09-16). 뒤집기 전에는 챕터가 곧 워크북이라 판만 걷으면
        //    됐다. 이제 챕터는 `Project.Chapters`에 살고, 목록·트리·검증이 전부 그것을 본다 —
        //    안 걷으면 <b>지웠다고 말한 챕터가 그대로 보인다</b>.
        ProjectEditor editor = Build();

        Assert.True(ChapterDeleter.Delete(editor, ProjectPath, Doomed).Deleted);

        Assert.Null(editor.FindChapter(Doomed));
        Assert.Equal([Kept], editor.Project.Chapters.Select(chapter => chapter.ChapterId).ToArray());
    }

    [Fact]
    public void 되돌리기_한_번에_챕터와_판이_함께_돌아온다()
    {
        // 둘이 한 번의 변경이어야 한다 — 갈라 두면 되돌린 뒤 챕터는 있는데 판이 없는
        // (또는 그 반대) 상태가 남고, 그 상태에서는 새 노드가 갈 자리가 없다.
        ProjectEditor editor = Build();

        ChapterDeleter.Delete(editor, ProjectPath, Doomed);
        editor.Undo();

        Assert.NotNull(editor.FindChapter(Doomed));
        Assert.Contains(editor.Project.Files, file =>
            string.Equals(file.Name, Doomed, StringComparison.Ordinal));

        // 카드와 그 원고까지 돌아온다 — 챕터 하나는 몇 달치 원고일 수 있다.
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Doomed, EpisodeId)!;
        Assert.Equal("복도는 조용했다", FirstLine(editor.Project, card));
    }

    [Fact]
    public void 판과_그_위의_노드가_함께_걷힌다()
    {
        // "연출그래프에 있던 것도 모두 자동으로 제거되도록" — 에피소드 노드도, 조건 공급
        // 배관도 그 판에 서 있으므로 판이 걷히면 함께 간다.
        ProjectEditor editor = Build();

        ChapterDeleter.Result result = ChapterDeleter.Delete(editor, ProjectPath, Doomed);

        Assert.True(result.Deleted, result.Failure);
        Assert.True(result.NodesRemoved > 0);

        Assert.DoesNotContain(editor.Project.Files, file =>
            string.Equals(file.Name, Doomed, StringComparison.Ordinal));

        // 배관이 프로젝트 어디에도 안 남는다 — 남으면 다음 공급이 옛 챕터를 되살린다.
        Assert.DoesNotContain(
            editor.Project.EnumerateNodes().OfType<SetNode>(),
            node => string.Equals(
                node.Name, ChapterBoardSupply.ConditionSupplyNodeName(Doomed), StringComparison.Ordinal));

        // 남긴 챕터는 안 건드린다 — 같은 EpisodeId의 카드도 그 판에 그대로 있다.
        Assert.Contains(editor.Project.Files, file =>
            string.Equals(file.Name, Kept, StringComparison.Ordinal));
        Assert.NotNull(EpisodeNaming.CardFor(editor.Project, Kept, EpisodeId));
    }

    [Fact]
    public void 다시_열어도_지운_챕터가_되살아나지_않는다()
    {
        // 판만 걷고 챕터를 두면 다음에 열 때 판 맞추기가 그 챕터의 판을 <b>다시 세운다</b>.
        ProjectEditor editor = Build();

        ChapterDeleter.Delete(editor, ProjectPath, Doomed);
        ProjectStore.Save(ProjectPath, editor.Project);

        var reopened = new ProjectEditor(ProjectStore.Load(ProjectPath).Project);
        reopened.AlignBoardsWithChapters();

        Assert.Equal([Kept], reopened.Project.Chapters.Select(chapter => chapter.ChapterId).ToArray());
        Assert.DoesNotContain(reopened.Project.Files, file =>
            string.Equals(file.Name, Doomed, StringComparison.Ordinal));
    }

    // ── 고치면서 생긴 사고 자리 ─────────────────────────────────────────────

    [Fact]
    public void 마지막_판은_지우지_않고_아무것도_건드리지_않는다()
    {
        // 판이 하나뿐이면 새 노드가 갈 자리가 없어진다(ProjectEditor의 오랜 불변식).
        //
        // ⚠ 중요한 것은 <b>거절이 아무것도 안 바꾸는가</b>다. 챕터만 걷고 거절하면
        //    판은 남는데 챕터가 없는 — 이 기능이 고치려던 바로 그 반쪽 상태가 된다.
        ProjectEditor editor = Build(secondBoard: false);
        long revision = editor.Revision;

        ChapterDeleter.Result result = ChapterDeleter.Delete(editor, ProjectPath, Doomed);

        Assert.False(result.Deleted);
        Assert.Contains("마지막 판", result.Failure!, StringComparison.Ordinal);

        Assert.Equal(revision, editor.Revision);
        Assert.NotNull(editor.FindChapter(Doomed));
        Assert.Contains(editor.Project.Files, file =>
            string.Equals(file.Name, Doomed, StringComparison.Ordinal));
    }

    [Fact]
    public void 내보낸_진행_JSON은_지우지_않고_이름을_말한다()
    {
        // 산출물이라도 남의 폴더(개발자가 가져가는 자리)에 있는 파일이다. 그냥 두면
        // 런타임이 없는 챕터를 계속 싣게 되므로, 지우는 대신 <b>이름을 말한다</b>.
        ProjectEditor editor = Build();

        string export = ChapterExportService.ExportPathFor(ProjectPath, Doomed);
        Directory.CreateDirectory(Path.GetDirectoryName(export)!);
        File.WriteAllText(export, "{}");

        ChapterDeleter.Result result = ChapterDeleter.Delete(editor, ProjectPath, Doomed);

        Assert.True(result.Deleted, result.Failure);
        Assert.Equal(Path.GetFileName(export), result.StaleExport);
        Assert.True(File.Exists(export));
    }

    [Fact]
    public void 내보낸_것이_없으면_말할_것도_없다()
    {
        ProjectEditor editor = Build();

        ChapterDeleter.Result result = ChapterDeleter.Delete(editor, ProjectPath, Doomed);

        Assert.True(result.Deleted, result.Failure);
        Assert.Null(result.StaleExport);
    }

    [Fact]
    public void 아직_저장하지_않은_프로젝트도_챕터를_지운다()
    {
        // 원본은 프로젝트다 — 내보낸 파일을 볼 자리가 아직 없을 뿐, 제거를 막을 이유가 아니다.
        ProjectEditor editor = Build();

        ChapterDeleter.Result result = ChapterDeleter.Delete(editor, projectManifestPath: null, Doomed);

        Assert.True(result.Deleted, result.Failure);
        Assert.Null(result.StaleExport);
        Assert.Null(editor.FindChapter(Doomed));
    }

    [Fact]
    public void 없는_챕터는_조용히_성공하지_않는다()
    {
        ProjectEditor editor = Build();

        ChapterDeleter.Result result = ChapterDeleter.Delete(editor, ProjectPath, "없는챕터");

        Assert.False(result.Deleted);
        Assert.Contains("없는챕터", result.Failure!, StringComparison.Ordinal);
    }

    // ── 무대 ────────────────────────────────────────────────────────────────

    /// <summary>챕터(스탯·조건·에피소드 하나) + 판 + 원고가 든 카드 + 조건 배관이 선 프로젝트.</summary>
    /// <param name="secondBoard">
    /// 남길 챕터를 하나 더 세운다. 판이 하나뿐이면 제거가 <b>정당하게</b> 거절되므로,
    /// 지워지는 길을 재려면 둘이 있어야 한다. 남길 챕터에도 <b>같은 EpisodeId</b>가 있다 —
    /// EpisodeId는 챕터 안에서만 유일하다.
    /// </param>
    private static ProjectEditor Build(bool secondBoard = true)
    {
        var editor = new ProjectEditor(new StoryProject { Title = "제거" });

        ChapterDocument chapter = editor.EnsureChapter(Doomed);
        chapter.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 5));

        // 에피소드를 세우면 판과 카드가 함께 서고, 판이 선 뒤의 조건은 판의 공급 노드로 간다.
        editor.AddEpisode(Doomed, EpisodeId, "조용한 복도", 0, 0);
        editor.AddChapterCondition(Doomed, "신뢰높음", "trust >= 3", "라루를 신뢰");

        DialogueNode card = EpisodeNaming.CardFor(editor.Project, Doomed, EpisodeId)!;
        ScriptDocument script = editor.Project.FindScript(card.ScriptId)!;
        editor.SetScriptLineText(script.Id, script.ActiveLines.First().Id, "윌로", "복도는 조용했다");

        if (secondBoard)
        {
            editor.EnsureChapter(Kept);
            editor.AddEpisode(Kept, EpisodeId, "다른 복도", 0, 0);
        }

        // 배관이 실제로 섰는지 확인하고 시작한다 — 없으면 첫 테스트가 헛돈다.
        Assert.Single(
            editor.Project.EnumerateNodes().OfType<SetNode>(),
            node => ChapterBoardSupply.IsConditionSupplyNodeName(node.Name));

        return editor;
    }

    private static string FirstLine(StoryProject project, DialogueNode card)
    {
        ScriptDocument script = project.FindScript(card.ScriptId)!;

        return script.Text(script.ActiveLines.First().Id, script.PrimaryLocale).Text;
    }
}
