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
/// <b>카드 아래 빈 선택지 칸은 하나다 — 인스펙터의 「선택지 3칸」을 켜면 셋</b> (2026-10-30 소유자: *"선택지들이 연결되어 있지
/// 않더라도 그게 보이다보니 너무 지저분하네. … 에피소드 이름 옆쪽에 … 체크박스를 둔 뒤에, 끄면 1개만 보이고, 키면 현재처럼 3개 모두
/// 보이도록"*).
///
/// ⚠ <b>이은 선택지는 어느 쪽이든 언제나 선다</b> — 줄이는 것은 빈 칸뿐이다. 이은 길을 숨기면 판이 거짓말을 한다.
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
    public void 끈_카드는_빈_칸이_하나고_켜면_셋이며_이은_선택지는_언제나_선다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = Card(session, "root").Id;

        // 아무것도 안 이었다 — 빈 칸 하나(자동 길 자리).
        Assert.Single(SlotRows(graph, "root"));

        session.SetFullChoices(root, full: true);
        graph.Rebuild();
        Assert.Equal(3, SlotRows(graph, "root").Count);

        // 끄고 둘을 이으면 — 이은 둘이 다 서고 빈 칸은 붙지 않는다(1은 빈 칸을 채우는 수이지 상한이 아니다).
        session.SetFullChoices(root, full: false);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");
        session.Editor.AddEdge("ch01", "root", "b", optionLabel: "옆");
        graph.Rebuild();

        Assert.Equal(2, SlotRows(graph, "root").Count);

        // 다시 켜면 셋까지 빈 칸이 채워진다 — 이은 둘 + 빈 칸 하나.
        session.SetFullChoices(root, full: true);
        graph.Rebuild();
        Assert.Equal(3, SlotRows(graph, "root").Count);
    });

    [Fact]
    public void 인스펙터의_체크로_켜고_끄며_다음에_열어도_기억한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch01", "b")!;
        session.Select(card.Id);
        Dispatcher.UIThread.RunJobs();

        CheckBox box = FullChoicesBox(window);
        Assert.True(box.IsVisible);
        Assert.False(box.IsChecked);
        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;
        Assert.Single(SlotRows(graph, "b"));

        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(card.Id, session.FullChoiceNodeIds);
        Assert.Equal(3, SlotRows(graph, "b").Count);

        // 다음에 열어도 — 같은 프로젝트를 다시 열면 켠 카드가 그대로다.
        var again = new AuthoringSession();
        again.Open(session.ProjectPath!);
        Assert.Contains(card.Id, again.FullChoiceNodeIds);

        box.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(card.Id, session.FullChoiceNodeIds);
        Assert.Single(SlotRows(graph, "b"));

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

    private static CheckBox FullChoicesBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single()
            .FindControl<CheckBox>("FullChoicesBox")!;
}
