using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>에피소드를 다른 챕터의 장면으로 옮긴다</b> (2026-11-26 소유자 — 「에피소드를 다른 챕터의 다른 장면으로 옮기고 싶어질 때가 있다」 ·
/// 「선택지연결은 모두 끊고」). 선은 모두 끊기고, 카드와 그 에피소드만 부르는 분기 카드가 함께 가고, 새 챕터에 없는 조건 · 스탯은 함께 적힌다.
/// 챕터 시작 · 출시 기준선이 있는 챕터 · 새 챕터에 같은 ID는 옮기지 않는다. 한 되돌리기 칸이다(<see cref="ChapterMoveGuard"/> ·
/// <see cref="AutoIdFollower.MoveEpisodeToChapter"/>).
/// </summary>
public sealed class ChapterMoveTests
{
    [Fact]
    public void 다른_챕터로_옮기면_선은_모두_끊기고_카드와_분기_카드가_함께_가며_없는_조건과_스탯이_함께_적힌다()
    {
        ProjectEditor editor = Setup();
        DialogueNode card = Card(editor, "ch01", "a");
        string line = card.Script.ActiveLines.First().Id;
        editor.SetScriptLineText(card.Id, line, string.Empty, "믿음이 {stat(\"trust\")}이다");
        DialogueNode branch = editor.AddBranchMarker(card.Id, line);
        editor.SetLineTransition(card.Id, line, LineConditionTransition.BeginIf("믿음"));
        double offsetX = branch.Layout.X - card.Layout.X;
        double offsetY = branch.Layout.Y - card.Layout.Y;
        DialogueNode x = Card(editor, "ch02", "x");
        long before = editor.Revision;

        ChapterMovePlan plan = ChapterMoveGuard.Plan(editor.Project, "ch01", "a", "ch02", "street");

        Assert.Null(plan.Refusal);
        Assert.Equal([("root", "a"), ("a", "b")], plan.Cuts.Select(cut => (cut.FromEpisodeId, cut.ToEpisodeId)));
        Assert.Equal([branch.Id], plan.BranchCardIds);
        Assert.Equal(["믿음"], plan.CopiedConditions.Select(condition => condition.Label));
        Assert.Equal(["trust"], plan.CopiedStats.Select(stat => stat.Key));

        string note = ChapterMoveGuard.Note(editor.Project, "ch01", "ch02", plan, done: false);
        Assert.Contains("선 2개를 모두 끊습니다", note, StringComparison.Ordinal);
        Assert.Contains("'root'→'a' 「이쪽」", note, StringComparison.Ordinal);
        Assert.Contains("'ch02'에 없는 것을 함께 적습니다 — 조건 '믿음' · 스탯 'trust'", note, StringComparison.Ordinal);

        ChapterMoveResult moved = AutoIdFollower.MoveEpisodeToChapter(editor, "ch01", "a", "ch02", "street");

        Assert.Equal("a", moved.EpisodeId);
        Assert.Equal(2, moved.Cut.Count);
        Assert.Equal(before + 1, editor.Revision);   // 한 칸

        // 옛 챕터 — 에피소드도 선도 없다(root → a · a → b 모두).
        ChapterDocument ch01 = editor.FindChapter("ch01")!;
        Assert.DoesNotContain(ch01.Episodes, episode => episode.EpisodeId == "a");
        Assert.Empty(ch01.Edges);
        Assert.Null(EpisodeNaming.CardFor(editor.Project, "ch01", "a"));

        // 새 챕터 — 그 장면의 맨 뒤에, 같은 카드(Id 그대로 · 글째)와 분기 카드가 판에.
        ChapterDocument ch02 = editor.FindChapter("ch02")!;
        Assert.Equal("a", ch02.Episodes[^1].EpisodeId);
        Assert.Equal("street", ch02.Episodes[^1].SceneId);
        Assert.Equal("x", ch02.Episodes[0].EpisodeId);   // 챕터 시작은 그대로
        Assert.Same(card, EpisodeNaming.CardFor(editor.Project, "ch02", "a"));
        Assert.Equal("믿음이 {stat(\"trust\")}이다", card.Script.Text(line).Text);
        StoryFile board = EpisodeNaming.BoardOf(editor.Project, "ch02")!;
        Assert.Contains(branch, board.Nodes);
        Assert.Contains(branch.Id, card.FindExtension(line)!.DetourTargetNodeIds);
        Assert.True(card.Layout.Y > x.Layout.Y);   // 그 장면 카드의 밑
        Assert.Equal((offsetX, offsetY), (branch.Layout.X - card.Layout.X, branch.Layout.Y - card.Layout.Y));

        // 새 챕터에 없던 조건 · 스탯이 함께 적혔다 — 그 장면의 대사가 막히지 않는다.
        Assert.Equal("trust >= 5", ch02.Conditions.Single(condition => condition.Label == "믿음").Expression);
        Assert.Equal(10, ch02.Stats.Single(stat => stat.Key == "trust").Maximum);
        Assert.DoesNotContain(ch02.ToGraphModel("ch02").Errors, item => item.Message.Contains("믿음", StringComparison.Ordinal));

        // 되돌리기 한 번 — 모두 돌아온다.
        editor.Undo();
        ChapterDocument back = editor.FindChapter("ch01")!;
        Assert.Contains(back.Episodes, episode => episode.EpisodeId == "a");
        Assert.Equal(2, back.Edges.Count);
        Assert.NotNull(EpisodeNaming.CardFor(editor.Project, "ch01", "a"));
        Assert.DoesNotContain(editor.FindChapter("ch02")!.Episodes, episode => episode.EpisodeId == "a");
        Assert.Empty(editor.FindChapter("ch02")!.Conditions);
        Assert.Empty(editor.FindChapter("ch02")!.Stats);
    }

    [Fact]
    public void 자동_모양_ID는_새_챕터와_장면의_번호로_따라가고_사람이_지은_ID가_겹치면_옮기지_않는다()
    {
        ProjectEditor editor = Setup();
        editor.AddEpisode("ch01", "ch01_opening_new01", "새 에피소드", 660, 0, sceneId: "opening");

        ChapterMoveResult moved = AutoIdFollower.MoveEpisodeToChapter(editor, "ch01", "ch01_opening_new01", "ch02", "street");

        Assert.Equal("ch02_street_new01", moved.EpisodeId);
        Assert.Equal("ch02_street_new01", editor.FindChapter("ch02")!.Episodes[^1].EpisodeId);
        Assert.NotNull(EpisodeNaming.CardFor(editor.Project, "ch02", "ch02_street_new01"));

        // 사람이 지은 ID는 그대로 간다 — 새 챕터에 같은 ID가 있으면 옮기지 않고 까닭을 말한다.
        editor.AddEpisode("ch02", "b", "다른 b", 440, 0, sceneId: "street");
        ChapterMovePlan plan = ChapterMoveGuard.Plan(editor.Project, "ch01", "b", "ch02", "street");
        Assert.Contains("'b'인 에피소드가 이미 있습니다", plan.Refusal, StringComparison.Ordinal);
        Assert.Empty(plan.Cuts);
        Assert.Throws<InvalidOperationException>(() => AutoIdFollower.MoveEpisodeToChapter(editor, "ch01", "b", "ch02", "street"));
        Assert.Contains(editor.FindChapter("ch01")!.Episodes, episode => episode.EpisodeId == "b");
    }

    [Fact]
    public void 챕터_시작과_출시_기준선이_있는_챕터의_에피소드는_옮기지_않는다()
    {
        ProjectEditor editor = Setup();

        // 챕터 시작은 목록의 첫 에피소드다 — 옮기면 남은 아무 에피소드나 시작이 된다.
        Assert.Contains("시작 에피소드입니다", ChapterMoveGuard.Plan(editor.Project, "ch01", "root", "ch02", "street").Refusal, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => AutoIdFollower.MoveEpisodeToChapter(editor, "ch01", "root", "ch02", "street"));
        Assert.Equal("root", editor.FindChapter("ch01")!.Episodes[0].EpisodeId);

        // 출시 기준선이 있는 챕터 — 세이브가 그 에피소드를 가리키고, 끊는 선이 선택지 순서를 바꾼다.
        Assert.Contains("출시 기준선", ChapterMoveGuard.Plan(editor.Project, "ch01", "a", "ch02", "street", sourceReleased: true).Refusal, StringComparison.Ordinal);

        // 같은 챕터 · 없는 에피소드는 이 계획의 일이 아니다.
        ChapterMovePlan same = ChapterMoveGuard.Plan(editor.Project, "ch01", "a", "ch01", "hall");
        Assert.Null(same.Refusal);
        Assert.Empty(same.Cuts);
        Assert.Equal("a", same.EpisodeId);
    }

    [Fact]
    public void 다른_에피소드도_부르는_분기_카드는_옛_챕터에_남고_옮긴_카드의_분기점은_이어지지_않은_채_남는다()
    {
        ProjectEditor editor = Setup();
        DialogueNode a = Card(editor, "ch01", "a");
        DialogueNode b = Card(editor, "ch01", "b");
        string aLine = a.Script.ActiveLines.First().Id;
        string bLine = b.Script.ActiveLines.First().Id;
        DialogueNode branch = editor.AddBranchMarker(a.Id, aLine);
        editor.SetExitTarget(b.Id, ExitPortKind.Detour, bLine, branch.Id);
        Assert.Contains(branch.Id, b.FindExtension(bLine)!.DetourTargetNodeIds);

        ChapterMovePlan plan = ChapterMoveGuard.Plan(editor.Project, "ch01", "a", "ch02", "street");

        Assert.Empty(plan.BranchCardIds);
        ChapterMoveDetour shared = Assert.Single(plan.SharedDetours);
        Assert.Equal((a.Id, aLine, branch.Id), (shared.NodeId, shared.LineId, shared.TargetNodeId));
        Assert.Contains("옛 챕터에 남고", ChapterMoveGuard.Note(editor.Project, "ch01", "ch02", plan, done: false), StringComparison.Ordinal);

        AutoIdFollower.MoveEpisodeToChapter(editor, "ch01", "a", "ch02", "street");

        Assert.Contains(branch, EpisodeNaming.BoardOf(editor.Project, "ch01")!.Nodes);
        Assert.Contains(branch.Id, b.FindExtension(bLine)!.DetourTargetNodeIds);   // 남는 쪽은 그대로
        Assert.DoesNotContain(branch.Id, a.FindExtension(aLine)!.DetourTargetNodeIds);
        Assert.Equal(1, a.FindExtension(aLine)!.OpenDetours);   // 분기점은 이어지지 않은 채 제자리에
    }

    [Fact]
    public void 같은_이름의_조건과_스탯은_새_챕터의_것을_쓰고_덮어쓰지_않는다()
    {
        ProjectEditor editor = Setup();
        editor.AddChapterStat("ch02", "trust", 0, 0, 3);
        editor.AddChapterCondition("ch02", "믿음", "trust >= 1");
        DialogueNode card = Card(editor, "ch01", "a");
        string line = card.Script.ActiveLines.First().Id;
        editor.SetScriptLineText(card.Id, line, string.Empty, "{stat(\"trust\")}");
        editor.SetLineTransition(card.Id, line, LineConditionTransition.BeginIf("믿음"));

        ChapterMovePlan plan = ChapterMoveGuard.Plan(editor.Project, "ch01", "a", "ch02", "street");

        Assert.Empty(plan.CopiedConditions);
        Assert.Empty(plan.CopiedStats);
        Assert.Equal(["조건 '믿음'", "스탯 'trust'"], plan.SameNames);
        Assert.Contains("같은 이름이 이미 있어 'ch02'의 것을 씁니다", ChapterMoveGuard.Note(editor.Project, "ch01", "ch02", plan, done: true), StringComparison.Ordinal);

        AutoIdFollower.MoveEpisodeToChapter(editor, "ch01", "a", "ch02", "street");

        Assert.Equal("trust >= 1", editor.FindChapter("ch02")!.Conditions.Single().Expression);
        Assert.Equal(3, editor.FindChapter("ch02")!.Stats.Single().Maximum);
    }

    /// <summary>ch01: root(opening · 챕터 시작) → a(opening) → b(hall), 스탯 trust(0~10) · 조건 믿음(trust ≥ 5). ch02: x(street).</summary>
    private static ProjectEditor Setup()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "root", "root", 0, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "a", "a", 220, 0, sceneId: "opening");
        editor.AddEpisode("ch01", "b", "b", 440, 0, sceneId: "hall");
        editor.AddEdge("ch01", "root", "a", optionLabel: "이쪽");
        editor.AddEdge("ch01", "a", "b", optionLabel: "저쪽");
        editor.AddChapterStat("ch01", "trust", 0, 0, 10);
        editor.AddChapterCondition("ch01", "믿음", "trust >= 5");
        editor.EnsureChapter("ch02");
        editor.AddEpisode("ch02", "x", "x", 0, 0, sceneId: "street");
        return editor;
    }

    private static DialogueNode Card(ProjectEditor editor, string chapterId, string episodeId) =>
        EpisodeNaming.CardFor(editor.Project, chapterId, episodeId)!;
}
