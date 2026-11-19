using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Editing;

/// <summary>
/// <b>카드 하나의 줄 변경은 그 카드를 이름 대어 알린다</b> (2026-11-19 · usability UX13) — 판이 그 카드만 다시 짓게. ⛔ 구조로 알려 판을 통째로
/// 다시 지었다(에피소드 160개에서 새 줄 하나에 2초).
/// </summary>
public sealed class DialogueLinesChangeTests
{
    private readonly ProjectEditor _editor = new(new StoryProject());
    private readonly List<ProjectChangedEventArgs> _raised = [];

    public DialogueLinesChangeTests()
    {
        _editor.EnsureChapter("ch01");
        _editor.AddEpisode("ch01", "a", "a", 0, 0, sceneId: "s");
        _editor.AddEpisode("ch01", "b", "b", 300, 0, sceneId: "s");
        _editor.ApplyScenarioText(Card("a"), "라루: 첫 줄.");
        _editor.ApplyScenarioText(Card("b"), "윌로: 첫 줄.");
        _editor.Changed += (_, args) => _raised.Add(args);
    }

    [Fact]
    public void 새_줄은_그_카드의_줄_변경이고_글자만_고치면_대사_내용이다()
    {
        _editor.ApplyScenarioText(Card("a"), "라루: 첫 줄.\n라루: 둘째 줄.");
        Assert.Equal((ProjectChangeKind.DialogueLines, Card("a")), (_raised[^1].Kind, _raised[^1].NodeId));

        _editor.ApplyScenarioText(Card("a"), "라루: 첫 줄!\n라루: 둘째 줄.");
        Assert.Equal(ProjectChangeKind.DialogueContent, _raised[^1].Kind);

        // 되돌리기도 그 종류 · 그 노드로 온다(usability UX21) — 받는 법은 화면이 정한다(줄 변경의 되돌리기는 구조로 받는다).
        _editor.Undo();
        _editor.Undo();
        Assert.Equal((ProjectChangeKind.DialogueLines, true, Card("a")), (_raised[^1].Kind, _raised[^1].FromHistory, _raised[^1].NodeId));
    }

    [Fact]
    public void 묶음_안에서_두_카드의_줄이_바뀌면_구조다()
    {
        _editor.Batch(() =>
        {
            _editor.ApplyScenarioText(Card("a"), "라루: 첫 줄.\n라루: 둘째.");
            _editor.ApplyScenarioText(Card("b"), "윌로: 첫 줄.\n윌로: 둘째.");
        });
        Assert.Equal((ProjectChangeKind.Structure, (string?)null), (_raised[^1].Kind, _raised[^1].NodeId));

        // 한 카드뿐이면 그 카드다.
        _editor.Batch(() =>
        {
            _editor.ApplyScenarioText(Card("a"), "라루: 첫 줄.\n라루: 둘째.\n라루: 셋째.");
            _editor.ApplyScenarioText(Card("a"), "라루: 첫 줄.\n라루: 둘째.\n라루: 셋째.\n라루: 넷째.");
        });
        Assert.Equal((ProjectChangeKind.DialogueLines, Card("a")), (_raised[^1].Kind, _raised[^1].NodeId));
    }

    [Fact]
    public void 제목만_바꾸면_그_카드의_제목_변경이고_장면을_바꾸면_구조다()
    {
        // usability UX15 — 판은 그 카드의 제목 글만 고친다. ⛔ 구조로 알려 판을 통째로 다시 지었다.
        _editor.UpdateEpisode("ch01", "a", title: "새 제목");
        Assert.Equal((ProjectChangeKind.CardTitle, Card("a")), (_raised[^1].Kind, _raised[^1].NodeId));

        _editor.UpdateEpisode("ch01", "a", sceneId: "t");
        Assert.Equal((ProjectChangeKind.Structure, (string?)null), (_raised[^1].Kind, _raised[^1].NodeId));

        // 분기 카드의 제목도 같다(⛔ NodeMetadata).
        DialogueNode a = _editor.Project.FindDialogue(Card("a"))!;
        DialogueNode branch = _editor.AddBranchMarker(a.Id, a.Script.ActiveLines.First().Id);
        _editor.SetBranchCardTitle(branch.Id, "들렀다 온다");
        Assert.Equal((ProjectChangeKind.CardTitle, branch.Id), (_raised[^1].Kind, _raised[^1].NodeId));
    }

    private string Card(string episodeId) => EpisodeNaming.CardFor(_editor.Project, "ch01", episodeId)!.Id;
}
