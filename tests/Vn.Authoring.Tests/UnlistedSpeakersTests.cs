using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>대본에만 있는 화자</b> (2026-11-19 소유자 — 「작가들은 화자를 추가하지 않고도 그대로 대사에 speaker로 바로 쓰기도 합니다. 이런 등록되지 않은
/// speaker들 역시 화자 탭에서 모아 둔 뒤에 관리하는 기능」 · usability UX34). 모으기(<see cref="UnlistedSpeakers"/>) · 한꺼번에 목록에 올리기
/// (<see cref="ProjectEditor.AddSpeakers"/> — 되돌리기 한 칸) · 목록의 이름으로 고쳐 합치기(<see cref="ProjectEditor.RenameSpeaker"/>).
/// </summary>
public sealed class UnlistedSpeakersTests
{
    [Fact]
    public void 목록에_없는_화자_이름을_모든_카드에서_줄_수와_함께_가나다_차례로_모은다()
    {
        (ProjectEditor editor, string first, string second) = Build();
        editor.AddSpeaker("라루");
        Write(editor, first, "윌로: 안녕.\n라루: 응.\n바람이 분다.\n윌러: 오타다.");
        Write(editor, second, "윌로: 또 보네.\n시구레: 누구야?");

        Assert.Equal(
            [new UnlistedSpeakers.Entry("시구레", 1), new UnlistedSpeakers.Entry("윌러", 1), new UnlistedSpeakers.Entry("윌로", 2)],
            UnlistedSpeakers.Of(editor.Project));
    }

    [Fact]
    public void 모두_올리면_이름마다_임시_키가_서고_되돌리기_한_칸이다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.AddSpeaker("하린");
        long revision = editor.Revision;

        IReadOnlyList<Speaker> added = editor.AddSpeakers(["Harin", " 윌로 ", "", "하린", "윌로"]);

        Assert.Equal([new Speaker("Harin", "temp_harin2"), new Speaker("윌로", "temp_willo")], added);
        Assert.Equal(["하린", "Harin", "윌로"], editor.Project.Speakers.Select(speaker => speaker.Name));
        Assert.Equal(revision + 1, editor.Revision);

        editor.Undo();
        Assert.Equal(["하린"], editor.Project.Speakers.Select(speaker => speaker.Name));
    }

    [Fact]
    public void 올릴_것이_없으면_적지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.AddSpeaker("하린");
        long revision = editor.Revision;

        Assert.Empty(editor.AddSpeakers(["하린", "  "]));
        Assert.Equal(revision, editor.Revision);
    }

    [Fact]
    public void 대본에만_있는_이름을_목록의_이름으로_고치면_그_화자로_합쳐진다()
    {
        (ProjectEditor editor, string first, string second) = Build();
        editor.AddSpeaker("윌로");
        Write(editor, first, "윌러: 오타다.\n윌로: 맞다.");
        Write(editor, second, "윌러: 또 오타다.");
        long revision = editor.Revision;

        Assert.Equal(2, editor.RenameSpeaker("윌러", "윌로"));

        Assert.Equal(revision + 1, editor.Revision);
        Assert.Empty(UnlistedSpeakers.Of(editor.Project));
        Assert.Equal(["윌로", "윌로"], Speakers(editor, first));
        Assert.Equal(["윌로"], Speakers(editor, second));
        Assert.Equal(["윌로"], editor.Project.Speakers.Select(speaker => speaker.Name));   // 목록은 그대로
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>챕터 ch01 · 그 판의 에피소드 카드 둘.</summary>
    private static (ProjectEditor Editor, string First, string Second) Build()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        string fileId = editor.EnsureChapterBoard("ch01");
        string first = editor.AddDialogueNode(fileId, "main01.01", name: "main01.01").Id;
        string second = editor.AddDialogueNode(fileId, "main01.02", name: "main01.02").Id;
        return (editor, first, second);
    }

    private static void Write(ProjectEditor editor, string nodeId, string text)
    {
        ScenarioPasteOutcome outcome = editor.ApplyScenarioText(nodeId, text, confirmDeletes: true);
        Assert.True(outcome.Applied, outcome.Summary());
    }

    private static string[] Speakers(ProjectEditor editor, string nodeId)
    {
        ScriptDocument script = editor.Project.FindDialogue(nodeId)!.Script;
        return script.ActiveLines.Select(line => script.Text(line.Id).Speaker).Where(speaker => speaker.Length > 0).ToArray();
    }
}
