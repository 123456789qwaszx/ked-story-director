using Vn.Authoring.Chapters;
using Vn.Authoring.Definition;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// 챕터별 원고 격리 (2026-08-16 소유자 보고) — "다른 챕터의 에피소드를 눌렀는데 기존 이름이
/// 같은 대본 엑셀이 열린다."
///
/// EpisodeId는 <b>챕터 안에서만</b> 유일하다. 원고는 카드의 대본에 살고, 카드는 <b>그 챕터의
/// 판에서만</b> 찾는다(<see cref="EpisodeNaming.CardFor"/>) — 같은 이름의 에피소드가 두 챕터에
/// 있어도 카드·대본이 따로다. 조회 자체의 규칙은 <c>EpisodeNamingLookupTests</c>가 진다.
///
/// (2026-10-29 · D2: 대본 파일 경로(<c>episodes/{챕터}/</c>)·평면 대본 입양 검사는 엑셀 연동과 함께 걷혔다.)
/// </summary>
public sealed class ChapterEpisodeIsolationTests
{
    private static readonly GameDefinition Definition = GameDefinition.Parse("""
        { "speakers": [ { "name": "라루", "characterId": "laru" } ] }
        """)!;

    [Fact]
    public void 같은_이름의_에피소드가_챕터마다_다른_카드와_대본을_갖는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch00");
        editor.EnsureChapter("ch01");

        editor.AddEpisode("ch00", "new01", "new01", 0, 0);
        editor.AddEpisode("ch01", "new01", "new01", 0, 0); // 두 번째도 정말 따로 선다

        DialogueNode cardA = EpisodeNaming.CardFor(editor.Project, "ch00", "new01")!;
        DialogueNode cardB = EpisodeNaming.CardFor(editor.Project, "ch01", "new01")!;

        Assert.NotEqual(cardA.Id, cardB.Id);
        Assert.NotSame(cardA.Script, cardB.Script);

        // 한쪽 원고를 고쳐도 다른 쪽은 그대로다 — 이것이 보고된 버그의 핵심이다.
        editor.ApplyScenarioText(cardA.Id, "라루: ch00의 대사", Definition, confirmDeletes: true);

        Assert.Contains("ch00의 대사", TextOf(editor.Project, cardA));
        Assert.DoesNotContain("ch00의 대사", TextOf(editor.Project, cardB));
    }

    [Fact]
    public void 판_맞추기는_그_챕터의_판_안에서만_카드를_찾는다()
    {
        // 두 판(챕터)에 같은 이름의 에피소드가 있어도, 판 맞추기는 자기 판의 카드만 본다 —
        // 남의 판에서 찾아 버리면 이 챕터의 에피소드는 원고를 쓸 카드가 없다.
        var editor = new ProjectEditor(new StoryProject());

        // ch00 판에 'new01' 카드가 이미 서 있다(다른 챕터의 원고를 담은 카드).
        editor.EnsureChapter("ch00");
        editor.AddEpisode("ch00", "new01", "new01", 0, 0);
        DialogueNode standing = EpisodeNaming.CardFor(editor.Project, "ch00", "new01")!;
        editor.ApplyScenarioText(standing.Id, "라루: ch00의 대사다.", Definition, confirmDeletes: true);

        // ch01에는 같은 이름의 에피소드만 있고 카드가 없다 — 옛 프로젝트의 어긋난 모양이다.
        editor.EnsureChapter("ch01").Episodes.Add(new ChapterEpisode(
            EpisodeId: "new01", Title: "new01", Index: string.Empty,
            DialogueEntry: "new01", X: 0, Y: 0, Memo: null));

        BoardAlignment alignment = editor.AlignBoardsWithChapters();

        Assert.Equal(["ch01/new01"], alignment.CardsStood);

        // ch01 판에 새 카드가 섰고, ch00의 카드는 건드리지 않았다.
        DialogueNode created = EpisodeNaming.CardFor(editor.Project, "ch01", "new01")!;

        Assert.Equal(
            EpisodeNaming.BoardOf(editor.Project, "ch01")!.Id,
            editor.Project.FindFileContainingNode(created.Id)!.Id);
        Assert.NotEqual(standing.Id, created.Id);

        // 새 카드에 쓴 원고는 그 카드에만 간다 — 남의 원고가 쏟아지지 않는다.
        editor.ApplyScenarioText(created.Id, "라루: ch01의 대사다.", Definition, confirmDeletes: true);

        Assert.DoesNotContain("ch01의 대사다.", TextOf(editor.Project, standing));
        Assert.Contains("ch00의 대사다.", TextOf(editor.Project, standing));
        Assert.Contains("ch01의 대사다.", TextOf(editor.Project, created));
    }

    /// <summary>그 카드가 들고 있는 글 전부 — 어느 원고가 들어갔는지의 근거.</summary>
    private static string TextOf(StoryProject project, DialogueNode node) => string.Join(
        " | ",
        project.FindDialogue(node.Id)!.Script.Locales
            .SelectMany(locale => locale.Entries.Values)
            .Select(line => line.Text));
}
