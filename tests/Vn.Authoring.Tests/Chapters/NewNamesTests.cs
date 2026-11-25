using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 새로 만들 때의 자동 ID (2026-11-16 · display-names N2). 작가는 ID를 정하지 않는다 — 겹치지 않는
/// 자리표시가 붙는다.
/// </summary>
public sealed class NewNamesTests
{
    [Fact]
    public void 챕터_ID는_ch01부터_안_쓰인_첫_번호이고_대소문자와_판_이름도_피한다()
    {
        var project = new StoryProject();
        Assert.Equal("ch01", NewNames.NextChapterId(project));

        var editor = new ProjectEditor(project);
        editor.EnsureChapter("CH01");                 // 대소문자만 다른 이름 — 파일 이름이 겹친다
        project.Files.Add(new StoryFile("sf_x", "ch02"));   // 판 이름(챕터 = 판 1:1)

        Assert.Equal("ch03", NewNames.NextChapterId(project));
    }

    [Fact]
    public void 장면_ID는_주인_없는_항목의_열쇠도_피한다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "", 0, 0, sceneId: "scene01");
        editor.SetSceneNote("ch01", "scene02", "주인 없는 메모");   // 옛 장면의 메모 — 새 장면이 물려받으면 안 된다

        Assert.Equal("scene03", NewNames.NextSceneId(editor.FindChapter("ch01")!));
    }
}
