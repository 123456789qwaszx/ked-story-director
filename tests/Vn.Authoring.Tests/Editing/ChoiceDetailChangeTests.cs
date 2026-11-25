using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Editing;

/// <summary>
/// <b>선택지의 내용 · 챕터 조건만 바뀐 변경은 그렇게 알린다</b> (2026-11-19 · usability UX16) — 판이 카드를 그대로 두고 그 출발의 철도 · 분기
/// 카드의 조건 표만 다시 그리게. ⛔ 구조로 알려 판을 통째로 다시 지었다(에피소드 160개에서 선택지 문구 하나 · 조건 하나에 2.5초).
/// </summary>
public sealed class ChoiceDetailChangeTests
{
    private readonly ProjectEditor _editor = new(new StoryProject());
    private readonly List<ProjectChangedEventArgs> _raised = [];

    public ChoiceDetailChangeTests()
    {
        _editor.EnsureChapter("ch01");
        _editor.AddEpisode("ch01", "a", "a", 0, 0, sceneId: "s");
        _editor.AddEpisode("ch01", "b", "b", 300, 0, sceneId: "s");
        _editor.AddEpisode("ch01", "c", "c", 600, 0, sceneId: "s");
        _editor.FindChapter("ch01")!.Stats.Add(new ChapterStat("trust", 0, 0, 10));
        _editor.AddEdge("ch01", "a", "b", optionLabel: "간다");
        _editor.AddEdge("ch01", "a", "c", optionLabel: "남는다");
        _editor.Changed += (_, args) => _raised.Add(args);
    }

    [Fact]
    public void 선택지의_문구_스탯변화_조건은_출발_카드를_이름_댄_선택지_내용이다()
    {
        _editor.UpdateEdge("ch01", "a", "b", matchOptionLabel: "간다", optionLabel: "달린다");
        Assert.Equal((ProjectChangeKind.ChoiceDetail, Card("a")), (_raised[^1].Kind, _raised[^1].NodeId));

        _editor.UpdateEdge("ch01", "a", "b", matchOptionLabel: "달린다", statChanges: "trust +2");
        Assert.Equal((ProjectChangeKind.ChoiceDetail, Card("a")), (_raised[^1].Kind, _raised[^1].NodeId));

        _editor.UpdateEdge("ch01", "a", "c", matchOptionLabel: "남는다", lockedMessage: "아직은 못 간다");
        Assert.Equal((ProjectChangeKind.ChoiceDetail, Card("a")), (_raised[^1].Kind, _raised[^1].NodeId));

        // 되돌리기도 그 종류 · 그 출발 카드로 온다(usability UX21) — 판은 그 출발의 선만 다시 긋는다.
        _editor.Undo();
        Assert.Equal((ProjectChangeKind.ChoiceDetail, true, Card("a")), (_raised[^1].Kind, _raised[^1].FromHistory, _raised[^1].NodeId));
        _editor.Redo();
        Assert.Equal((ProjectChangeKind.ChoiceDetail, true, Card("a")), (_raised[^1].Kind, _raised[^1].FromHistory, _raised[^1].NodeId));
    }

    [Fact]
    public void 선택지_사전에_문구만_더하면_판이_볼_것이_없는_내용이다()
    {
        _editor.AddChoiceLabel("ch01", "돌아본다");

        Assert.Equal(ProjectChangeKind.Content, _raised[^1].Kind);
        Assert.Contains(_editor.FindChapter("ch01")!.ChoiceOptions, option => option.Text == "돌아본다");
    }

    [Fact]
    public void 조건을_더하고_식을_고치면_한_칸의_챕터_조건_변경이다()
    {
        long revision = _editor.Revision;

        // 알림은 하나, 되돌리기도 한 칸이다. 판에는 아무것도 서지 않는다(⛔ 2026-11-25까지는 숨은 공급 노드를 지었다 · condition-supply C1).
        int nodes = _editor.Project.EnumerateNodes().Count();
        _editor.AddChapterCondition("ch01", "믿음", "trust >= 3");
        Assert.Equal(revision + 1, _editor.Revision);
        Assert.Equal((ProjectChangeKind.ChapterCondition, (string?)null), (_raised[^1].Kind, _raised[^1].NodeId));
        Assert.Equal(nodes, _editor.Project.EnumerateNodes().Count());

        _editor.UpdateChapterCondition("ch01", "믿음", "trust >= 5");
        Assert.Equal(ProjectChangeKind.ChapterCondition, _raised[^1].Kind);

        _editor.Undo();
        _editor.Undo();
        Assert.Empty(_editor.FindChapter("ch01")!.Conditions);
    }

    [Fact]
    public void 바깥_묶음_안에서는_바깥_묶음이_종류를_정한다()
    {
        _editor.Batch(() =>
        {
            _editor.AddChapterCondition("ch01", "믿음", "trust >= 3");
            _editor.AddEpisode("ch01", "d", "d", 900, 0, sceneId: "s");
        });

        Assert.Equal(ProjectChangeKind.Structure, _raised[^1].Kind);
    }

    private string Card(string episodeId) => EpisodeNaming.CardFor(_editor.Project, "ch01", episodeId)!.Id;
}
