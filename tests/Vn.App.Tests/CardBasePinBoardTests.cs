using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// <b>[대본] 카드 기본 크기 · 글 맞춤이 지키는 크기 · 기본 크기로 고정</b>(2026-11-25 소유자 — 「새 에피소드를 처음 만들었을 때, 줄일 수있는 최소 한도크기와는 별개로,
/// 생성 시에 세팅해주는 기본크기는 별개여야 한다는 거야. 또 가로 세로 크기 맞춤은 이 기본 크기는 지켜주도록 세팅이 가능해야하며, 또 노드의 머리부분 중,
/// 제목영역이 아닌 우측 빈 공간을 더블 클릭 했을 시에, 이런 가로 세로 크기 맞춤을 무시 + 기본크기로 돌려보내는 일종의 최소화 모드 / 반대로 그 상태에서
/// 다시 더블 클릭하면 가로 세로 크기맞춤의 영향을 받도록 하는 기능이 필요하고, 이것은 가로 세로 맞춤버튼 왼쪽에 별개로 같이 존재해야」). 고른 것:
/// 새 카드 320 × 160 · 설정 창에서 바꾼다 · 가장 작은 크기는 210 × 88 그대로 · 「글 맞춤이 이 크기보다 작게 줄이지 않는다」는 설정 창의 체크 하나(처음 켜짐).
/// </summary>
public sealed class CardBasePinBoardTests : IDisposable
{
    /// <summary>기본 폭에서는 몇 번 꺾이는 대사 — 서른 글자 남짓.</summary>
    private const string Long = "라루: 오늘따라 바람이 유난히 차가워서 겉옷을 챙겨 오지 않은 것을 몹시 후회했다.";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-card-basePin", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 새_카드는_설정의_크기로_서고_손잡이를_두_번_누르면_그_크기로_돌아간다() => HeadlessUi.Run(() =>
    {
        Assert.Equal((320d, 160d), (CardDefaults.DefaultWidth, CardDefaults.DefaultWritingHeight));
        Assert.Equal((210d, 88d), (CardDefaults.MinWidth, CardDefaults.MinWritingHeight));
        (GraphEditorView graph, AuthoringSession session) = Show();

        session.Editor.AddEpisode("ch01", "fresh", title: "fresh", 0, 600, sceneId: "opening");
        Settle(graph);
        string fresh = CardId(session, "fresh");
        NodeLayout made = session.Project.FindNode(fresh)!.Layout;
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (made.Width!.Value, made.WritingHeight!.Value));
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (Card(graph, fresh).Width, WritingBox(graph, fresh).Height));

        // 가장 작은 크기(210 × 88)로 선 카드를 넓혔다가 손잡이를 두 번 — 새 카드 크기로 돌아간다(⛔ 그 전에는 가장 작은 크기였다).
        string root = CardId(session, "root");
        session.Editor.ResizeNode(root, 600, 300);
        Settle(graph);
        Press(Grip(graph, root, GraphEditorView.ResizeCornerGripTag), clickCount: 2);
        Settle(graph);

        NodeLayout reset = session.Project.FindNode(root)!.Layout;
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (reset.Width!.Value, reset.WritingHeight!.Value));
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));
    });

    [Fact]
    public void 글_맞춤은_새_카드_크기보다_작게_줄이지_않고_설정을_끄면_가장_작은_크기까지_줄인다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");   // 가장 작은 크기로 선 카드 · 세로 글 맞춤이 켜져 있다
        ClickFit(graph, root, width: true);
        Type(graph, root, "윌로: 응.");

        NodeLayout kept = session.Project.FindNode(root)!.Layout;
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (kept.Width!.Value, kept.WritingHeight!.Value));
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));

        // 끄면 가장 작은 크기까지 줄인다.
        using (CardDefaults.Pin(CardDefaults.Width, CardDefaults.WritingHeight, fitKeepsSize: false))
        {
            Type(graph, root, "윌로: 응!");
            NodeLayout shrunk = session.Project.FindNode(root)!.Layout;
            Assert.Null(shrunk.Width);
            Assert.Null(shrunk.WritingHeight);
            Assert.Equal((CardDefaults.MinWidth, CardDefaults.MinWritingHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));
        }
    });

    [Fact]
    public void 머리의_빈_곳을_두_번_누르면_기본_크기로_고정하고_맞춤은_쉬며_다시_두_번_누르면_돌아와_맞춘다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        ClickFit(graph, root, width: true);
        string[] lines = [Long, .. Enumerable.Range(1, 9).Select(index => $"윌로: {index}번째 대답이다.")];
        Type(graph, root, string.Join('\n', lines));
        NodeLayout grown = session.Project.FindNode(root)!.Layout;
        (double Width, double Height) wide = (grown.Width!.Value, grown.WritingHeight!.Value);
        Assert.True(wide.Width > CardDefaults.Width && wide.Height > CardDefaults.WritingHeight);

        // 제목 글자 오른쪽의 빈 곳을 두 번 — 제목 칸은 서지 않고 기본 크기로 고정한다.
        PressHeaderBlank(graph, root);
        Assert.False(graph.IsRenamingCard);
        NodeLayout folded = session.Project.FindNode(root)!.Layout;
        Assert.True(folded.PinnedToBase);
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (folded.Width!.Value, folded.WritingHeight!.Value));
        Assert.Equal(wide, (folded.UnpinnedWidth!.Value, folded.UnpinnedWritingHeight!.Value));
        Assert.True(folded.FitWidth && folded.FitHeight);   // 맞춤은 켜진 채 쉰다
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));
        Assert.Contains(GraphEditorView.FitOnClass, BasePinButton(graph, root).Classes);
        Assert.False(Fit(graph, root, width: true).IsEnabled);
        Assert.False(Fit(graph, root, width: false).IsEnabled);

        // 기본 크기로 고정한 동안 글을 더 써도 크기는 그대로다.
        Type(graph, root, string.Join('\n', lines) + "\n" + Long + " 그리고 더 길게 이어진다.");
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight),
            (session.Project.FindNode(root)!.Layout.Width!.Value, session.Project.FindNode(root)!.Layout.WritingHeight!.Value));

        // 다시 두 번 — 펴고, 켜 둔 맞춤이 지금 글에 맞춘다(더 긴 줄 · 더 많은 줄).
        PressHeaderBlank(graph, root);
        NodeLayout opened = session.Project.FindNode(root)!.Layout;
        Assert.False(opened.PinnedToBase);
        Assert.Null(opened.UnpinnedWidth);
        Assert.True(opened.Width > wide.Width);
        Assert.True(opened.WritingHeight > wide.Height);
        Assert.Equal((opened.Width!.Value, opened.WritingHeight!.Value), (Card(graph, root).Width, WritingBox(graph, root).Height));
        Assert.DoesNotContain(GraphEditorView.FitOnClass, BasePinButton(graph, root).Classes);
        Assert.True(Fit(graph, root, width: true).IsEnabled);

        // 풀기와 맞춘 크기가 한 칸이다 — 한 번 되돌리면 다시 기본 크기로 고정한 모습이다.
        session.Editor.Undo();
        Settle(graph);
        Assert.True(session.Project.FindNode(root)!.Layout.PinnedToBase);
        Assert.Equal((CardDefaults.Width, CardDefaults.WritingHeight), (Card(graph, root).Width, WritingBox(graph, root).Height));
    });

    [Fact]
    public void 머리의_빈_곳은_같은_카드의_빈_곳을_잇달아_두_번_눌렀다_떼야만_오가고_한_번_다시_눌러_끌기_세_번째에는_오가지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「더블클릭이 아니라, 그저 클릭 한번만 했는데도 좁아지거나 펴지는 경우가 있습니다」. 운영체제의 두 번 누름 셈은 무엇을 눌렀든
        // 같은 자리 · 짧은 틈이면 2가 된다 — 카드를 눌러 고르고 곧바로 다시 눌러 끌려던 것도 2였다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        bool Pinned() => session.Project.FindNode(root)!.Layout.PinnedToBase;

        // 운영체제가 2로 센 누름이라도 바로 앞의 누름이 이 카드의 머리 빈 곳이 아니었으면(맞춤 단추 · 다른 데) 오가지 않는다.
        PressHeaderBlankOnce(graph, root, clicks: 2);
        Assert.False(Pinned());

        // 한 번 — 오가지 않는다.
        PressHeaderBlankOnce(graph, root, clicks: 1);
        Assert.False(Pinned());

        // 곧바로 다시 눌러 끌었다 — 끌기다. 오가지 않고 카드가 옮겨진다.
        double left = Canvas.GetLeft(Card(graph, root));
        PressHeaderBlankOnce(graph, root, clicks: 2, dragX: 80, dragY: 40);
        Assert.False(Pinned());
        Assert.True(Canvas.GetLeft(Card(graph, root)) > left + 40);

        // 세 번 — 두 번째에 한 번만 오간다(접혔다가 곧바로 펴지지 않는다).
        PressHeaderBlankOnce(graph, root, clicks: 1);
        PressHeaderBlankOnce(graph, root, clicks: 2);
        Assert.True(Pinned());
        PressHeaderBlankOnce(graph, root, clicks: 3);
        Assert.True(Pinned());
    });

    [Fact]
    public void 쉬지_않고_잇달아_누르면_두_번마다_한_번_오간다() => HeadlessUi.Run(() =>
    {
        // 2026-11-27 소유자 — 「에피소드 노드의 제목을 더블클릭 해서 한번 상태를 바꾼 다음, 멈추지 않고 계속 클릭을 하면 그 뒤로는 더블클릭해도 아무런 반응이
        // 없습니다」. 운영체제의 셈은 쉬지 않고 누르는 동안 3 · 4 · 5 …로 는다 — 짝수째가 두 번 누름이다(Clicks). ⛔ 그 전에는 「꼭 1 · 꼭 2」로 세어 셋째부터
        // 아무 반응이 없었다.
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        bool Pinned() => session.Project.FindNode(root)!.Layout.PinnedToBase;

        var states = new List<bool>();

        for (int clicks = 1; clicks <= 6; clicks++)
        {
            PressHeaderBlankOnce(graph, root, clicks);
            states.Add(Pinned());
        }

        Assert.Equal([false, true, true, false, false, true], states);
    });

    [Fact]
    public void 맞춤_단추_왼쪽의_기본_크기_고정_단추도_같고_손잡이로_크기를_정하면_풀린다() => HeadlessUi.Run(() =>
    {
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");
        session.Editor.ResizeNode(root, 500, 260, fitHeight: false);   // 맞춤이 모두 꺼진 카드
        Settle(graph);

        // 단추 셋이 「기본 크기 고정 · 가로 · 세로」 차례로 선다. 기본 크기 고정 단추도 초점을 가져가지 않는다.
        Button basePin = BasePinButton(graph, root);
        List<Button> row = Card(graph, root).GetVisualDescendants().OfType<Button>()
            .Where(button => button.Tag is string tag && tag.EndsWith(root, StringComparison.Ordinal))
            .OrderBy(button => button.TranslatePoint(default, Card(graph, root))!.Value.X)
            .ToList();
        Assert.Equal([basePin, Fit(graph, root, width: true), Fit(graph, root, width: false)], row);
        Assert.False(basePin.Focusable);
        Assert.Contains("기본 크기로 고정", ToolTip.GetTip(basePin) as string, StringComparison.Ordinal);

        basePin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(graph);
        Assert.True(session.Project.FindNode(root)!.Layout.PinnedToBase);
        Assert.Equal(CardDefaults.Width, Card(graph, root).Width);

        BasePinButton(graph, root).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(graph);
        NodeLayout opened = session.Project.FindNode(root)!.Layout;
        Assert.False(opened.PinnedToBase);
        Assert.Equal((500d, 260d), (opened.Width!.Value, opened.WritingHeight!.Value));   // 맞춤이 꺼진 쪽은 고정하기 전 크기

        // 기본 크기로 고정한 채 손잡이로 넓히면 기본 크기 고정이 풀린다 — 손으로 정한 크기다.
        BasePinButton(graph, root).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(graph);
        session.Editor.ResizeNode(root, 700, CardDefaults.WritingHeight, fitWidth: false);
        Settle(graph);
        Assert.False(session.Project.FindNode(root)!.Layout.PinnedToBase);
        Assert.Equal(700, Card(graph, root).Width);
    });

    [Fact]
    public void 가로만_고정하는_설정이면_폭만_기본_폭이_되고_세로_글_맞춤은_쓰는_대로_따라온다() => HeadlessUi.Run(() =>
    {
        // 2026-11-26 소유자 — 「가로만 활성으로 … 가로영역은 기본크기로 두되, 세로는 세로글 맞춤을 둘 수 있도록」.
        using IDisposable widthOnly = CardDefaults.PinWidthOnlyForTest(true);
        (GraphEditorView graph, AuthoringSession session) = Show();
        string root = CardId(session, "root");   // 세로 글 맞춤이 켜져 있다
        ClickFit(graph, root, width: true);
        Type(graph, root, Long + "\n윌로: 응.");
        double wide = session.Project.FindNode(root)!.Layout.Width!.Value;
        Assert.True(wide > CardDefaults.Width);
        Assert.Contains("가로만", ToolTip.GetTip(BasePinButton(graph, root)) as string, StringComparison.Ordinal);

        PressHeaderBlank(graph, root);
        NodeLayout pinned = session.Project.FindNode(root)!.Layout;
        Assert.True(pinned.PinnedToBase);
        Assert.True(pinned.PinnedWidthOnly);
        Assert.Equal(CardDefaults.Width, pinned.Width);
        Assert.Equal(CardDefaults.Width, Card(graph, root).Width);
        Assert.False(Fit(graph, root, width: true).IsEnabled);    // 가로 글 맞춤만 쉰다
        Assert.True(Fit(graph, root, width: false).IsEnabled);
        AssertAllVisible(graph, root);                             // 좁아진 폭에서 꺾인 줄까지 세로가 다시 맞췄다
        Assert.Contains("기본 폭", session.StatusMessage, StringComparison.Ordinal);

        // 더 쓰면 — 폭은 그대로, 글 칸 높이는 따라온다(넣기 전에도).
        double height = WritingBox(graph, root).Height;
        TextBox box = WritingBox(graph, root);
        box.Focus();
        box.Text = string.Join('\n', [Long, .. Enumerable.Range(1, 9).Select(index => $"윌로: {index}번째 대답이다.")]);
        Settle(graph);
        Assert.Equal(CardDefaults.Width, Card(graph, root).Width);
        Assert.True(box.Height > height);
        graph.CommitLiveWriting();
        Settle(graph);
        Assert.Equal(CardDefaults.Width, session.Project.FindNode(root)!.Layout.Width);
        Assert.Equal(box.Height, session.Project.FindNode(root)!.Layout.WritingHeight);

        // 풀면 폭이 돌아가고 켜 둔 가로 맞춤이 다시 맞춘다.
        PressHeaderBlank(graph, root);
        Assert.False(session.Project.FindNode(root)!.Layout.PinnedToBase);
        Assert.True(Card(graph, root).Width > CardDefaults.Width);
        Assert.True(Fit(graph, root, width: true).IsEnabled);
    });

    [Fact]
    public void 설정_창의_가로만_고정은_처음_꺼져_있고_누르는_즉시_기억한다() => HeadlessUi.Run(() =>
    {
        bool before = CardDefaults.PinsWidthOnly;

        try
        {
            CardDefaults.SetPinsWidthOnly(false);
            (MainWindow window, _) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("기본 크기로 고정할 때 가로만 고정한다", settings.PinWidthOnlyCheck.Content);
            Assert.False(settings.PinWidthOnlyCheck.IsChecked);

            settings.PinWidthOnlyCheck.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(CardDefaults.PinsWidthOnly);
            Assert.True(AppSettingsService.LoadPinWidthOnly());

            // 처음 값으로 — 가로 · 세로 모두 고정으로 돌아간다.
            settings.BaseSizeDefaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(CardDefaults.PinsWidthOnly);
            Assert.False(settings.PinWidthOnlyCheck.IsChecked);

            settings.Close();
            window.Close();
        }
        finally
        {
            CardDefaults.SetPinsWidthOnly(before);
        }
    });

    [Fact]
    public void 설정_창에서_새_카드_크기와_지키기를_바꾸고_기본으로_돌린다() => HeadlessUi.Run(() =>
    {
        try
        {
            CardDefaults.Reset();
            (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
            SettingsWindow settings = window.OpenSettings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((320m, 160m, true), (settings.BaseWidthBox.Value!.Value, settings.BaseHeightBox.Value!.Value, settings.FitKeepsSizeCheck.IsChecked == true));
            Assert.Equal((210m, 88m), (settings.BaseWidthBox.Minimum, settings.BaseHeightBox.Minimum));

            settings.BaseWidthBox.Value = 400;
            settings.BaseHeightBox.Value = 50;   // 가장 작은 높이로 당겨진다
            settings.FitKeepsSizeCheck.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((400d, 88d, false), (CardDefaults.Width, CardDefaults.WritingHeight, CardDefaults.FitKeepsSize));
            Assert.Equal((400d, 88d, false), AppSettingsService.LoadNewCardSize());
            Assert.False(session.Editor.CanUndo);   // 프로젝트는 바뀌지 않는다(켜진 맞춤이 없는 카드뿐이면)

            // 새 카드는 그 크기로 선다 — 높이는 가장 작은 높이라 적지 않는다.
            session.Editor.AddEpisode("ch01", "z", title: "z", 0, 600, sceneId: "opening");
            Dispatcher.UIThread.RunJobs();
            NodeLayout made = session.Project.FindNode(CardId(session, "z"))!.Layout;
            Assert.Equal(400, made.Width);
            Assert.Null(made.WritingHeight);

            settings.BaseSizeDefaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((320d, 160d, true), AppSettingsService.LoadNewCardSize());
            Assert.Equal(320m, settings.BaseWidthBox.Value);

            settings.Close();
            window.Close();
        }
        finally
        {
            CardDefaults.Reset();   // 설정은 이 시험 프로세스가 함께 쓴다
        }
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    private (GraphEditorView Graph, AuthoringSession Session) Show()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: MainTab.Script);
        return (window.FindControl<GraphEditorView>("Graph")!, session);
    }

    private static string CardId(AuthoringSession session, string episodeId) =>
        EpisodeNaming.CardFor(session.Project, "ch01", episodeId)!.Id;

    /// <summary>글 칸에 쓰고 나온다 — 넣지 않은 글(지운 줄도)이 들어가고 켠 쪽을 맞춘다.</summary>
    private static void Type(GraphEditorView graph, string nodeId, string text)
    {
        TextBox box = WritingBox(graph, nodeId);
        box.Focus();
        box.Text = text;
        Dispatcher.UIThread.RunJobs();
        graph.CommitLiveWriting();
        Settle(graph);
        graph.FindControl<Border>("WritingFocusSink")!.Focus();
        Settle(graph);
    }

    private static void Settle(GraphEditorView graph)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            graph.UpdateLayout();
        }
    }

    private static void ClickFit(GraphEditorView graph, string nodeId, bool width)
    {
        Fit(graph, nodeId, width).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(graph);
    }

    /// <summary>제목 글자 오른쪽의 빈 곳(제목 줄 안)을 두 번 눌렀다 뗀다 — 사람의 두 번 누름 그대로(누르고 떼고, 누르고 떼고).</summary>
    private static void PressHeaderBlank(GraphEditorView graph, string nodeId)
    {
        PressHeaderBlankOnce(graph, nodeId, clicks: 1);
        PressHeaderBlankOnce(graph, nodeId, clicks: 2);
    }

    /// <summary>
    /// 제목 글자 오른쪽의 빈 곳을 한 번 눌렀다 뗀다 — <paramref name="clicks"/>는 운영체제가 센 누름 수(ClickCount)다. <paramref name="dragX"/> ·
    /// <paramref name="dragY"/>만큼 누른 채 끌었다가 뗄 수 있다.
    /// </summary>
    private static void PressHeaderBlankOnce(GraphEditorView graph, string nodeId, int clicks, double dragX = 0, double dragY = 0)
    {
        TextBlock title = Card(graph, nodeId).GetVisualDescendants().OfType<TextBlock>()
            .First(block => block.Tag as string == GraphEditorView.CardTitleTag);
        var at = new Point(title.Bounds.Width + 30, 8);
        var root = (Visual)TopLevel.GetTopLevel(title)!;
        Point onRoot = title.TranslatePoint(at, root) ?? default;
        var pointer = new Pointer(0, PointerType.Mouse, true);
        title.RaiseEvent(new PointerPressedEventArgs(
            title, pointer, root, onRoot, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clicks));
        Dispatcher.UIThread.RunJobs();

        Point end = new(onRoot.X + dragX, onRoot.Y + dragY);

        if (dragX != 0 || dragY != 0)
        {
            Canvas canvas = graph.FindControl<Canvas>("GraphCanvas")!;

            for (int step = 1; step <= 4; step++)
            {
                canvas.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, canvas, pointer, root,
                    new Point(onRoot.X + (dragX * step / 4), onRoot.Y + (dragY * step / 4)), 0,
                    new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
                Dispatcher.UIThread.RunJobs();
            }
        }

        title.RaiseEvent(new PointerReleasedEventArgs(
            title, pointer, root, end, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Settle(graph);
    }

    private static void Press(Control control, int clickCount)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None, clickCount));
        Dispatcher.UIThread.RunJobs();
    }

    private static Border Grip(GraphEditorView graph, string nodeId, string tag) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Border>().Single(grip => grip.Tag as string == tag + nodeId);

    private static Border Card(GraphEditorView graph, string nodeId) =>
        graph.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Border>().Single(card => card.Tag as string == nodeId);

    private static Button BasePinButton(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Button>().Single(button => button.Tag as string == GraphEditorView.BasePinToggleTag + nodeId);

    private static Button Fit(GraphEditorView graph, string nodeId, bool width) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<Button>().Single(button =>
            button.Tag as string == (width ? GraphEditorView.FitWidthToggleTag : GraphEditorView.FitHeightToggleTag) + nodeId);

    /// <summary>글 칸이 스크롤 없이 모든 줄을 보인다.</summary>
    private static void AssertAllVisible(GraphEditorView graph, string nodeId)
    {
        ScrollViewer scroll = WritingBox(graph, nodeId).GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroll.Extent.Height <= scroll.Viewport.Height + 0.5, $"글 높이 {scroll.Extent.Height} · 보이는 높이 {scroll.Viewport.Height}");
    }

    private static TextBox WritingBox(GraphEditorView graph, string nodeId) =>
        Card(graph, nodeId).GetVisualDescendants().OfType<TextBox>().Single(box => box.Tag as string == GraphEditorView.WritingTag + nodeId);
}
