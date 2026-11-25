using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>대사가 쓸 수 있는 조건은 챕터 조건에서 바로 계산한다</b> (2026-11-25 · condition-supply C1). 이름이 열쇠이고(줄 전환이 그 이름을 든다),
/// 식은 그때마다 챕터 조건에서 옮긴다 — 사본이 없으므로 맞출 것도 없다.
///
/// 못 박는 것: ① 식 미정인 조건도 목록에 서고 식은 항상 통과(<c>true</c>)다, 여럿이어도 따로다 ② 분기에 걸 수 있고, 식을 채우면 분기가 새 식을
/// 따른다 ③ 식을 고쳐도 비워도 이름(열쇠)이 그대로다 ④ 식이 같은 조건 둘은 따로다 ⑤ 틀린 식은 적힌 글자 그대로 보인다(검증이 짚는다)
/// ⑥ 아직 없는 스탯을 적은 식은 그 스탯을 더하면 곧바로 읽힌다.
/// ⛔ 2026-11-17 ~ 11-25에는 판의 공급 노드가 사본을 이름으로 짝지어 들었다(그 전에는 식으로 짝지어 식 미정인 조건은 사본이 없었다).
/// </summary>
public sealed class ChapterConditionCatalogTests
{
    [Fact]
    public void 식_미정인_조건도_목록에_서고_여럿이어도_따로다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "미정하나", string.Empty);
        editor.AddChapterCondition("ch01", "미정둘", "  ");

        Assert.Equal(
            [("미정하나", AvailableConditionResolver.UndecidedYarn), ("미정둘", AvailableConditionResolver.UndecidedYarn)],
            Catalog(editor).Select(item => (item.Name, item.Expression)));
        Assert.Equal(["미정하나", "미정둘"], Catalog(editor).Select(item => item.Id));
    }

    [Fact]
    public void 식_미정인_조건을_분기에_걸고_식을_채우면_분기가_그_식을_따른다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "친해졌을 때", string.Empty);
        DialogueNode branch = Branch(editor);

        Assert.Null(BranchEntryCondition.Set(editor, branch.Id, "친해졌을 때"));
        Assert.Equal("친해졌을 때", BranchEntryCondition.Read(editor.Project, branch.Id).Label);
        Assert.Equal("친해졌을 때", HeadTransition(editor, branch.Id).ConditionId);

        editor.UpdateChapterCondition("ch01", "친해졌을 때", "trust >= 3");

        AvailableCondition condition = Catalog(editor).Single();
        Assert.Equal(("친해졌을 때", "친해졌을 때", "stat(\"trust\") >= 3"), (condition.Id, condition.Name, condition.Expression));
        Assert.Equal("친해졌을 때", BranchEntryCondition.Read(editor.Project, branch.Id).Label);
        Assert.Equal(
            "stat(\"trust\") >= 3",
            AvailableConditionResolver.Resolve(editor.Project, branch.Id).Find(HeadTransition(editor, branch.Id).ConditionId)!.Expression);
    }

    [Fact]
    public void 식을_고쳐도_비워도_이름은_그대로다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

        editor.UpdateChapterCondition("ch01", "신뢰높음", "trust >= 5");
        Assert.Equal(("신뢰높음", "stat(\"trust\") >= 5"), (Catalog(editor).Single().Id, Catalog(editor).Single().Expression));

        // 식을 비우면 식 미정 — 항상 통과.
        editor.UpdateChapterCondition("ch01", "신뢰높음", string.Empty);
        Assert.Equal(("신뢰높음", AvailableConditionResolver.UndecidedYarn), (Catalog(editor).Single().Id, Catalog(editor).Single().Expression));

        // 되돌리기 한 칸.
        editor.Undo();
        Assert.Equal("stat(\"trust\") >= 5", Catalog(editor).Single().Expression);
    }

    [Fact]
    public void 식이_같은_조건_둘은_따로고_하나를_고쳐도_다른_하나는_그대로다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "가", "trust >= 3");
        editor.AddChapterCondition("ch01", "나", "trust >= 3");

        Assert.Equal(["가", "나"], Catalog(editor).Select(item => item.Name));

        editor.UpdateChapterCondition("ch01", "가", "trust >= 9");

        Assert.Equal(
            [("가", "stat(\"trust\") >= 9"), ("나", "stat(\"trust\") >= 3")],
            Catalog(editor).Select(item => (item.Name, item.Expression)));
    }

    [Fact]
    public void 식이_틀린_조건은_적힌_글자_그대로_보인다()
    {
        // 글 칸의 <<if>>에 작가가 적은 글자가 그대로 선다 — 검증 보고가 짚고, 진행 JSON · Yarn은 막힌다. ⛔ 공급 사본 시절에는 사본이 옛 식에
        //    머물러 글 칸이 고치기 전의 식을 보였다.
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

        editor.UpdateChapterCondition("ch01", "신뢰높음", "없는스탯 >= 3");

        Assert.Equal("없는스탯 >= 3", Catalog(editor).Single().Expression);
    }

    [Fact]
    public void 아직_없는_스탯을_적은_식은_그_스탯을_더하면_곧바로_읽힌다()
    {
        // 2026-11-23 소유자 보고(「뒤늦게 구성 탭에서 조건 및 스탯쪽에서 식을 추가하더라도, 대본탭에서는 그 식이 즉시 반영이 안되네」) — 사본이
        // 없으므로 스탯을 더하는 그 순간 식이 읽힌다.
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "용기있음", string.Empty);
        editor.UpdateChapterCondition("ch01", "용기있음", "courage >= 2");   // courage는 아직 없다 — 식이 안 읽혀 글자 그대로다
        Assert.Equal("courage >= 2", Catalog(editor).Single().Expression);

        editor.AddChapterStat("ch01", "courage", initial: 0, minimum: 0, maximum: 5);

        Assert.Equal(("용기있음", "stat(\"courage\") >= 2"), (Catalog(editor).Single().Id, Catalog(editor).Single().Expression));

        editor.Undo();
        Assert.Equal("courage >= 2", Catalog(editor).Single().Expression);
        Assert.DoesNotContain(editor.FindChapter("ch01")!.Stats, stat => stat.Key == "courage");
    }

    private static ProjectEditor World()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01").Stats.Add(new ChapterStat("trust", 0, 0, 10));
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        editor.EnsureChapterBoard("ch01");
        return editor;
    }

    /// <summary>root의 첫 줄에서 다녀오는 분기 카드 — 조건은 그 카드의 첫머리에 선다.</summary>
    private static DialogueNode Branch(ProjectEditor editor)
    {
        DialogueNode root = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        return editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
    }

    private static LineConditionTransition HeadTransition(ProjectEditor editor, string nodeId)
    {
        DialogueNode node = editor.Project.FindDialogue(nodeId)!;
        return node.FindExtension(node.Script.ActiveLines.First().Id)!.Transition!;
    }

    private static IReadOnlyList<AvailableCondition> Catalog(ProjectEditor editor) =>
        AvailableConditionResolver.Of(editor.FindChapter("ch01")!).Conditions;
}
