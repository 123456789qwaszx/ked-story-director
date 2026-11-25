using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터의 스탯·조건 조작은 그 챕터만 건드린다</b> (2026-10-29 · L4 · 2026-11-25 condition-supply C1).
///
/// 스탯과 조건은 챕터마다 따로다 — ch01과 ch02가 둘 다 <c>trust</c>와 <c>신뢰높음</c>을 가질 수 있다. 줄 전환은 조건을 <b>이름</b>으로
/// 가리키므로, 개명 · 쓰임 찾기는 <b>그 챕터 판의</b> 카드만 본다. ⛔ 2026-10-29까지는 이름 모양(<c>챕터 … 조건</c>)만 보고 모든 챕터의 공급
/// 노드를 훑어서, 한 챕터를 고치면 다른 챕터의 판이 함께 바뀌었다.
/// </summary>
public sealed class ConditionScopeTests
{
    [Fact]
    public void 스탯_개명은_다른_챕터의_조건식을_안_건드린다()
    {
        ProjectEditor editor = TwoChapters();

        Assert.True(editor.RenameChapterStat("ch01", "trust", "favor").Applied);

        Assert.Equal("stat(\"favor\") >= 3", Condition(editor, "ch01").Expression);
        Assert.Equal("stat(\"trust\") >= 3", Condition(editor, "ch02").Expression);
    }

    [Fact]
    public void 조건을_지워도_다른_챕터의_같은_이름_조건은_남는다()
    {
        ProjectEditor editor = TwoChapters();

        ChapterConditionRemoveOutcome outcome = editor.RemoveChapterCondition("ch01", "신뢰높음");

        Assert.True(outcome.Applied);
        Assert.Empty(AvailableConditionResolver.Of(editor.FindChapter("ch01")!).Conditions);
        Assert.Single(AvailableConditionResolver.Of(editor.FindChapter("ch02")!).Conditions);
    }

    [Fact]
    public void 다른_챕터의_갈래는_이_챕터_조건의_쓰임이_아니다()
    {
        ProjectEditor editor = TwoChapters();
        Branch(editor, "ch02");

        Assert.Empty(editor.FindConditionUses("ch01", "신뢰높음"));
        Assert.Single(editor.FindConditionUses("ch02", "신뢰높음"));
    }

    [Fact]
    public void 조건_개명은_다른_챕터의_같은_이름_갈래를_안_건드린다()
    {
        ProjectEditor editor = TwoChapters();
        LineConditionTransition here = Branch(editor, "ch01");
        LineConditionTransition there = Branch(editor, "ch02");

        Assert.True(editor.RenameChapterCondition("ch01", "신뢰높음", "믿음").Applied);

        Assert.Equal("믿음", Transition(editor, "ch01").ConditionId);
        Assert.Equal("신뢰높음", Transition(editor, "ch02").ConditionId);
        Assert.Equal(here.Kind, Transition(editor, "ch01").Kind);
        Assert.Same(there, Transition(editor, "ch02"));
    }

    [Fact]
    public void 스탯을_비우고_지워도_다른_챕터의_조건식은_남는다()
    {
        ProjectEditor editor = TwoChapters();

        Assert.True(editor.RemoveChapterStat("ch01", "trust", clearUses: true).Applied);

        Assert.Equal(AvailableConditionResolver.UndecidedYarn, Condition(editor, "ch01").Expression);
        Assert.Equal("stat(\"trust\") >= 3", Condition(editor, "ch02").Expression);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static AvailableCondition Condition(ProjectEditor editor, string chapterId) =>
        Assert.Single(AvailableConditionResolver.Of(editor.FindChapter(chapterId)!).Conditions);

    /// <summary>그 챕터 root 카드의 첫 줄에 「신뢰높음」 갈래를 연다.</summary>
    private static LineConditionTransition Branch(ProjectEditor editor, string chapterId)
    {
        DialogueNode node = EpisodeNaming.CardFor(editor.Project, chapterId, "root")!;
        editor.SetLineTransition(node.Id, node.Script.ActiveLines.First().Id, LineConditionTransition.BeginIf("신뢰높음"));
        return Transition(editor, chapterId);
    }

    private static LineConditionTransition Transition(ProjectEditor editor, string chapterId)
    {
        DialogueNode node = EpisodeNaming.CardFor(editor.Project, chapterId, "root")!;
        return node.FindExtension(node.Script.ActiveLines.First().Id)!.Transition!;
    }

    /// <summary>같은 이름의 스탯 <c>trust</c>와 조건 <c>신뢰높음</c>을 가진 두 챕터 — 둘 다 판과 root 카드가 서 있다.</summary>
    private static ProjectEditor TwoChapters()
    {
        var editor = new ProjectEditor(new StoryProject());

        foreach (string chapterId in new[] { "ch01", "ch02" })
        {
            ChapterDocument chapter = editor.EnsureChapter(chapterId);
            chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));

            editor.AddEpisode(chapterId, "root", title: "root", 0, 0, sceneId: "root");
            editor.AddChapterCondition(chapterId, "신뢰높음", "trust >= 3");
            editor.EnsureChapterBoard(chapterId);
        }

        return editor;
    }
}
