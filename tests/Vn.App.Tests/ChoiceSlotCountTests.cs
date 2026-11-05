using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>카드 아래 선택지 칸은 셋이다 — 인스펙터의 「빈 선택지 숨기기」를 켜면 이은 선택지만</b> 선다. 다 이은 카드의
/// 안 이은 칸은 판만 어지럽힌다. 감추는 것은 안 이은 칸뿐이고(자동 길 자리도 이은 것이 없으면 감춘다), 이은 길은
/// 언제나 선다 — 이은 길을 숨기면 판이 거짓말을 한다.
/// 켠 카드는 사용자 설정에 프로젝트별로 기억한다(화면 편의라 프로젝트 파일이 아니다).
/// </summary>
public sealed class ChoiceSlotCountTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-choice-slot-count", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 기본은_칸이_셋이고_숨기면_이은_선택지만_서며_자동_칸도_이은_것이_없으면_감춘다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = Card(session, "root").Id;

        // 기본 — 아무것도 안 이었어도 칸이 셋이다(첫 칸이 자동 길 자리).
        Assert.Equal(3, SlotRows(graph, "root").Count);

        // 숨기면 — 이은 것이 없으니 자동 칸까지 아무것도 안 선다. 레일이 없으니 카드 아래 깊이도 없다.
        session.SetHideEmptyChoices(root, hide: true);
        graph.Rebuild();
        Assert.Empty(SlotRows(graph, "root"));

        // 둘을 이으면 — 이은 둘만 선다. 빈 칸은 안 붙는다.
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "옆");
        graph.Rebuild();
        Assert.Equal(["가자", "옆"], Labels(graph, "root"));

        // 다시 끄면 셋까지 빈 칸이 채워진다 — 이은 둘 + 빈 칸 하나.
        session.SetHideEmptyChoices(root, hide: false);
        graph.Rebuild();
        Assert.Equal(3, SlotRows(graph, "root").Count);
    });

    [Fact]
    public void 숨겨도_자동_길이_이어져_있으면_그_길은_선다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = Card(session, "root").Id;
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: null, auto: true);
        session.SetHideEmptyChoices(root, hide: true);
        graph.Rebuild();

        Assert.Single(SlotRows(graph, "root"));
    });

    [Fact]
    public void 인스펙터의_체크로_켜고_끄며_다음에_열어도_기억한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        session.Select(card.Id);
        Dispatcher.UIThread.RunJobs();

        CheckBox box = HideEmptyChoicesBox(window);
        Assert.True(box.IsVisible);
        Assert.False(box.IsChecked);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Assert.Equal(3, SlotRows(graph, "b").Count);

        // b는 아무 데도 안 이었다 — 켜면 칸이 하나도 안 선다.
        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(card.Id, session.HiddenChoiceNodeIds);
        Assert.Empty(SlotRows(graph, "b"));

        // 다음에 열어도 — 같은 프로젝트를 다시 열면 켠 카드가 그대로다.
        var again = new AuthoringSession();
        again.Open(session.ProjectPath!);
        Assert.Contains(card.Id, again.HiddenChoiceNodeIds);

        box.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(card.Id, session.HiddenChoiceNodeIds);
        Assert.Equal(3, SlotRows(graph, "b").Count);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>ch01: root · a · b — 이은 것 없음.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "선택지 칸" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");
        session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "root", "root", 0, 0);
        session.Editor.AddEpisode("ch01", "a", "a", 0, 500);
        session.Editor.AddEpisode("ch01", "b", "b", 400, 500);

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        return (graph, session);
    }

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    /// <summary>그 에피소드의 선택지 칸 줄들 — Tag가 「{에피소드}#{칸}」이다.</summary>
    private static List<StackPanel> SlotRows(GraphEditorView graph, string episodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<StackPanel>()
            .Where(row => row.Tag is string tag && tag.StartsWith(episodeId + "#", StringComparison.Ordinal))
            .ToList();

    /// <summary>그 에피소드의 선택지 칸에 선 문구들 — 위에서 아래 차례.</summary>
    private static List<string> Labels(GraphEditorView graph, string episodeId) =>
        SlotRows(graph, episodeId)
            .OrderBy(Canvas.GetTop)
            .Select(row => row.Children.OfType<TextBlock>().First().Text ?? string.Empty)
            .ToList();

    private static CheckBox HideEmptyChoicesBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single()
            .FindControl<CheckBox>("HideEmptyChoicesBox")!;
}
