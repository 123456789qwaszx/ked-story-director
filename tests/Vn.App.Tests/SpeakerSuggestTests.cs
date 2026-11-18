using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Tests.Fixtures;
using Path = System.IO.Path;

namespace Vn.App.Tests;

/// <summary>
/// <b>화자 칸의 자동완성 — 첫 글자 · 자음부터</b>(<c>docs/work-orders/writer-comfort-orders.md</c> WC3).
///
/// 한글은 조합 중인 글자가 칸의 글에 들어가지 않는다 — 그래서 둘째 글자를 쳐야 자동완성이 움직였다(⛔).
/// 칸 안의 글 표시기가 든 조합 중인 글(<see cref="TextPresenter.PreeditText"/>)을 직접 걸어, 사람이 「ㅅ」 한 자를 누른 때를 흉내 낸다.
/// </summary>
public sealed class SpeakerSuggestTests
{
    [Fact]
    public void 조합_중인_자음_하나로_후보가_열린다() => HeadlessUi.Run(() =>
    {
        (AutoCompleteBox speaker, TextPresenter presenter) = SpeakerBox();

        presenter.PreeditText = "ㅅ";
        Dispatcher.UIThread.RunJobs();

        List<string> shown = Items(speaker);
        Assert.Contains("샤통", shown);
        Assert.DoesNotContain("베니토", shown);
        Assert.True(speaker.IsDropDownOpen);
    });

    [Fact]
    public void 조합_중인_첫_글자_하나로_후보가_열린다() => HeadlessUi.Run(() =>
    {
        // 「베」까지 조합한 때 — 받침 없는 마지막 글자는 같은 초성 · 중성으로 시작하는 이름에 맞는다(「벤」을 칠 수도 있으니).
        (AutoCompleteBox speaker, TextPresenter presenter) = SpeakerBox();

        presenter.PreeditText = "베";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["베니토"], Items(speaker));
        Assert.True(speaker.IsDropDownOpen);
    });

    [Fact]
    public void 확정된_글에_조합_중인_글을_이어_거른다() => HeadlessUi.Run(() =>
    {
        // 「샤」를 확정하고 「ㅌ」을 조합 중 — 「샤ㅌ」으로 거른다.
        (AutoCompleteBox speaker, TextPresenter presenter) = SpeakerBox();

        speaker.Text = "샤";
        presenter.PreeditText = "ㅌ";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["샤통"], Items(speaker));
    });

    [Fact]
    public void 이름을_다_치면_목록을_닫는다() => HeadlessUi.Run(() =>
    {
        // 고른 뒤에 목록이 다시 떠 칸을 가리지 않는다.
        (AutoCompleteBox speaker, _) = SpeakerBox();

        speaker.Text = "샤통";
        Dispatcher.UIThread.RunJobs();

        Assert.False(speaker.IsDropDownOpen);
    });

    private static List<string> Items(AutoCompleteBox box) => box.ItemsSource!.Cast<string>().ToList();

    /// <summary>견본 챕터의 에피소드 카드를 인스펙터에 띄우고, 첫 줄의 화자 칸에 초점을 준다 — 칸은 비운다.</summary>
    private static (AutoCompleteBox Speaker, TextPresenter Presenter) SpeakerBox()
    {
        string directory = Path.Combine(Path.GetTempPath(), "vn-speaker-suggest", Guid.NewGuid().ToString("N"));
        var session = new AuthoringSession();
        session.Open(SampleProject.Corridor(directory, "화자 자동완성"));
        session.Editor.AddSpeaker("샤통");
        session.Editor.AddSpeaker("베니토");

        DialogueNode card = EpisodeNaming.CardFor(session.Project, SampleChapters.Corridor, "main05.02")!;
        var editor = new DialogueNodeEditor();
        var window = new Window { Width = 1200, Height = 800, Content = editor };
        window.Show();
        editor.Attach(session);
        editor.Show(card.Id);
        Dispatcher.UIThread.RunJobs();

        AutoCompleteBox speaker = editor.FindControl<StackPanel>("LineHost")!
            .GetVisualDescendants().OfType<AutoCompleteBox>().First();
        TextBox text = speaker.GetVisualDescendants().OfType<TextBox>().First();
        TextPresenter presenter = text.GetVisualDescendants().OfType<TextPresenter>().First();

        text.Focus();
        speaker.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();
        return (speaker, presenter);
    }
}
