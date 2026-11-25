using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
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
/// <b>화자 칸의 자동완성 — 첫 글자 · 자음부터</b>(writer-comfort WC3).
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

    [Fact]
    public void 후보를_누르면_그_이름이_칸과_대본에_들어간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「마우스로 원하는 화자를 클릭했음에도 정작 화자 이름이 완성되지 않고 힌트만 꺼진다」. ⛔ 고르기로 바뀐 칸의 글에도 후보를
        // 다시 걸러, 목록이 그 이름 하나로 좁혀 닫히며 고른 것이 사라졌다.
        Speaker it = SpeakerLine("샤를");

        Type(it, "샤");
        Assert.Equal(["샤통", "샤를"], Items(it.Box));
        Assert.True(it.Box.IsDropDownOpen);

        ListBoxItem item = DropDownItems(it.Box).Single(entry => (string?)entry.Content == "샤를");
        var root = TopLevel.GetTopLevel(item)!;
        Point at = item.TranslatePoint(new Point(6, 6), root)!.Value;
        root.MouseDown(at, MouseButton.Left);
        root.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("샤를", it.Box.Text);
        Assert.False(it.Box.IsDropDownOpen);
        Assert.Equal("샤를", it.SpeakerInScript());
    });

    [Fact]
    public void 화살표로_후보를_오가고_Enter로_넣는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「키보드 화살표로 원하는 화자를 고르고 엔터를 눌러 바로 화자에 넣는」. 오가는 동안은 칸에 미리 보일 뿐 대본에 넣지 않는다.
        Speaker it = SpeakerLine("샤를");

        Type(it, "샤");
        Press(it, Key.Down);
        Assert.Equal("샤통", it.Box.Text);
        Assert.True(it.Box.IsDropDownOpen);

        Press(it, Key.Down);
        Assert.Equal("샤를", it.Box.Text);
        Assert.True(it.Box.IsDropDownOpen);
        Assert.Equal("샤", it.SpeakerInScript());   // 아직 넣지 않았다

        Press(it, Key.Up);
        Assert.Equal("샤통", it.Box.Text);

        Press(it, Key.Enter);
        Assert.Equal("샤통", it.Box.Text);
        Assert.False(it.Box.IsDropDownOpen);
        Assert.Equal("샤통", it.SpeakerInScript());
    });

    [Fact]
    public void 화살표로_오가다_Esc면_치던_글로_돌아간다() => HeadlessUi.Run(() =>
    {
        Speaker it = SpeakerLine("샤를");

        Type(it, "샤");
        Press(it, Key.Down);
        Press(it, Key.Escape);

        Assert.Equal("샤", it.Box.Text);
        Assert.False(it.Box.IsDropDownOpen);
        Assert.Equal("샤", it.SpeakerInScript());
    });

    [Fact]
    public void 조합하던_중에_후보를_골라도_그_이름이_남고_입력기_연결은_그대로다() => HeadlessUi.Run(() =>
    {
        // 「ㅅ」을 조합하는 중에 후보를 고른다 — 칸이 그 이름을 쓰면 글자 겹이 조합 글자를 지운다. 목록을 다시 걸러 닫지 않고(고른 그대로), 입력기에는
        // 조합을 버리게 한다(다음 키에 「ㅅ」이 이름 뒤에 붙지 않게). 연결은 곧바로 다시 잇는다.
        Speaker it = SpeakerLine("샤를");

        it.Presenter.PreeditText = "ㅅ";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["샤통", "샤를"], Items(it.Box));

        it.Box.SelectedItem = "샤를";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("샤를", it.Box.Text);
        Assert.True(string.IsNullOrEmpty(it.Presenter.PreeditText));
        Assert.Equal(["샤통", "샤를"], Items(it.Box));
        Assert.True(InputMethod.GetIsInputMethodEnabled(it.Text));
    });

    private static List<string> Items(AutoCompleteBox box) => box.ItemsSource!.Cast<string>().ToList();

    private static List<ListBoxItem> DropDownItems(AutoCompleteBox box) =>
        ((box.GetVisualDescendants().OfType<Popup>().First().Child as Visual)?.GetVisualDescendants().OfType<ListBoxItem>() ?? []).ToList();

    /// <summary>사람이 친 글 — 칸 안의 글 칸에 글자를 넣는다(고르기로 바뀐 글과 달리 칸이 후보를 거른다).</summary>
    private static void Type(Speaker it, string typed)
    {
        it.Text.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = typed, Source = it.Text });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Speaker it, Key key)
    {
        it.Text.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = it.Text });
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>인스펙터의 첫 줄 화자 칸과 그 줄 — 대본에 든 화자를 읽는다.</summary>
    private sealed record Speaker(AutoCompleteBox Box, TextBox Text, TextPresenter Presenter, Func<string> SpeakerInScript);

    /// <summary><see cref="SpeakerBox"/>와 같되 화자를 더 등록하고 대본을 읽는 손잡이까지.</summary>
    private static Speaker SpeakerLine(params string[] more)
    {
        (AutoCompleteBox box, TextPresenter presenter, AuthoringSession session, DialogueNode card) = Open(more);
        string lineId = card.Script.ActiveLines.First().Id;
        TextBox text = box.GetVisualDescendants().OfType<TextBox>().First();
        return new Speaker(box, text, presenter, () => session.Project.FindDialogue(card.Id)!.Script.Text(lineId).Speaker);
    }

    /// <summary>견본 챕터의 에피소드 카드를 인스펙터에 띄우고, 첫 줄의 화자 칸에 초점을 준다 — 칸은 비운다.</summary>
    private static (AutoCompleteBox Speaker, TextPresenter Presenter) SpeakerBox()
    {
        (AutoCompleteBox speaker, TextPresenter presenter, _, _) = Open([]);
        return (speaker, presenter);
    }

    private static (AutoCompleteBox Speaker, TextPresenter Presenter, AuthoringSession Session, DialogueNode Card) Open(string[] more)
    {
        string directory = Path.Combine(Path.GetTempPath(), "vn-speaker-suggest", Guid.NewGuid().ToString("N"));
        var session = new AuthoringSession();
        session.Open(SampleProject.Corridor(directory, "화자 자동완성"));
        session.Editor.AddSpeaker("샤통");
        session.Editor.AddSpeaker("베니토");

        foreach (string name in more)
        {
            session.Editor.AddSpeaker(name);
        }

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
        return (speaker, presenter, session, card);
    }
}
