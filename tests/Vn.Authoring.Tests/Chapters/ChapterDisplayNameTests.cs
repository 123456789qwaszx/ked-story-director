using System.Text.Json;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 챕터 표시 이름 · 에피소드 제목 (2026-11-04). 런타임의 끝 카드가 머리에 챕터 <c>DisplayName</c>("축제 전날")을,
/// "마지막 에피소드" 줄에 에피소드 <c>Title</c>을 보여 준다. 표시 이름은 늘 챕터 Id로 나갔고(칸이 없었다),
/// 제목은 2026-08-16에 패널에서 빠져 UI로 만든 에피소드는 빈 글자가 나갔다.
/// </summary>
public sealed class ChapterDisplayNameTests
{
    private static ProjectEditor Editor()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("festival_eve");
        editor.AddEpisode("festival_eve", "Festival_Classroom", "", 0, 0);
        return editor;
    }

    private static JsonElement Export(ProjectEditor editor)
    {
        ChapterExportResult export = ChapterProgressionExporter.Export(
            editor.FindChapter("festival_eve")!.ToGraphModel("festival_eve"));

        Assert.False(export.Refused);
        return JsonDocument.Parse(export.Json!).RootElement.Clone();
    }

    [Fact]
    public void 표시_이름이_없으면_챕터_Id가_나간다()
    {
        Assert.Equal("festival_eve", Export(Editor()).GetProperty("DisplayName").GetString());
    }

    [Fact]
    public void 표시_이름을_정하면_진행_JSON의_DisplayName이다()
    {
        ProjectEditor editor = Editor();

        editor.UpdateChapterDisplayName("festival_eve", "  축제 전날 ");

        Assert.Equal("축제 전날", editor.FindChapter("festival_eve")!.DisplayName);
        Assert.Equal("축제 전날", Export(editor).GetProperty("DisplayName").GetString());
    }

    [Fact]
    public void 표시_이름을_비우면_Id로_물러서고_되돌리기는_한_칸이다()
    {
        ProjectEditor editor = Editor();
        editor.UpdateChapterDisplayName("festival_eve", "축제 전날");

        editor.UpdateChapterDisplayName("festival_eve", "   ");
        Assert.Null(editor.FindChapter("festival_eve")!.DisplayName);

        editor.Undo();
        Assert.Equal("축제 전날", editor.FindChapter("festival_eve")!.DisplayName);
    }

    [Fact]
    public void 표시_이름은_저장을_왕복한다()
    {
        ProjectEditor editor = Editor();
        editor.UpdateChapterDisplayName("festival_eve", "축제 전날");

        StoryProject decoded = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project));

        Assert.Equal("축제 전날", Assert.Single(decoded.Chapters).DisplayName);
    }

    [Fact]
    public void 같은_값이면_되돌리기_칸을_쓰지_않는다()
    {
        ProjectEditor editor = Editor();
        editor.UpdateChapterDisplayName("festival_eve", "축제 전날");
        string before = ProjectSnapshotCodec.Encode(editor.Project);

        editor.UpdateChapterDisplayName("festival_eve", "축제 전날");
        editor.Undo();

        // 같은 값 저장이 칸을 썼다면 이 되돌리기는 그것을 지우고 "축제 전날"이 남는다 — 실제로는 앞 편집이 풀린다.
        Assert.NotEqual(before, ProjectSnapshotCodec.Encode(editor.Project));
        Assert.Null(editor.FindChapter("festival_eve")!.DisplayName);
    }

    [Fact]
    public void 제목이_비면_에피소드_이름이_나가고_정하면_그_글자다()
    {
        ProjectEditor editor = Editor();

        JsonElement node = Export(editor).GetProperty("Nodes")[0];
        Assert.Equal("Festival_Classroom", node.GetProperty("Title").GetString());

        editor.UpdateEpisode("festival_eve", "Festival_Classroom", title: "교실 - 반쯤 빈 간판");

        node = Export(editor).GetProperty("Nodes")[0];
        Assert.Equal("교실 - 반쯤 빈 간판", node.GetProperty("Title").GetString());
    }
}
