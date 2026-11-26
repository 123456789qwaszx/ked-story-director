using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>장면 색</b>(design/scene-colors.md · 2026-11-26 소유자 — 「권고대로」). 장면마다 팔레트의 한 칸(1 ~ 5)이다. 고른 칸이 먼저, 그 밖은 자동 — 장면ID에서
/// 첫 후보를 뽑고 이야기 차례로 바로 앞 두 장면과 같으면 다음 칸. 고른 색은 장면 곁 자료(<see cref="ChapterSceneNote.Color"/>)에 살고 저장을 왕복한다.
/// </summary>
public sealed class SceneColorsTests
{
    [Fact]
    public void 자동_색은_팔레트_안이고_같은_입력이면_같고_이야기_차례로_앞_두_장면과_다르다()
    {
        List<ChapterScene> scenes = Scenes(Enumerable.Range(1, 40).Select(index => $"scene{index:00}").ToArray());

        IReadOnlyDictionary<string, int> first = SceneColors.Of(scenes, []);
        IReadOnlyDictionary<string, int> second = SceneColors.Of(scenes, []);

        Assert.Equal(first, second);
        Assert.All(first.Values, color => Assert.InRange(color, 1, SceneColors.Count));

        for (int i = 1; i < scenes.Count; i++)
        {
            int color = first[scenes[i].SceneId];
            Assert.NotEqual(first[scenes[i - 1].SceneId], color);

            if (i >= 2)
            {
                Assert.NotEqual(first[scenes[i - 2].SceneId], color);
            }
        }

        Assert.True(first.Values.Distinct().Count() >= 3, "다섯 칸이 고루 쓰이지 않는다");
    }

    [Fact]
    public void 뒤에_장면을_더해도_앞_장면들의_색은_그대로다()
    {
        string[] ids = ["opening", "hall", "rooftop", "station"];
        IReadOnlyDictionary<string, int> before = SceneColors.Of(Scenes(ids), []);
        IReadOnlyDictionary<string, int> after = SceneColors.Of(Scenes([.. ids, "epilogue"]), []);

        Assert.All(ids, id => Assert.Equal(before[id], after[id]));
    }

    [Fact]
    public void 고른_색이_먼저이고_다음_장면은_그것을_피한다()
    {
        List<ChapterScene> scenes = Scenes("opening", "hall");
        int auto = SceneColors.Of(scenes, [])["hall"];
        int picked = (auto % SceneColors.Count) + 1;   // 자동과 다른 칸

        IReadOnlyDictionary<string, int> colors = SceneColors.Of(scenes, [new ChapterSceneNote("opening", string.Empty, Color: picked)]);

        Assert.Equal(picked, colors["opening"]);
        Assert.NotEqual(picked, colors["hall"]);
    }

    [Fact]
    public void 고르면_곁_자료에_적히고_자동으로_돌리면_빈_항목은_걷히며_되돌리기_한_칸이다()
    {
        ProjectEditor editor = Editor();

        editor.SetSceneColor("ch01", "hall", 4);
        Assert.Equal(4, ChapterSceneNotes.ColorOf(Chapter(editor).SceneNotes, "hall"));
        Assert.Equal(4, SceneColors.Of(Grouping(editor), Chapter(editor).SceneNotes)["hall"]);

        long revision = editor.Revision;
        editor.SetSceneColor("ch01", "hall", 4);   // 같은 값이면 칸을 쓰지 않는다
        Assert.Equal(revision, editor.Revision);

        editor.SetSceneColor("ch01", "hall", null);
        Assert.Empty(Chapter(editor).SceneNotes);   // 색만 있던 항목은 걷힌다

        editor.Undo();
        Assert.Equal(4, ChapterSceneNotes.ColorOf(Chapter(editor).SceneNotes, "hall"));

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetSceneColor("ch01", "hall", SceneColors.Count + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetSceneColor("ch01", "hall", 0));
    }

