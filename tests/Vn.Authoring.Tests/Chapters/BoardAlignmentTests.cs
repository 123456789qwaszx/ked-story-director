using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>엑셀 [대본 가져오기]가 하던 일 중 엑셀이 아니던 몫</b>이 제자리를 찾았다 (2026-10-29 · D2).
///
/// <list type="bullet">
/// <item>에피소드마다 카드가 선다 — 프로젝트를 열 때 <see cref="ProjectEditor.AlignBoardsWithChapters"/>가 맞춘다.</item>
/// <item>챕터 조건이 판의 공급 노드로 건너간다 — 조건을 고치는 명령과 판이 서는 순간이 부른다.</item>
/// </list>
///
/// ⛔ 예전에는 둘 다 단추를 눌러야 일어났다. 단추가 사라졌는데 이 일들이 따라오지 않으면,
/// 카드 없는 에피소드는 <b>글을 쓸 자리가 없고</b> 툴에서 적은 조건은 <b>대사 노드의 조건 목록에
/// 안 뜬다</b>.
/// </summary>
public sealed class BoardAlignmentTests
{
    // ── 카드 ────────────────────────────────────────────────────────────────

    [Fact]
    public void 카드가_없는_에피소드에_빈_카드가_선다()
    {
        ProjectEditor editor = LegacyChapter("ep01", "ep02");

        BoardAlignment aligned = editor.AlignBoardsWithChapters();

        Assert.Equal(["ch01/ep01", "ch01/ep02"], aligned.CardsStood);

        foreach (string episodeId in (string[])["ep01", "ep02"])
        {
            DialogueNode card = Assert.IsType<DialogueNode>(
                EpisodeNaming.CardFor(editor.Project, "ch01", episodeId));

            // 표식이 신원이다 — 이름만 맞으면 개명 한 번에 끊긴다.
            Assert.Equal(episodeId, card.MarkedEpisodeId);
            Assert.NotNull(card.Script);
        }

        // 에피소드가 늘지 않았다 — 있던 에피소드에 카드만 붙었다.
        Assert.Equal(2, editor.FindChapter("ch01")!.Episodes.Count);
    }

    [Fact]
    public void 두_번째_맞추기는_아무것도_안_바꾼다()
    {
        ProjectEditor editor = LegacyChapter("ep01");
        editor.AlignBoardsWithChapters();

        string before = ProjectSnapshotCodec.Encode(editor.Project);
        int raised = 0;
        editor.Changed += (_, _) => raised++;

        BoardAlignment again = editor.AlignBoardsWithChapters();

        Assert.False(again.Changed);
        Assert.Equal(0, raised);
        Assert.Equal(before, ProjectSnapshotCodec.Encode(editor.Project));
    }

    [Fact]
    public void 맞추기는_되돌리기_한_칸이다()
    {
        ProjectEditor editor = LegacyChapter("ep01", "ep02");
        string before = ProjectSnapshotCodec.Encode(editor.Project);

        editor.AlignBoardsWithChapters();
        editor.Undo();

        Assert.Equal(before, ProjectSnapshotCodec.Encode(editor.Project));
    }

    [Fact]
    public void 툴에서_만든_에피소드는_맞출_것이_없다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "ep01", "복도", 0, 0);

        Assert.False(editor.AlignBoardsWithChapters().Changed);
    }

    // ── 조건 공급 ───────────────────────────────────────────────────────────

    [Fact]
    public void 판이_있는_챕터에_조건을_더하면_공급_노드가_함께_받는다()
    {
        ProjectEditor editor = ChapterWithBoard();

        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

        ConditionDefinition supplied = Assert.Single(Supply(editor).Conditions);
        Assert.Equal("신뢰높음", supplied.Name);
        Assert.Equal("stat(\"trust\") >= 3", supplied.Expression);
    }

    [Fact]
    public void 조건_더하기와_공급은_되돌리기_한_번에_함께_간다()
    {
        ProjectEditor editor = ChapterWithBoard();
        string before = ProjectSnapshotCodec.Encode(editor.Project);

        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        editor.Undo();

        Assert.Equal(before, ProjectSnapshotCodec.Encode(editor.Project));
    }

    [Fact]
    public void 식을_고치면_공급된_조건이_Id를_지킨_채_따라간다()
    {
        // ⚠ 공급은 식으로 짝을 찾는다 — 그냥 두면 식을 고칠 때마다 새 Id로 하나 더 생기고,
        //    줄에 매달린 전환은 옛 Id를 붙든 채 옛 식으로 남는다.
        ProjectEditor editor = ChapterWithBoard();
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        string id = Assert.Single(Supply(editor).Conditions).Id;

        editor.UpdateChapterCondition("ch01", "신뢰높음", "trust >= 7");

        ConditionDefinition supplied = Assert.Single(Supply(editor).Conditions);
        Assert.Equal(id, supplied.Id);
        Assert.Equal("stat(\"trust\") >= 7", supplied.Expression);
    }

    [Fact]
    public void 판이_없으면_조건을_더해도_판을_미리_만들지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));

        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

        Assert.Null(EpisodeNaming.BoardOf(editor.Project, "ch01"));
    }

    [Fact]
    public void 조건을_먼저_적고_판이_나중에_서도_공급된다()
    {
        // 조건부터 적고 에피소드를 나중에 만드는 순서 — 판은 첫 에피소드와 함께 선다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
        editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

        editor.AddEpisode("ch01", "ep01", "복도", 0, 0);

        Assert.Equal("신뢰높음", Assert.Single(Supply(editor).Conditions).Name);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>엑셀에서 들여온 옛 프로젝트처럼 — 챕터에 에피소드는 있는데 판도 카드도 없다.</summary>
    private static ProjectEditor LegacyChapter(params string[] episodeIds)
    {
        var editor = new ProjectEditor(new StoryProject());
        ChapterDocument chapter = editor.EnsureChapter("ch01");

        for (int index = 0; index < episodeIds.Length; index++)
        {
            chapter.Episodes.Add(new ChapterEpisode(
                episodeIds[index], episodeIds[index], Index: string.Empty,
                DialogueEntry: episodeIds[index], index * 220, 0, Memo: null));
        }

        return editor;
    }

    /// <summary>스탯 trust가 있고 판이 선 챕터.</summary>
    private static ProjectEditor ChapterWithBoard()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
        editor.AddEpisode("ch01", "ep01", "복도", 0, 0);

        return editor;
    }

    private static SetNode Supply(ProjectEditor editor) =>
        editor.Project.EnumerateNodes().OfType<SetNode>()
            .Single(node => ChapterBoardSupply.IsConditionSupplyNodeName(node.Name));
}
