using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Path = System.IO.Path;
using ShapePath = Avalonia.Controls.Shapes.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// <b>카드 아래 선택지 칸을 접었다 편다</b> (2026-10-30 CF — 소유자: *"노드 아래 선택지 3개가 항상 떠있는데, 이걸 접었다 펼 수
/// 있도록 해줘"*).
///
/// 접으면 칸 셋(문구 · 빈 칸 · 끌 점)이 칩 하나(「▸ 선택지 N」)가 되고, <b>이은 길은 그 칩에서 나가 그대로 보인다</b>. 카드마다는
/// 줄기 위 「▾」와 그 칩으로, 모두는 도구줄의 「선택지 접기 / 펴기」로.
/// </summary>
public sealed class ChoiceFoldTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-choice-fold", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 카드의_칸을_접으면_칩_하나가_서고_이은_길은_남으며_칩을_누르면_다시_펴진다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = Card(session, "root").Id;

        // 처음에는 칸 셋(이은 「가자」 하나 + 빈 칸 둘)이 다 선다.
        Assert.Equal(3, SlotRows(graph, "root").Count);

        Press(Tagged(graph, GraphEditorView.ChoiceFoldTag + root));

        Assert.True(graph.ChoicesFolded(root));
        Assert.Empty(SlotRows(graph, "root"));
        Border chip = Tagged(graph, GraphEditorView.ChoiceUnfoldTag + root);
        Assert.Equal("▸ 선택지 1", ((TextBlock)chip.Child!).Text);
        Assert.NotNull(Lane(graph, "root", "a"));   // 이은 길은 그대로 보인다

        // 다른 카드는 그대로다.
        Assert.Equal(3, SlotRows(graph, "a").Count);

        Press(chip);

        Assert.False(graph.ChoicesFolded(root));
        Assert.Equal(3, SlotRows(graph, "root").Count);
    });

    [Fact]
    public void 도구줄의_선택지_접기는_모든_카드를_접고_다시_누르면_모두_편다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        Button all = graph.FindControl<Button>("FoldChoicesButton")!;

        all.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(graph.ChoicesFolded(Card(session, "root").Id));
        Assert.True(graph.ChoicesFolded(Card(session, "a").Id));
        Assert.Empty(SlotRows(graph, "root"));
        Assert.Empty(SlotRows(graph, "a"));
        Assert.Equal("선택지 펴기", all.Content);

        // 다시 그려도(카드를 고치는 등) 접힌 채다 — 화면 상태가 판을 다시 세울 때 사라지면 안 된다.
        graph.Rebuild();
        Assert.Empty(SlotRows(graph, "root"));

        all.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.False(graph.ChoicesFolded(Card(session, "root").Id));
        Assert.Equal(3, SlotRows(graph, "root").Count);
        Assert.Equal("선택지 접기", all.Content);
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>ch01: root → a(「가자」). 위에서 아래로.</summary>
    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        var session = new AuthoringSession();
        Directory.CreateDirectory(_directory);
        string manifest = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
        ProjectStore.Save(manifest, new StoryProject { Title = "선택지 접기" });
        session.Open(manifest);

        session.Editor.EnsureChapter("ch01");
        session.EnsureChapterBoard("ch01");
        session.Editor.AddEpisode("ch01", "root", "root", 0, 0);
        session.Editor.AddEpisode("ch01", "a", "a", 0, 500);
        session.Editor.AddEdge("ch01", "root", "a", optionLabel: "가자");

        var graph = new GraphEditorView();
        var window = new Window { Width = 1400, Height = 900, Content = graph };
        window.Show();
        graph.Attach(session);
        graph.Rebuild();
        Dispatcher.UIThread.RunJobs();

        return (graph, session);
    }

    private static Canvas Board(GraphEditorView graph) => graph.FindControl<Canvas>("GraphCanvas")!;

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    /// <summary>그 에피소드의 선택지 칸 줄들 — Tag가 「{에피소드}#{칸}」이다.</summary>
    private static List<StackPanel> SlotRows(GraphEditorView graph, string episodeId) =>
        Board(graph).Children.OfType<StackPanel>()
            .Where(row => row.Tag is string tag && tag.StartsWith(episodeId + "#", StringComparison.Ordinal))
            .ToList();

    private static Border Tagged(GraphEditorView graph, string tag) =>
        Board(graph).Children.OfType<Border>().Single(border => border.Tag as string == tag);

    private static ShapePath? Lane(GraphEditorView graph, string from, string to) =>
        Board(graph).Children.OfType<ShapePath>().FirstOrDefault(path => path.Tag as string == $"레인:{from}>{to}");

    private static void Press(Control target)
    {
        target.RaiseEvent(new PointerPressedEventArgs(
            target, new Pointer(0, PointerType.Mouse, true), target, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();
    }
}
