using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>쓰던 글 칸은 보던 자리에 있다</b> (usability UX39 · 2026-11-23 소유자 — 「에피소드 내에서 엔터를 눌러서 줄을 바꿀 시에, 맨 위로 이동이 되는
/// 버그」 · 「두번째 글자를 치는 순간 인스펙터에서 첫번째 글자만 반영이 되면서 위쪽으로 확 올라가네요」).
///
/// 쓰다 멈추면 글이 들어가고, 그 넣기로 대사 줄이 늘면 카드를 새로 지어 글 칸도 새 칸이 된다. 못 박는 것: ① 새 칸은 옛 칸의 스크롤 자리에서
/// 시작한다 — 그리기 전에(⛔ 맨 위에서 시작해 Avalonia가 미뤄 둔 커서 스크롤이 돌 때까지 맨 위를 비췄다) ② 크기를 정한 카드도 그 자리에 남는다 —
/// 카드를 지으며 「글에 맞춤」을 재느라 칸을 잠시 키워도(⛔ 깎인 스크롤이 맨 위에 남아 다음 글자를 칠 때까지 거기 있었다).
/// </summary>
public sealed class WritingScrollTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 쓰다_멈춰_줄이_늘어도_글_칸은_보던_자리에_있다(bool sized) => HeadlessUi.Run(() =>
    {
        var window = new MainWindow { Width = 1400, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AuthoringSession session = window.SessionProbe;
        session.Editor.AddSpeaker("하린");
        DialogueNode card = session.Editor.AddDialogueNode(TestChapter.Board(session).Id, "본문", name: "본문");
        Assert.True(session.Editor.ApplyScenarioText(
            card.Id, string.Join('\n', Enumerable.Range(1, 14).Select(i => $"하린: {i}번째 줄"))).Applied);
        session.Editor.MoveNode(card.Id, 300, 400);

        if (sized)
        {
            session.Editor.ResizeNode(card.Id, 260, 140);   // 사람이 키운 카드 — 지을 때 「글에 맞춤」을 잰다
        }

        session.Select(card.Id);
        window.FindControl<TabControl>("MainTabs")!.SelectedIndex = MainTab.Script;
        Dispatcher.UIThread.RunJobs();

        GraphEditorView graph = window.FindControl<GraphEditorView>("Graph")!;

        TextBox Box() => graph.FindControl<Canvas>("GraphCanvas")!.GetLogicalDescendants().OfType<TextBox>()
            .Single(box => box.Tag as string == GraphEditorView.WritingTag + card.Id);

        static double Scroll(TextBox box) => box.GetVisualDescendants().OfType<ScrollViewer>().First().Offset.Y;

        // 글 끝에서 Enter를 치고 새 줄에 한 글자를 쓴다 — 칸은 글 끝(아래)을 보고 있다.
        TextBox typing = Box();
        typing.Focus();
        typing.CaretIndex = typing.Text!.Length;
        Dispatcher.UIThread.RunJobs();
        typing.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = typing });
        typing.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "새", Source = typing });
        Dispatcher.UIThread.RunJobs();
        double seen = Scroll(typing);
        int caret = typing.CaretIndex;
        Assert.True(seen > 0, $"글이 칸을 넘쳐 아래를 보고 있어야 한다 — {seen}");

        // 멈췄다 — 글이 들어가 줄이 늘고 카드를 새로 짓는다.
        int rebuilds = graph.CardRebuildCount;
        graph.CommitLiveWriting();
        TextBox fresh = Box();
        Assert.NotSame(typing, fresh);
        Assert.Equal(rebuilds + 1, graph.CardRebuildCount);
        Assert.Equal(15, session.Project.FindDialogue(card.Id)!.Script.ActiveLines.Count());
        Assert.True(Math.Abs(Scroll(fresh) - seen) < 1, $"그리기 전 — 보던 자리 {seen}, 새 칸 {Scroll(fresh)}");

        // 미뤄 둔 일(카드 크기 재기 · 커서 스크롤)이 다 돌아도 그 자리다.
        Dispatcher.UIThread.RunJobs();
        Assert.Same(fresh, Box());
        Assert.True(fresh.IsFocused);
        Assert.Equal(caret, fresh.CaretIndex);
        Assert.True(Math.Abs(Scroll(fresh) - seen) < 1, $"다 돈 뒤 — 보던 자리 {seen}, 새 칸 {Scroll(fresh)}");

        window.Close();
    });
}
