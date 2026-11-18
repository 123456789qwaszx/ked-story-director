using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>보내기</b> (2026-11-17 · <c>docs/work-orders/export-destinations-orders.md</c> G1) — 저장할 때 <c>exported/</c>를 형식마다 보낼 곳으로
/// 비춘다. 지난 파일은 이 툴이 보낸 그대로일 때만 걷고(보낸 기록), 남의 파일 · 사람이 고친 파일 · 옛 보낼 곳은 건드리지 않는다.
/// </summary>
public sealed class ExportDeliveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vn-delivery", Guid.NewGuid().ToString("N"));

    public ExportDeliveryTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Manifest => Path.Combine(_root, "proj", "project.vnproject.json");

    private string Folder(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void 형식마다_보낼_곳으로_exported를_비추고_나간_자리를_말한다()
    {
        ProjectEditor editor = Export("ch01");
        string game = Folder("game");
        string scenes = Folder("scenes");
        string yarn = Folder("yarn");
        ExportDestinations destinations = ExportDestinations.None
            .With(ExportFormat.Progression, ExportDestinations.Relativize(Manifest, game))
            .With(ExportFormat.Scenes, ExportDestinations.Relativize(Manifest, scenes))
            .With(ExportFormat.DialogueYarn, ExportDestinations.Relativize(Manifest, yarn));

        ExportDeliveryRun run = ExportDelivery.Deliver(Manifest, destinations);

        Assert.Equal(
            File.ReadAllBytes(ChapterExportService.ExportPathFor(Manifest, "ch01")),
            File.ReadAllBytes(Path.Combine(game, "ch01" + ChapterExportService.ExportFileSuffix)));
        Assert.True(File.Exists(Path.Combine(scenes, "ch01" + ChapterScenesExporter.FileSuffix)));
        Assert.Equal(
            Directory.GetFiles(ChapterYarnExporter.FolderFor(Manifest)).Select(Path.GetFileName).Order(),
            Directory.GetFiles(yarn).Select(Path.GetFileName).Order());
        Assert.NotEmpty(Directory.GetFiles(yarn));
        Assert.Empty(Directory.GetFiles(game, "*.yarn"));   // 형식마다 제 자리로만
        Assert.Null(run.Problem);
        Assert.Contains("보낸 곳 → 진행 JSON ../game · 장면 파일 ../scenes · 대사 Yarn ../yarn", run.Where);
        Assert.Contains("보낸 곳 →", (new ChapterExportRun([], []) with { Delivery = run }).WhereNotice);
        _ = editor;
    }

    [Fact]
    public void 지난_파일은_이_툴이_보낸_그대로일_때만_걷는다()
    {
        // 보낼 곳은 런타임 저장소 — 다른 프로젝트 · 견본과 나눠 쓴다. 보낸 기록과 바이트가 같은 것만 걷는다.
        string game = Folder("game");
        ExportDestinations destinations = ExportDestinations.None.With(ExportFormat.Progression, game);
        Export("ch01", "ch_old", "ch_hand");
        ExportDelivery.Deliver(Manifest, destinations);

        string handEdited = Path.Combine(game, "ch_hand" + ChapterExportService.ExportFileSuffix);
        File.WriteAllText(handEdited, "{ \"사람이\": \"고쳤다\" }");
        string foreign = Path.Combine(game, "ch_other" + ChapterExportService.ExportFileSuffix);
        File.WriteAllText(foreign, "{}");

        Export("ch01");   // 두 챕터를 지웠다 — exported/의 그 파일은 다시 읽기가 걷는다
        ExportDeliveryRun run = ExportDelivery.Deliver(Manifest, destinations);

        Assert.True(File.Exists(Path.Combine(game, "ch01" + ChapterExportService.ExportFileSuffix)));
        Assert.False(File.Exists(Path.Combine(game, "ch_old" + ChapterExportService.ExportFileSuffix)));
        Assert.True(File.Exists(handEdited));
        Assert.True(File.Exists(foreign));   // 보낸 적이 없다
        Assert.Equal(["ch_old" + ChapterExportService.ExportFileSuffix], run.Removed);
    }

    [Fact]
    public void 보낼_곳을_바꾸거나_끄면_옛_폴더는_건드리지_않는다()
    {
        string first = Folder("first");
        string second = Folder("second");
        Export("ch01");
        ExportDelivery.Deliver(Manifest, ExportDestinations.None.With(ExportFormat.Progression, first));

        ExportDelivery.Deliver(Manifest, ExportDestinations.None.With(ExportFormat.Progression, second));
        ExportDelivery.Deliver(Manifest, ExportDestinations.None);

        Assert.True(File.Exists(Path.Combine(first, "ch01" + ChapterExportService.ExportFileSuffix)));
        Assert.True(File.Exists(Path.Combine(second, "ch01" + ChapterExportService.ExportFileSuffix)));
    }

    [Fact]
    public void 없는_폴더는_만들지_않고_말한다()
    {
        Export("ch01");
        string missing = Path.Combine(_root, "nowhere");

        ExportDeliveryRun run = ExportDelivery.Deliver(Manifest, ExportDestinations.None.With(ExportFormat.Scenes, missing));

        Assert.False(Directory.Exists(missing));
        Assert.Contains("보낼 곳이 없어 보내지 않았습니다: 장면 파일", run.Problem);
        Assert.Contains("보낼 곳이 없어", (new ChapterExportRun([], []) with { Delivery = run }).Notice);
    }

    [Fact]
    public void 대사_Yarn은_게임_폴더로_보내지_않는다()
    {
        // 게임의 Yarn 프로젝트가 compositor의 연출 든 Yarn과 같은 노드 이름을 둘 읽어 컴파일이 깨진다.
        Export("ch01");
        string game = Folder("game");

        ExportDeliveryRun run = ExportDelivery.Deliver(Manifest, ExportDestinations.None
            .With(ExportFormat.Progression, game)
            .With(ExportFormat.DialogueYarn, game));

        Assert.Empty(Directory.GetFiles(game, "*.yarn"));
        Assert.True(File.Exists(Path.Combine(game, "ch01" + ChapterExportService.ExportFileSuffix)));
        Assert.Contains("게임 폴더와 같은 곳으로는 보내지 않습니다", run.Problem);
    }

    /// <summary>그 챕터들만 든 프로젝트를 저장하고 exported/로 낸다(다시 읽기의 내보내기와 같다 — 없는 챕터의 파일은 걷힌다).</summary>
    private ProjectEditor Export(params string[] chapterIds)
    {
        var editor = new ProjectEditor(new StoryProject());

        foreach (string chapterId in chapterIds)
        {
            DialogueNode card = editor.AddDialogueNode(TestChapter.Board(editor, chapterId).Id, "scene01", 0, 0);
            editor.SetScriptLineText(card.Id, card.Script.ActiveLines.First().Id, "라루", $"{chapterId}의 첫 줄");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Manifest)!);
        ProjectStore.Save(Manifest, editor.Project);

        ChapterExportRun run = new ChapterExportService().ExportAll(
            [.. chapterIds.Select(id => new ChapterEntry(id, editor.FindChapter(id)!.ToGraphModel(id)))], Manifest, editor.Project);
        Assert.True(run.AllExported, run.Notice);

        return editor;
    }
}
