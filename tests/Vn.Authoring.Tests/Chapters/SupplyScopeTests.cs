using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터의 스탯·조건 조작은 그 챕터의 공급 사본만 건드린다</b> (2026-10-29 · L4).
///
/// 스탯과 조건은 챕터마다 따로다 — ch01과 ch02가 둘 다 <c>trust</c>와 <c>신뢰높음</c>을 가질 수
/// 있다. 그때까지 개명·삭제·쓰임 찾기는 이름 모양(<c>챕터 … 조건</c>)만 보고 <b>모든 챕터의</b>
/// 공급 노드를 훑어서, 한 챕터를 고치면 다른 챕터의 판이 함께 바뀌었다.
/// </summary>
public sealed class SupplyScopeTests
{
    [Fact]
    public void 스탯_개명은_다른_챕터의_공급_사본을_안_건드린다()
    {
        ProjectEditor editor = TwoChapters();

        StatRenameOutcome outcome = editor.RenameChapterStat("ch01", "trust", "호감");

        Assert.Equal(1, outcome.SuppliedConditions);
        Assert.Equal("stat(\"호감\") >= 3", Supplied(editor, "ch01").Expression);
        Assert.Equal("stat(\"trust\") >= 3", Supplied(editor, "ch02").Expression);
    }

    [Fact]
    public void 조건을_지워도_다른_챕터의_같은_이름_사본은_남는다()
    {
        ProjectEditor editor = TwoChapters();

        ChapterConditionRemoveOutcome outcome = editor.RemoveChapterCondition("ch01", "신뢰높음");

        Assert.True(outcome.Applied);
        Assert.Equal(1, outcome.SuppliedRemoved);
        Assert.Empty(SupplyOf(editor, "ch01").Conditions);
        Assert.Single(SupplyOf(editor, "ch02").Conditions);
    }

    [Fact]
    public void 다른_챕터의_갈래는_이_챕터_조건의_쓰임이_아니다()
    {
        ProjectEditor editor = TwoChapters();

        string fileId = editor.Project.Files.Single(file => file.Name == "ch02").Id;
        DialogueNode node = editor.AddDialogueNode(fileId, 0, 0, "root");
        string lineId = editor.Project.FindScript(node.ScriptId)!.ActiveLines.First().Id;

        editor.SetLineTransition(
            node.Id,
            lineId,
            new LineConditionTransition(ConditionTransitionKind.BeginIf, Supplied(editor, "ch02").Id));

        Assert.Empty(editor.FindConditionUses("ch01", "신뢰높음"));
        Assert.Single(editor.FindConditionUses("ch02", "신뢰높음"));
    }

    [Fact]
    public void 스탯을_비우고_지워도_다른_챕터의_공급식은_남는다()
    {
        ProjectEditor editor = TwoChapters();

        Assert.True(editor.RemoveChapterStat("ch01", "trust", clearUses: true).Applied);

        Assert.Equal("stat(\"trust\") >= 3", Supplied(editor, "ch02").Expression);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static SetNode SupplyOf(ProjectEditor editor, string chapterId) =>
        editor.Project.EnumerateNodes().OfType<SetNode>().Single(node =>
            string.Equals(node.Name, ChapterBoardSupply.ConditionSupplyNodeName(chapterId), StringComparison.Ordinal));

    private static ConditionDefinition Supplied(ProjectEditor editor, string chapterId) =>
        Assert.Single(SupplyOf(editor, chapterId).Conditions);

    /// <summary>같은 이름의 스탯 <c>trust</c>와 조건 <c>신뢰높음</c>을 가진 두 챕터, 둘 다 판에 공급된 상태.</summary>
    private static ProjectEditor TwoChapters()
    {
        var editor = new ProjectEditor(new StoryProject());

        foreach (string chapterId in new[] { "ch01", "ch02" })
        {
            ChapterDocument chapter = editor.EnsureChapter(chapterId);
            chapter.Stats.Add(new ChapterStat("trust", "trust", 0, 0, 10, SourceRow: 0));

            editor.AddEpisode(chapterId, "root", title: "root", 0, 0);
            editor.AddChapterCondition(chapterId, "신뢰높음", "trust >= 3");

            ChapterBoardSupply.SupplyChapterConditionsToBoard(
                editor,
                editor.EnsureChapterBoard(chapterId),
                editor.FindChapter(chapterId)!.ToGraphModel(chapterId, null));
        }

        return editor;
    }
}
