using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>챕터에 쓴 글의 양</b>(2026-11-18 소유자 — 「챕터에 작성해둔 글자 수를 표기해주면 작가들이 좋아할 것입니다. 자기의 작업량을 알 수
/// 있으니까요」 · <c>docs/work-orders/writer-comfort-orders.md</c> WC7). 대사 · 지문의 글만 — 화자 이름 · 빈 줄 · 다른 챕터는 세지 않는다.
/// </summary>
public sealed class ChapterTextCountTests
{
    [Fact]
    public void 대사와_지문의_글자를_공백_포함_제외로_세고_화자_이름은_세지_않는다()
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01");
        editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "ep01", "식당", 0, 0, sceneId: "식당");
        editor.AddEpisode("ch01", "ep02", "사막", 300, 0, sceneId: "사막");
        editor.EnsureChapter("ch02");
        editor.EnsureChapterBoard("ch02");
        editor.AddEpisode("ch02", "x", "다른 챕터", 0, 0, sceneId: "x");
        editor.AddSpeaker("샤통");

        Write(editor, "ch01", "ep01", ("샤통", "배고파."), ("샤통", "나초 먹자."), ("", "두 사람은 식당으로 들어간다."), ("", "   "));
        Write(editor, "ch01", "ep02", ("샤통", "🙂"));
        Write(editor, "ch02", "x", ("샤통", "세지 않는 글"));

        // 분기 카드의 글도 이 챕터의 것이다.
        DialogueNode first = EpisodeNaming.CardFor(editor.Project, "ch01", "ep01")!;
        DialogueNode branch = editor.AddBranchMarker(first.Id, first.Script.ActiveLines.First().Id);
        editor.SetScriptLineText(branch.Id, branch.Script.ActiveLines.First().Id, string.Empty, "곁가지");

        ChapterTextCount count = ChapterScripts.Count(editor.FindChapter("ch01")!.ToGraphModel("ch01"), editor.Project);

        // 배고파.(4) + 나초 먹자.(6 · 공백 빼면 5) + 두 사람은 식당으로 들어간다.(16 · 13) + 🙂(1) + 곁가지(3) — 공백뿐인 줄은 없는 줄이다.
        Assert.Equal(new ChapterTextCount(30, 26, 5), count);
    }

    /// <summary>그 에피소드 카드에 줄들을 쓴다 — 새 카드의 빈 줄 하나를 첫 줄로 채우고 나머지를 끝에 더한다.</summary>
    private static void Write(ProjectEditor editor, string chapterId, string episodeId, params (string Speaker, string Text)[] lines)
    {
        DialogueNode card = EpisodeNaming.CardFor(editor.Project, chapterId, episodeId)!;
        editor.SetScriptLineText(card.Id, card.Script.ActiveLines.First().Id, lines[0].Speaker, lines[0].Text);

        foreach ((string speaker, string text) in lines.Skip(1))
        {
            editor.SetScriptLineText(card.Id, editor.InsertScriptLine(card.Id).Id, speaker, text);
        }
    }
}
