using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>간선을 원하는 자리에 끼워 넣는다</b> (H2 · 2026-10-29 소유자: "선택지 문구의 순서를 유지해"). 한 에피소드에서 나가는
/// 간선의 순서가 곧 선택지 순서(런타임 <c>OptionIndex</c>)라, 연출 그래프는 사람이 적어 둔 줄 자리에 맞춰 넣는다.
/// </summary>
public sealed class EdgeInsertTests
{
    [Fact]
    public void 앞에_둘_간선을_주면_그_앞에_서고_되돌리기_한_칸이다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "a", "a", 0, 0);
        editor.AddEpisode("ch01", "b", "b", 0, 0);
        ChapterEdge second = editor.AddEdge("ch01", "a", "b", optionLabel: "둘");

        editor.AddEdge("ch01", "a", "b", optionLabel: "하나", before: second);

        Assert.Equal(["하나", "둘"], editor.FindChapter("ch01")!.Edges.Select(edge => edge.OptionLabel));

        editor.Undo();
        Assert.Equal(["둘"], editor.FindChapter("ch01")!.Edges.Select(edge => edge.OptionLabel));
    }

    [Fact]
    public void 앞에_둘_간선이_없으면_끝에_붙는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "a", "a", 0, 0);
        editor.AddEpisode("ch01", "b", "b", 0, 0);
        editor.AddEdge("ch01", "a", "b", optionLabel: "하나");

        editor.AddEdge("ch01", "a", "b", optionLabel: "둘");

        Assert.Equal(["하나", "둘"], editor.FindChapter("ch01")!.Edges.Select(edge => edge.OptionLabel));
    }
}
