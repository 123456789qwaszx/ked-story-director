using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터 조건의 판 사본은 이름으로 짝짓는다</b> (2026-11-17 소유자 — 식 미정인 조건도 분기에 건다). 전에는 식으로 짝지어서 식이 빈
/// 조건은 사본이 없었고(분기에 못 걸었다) 식이 같은 조건 둘은 사본 하나를 나눠 썼다.
///
/// 못 박는 것: ① 식 미정인 조건도 사본이 서고 식은 항상 통과(<c>true</c>)다, 여럿이어도 따로다 ② 분기에 걸 수 있고, 식을 채우면
/// 사본의 Id가 그대로라 분기가 새 식을 따른다 ③ 식을 고쳐도 Id가 그대로다 ④ 식이 같은 조건 둘은 사본이 따로다 — 하나를 고쳐도 다른
/// 하나는 그대로다 ⑤ 옛 사본(이름이 어긋나고 식이 같은 것)은 이어받는다 ⑥ 식을 비우면 사본이 식 미정이 된다.
/// </summary>
public sealed class ConditionSupplyByNameTests
{
    [Fact]
    public void 식_미정인_조건도_사본이_서고_여럿이어도_따로다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "미정하나", string.Empty);
        editor.AddChapterCondition("ch01", "미정둘", "  ");

        Assert.Equal(
            [("미정하나", ChapterBoardSupply.UndecidedYarn), ("미정둘", ChapterBoardSupply.UndecidedYarn)],
            Supplied(editor).Select(item => (item.Name, item.Expression)));
        Assert.Equal(2, Supplied(editor).Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void 식_미정인_조건을_분기에_걸고_식을_채우면_분기가_그_식을_따른다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "친해졌을 때", string.Empty);
        DialogueNode branch = Branch(editor);

        Assert.Null(BranchEntryCondition.Set(editor, branch.Id, "친해졌을 때"));
        string id = Supplied(editor).Single().Id;
        Assert.Equal("친해졌을 때", BranchEntryCondition.Read(editor.Project, branch.Id).Label);

        editor.UpdateChapterCondition("ch01", "친해졌을 때", "trust >= 3");

        ConditionDefinition copy = Supplied(editor).Single();
        Assert.Equal((id, "친해졌을 때", "stat(\"trust\") >= 3"), (copy.Id, copy.Name, copy.Expression));
        Assert.Equal("친해졌을 때", BranchEntryCondition.Read(editor.Project, branch.Id).Label);
    }

    [Fact]
    public void 식을_고쳐도_비워도_사본의_Id는_그대로다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        string id = Supplied(editor).Single().Id;

        editor.UpdateChapterCondition("ch01", "신뢰높음", "trust >= 5");
        Assert.Equal((id, "stat(\"trust\") >= 5"), (Supplied(editor).Single().Id, Supplied(editor).Single().Expression));

        // 식을 비우면 식 미정 — 항상 통과.
        editor.UpdateChapterCondition("ch01", "신뢰높음", string.Empty);
        Assert.Equal((id, ChapterBoardSupply.UndecidedYarn), (Supplied(editor).Single().Id, Supplied(editor).Single().Expression));

        // 되돌리기 한 칸.
        editor.Undo();
        Assert.Equal("stat(\"trust\") >= 5", Supplied(editor).Single().Expression);
    }

    [Fact]
    public void 식이_같은_조건_둘은_사본이_따로고_하나를_고쳐도_다른_하나는_그대로다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "가", "trust >= 3");
        editor.AddChapterCondition("ch01", "나", "trust >= 3");

        Assert.Equal(["가", "나"], Supplied(editor).Select(item => item.Name));

        editor.UpdateChapterCondition("ch01", "가", "trust >= 9");

        Assert.Equal(
            [("가", "stat(\"trust\") >= 9"), ("나", "stat(\"trust\") >= 3")],
            Supplied(editor).Select(item => (item.Name, item.Expression)));
    }

    [Fact]
    public void 이름이_어긋난_옛_사본은_식이_같으면_이어받고_Id를_지킨다()
    {
        // 식으로 짝짓던 때의 프로젝트 — 사본의 이름이 지금 어느 조건의 이름도 아니다.
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        ConditionDefinition copy = Supplied(editor).Single();
        editor.UpdateCondition(copy.Id, "옛이름", copy.Expression);

        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            editor, EpisodeNaming.BoardOf(editor.Project, "ch01")!.Id, editor.FindChapter("ch01")!.ToGraphModel("ch01"));

        ConditionDefinition after = Supplied(editor).Single();
        Assert.Equal((copy.Id, "신뢰높음"), (after.Id, after.Name));
    }

    [Fact]
    public void 식이_틀린_조건은_사본을_건드리지_않는다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

        editor.UpdateChapterCondition("ch01", "신뢰높음", "없는스탯 >= 3");

        Assert.Equal("stat(\"trust\") >= 3", Supplied(editor).Single().Expression);
    }

    private static ProjectEditor World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.AddEpisode("ch01", "root", title: "root", 0, 0);
        editor.EnsureChapterBoard("ch01");
        return editor;
    }

    /// <summary>root의 첫 줄에서 다녀오는 분기 카드 — 조건은 그 카드의 첫머리에 선다.</summary>
    private static DialogueNode Branch(ProjectEditor editor)
    {
        DialogueNode root = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        return editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
    }

    private static List<ConditionDefinition> Supplied(ProjectEditor editor) =>
        editor.Project.EnumerateNodes().OfType<SetNode>()
            .Where(node => node.Name == ChapterBoardSupply.ConditionSupplyNodeName("ch01"))
            .SelectMany(node => node.Conditions)
            .ToList();
}
