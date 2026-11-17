using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>조건·화자의 이름을 편집해도 연결이 이어진다</b> (2026-08-24 소유자: "조건이나 화자의
/// 이름을 편집한 경우도 마찬가지입니다").
///
/// 둘은 끊어지는 자리가 다르다.
/// <list type="bullet">
///   <item><b>조건</b> — 줄에 매달린 전환은 <c>ConditionId</c>로 잇는다. 그래서 라벨이 바뀌었을
///     때 조건을 <em>새로</em> 만들면 Id가 갈려 이미 매달린 갈래가 전부 고아가 된다. 라벨을 바꾸는 명령
///     (<see cref="ProjectEditor.RenameChapterCondition"/>)이 판 사본의 <b>이름만</b> 갈아 끼운다(<c>ConditionRenameTests</c>).</item>
///   <item><b>화자</b> — 붙드는 것이 이름 문자열 하나뿐이다(<c>LocalizedLine.Speaker</c>).
///     등록부에서만 갈면 그 줄들이 전부 미등록이 된다. 그래서 개명이 <b>프로젝트의 줄</b>을
///     함께 바꾼다(<see cref="ProjectEditor.RenameSpeaker"/>).</item>
/// </list>
///
/// (2026-10-29 · D2: 대본 워크북을 다시 내던 일과 그 검사(잠김·미룸)는 엑셀 연동과 함께 걷혔다.)
/// </summary>
public sealed class SpeakerAndConditionRenameTests
{
    // ── 조건 라벨 ───────────────────────────────────────────────────────────

    /// <summary>스탯 하나를 견주는 챕터 조건 하나짜리 모델. 라벨만 갈아 끼울 수 있다.</summary>
    private static ChapterGraphModel ChapterWith(string label) => new(
        chapterId: "ch01",
        sourcePath: "ch01",
        episodes: [],
        edges: [],
        conditions: [
            new ChapterCondition(
                label,
                "trust >= 3",
                Description: null,
                Parsed: [new ConditionTerm(ConditionTermKind.StatComparison, "trust", ConditionComparison.AtLeast, 3)],
                IsValid: true)
        ],
        stats: [],
        fixtures: [],
        diagnostics: []);

    [Fact]
    public void 라벨이_그대로면_조건에_손대지_않는다()
    {
        // 같은 값을 다시 쓰면 되돌리기 기록이 쌓이고 화면이 다시 그려진다.
        var project = new StoryProject();
        var file = new StoryFile("sf_ch01", "ch01", "story/ch01.vnstory.json");
        project.Files.Add(file);
        project.Chapters.Add(new global::Vn.Authoring.Chapters.ChapterDocument { ChapterId = "ch01" });   // 카드는 챕터 판에만 선다
        var editor = new ProjectEditor(project);

        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            editor, file.Id, ChapterWith("신뢰 높음"));

        long revision = editor.Revision;

        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            editor, file.Id, ChapterWith("신뢰 높음"));

        Assert.Equal(revision, editor.Revision);
    }

    // ── 화자 ────────────────────────────────────────────────────────────────

    private sealed record World(ProjectEditor Editor, ScriptDocument Script, ScriptLine Line);

    /// <summary>챕터 ch01 · 그 판의 에피소드 카드 하나 · 화자가 적힌 줄 하나가 선 프로젝트.</summary>
    private static World BuildProject(string speaker, string episodeId = "main01.01")
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        string fileId = editor.EnsureChapterBoard("ch01");

        // 챕터 판에 세우면 에피소드도 함께 선다 — 이름이 곧 EpisodeId다.
        DialogueNode node = editor.AddDialogueNode(fileId, name: episodeId);
        ScriptDocument script = node.Script;
        ScriptLine line = script.ActiveLines.First();
        editor.SetScriptLineText(node.Id, line.Id, speaker, "문을 연다");

        return new World(editor, script, line);
    }

    [Fact]
    public void 화자_개명이_에피소드_카드의_줄에_간다()
    {
        World world = BuildProject("라루");

        Assert.Equal(1, world.Editor.RenameSpeaker("라루", "라루엘"));

        Assert.Equal("라루엘", world.Script.Text(world.Line.Id, world.Script.PrimaryLocale).Speaker);
    }

    [Fact]
    public void 글자가_다른_이름은_건드리지_않는다()
    {
        // 추측 보정 금지 — 사람이 손으로 적은 다른 이름을 툴이 고쳐 주지 않는다. 그리고 바뀐
        // 것이 없으면 변경도 없다(되돌리기 기록이 쌓이고 화면이 다시 그려지는 일이 없다).
        World world = BuildProject("라루");
        long revision = world.Editor.Revision;

        Assert.Equal(0, world.Editor.RenameSpeaker("라루X", "누구"));

        Assert.Equal("라루", world.Script.Text(world.Line.Id, world.Script.PrimaryLocale).Speaker);
        Assert.Equal(revision, world.Editor.Revision);
    }

}
