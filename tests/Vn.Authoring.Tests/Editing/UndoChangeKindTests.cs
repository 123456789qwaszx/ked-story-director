using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Editing;

/// <summary>
/// <b>되돌리기 · 다시 실행은 그 편집의 종류로 알린다</b>(2026-11-18 소유자 — 「타자친 다음 ctrl+z, ctrl+y하는 것도 렉이 걸리던데」 ·
/// board-speed PF1). ⛔ 그 전에는 무엇을 되돌리든 구조 변경으로 알려 화면이 판을 통째로 다시 지었다.
///
/// 못 박는 것: ① 글만 고친 것을 되돌리고 다시 하면 글 변경으로 알린다 ② 묶음은 묶음의 종류로 ③ 구조 변경은 구조로 ④ 되돌리기에서 왔다는
/// 표시가 붙는다(객체가 모두 새것이다) — 보통 편집에는 없다.
/// </summary>
public sealed class UndoChangeKindTests
{
    private readonly ProjectEditor _editor = new(new StoryProject { Title = "되돌리기" });

    private readonly List<ProjectChangedEventArgs> _raised = [];

    public UndoChangeKindTests()
    {
        _editor.EnsureChapter("ch01");
        _editor.AddEpisode("ch01", "a", title: "a", 0, 0, sceneId: "opening");
        _editor.AddEpisode("ch01", "b", title: "b", 240, 0, sceneId: "opening");
        _editor.ApplyScenarioText(Card("a"), "첫 줄.", confirmDeletes: true);
        _editor.Changed += (_, args) => _raised.Add(args);
    }

    [Fact]
    public void 글만_고친_것은_되돌리기와_다시_실행도_글_변경이다()
    {
        _editor.ApplyScenarioText(Card("a"), "고친 첫 줄.");
        Assert.Equal((ProjectChangeKind.DialogueContent, false), Last());

        _editor.Undo();
        Assert.Equal((ProjectChangeKind.DialogueContent, true), Last());
        Assert.Contains("첫 줄.", Text("a"));

        _editor.Redo();
        Assert.Equal((ProjectChangeKind.DialogueContent, true), Last());
        Assert.Contains("고친 첫 줄.", Text("a"));
    }

    [Fact]
    public void 묶음과_구조_변경은_그_종류로_되돌린다()
    {
        // [구성] 자리 — 묶음 안에서 둘을 옮겨도 한 칸, 그 종류로.
        _editor.Batch(() =>
        {
            _editor.MoveEpisode("ch01", "a", 10, 10);
            _editor.MoveEpisode("ch01", "b", 20, 20);
        });
        _editor.Undo();
        Assert.Equal((ProjectChangeKind.ChapterLayout, true), Last());

        // 간선을 더한 것은 선택지의 길이다(usability UX19) — 되돌리기도 그 종류로 온다(화면은 되돌리기를 구조로 받는다).
        _editor.AddEdge("ch01", "a", "b", optionLabel: "간다");
        _editor.Undo();
        Assert.Equal((ProjectChangeKind.ChoiceRoute, true), Last());
        _editor.Redo();
        Assert.Equal((ProjectChangeKind.ChoiceRoute, true), Last());
        Assert.Single(_editor.FindChapter("ch01")!.Edges);

        // 에피소드를 더한 것은 구조다.
        _editor.AddEpisode("ch01", "c", "c", 600, 0, sceneId: "s");
        _editor.Undo();
        Assert.Equal((ProjectChangeKind.Structure, true), Last());
    }

    private (ProjectChangeKind Kind, bool FromHistory) Last() => (_raised[^1].Kind, _raised[^1].FromHistory);

    private string Card(string episodeId) => EpisodeNaming.CardFor(_editor.Project, "ch01", episodeId)!.Id;

    private string Text(string episodeId) =>
        Vn.Authoring.Rendering.WorkingDialoguePreview.ScenarioWriting(_editor.Project, Card(episodeId)).Text;
}