    [Fact]
    public void 장면ID를_바꿔도_색은_그대로다()
    {
        // 자동 색은 장면ID에서 나온다 — 바꿀 때 그때의 색을 묶는다.
        ProjectEditor editor = Editor();
        editor.SetSceneName("ch01", "hall", "복도");
        int before = SceneColors.Of(Grouping(editor), Chapter(editor).SceneNotes)["hall"];

        Assert.Equal(1, editor.RenameScene("ch01", "hall", "corridor"));

        Assert.Equal(before, SceneColors.Of(Grouping(editor), Chapter(editor).SceneNotes)["corridor"]);
        Assert.Equal(before, ChapterSceneNotes.ColorOf(Chapter(editor).SceneNotes, "corridor"));
        Assert.Equal("복도", ChapterSceneNotes.NameOf(Chapter(editor).SceneNotes, "corridor"));   // 이름도 함께 간다
    }

    [Fact]
    public void 고른_색은_저장을_왕복하고_자동이면_적지_않으며_주인_없는_색만의_항목은_쓰지_않는다()
    {
        var chapter = new ChapterDocument { ChapterId = "ch01" };
        chapter.Episodes.Add(new ChapterEpisode("ep01", "", "", 0, 0, Memo: null) { SceneId = "hall" });
        chapter.SceneNotes.Add(new ChapterSceneNote("hall", string.Empty, Color: 3));
        chapter.SceneNotes.Add(new ChapterSceneNote("gone", string.Empty, Color: 2));       // 주인 없는 색 — 남길 까닭이 없다
        chapter.SceneNotes.Add(new ChapterSceneNote("gone2", "메모", Color: 5));            // 주인 없는 메모 — 글은 남긴다

        JsonArray json = ChapterDocumentJson.Write([chapter])!;
        JsonArray notes = json[0]!["sceneNotes"]!.AsArray();

        Assert.Equal(["hall", "gone2"], notes.Select(note => (string)note!["sceneId"]!));
        Assert.Equal(3, (int)notes[0]!["color"]!);
        Assert.Null(notes[0]!["text"]);

        ChapterDocument read = Assert.Single(ChapterDocumentJson.Read(json));
        Assert.Equal(3, ChapterSceneNotes.ColorOf(read.SceneNotes, "hall"));
        Assert.Equal(5, ChapterSceneNotes.ColorOf(read.SceneNotes, "gone2"));

        // 자동이면 color 칸이 없다.
        chapter.SceneNotes.Clear();
        chapter.SceneNotes.Add(new ChapterSceneNote("hall", "메모"));
        Assert.Null(ChapterDocumentJson.Write([chapter])![0]!["sceneNotes"]![0]!["color"]);
    }

    [Fact]
    public void 팔레트_밖의_색이_적힌_파일은_읽지_않는다()
    {
        var chapter = new ChapterDocument { ChapterId = "ch01" };
        chapter.Episodes.Add(new ChapterEpisode("ep01", "", "", 0, 0, Memo: null) { SceneId = "hall" });
        chapter.SceneNotes.Add(new ChapterSceneNote("hall", string.Empty, Color: 3));
        JsonArray json = ChapterDocumentJson.Write([chapter])!;
        json[0]!["sceneNotes"]![0]!["color"] = 9;

        Assert.Throws<InvalidDataException>(() => ChapterDocumentJson.Read(json));
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static List<ChapterScene> Scenes(params string[] ids) =>
        ids.Select(id => new ChapterScene(id, id, id + "_root", [], [])).ToList();

    private static ChapterDocument Chapter(ProjectEditor editor) => editor.FindChapter("ch01")!;

    private static IReadOnlyList<ChapterScene> Grouping(ProjectEditor editor) =>
        ChapterSceneGrouping.Of(Chapter(editor).ToGraphModel("ch01"));

    /// <summary>ch01 — 장면 opening(root) · hall(a) · rooftop(b), 판과 카드.</summary>
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "hall");
        editor.AddEpisode("ch01", "b", title: "b", 0, 0, sceneId: "rooftop");
        editor.EnsureChapterBoard("ch01");
        return editor;
    }
}
