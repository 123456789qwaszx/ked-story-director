using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>연출 그래프 인스펙터의 이름 칸으로 에피소드 이름을 바꾼다</b> (2026-10-30 소유자: *"연출 그래프에서 에피소드의 이름을 바꾸려는데
/// 안바뀌는 버그 고쳐줘"*).
///
/// 칸은 고칠 수 있게 보였는데 에피소드 카드면 읽기 전용으로 잠겨 있었다(옛 규칙 "개명은 챕터 그래프에서만"). 이제 하이어아키와 같은
/// 한 길(<see cref="EpisodeRenamer"/> — 에피소드 · 간선 · 카드를 함께, 되돌리기 한 칸)로 바꾼다.
/// </summary>
public sealed class InspectorEpisodeRenameTests : IDisposable
{
    private const int GraphTab = 1;

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-inspector-rename", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 이름_칸에_새_이름을_적고_Enter를_누르면_에피소드가_개명되고_챕터_그래프도_따라간다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode card = Card(session, "b");
        session.Select(card.Id);
        Dispatcher.UIThread.RunJobs();

        TextBox name = NameBox(window);
        Assert.False(name.IsReadOnly);

        name.Text = "lobby";
        Enter(name);

        Assert.Contains(session.Editor.FindChapter("ch01")!.Episodes, episode => episode.EpisodeId == "lobby");
        Assert.Equal(("lobby", "lobby"), (card.Name, card.MarkedEpisodeId));
        Assert.Equal(card.Id, session.SelectedNodeId);   // 같은 카드다 — 신원표만 바뀌었다
        Assert.Equal("lobby", NameBox(window).Text);
        Assert.Equal("lobby", window.FindControl<ChapterGraphView>("ChapterGraph")!.FindControl<TextBox>("IdBox")!.Text);
        Assert.Contains("개명했습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    [Fact]
    public void 같은_이름이_이미_있으면_칸이_원래_이름으로_돌아오고_사유를_말한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, GraphTab);
        DialogueNode card = Card(session, "b");
        session.Select(card.Id);
        Dispatcher.UIThread.RunJobs();

        TextBox name = NameBox(window);
        name.Text = "root";
        Enter(name);

        Assert.Contains(session.Editor.FindChapter("ch01")!.Episodes, episode => episode.EpisodeId == "b");
        Assert.Equal("b", card.Name);
        Assert.Equal("b", NameBox(window).Text);
        Assert.Contains("이미 있습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static DialogueNode Card(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!;

    private static TextBox NameBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single().FindControl<TextBox>("NameBox")!;

    private static void Enter(TextBox box)
    {
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();
    }
}
