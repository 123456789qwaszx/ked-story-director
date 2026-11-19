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

        // 되돌리기는 그 종류로 오되 노드는 싣지 않는다 — 되돌리면 객체가 모두 새것이라 화면이 스스로 정한다.
        _editor.Undo();
        _editor.Undo();
        Assert.Equal((ProjectChangeKind.DialogueLines, true, (string?)null), (_raised[^1].Kind, _raised[^1].FromHistory, _raised[^1].NodeId));
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

    private string Card(string episodeId) => EpisodeNaming.CardFor(_editor.Project, "ch01", episodeId)!.Id;
}
