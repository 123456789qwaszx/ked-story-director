using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 증명 캐시와 내보내기 — 2026-08-23에 `ChapterGraphView` 3,835줄에서 나온 규칙들.
///
/// <b>이 파일이 이 리팩터의 목적이다.</b> 여기 붙드는 것들은 그동안 헤드리스 UI 스위트
/// (41초)를 통해서만 간접적으로 닿았고, 그래서 <b>규칙으로 읽히지 않았다.</b> 실제로
/// 물린 적이 있다 — "동기화는 고른 챕터만, 내보내기는 전 챕터"라는 비대칭이 코드비하인드에
/// 묻혀 있어, 저작 관문을 걸려던 시도가 뒤늦게 그것을 알았다.
/// </summary>
public sealed class ChapterExportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "vn-chapter-export-service", Guid.NewGuid().ToString("N"));

    private string ProjectPath => Path.Combine(_root, "예제.vnproject.json");

    public ChapterExportServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // ── 증명 캐시 ───────────────────────────────────────────────────────────

    [Fact]
    public void 모델이_그대로면_다시_증명하지_않는다()
    {
        // 챕터 하나 증명에 상태공간을 훑는 값이 든다. 화면은 갱신할 때마다 이것을 처음부터
        // 다시 돌리고 있었다.
        var service = new ChapterExportService();
        ChapterEntry entry = Entry("ch01", Sound);

        service.ValidationFor(entry);
        service.ValidationFor(entry);
        service.ValidationFor(entry);

        Assert.Equal(1, service.ValidationComputeCount);
    }

    [Fact]
    public void 모델_내용이_바뀌면_다시_증명한다()
    {
        // 지문은 검증이 읽는 것 — 모델 — 의 내용이다. 간선 문구 하나가 달라도 다시 증명한다.
        var service = new ChapterExportService();
        ChapterEntry entry = Entry("ch01", Sound);

        service.ValidationFor(entry);

        ChapterGraphModel edited = new(
            entry.ChapterId, string.Empty,
            entry.Model!.Episodes,
            [new ChapterEdge("ep1", "ep2", "다른 문구", null, null)],
            [], entry.Model.Stats, [], []);

        service.ValidationFor(entry with { Model = edited });

        Assert.Equal(2, service.ValidationComputeCount);
    }

    [Fact]
    public void 간선의_스탯변화만_바뀌어도_다시_증명한다()
    {
        // 레코드의 글은 목록 내용을 싣지 않는다 — 스탯변화만 고친 간선이 같은 지문을 내면 옛 도착 스탯 · 도달성을 되쓴다.
        // 편집기는 스탯변화를 늘 같은 꼴의 목록으로 적는다 — 그 안의 값만 다르다.
        var service = new ChapterExportService();
        ChapterEntry entry = Entry("ch01", Sound);

        ChapterEntry WithTrust(int amount) => entry with
        {
            Model = new ChapterGraphModel(
                entry.ChapterId, string.Empty,
                entry.Model!.Episodes,
                [entry.Model.Edges.Single() with { StatChanges = new List<StatDelta> { new("trust", amount) } }],
                [], entry.Model.Stats, [], [])
        };

        ChapterValidationResult before = service.ValidationFor(WithTrust(1));
        ChapterValidationResult after = service.ValidationFor(WithTrust(2));

        Assert.Equal(2, service.ValidationComputeCount);
        Assert.Equal(2, before.Reachability.SpansFor("ep2").Single(span => span.Key == "trust").Minimum);
        Assert.Equal(3, after.Reachability.SpansFor("ep2").Single(span => span.Key == "trust").Minimum);
    }

    [Fact]
    public void 챕터마다_따로_기억한다()
    {
        var service = new ChapterExportService();

        service.ValidationFor(Entry("ch01", Sound));
        service.ValidationFor(Entry("ch02", Sound));
        service.ValidationFor(Entry("ch01", Sound));   // 이미 안다

        Assert.Equal(2, service.ValidationComputeCount);
    }

    // ── 내보내기 ────────────────────────────────────────────────────────────

    [Fact]
    public void 고른_챕터만이_아니라_전부_나간다()
    {
        // 2026-08-17 소유자 — 고른 챕터만 내보내면 나머지는 누른 순간의 낡은 판으로 굳는다.
        var service = new ChapterExportService();

        ChapterExportRun run = service.ExportAll(
            [Entry("ch01", Sound), Entry("ch02", Sound), Entry("ch03", Sound)],
            ProjectPath);

        Assert.True(run.AllExported, string.Join(" / ", run.Refused.Concat(run.Failed)));
        Assert.Null(run.Notice);

        foreach (string id in new[] { "ch01", "ch02", "ch03" })
        {
            Assert.True(
                File.Exists(ChapterExportService.ExportPathFor(ProjectPath, id)),
                $"{id}이 안 나갔다");
        }
    }

    [Fact]
    public void 검증에_걸린_챕터는_기존_파일을_손대지_않는다()
    {
        // ⛔ G8. 거부되면 파일을 안 건드린다 — 낡은 파일이 남더라도 쓰레기로 덮는 것보다
        // 낫다. 런타임이 읽는 것은 언제나 "한 번은 옳았던" 판이어야 한다.
        var service = new ChapterExportService();
        string path = ChapterExportService.ExportPathFor(ProjectPath, "ch01");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"한번은\": \"옳았던 판\" }");

        ChapterExportRun run = service.ExportAll([Entry("ch01", Blocked)], ProjectPath);

        Assert.Contains("ch01", run.Refused);
        Assert.Equal("{ \"한번은\": \"옳았던 판\" }", File.ReadAllText(path));
    }

    [Fact]
    public void 이어지지_않은_에피소드가_있어도_나가고_진행_JSON에_그대로_실린다()
    {
        // 2026-11-17 소유자 — 작업 중인(아직 안 이은) 에피소드는 내보내기를 막지 않는다. 게임에서는 거기 갈 길이 없을 뿐이다.
        ChapterGraphModel model = Sound("ch01");
        model = new ChapterGraphModel(
            model.ChapterId, model.SourcePath,
            [.. model.Episodes, new ChapterEpisode("ep9", "작업 중", "", "ep9", 400, 0, null)],
            model.Edges, model.Conditions, model.Stats, model.Fixtures, model.Diagnostics);

        ChapterExportRun run = new ChapterExportService().ExportAll([new ChapterEntry("ch01", model)], ProjectPath);

        Assert.True(run.AllExported, run.Notice);
        Assert.Contains("\"EpisodeId\": \"ep9\"", File.ReadAllText(ChapterExportService.ExportPathFor(ProjectPath, "ch01")));
    }

    [Fact]
    public void 같은_글이면_파일을_다시_쓰지_않는다()
    {
        // 다시 읽을 때마다 파일을 두드리면 클라우드 동기화가 계속 깨어나고, 바뀐 것이
        // 없는데 시각만 새로 찍힌다.
        var service = new ChapterExportService();
        ChapterEntry entry = Entry("ch01", Sound);

        service.ExportAll([entry], ProjectPath);

        string path = ChapterExportService.ExportPathFor(ProjectPath, "ch01");
        DateTime firstWrite = new FileInfo(path).LastWriteTimeUtc;

        // 시각 눈금이 확실히 갈리도록 뒤로 밀어 두고 다시 낸다 — 안 쓰면 이 값이 남는다.
        File.SetLastWriteTimeUtc(path, firstWrite.AddMinutes(-5));
        DateTime marked = new FileInfo(path).LastWriteTimeUtc;

        service.ExportAll([entry], ProjectPath);

        Assert.Equal(marked, new FileInfo(path).LastWriteTimeUtc);
    }

    [Fact]
    public void 못_나간_것만_말한다()
    {
        var service = new ChapterExportService();

        ChapterExportRun run = service.ExportAll(
            [Entry("ch01", Sound), Entry("ch02", Blocked)],
            ProjectPath);

        Assert.Equal(["ch02"], run.Refused);
        Assert.Empty(run.Failed);

        string notice = Assert.IsType<string>(run.Notice);
        Assert.Contains("ch02", notice);
        Assert.DoesNotContain("ch01", notice);   // 잘 나간 것은 말하지 않는다
    }

    [Fact]
    public void 프로젝트를_아직_저장하지_않았으면_아무것도_안_한다()
    {
        // 저장 전에는 산출물이 놓일 자리가 없다. 실패가 아니라 할 일이 없는 것이다.
        ChapterExportRun run = new ChapterExportService()
            .ExportAll([Entry("ch01", Sound)], projectPath: null);

        Assert.True(run.AllExported);
        Assert.Null(run.Notice);
    }

    [Fact]
    public void 증명을_두_번_돌리지_않는다()
    {
        // 2026-08-18 — 화면이 보고 패널을 세우려고 한 번 증명하는데, 내보내기가 안에서
        // 또 증명해 챕터 하나당 200ms를 두 번 치르고 있었다. 그래서 이 둘이 한 객체다.
        var service = new ChapterExportService();
        ChapterEntry entry = Entry("ch01", Sound);

        service.ValidationFor(entry);
        service.ExportAll([entry], ProjectPath);

        Assert.Equal(1, service.ValidationComputeCount);
    }

    [Fact]
    public void 내보낸_JSON이_실제로_읽히는_모양이다()
    {
        // 캐시·장부만 붙들고 내용은 안 보면, 나가긴 나가는데 쓸모없는 파일이 될 수 있다.
        var service = new ChapterExportService();

        service.ExportAll([Entry("ch01", Sound)], ProjectPath);

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(ChapterExportService.ExportPathFor(ProjectPath, "ch01")));

        Assert.Equal("ch01", document.RootElement.GetProperty("ChapterId").GetString());
        Assert.Equal("ep1", document.RootElement.GetProperty("StartEpisodeId").GetString());
    }

    // ── 지운 · 이름을 바꾼 챕터의 옛 파일 ─────────────────────────────────────

    [Fact]
    public void 프로젝트에_없는_챕터의_진행_JSON은_걷는다()
    {
        // 2026-11-06 소유자 — 만들었다가 지운 챕터와 개명 전 이름의 JSON이 폴더에 그대로 남았다. 두면 게임이 없는
        // 챕터를 계속 싣는다. exported/는 모델에서 짓는 산출물이라 걷어도 잃는 것이 없다(되돌리면 다시 나간다).
        var service = new ChapterExportService();
        service.ExportAll([Entry("ch01", Sound), Entry("ch_old", Sound), Entry("ch_gone", Sound)], ProjectPath);

        // ch_old → ch_new로 개명, ch_gone은 지웠다.
        ChapterExportRun run = service.ExportAll([Entry("ch01", Sound), Entry("ch_new", Sound)], ProjectPath);

        Assert.True(File.Exists(ChapterExportService.ExportPathFor(ProjectPath, "ch01")));
        Assert.True(File.Exists(ChapterExportService.ExportPathFor(ProjectPath, "ch_new")));
        Assert.False(File.Exists(ChapterExportService.ExportPathFor(ProjectPath, "ch_old")));
        Assert.False(File.Exists(ChapterExportService.ExportPathFor(ProjectPath, "ch_gone")));
        Assert.Equal(["ch_gone", "ch_old"], run.Removed!);
        Assert.Contains("ch_old.progression.json", run.WhereNotice!, StringComparison.Ordinal);
        Assert.Null(run.Notice);

        // 다음 내보내기에서는 걷을 것이 없다.
        Assert.Empty(service.ExportAll([Entry("ch01", Sound), Entry("ch_new", Sound)], ProjectPath).Removed!);
    }

    [Fact]
    public void 검증에_걸려_못_나간_챕터의_파일은_걷지_않는다()
    {
        // 프로젝트에 있는 챕터다 — 낡은 파일이라도 "한 번은 옳았던" 판이라 그대로 둔다(G8).
        var service = new ChapterExportService();
        string path = ChapterExportService.ExportPathFor(ProjectPath, "ch01");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"한번은\": \"옳았던 판\" }");

        ChapterExportRun run = service.ExportAll([Entry("ch01", Blocked)], ProjectPath);

        Assert.True(File.Exists(path));
        Assert.Empty(run.Removed!);
    }

    [Fact]
    public void 대소문자만_바꾼_개명은_새_파일을_걷지_않는다()
    {
        // 대소문자를 가리지 않는 파일 시스템은 방금 쓴 새 파일을 옛 이름으로 들고 있다 — 그것을 옛 파일로 보면 사라진다.
        var service = new ChapterExportService();
        service.ExportAll([Entry("Ch01", Sound)], ProjectPath);

        ChapterExportRun run = service.ExportAll([Entry("ch01", Sound)], ProjectPath);

        Assert.Empty(run.Removed!);
        Assert.True(File.Exists(ChapterExportService.ExportPathFor(ProjectPath, "ch01")));
    }


    [Fact]
    public void 노드가_생기면_캐시가_깨져_다시_증명한다()
    {
        // ⛔ 이것이 없으면 **"고쳤는데 계속 거부한다"**가 된다. 지문이 파일만 보면 카드를
        // 고쳐도 캐시가 옛 결론을 그대로 준다 — 캐시가 만드는 가장 나쁜 거짓말이
        // 여기서 한 번 더 나올 자리다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");   // 판은 챕터가 세운다
        string fileId = editor.EnsureChapterBoard("ch01");
        editor.AddDialogueNode(fileId, name: "ep1");
        DialogueNode second = editor.AddDialogueNode(fileId, name: "ep2");
        editor.RetireScriptLine(second.Id, second.Script.ActiveLines.Single().Id);   // 줄 없는 카드 — 빈 에피소드라 거부한다

        var service = new ChapterExportService();
        ChapterEntry entry = Entry("ch01", Sound);

        Assert.Contains("ch01", service.ExportAll([entry], ProjectPath, editor.Project).Refused);

        // 카드에 줄을 다시 넣는다 — 디스크는 한 글자도 안 바뀌었다.
        editor.InsertScriptLine(second.Id);

        ChapterExportRun after = service.ExportAll([entry], ProjectPath, editor.Project);

        Assert.True(after.AllExported, string.Join(" / ", after.Refused));
        Assert.Equal(2, service.ValidationComputeCount);   // 파일이 그대로여도 다시 증명했다
    }

    [Fact]
    public void 판을_안_넘기면_이_검사를_하지_않는다()
    {
        // 콘솔·테스트처럼 판을 볼 수 없는 자리에서도 나머지 검증은 그대로 돌아야 한다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");   // 판은 챕터가 세운다
        editor.AddDialogueNode(editor.EnsureChapterBoard("ch01"), name: "ep1");

        Assert.True(new ChapterExportService()
            .ExportAll([Entry("ch01", Sound)], ProjectPath)
            .AllExported);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────
    //
    // 지문도 검증도 **메모리의 모델**로 몬다(2026-10-29) — 챕터는 프로젝트에 살고 디스크에
    // 읽을 파일이 없다.

    private static ChapterEntry Entry(string chapterId, Func<string, ChapterGraphModel> build) =>
        new(chapterId, build(chapterId));

    /// <summary>ep1 → ep2. 오류 없이 나간다.</summary>
    private static ChapterGraphModel Sound(string chapterId) => new(
        chapterId,
        string.Empty,
        [
            new ChapterEpisode("ep1", "첫 화", "", "ep1", 0, 0, null),
            new ChapterEpisode("ep2", "둘째", "", "ep2", 200, 0, null)
        ],
        [new ChapterEdge("ep1", "ep2", "다음", null, null)],
        [],
        [new ChapterStat("trust", Initial: 1, Minimum: 0, Maximum: 5)],
        [],
        []);

    /// <summary>
    /// ep3로 가는 간선은 있지만 조건(trust ≥ 9)이 스탯 최대(5)를 넘어 못 닿는다 — 도달성 증명이 오류로 잡는다(D3). ⛔ 2026-11-17까지는
    /// 「들어오는 길이 없다」였다 — 이제 그것은 작업 중(이어지지 않음 — 경고)이다.
    /// </summary>
    private static ChapterGraphModel Blocked(string chapterId) => new(
        chapterId,
        string.Empty,
        [
            new ChapterEpisode("ep1", "첫 화", "", "ep1", 0, 0, null),
            new ChapterEpisode("ep2", "둘째", "", "ep2", 200, 0, null),
            new ChapterEpisode("ep3", "닿지 않는 화", "", "ep3", 400, 0, null)
        ],
        [new ChapterEdge("ep1", "ep2", "다음", null, null), new ChapterEdge("ep2", "ep3", "올라간다", "높음", null)],
        [
            new ChapterCondition("높음", "trust >= 9", null,
                [new ConditionTerm(ConditionTermKind.StatComparison, "trust", ConditionComparison.AtLeast, 9)],
                IsValid: true)
        ],
        [new ChapterStat("trust", Initial: 1, Minimum: 0, Maximum: 5)],
        [],
        []);
}
