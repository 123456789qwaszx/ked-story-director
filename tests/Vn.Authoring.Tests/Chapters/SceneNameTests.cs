using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 장면 이름 (2026-11-16 · <c>docs/work-orders/display-names-orders.md</c> N1). 작가가 [대본] 탭에서 붙이는 이름이고 SceneId(런타임 저장 ·
/// 롤백 경계)와 따로다 — 장면 메모 항목의 <c>name</c>. 지키는 것: 같은 이름의 장면이 합쳐지지 않는다 · 보이는 이름은 이름, 비면 SceneId ·
/// 이름과 메모를 따로 고친다 · 저장을 왕복하고 비면 쓰지 않는다 · 장면ID를 바꾸면 이름이 따라간다.
/// </summary>
public sealed class SceneNameTests : IDisposable
{
    private const string Ch = "ch01";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "vn-scene-names", Guid.NewGuid().ToString("N"));

    public SceneNameTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>scene01(ep01) · scene02(ep02) · 장면 없음(ep03).</summary>
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter(Ch);
        editor.AddEpisode(Ch, "ep01", "", 0, 0, sceneId: "scene01");
        editor.AddEpisode(Ch, "ep02", "", 0, 0, sceneId: "scene02");
        editor.AddEpisode(Ch, "ep03", "", 0, 0);
        return editor;
    }

    private static ChapterDocument Chapter(ProjectEditor editor) => editor.FindChapter(Ch)!;

    private static IReadOnlyList<ChapterScene> Scenes(ProjectEditor editor) =>
        ChapterSceneGrouping.Of(Chapter(editor).ToGraphModel(Ch));

    [Fact]
    public void 같은_이름의_두_장면은_합쳐지지_않고_보이는_이름은_이름이다()
    {
        ProjectEditor editor = Editor();

        editor.SetSceneName(Ch, "scene01", " 교실 ");
        editor.SetSceneName(Ch, "scene02", "교실");

        IReadOnlyList<ChapterScene> scenes = Scenes(editor);
        Assert.Equal(["scene01", "scene02", "__scene_ep03"], scenes.Select(scene => scene.SceneId));
        Assert.Equal(["교실", "교실", "미지정 · ep03"], scenes.Select(scene => scene.DisplayName));
        Assert.Equal(["scene01", "scene02", null], Chapter(editor).Episodes.Select(episode => episode.SceneId));
    }

    [Fact]
    public void 이름이_비면_장면ID가_보인다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneName(Ch, "scene01", "교실");

        editor.SetSceneName(Ch, "scene01", "  ");

        Assert.Equal("scene01", Scenes(editor)[0].DisplayName);
        Assert.Empty(Chapter(editor).SceneNotes);
    }

    [Fact]
    public void 이름과_메모는_따로_고치고_되돌리기는_한_칸씩이다()
    {
        ProjectEditor editor = Editor();

        editor.SetSceneName(Ch, "scene01", "교실");
        editor.SetSceneNote(Ch, "scene01", "사정");
        editor.SetSceneNote(Ch, "scene01", "");          // 글만 지운다 — 이름은 남는다

        ChapterSceneNote entry = Assert.Single(Chapter(editor).SceneNotes);
        Assert.Equal(("교실", ""), (entry.Name, entry.Text));

        editor.SetSceneName(Ch, "scene01", "교실");      // 같은 값 — 칸을 쓰지 않는다
        editor.Undo();
        Assert.Equal(("교실", "사정"), (Chapter(editor).SceneNotes[0].Name, Chapter(editor).SceneNotes[0].Text));
        editor.Undo();
        editor.Undo();
        Assert.Empty(Chapter(editor).SceneNotes);
    }

    [Fact]
    public void 이름은_저장을_왕복하고_비면_쓰지_않는다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneName(Ch, "scene01", "교실");
        editor.SetSceneNote(Ch, "scene02", "메모만");
        string manifest = Path.Combine(_directory, "p" + ProjectManifestJson.FileExtension);

        ProjectStore.Save(manifest, editor.Project);
        ChapterDocument read = Assert.Single(ProjectStore.Load(manifest).Project.Chapters);

        Assert.Equal([("scene01", "교실", ""), ("scene02", null, "메모만")],
            read.SceneNotes.Select(note => (note.SceneId, note.Name, note.Text)));

        JsonArray saved = JsonNode.Parse(File.ReadAllText(manifest))!["chapters"]![0]!["sceneNotes"]!.AsArray();
        Assert.Equal("{\"sceneId\":\"scene01\",\"name\":\"교실\"}", saved[0]!.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
        Assert.Null(saved[1]!["name"]);
    }

    [Fact]
    public void 장면ID를_바꾸면_이름과_메모가_따라간다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneName(Ch, "scene01", "교실");
        editor.SetSceneNote(Ch, "scene01", "사정");

        editor.RenameScene(Ch, "scene01", "classroom");

        Assert.Equal("교실", Scenes(editor).Single(scene => scene.SceneId == "classroom").DisplayName);
        Assert.Equal(("classroom", "교실", "사정"),
            (Chapter(editor).SceneNotes[0].SceneId, Chapter(editor).SceneNotes[0].Name, Chapter(editor).SceneNotes[0].Text));
    }

    [Fact]
    public void 이름만_남은_주인_없는_항목도_알리고_통째로_지운다()
    {
        ProjectEditor editor = Editor();
        editor.SetSceneName(Ch, "scene02", "복도");
        editor.RemoveEpisode(Ch, "ep02");

        ChapterDiagnostic orphan = Assert.Single(Chapter(editor).ToGraphModel(Ch).Diagnostics,
            item => item.Code == ChapterDiagnosticCode.SceneNoteOrphaned);
        Assert.Contains("'복도'", orphan.Message);

        editor.RemoveSceneNote(Ch, "scene02");
        Assert.Empty(Chapter(editor).SceneNotes);
    }

    [Fact]
    public void 챕터의_보이는_이름은_표시_이름이고_비면_ID다()
    {
        ProjectEditor editor = Editor();
        Assert.Equal(Ch, Chapter(editor).ShownName);

        editor.UpdateChapterDisplayName(Ch, " 축제 전날 ");

        Assert.Equal("축제 전날", Chapter(editor).ShownName);
        Assert.Equal("축제 전날", Chapter(editor).ToGraphModel(Ch).ShownName);
    }
}
