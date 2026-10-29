using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>새 카드가 연출 그래프에 설 자리 — 위에서 아래로</b> (D3 · 2026-10-29).
///
/// 소유자: *"챕터 그래프처럼 위에서 아래로 … 분기는 기존처럼 오른쪽으로."* 선택지로 가는 다음 에피소드는
/// 부모 카드 아래, 분기로 다녀갈 카드는 원본 오른쪽이다.
///
/// ⚠ <b>이미 선 카드는 안 옮긴다.</b> 자리는 사람이 끌어 정하는 것이다.
/// </summary>
public sealed class NodePlacementTests
{
    [Fact]
    public void 시작_카드는_빈_판의_원점에_선다()
    {
        StoryProject project = World("root");

        Assert.Equal((0, 0), NodePlacement.For(project, "ch01", "root"));
    }

    [Fact]
    public void 선택지로_가는_다음_카드는_부모_아래에_선다()
    {
        StoryProject project = World("root", "a");
        Edge(project, "root", "a");
        Place(project, "root", 0, 0);

        Assert.Equal((0, NodePlacement.Row), NodePlacement.For(project, "ch01", "a"));
    }

    [Fact]
    public void 간선이_아직_없어도_부모를_주면_그_아래에_선다()
    {
        // 회귀(§6-1): 에피소드와 간선을 한 변경으로 만드는 길은 자리를 물을 때 간선이 아직 없다.
        //    예전에는 그래서 언제나 판 오른쪽 끝에 섰다.
        StoryProject project = World("root");
        Place(project, "root", 0, 0);

        Assert.Equal((0, NodePlacement.Row), NodePlacement.For(project, "ch01", "새것", parentEpisodeId: "root"));
    }

    [Fact]
    public void 부모_아래가_차_있으면_오른쪽으로_비켜_선다()
    {
        StoryProject project = World("root", "a", "b");
        Edge(project, "root", "a");
        Edge(project, "root", "b");
        Place(project, "root", 0, 0);
        Place(project, "a", 0, NodePlacement.Row);

        Assert.Equal((NodePlacement.Column, NodePlacement.Row), NodePlacement.For(project, "ch01", "b"));
    }

    [Fact]
    public void 합류하는_카드는_가장_아래_부모의_밑에_선다()
    {
        // root → a → c,  root → c — c는 root가 아니라 a 밑에 서야 a의 레인이 위로 거슬러 오르지 않는다.
        StoryProject project = World("root", "a", "c");
        Edge(project, "root", "c");
        Edge(project, "a", "c");
        Place(project, "root", 0, 0);
        Place(project, "a", 0, NodePlacement.Row);

        Assert.Equal((0, 2 * NodePlacement.Row), NodePlacement.For(project, "ch01", "c"));
    }

    [Fact]
    public void 사람이_부모를_옮겨_뒀으면_그_아래를_따른다()
    {
        // 자리는 사람이 정하는 것이다 — 새 카드가 저 혼자 원래 자리로 돌아가면 안 된다.
        StoryProject project = World("root", "a");
        Edge(project, "root", "a");
        Place(project, "root", 1000, 700);

        Assert.Equal((1000, 700 + NodePlacement.Row), NodePlacement.For(project, "ch01", "a"));
    }

    [Fact]
    public void 부모가_없으면_판의_맨_아래_카드_밑에_선다()
    {
        StoryProject project = World("root");
        Place(project, "root", 0, 0);

        Assert.Equal((0, NodePlacement.Row), NodePlacement.For(project, "ch01", "곁가지"));
    }

    [Fact]
    public void 챕터가_아닌_판도_겹치지는_않게_둔다()
    {
        var project = new StoryProject();
        var editor = new ProjectEditor(project);

        string fileId = editor.EnsureChapterBoard("작가의 판");   // 같은 이름의 챕터가 없다
        editor.AddDialogueNode(fileId, 0, 0, "아무거나");

        Assert.Equal((0, NodePlacement.Row), NodePlacement.For(project, "작가의 판", "또 하나"));
    }

    [Fact]
    public void 분기로_다녀갈_카드는_원본_오른쪽에_선다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "root", "복도", 0, 0);

        DialogueNode caller = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        string lineId = caller.Script.ActiveLines.First().Id;

        DialogueNode branch = editor.AddBranchMarker(caller.Id, lineId);

        Assert.Equal(
            (caller.Layout.X + NodePlacement.Column, caller.Layout.Y),
            (branch.Layout.X, branch.Layout.Y));
    }

    [Fact]
    public void 챕터_그래프에서_이은_다음_에피소드의_카드는_부모_아래에_선다()
    {
        // 회귀(§6-1)를 편집기 명령으로 본다 — AddNextEpisode가 부모를 함께 준다.
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.AddEpisode("ch01", "root", "복도", 0, 0);
        editor.AddNextEpisode("ch01", "root", "next", "옥상", 0, 170, optionLabel: "간다");

        DialogueNode root = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;
        DialogueNode next = EpisodeNaming.CardFor(editor.Project, "ch01", "next")!;

        Assert.Equal((root.Layout.X, root.Layout.Y + NodePlacement.Row), (next.Layout.X, next.Layout.Y));
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>그 에피소드의 카드를 그 자리에 세운다 — 사람이 끌어다 둔 것과 같다.</summary>
    private static void Place(StoryProject project, string episodeId, double x, double y)
    {
        var editor = new ProjectEditor(project);

        editor.AddDialogueNode(editor.EnsureChapterBoard("ch01"), x, y, episodeId)
            .MarkedEpisodeId = episodeId;
    }

    private static void Edge(StoryProject project, string from, string to) =>
        project.Chapters.Single().Edges.Add(new ChapterEdge(from, to, "간다", null, null));

    /// <summary>ch01과 그 판 — 에피소드는 챕터에 있고, 카드는 아직 없다(엑셀에서 들여온 옛 프로젝트처럼).</summary>
    private static StoryProject World(params string[] episodeIds)
    {
        var project = new StoryProject();
        var editor = new ProjectEditor(project);

        ChapterDocument chapter = editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");   // 카드는 자리를 물어본 뒤에 선다

        foreach (string episodeId in episodeIds)
        {
            chapter.Episodes.Add(new ChapterEpisode(
                episodeId, episodeId, Index: string.Empty, DialogueEntry: episodeId,
                0, 0, Memo: null));
        }

        return project;
    }
}
