using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Tests;

/// <summary>
/// 챕터 그래프가 <b>분기를 보여 주고 그 조건을 받는다</b> — 분기도 선으로 이어 보이고,
/// 어떤 조건일 때 그 분기를 탈 수 있는지는 오른쪽 탭에서 받는다.
///
/// <b>선택지가 아니다.</b> 문구도 스탯변화도 없고, 표식을 만들거나 떼지도 않는다 —
/// 그것은 연출 그래프의 일이다.
/// </summary>
public sealed class ChapterBranchPanelTests
{
    [Fact]
    public void 분기는_점선으로_그려진다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;

        Line drawn = Assert.Single(
            canvas.Children.OfType<Line>(),
            line => (line.Tag as string)?.StartsWith("분기:", StringComparison.Ordinal) == true);

        // 점선인 것이 뜻이다 — 사람이 고르는 길(실선)이 아니라 반드시 다녀오는 통로다.
        Assert.NotNull(drawn.StrokeDashArray);
        Assert.NotEmpty(drawn.StrokeDashArray!);

        // <b>옆변에서 나간다</b>(점선) — 선택지는 아래변을 쓰므로 둘이 안 겹친다. 다녀갈 카드가
        //    오른쪽이라 오른변이다(왼쪽이면 왼변 — 아래 시험).
        Border card = canvas.Children.OfType<Border>().Single(border => (border.Tag as string) == "root");

        Assert.Equal(Canvas.GetLeft(card) + CardWidth, drawn.StartPoint.X, 3);
        Assert.Equal(Canvas.GetTop(card) + (CardHeight / 2), drawn.StartPoint.Y, 3);

        // 다녀갈 곳은 분기 카드 칩이다 — 점선이 그 칩의 왼변 가운데로 든다.
        Border chip = Chip(canvas, project.BranchNodeId);
        Assert.Equal(Canvas.GetLeft(chip), drawn.EndPoint.X, 3);
        Assert.Equal(Canvas.GetTop(chip) + (chip.Height / 2), drawn.EndPoint.Y, 3);
    });

    private const double CardWidth = 190;
    private const double CardHeight = 74;

    [Fact]
    public void 분기_카드는_부르는_에피소드_곁에_작은_칩으로_붙어_그_에피소드를_따라간다() => HeadlessUi.Run(() =>
    {
        // 소유자(2026-11-17): "구성탭에서도 점선으로 붙은 작은 카드로써 읽기 전용으로 둡시다." 에피소드가 아니라 판에 따로 서지 않는다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        var canvas = view.FindControl<Canvas>("GraphCanvas")!;

        Assert.DoesNotContain(canvas.Children.OfType<Border>(), border => (border.Tag as string) == project.BranchName);
        Border root = Card(canvas, "root");
        Border chip = Chip(canvas, project.BranchNodeId);
        Assert.True(Canvas.GetLeft(chip) > Canvas.GetLeft(root) + CardWidth, "칩이 카드 오른쪽에 선다");
        Assert.Equal(Canvas.GetTop(root), Canvas.GetTop(chip), 3);
        Assert.Contains(project.BranchName, ((TextBlock)chip.Child!).Text, StringComparison.Ordinal);   // 「ID로 보기」(처음 켜짐) — 제목이 아니라 ID(2026-11-23)
        Assert.True(chip.Height < CardHeight);

        // 에피소드를 옮기면 칩이 따라간다 — 칩의 자리는 저장하지 않는다.
        double gap = Canvas.GetLeft(chip) - Canvas.GetLeft(root);
        session.Editor.MoveEpisode("ch01", "root", 300, 200);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        root = Card(canvas, "root");
        chip = Chip(canvas, project.BranchNodeId);
        Assert.Equal(Canvas.GetLeft(root) + gap, Canvas.GetLeft(chip), 3);
        Assert.Equal(Canvas.GetTop(root), Canvas.GetTop(chip), 3);
    });

    [Fact]
    public void 칩이_왼쪽에_있으면_다른_곳의_분기는_왼변으로_나가_칩의_오른변으로_든다() => HeadlessUi.Run(() =>
    {
        // 칩은 집(먼저 부르는 에피소드 root)의 오른쪽에 선다. 오른쪽 멀리 선 east도 같은 분기 카드를 부르면 east의 분기점은 왼변으로
        // 나간다 — 연출 그래프의 분기 포트와 같은 규칙(EdgeSide.GoesLeft)이다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        session.Editor.AddEpisode("ch01", "east", title: "east", 900, 300, sceneId: "east");
        DialogueNode east = EpisodeNaming.CardFor(session.Project, "ch01", "east")!;
        session.Editor.SetExitTarget(east.Id, Vn.Authoring.Flow.ExitPortKind.Detour, east.Script.ActiveLines.First().Id, project.BranchNodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Border card = Card(canvas, "east");
        Border chip = Chip(canvas, project.BranchNodeId);
        Line drawn = BranchLines(canvas).Single(line => (line.Tag as string) == $"분기:east->{project.BranchName}");

        Assert.Equal(Canvas.GetLeft(card), drawn.StartPoint.X, 3);
        Assert.Equal(Canvas.GetTop(card) + (CardHeight / 2), drawn.StartPoint.Y, 3);
        Assert.Equal(Canvas.GetLeft(chip) + chip.Width, drawn.EndPoint.X, 3);
    });

    [Fact]
    public void 분기_카드_ID_칸은_이름을_바꾸고_비우거나_쓰인_이름은_받지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-11-17 — 분기 카드의 ID(= Yarn 노드 이름)는 [구성] 탭에서 정한다. 제목은 [대본] 탭의 것이라 그대로다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        view.SelectBranchCard(project.BranchNodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        TextBox box = view.FindControl<TextBox>("BranchIdBox")!;
        Assert.Equal(project.BranchName, box.Text);
        DialogueNode card = session.Project.FindDialogue(project.BranchNodeId)!;

        // 쓰인 이름(에피소드 카드 「root」)과 빈 이름은 받지 않는다 — 칸이 되돌아간다.
        foreach (string refused in new[] { "root", " " })
        {
            long unchanged = session.Editor.Revision;
            box.Text = refused;
            view.RenameInspectedBranchCard();
            Assert.Equal((project.BranchName, project.BranchName), (card.Name, box.Text));
            Assert.Equal(unchanged, session.Editor.Revision);
        }

        long before = session.Editor.Revision;
        box.Text = "ch01_hallway_flashback";
        view.RenameInspectedBranchCard();

        Assert.Equal(("ch01_hallway_flashback", TempProject.BranchTitle), (card.Name, card.Title));
        Assert.Equal(before + 1, session.Editor.Revision);
    });

    [Fact]
    public void 칩을_누르면_부르는_곳과_조건과_읽기_전용_대본이_선다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        var canvas = view.FindControl<Canvas>("GraphCanvas")!;

        Press(Chip(canvas, project.BranchNodeId));

        Assert.True(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);
        Assert.False(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.Contains(TempProject.BranchTitle, view.FindControl<TextBlock>("InspectorTitle")!.Text, StringComparison.Ordinal);
        Assert.Contains(
            view.FindControl<Panel>("BranchFromToPanel")!.Children.OfType<TextBlock>(),
            text => text.Text == "root");
        Assert.Contains("들렀다 간다", view.FindControl<TextBlock>("BranchScriptText")!.Text, StringComparison.Ordinal);

        // 칩이 강조되고, 조건을 고르면 그 분기 카드의 첫머리에 적힌다(되돌리기 한 칸).
        Assert.True(Chip(canvas, project.BranchNodeId).BorderThickness.Left > 2);
        long before = session.Editor.Revision;
        view.FindControl<ComboBox>("BranchConditionCombo")!.SelectedItem = "신뢰높음";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, project.BranchNodeId).Label);
        Assert.Equal(before + 1, session.Editor.Revision);

        // 칩에 조건이 보인다.
        Assert.Contains("신뢰높음", ((TextBlock)Chip(canvas, project.BranchNodeId).Child!).Text, StringComparison.Ordinal);

        // 빈 판을 누르면 풀린다.
        Press(canvas);
        Assert.False(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);
    });

    [Fact]
    public void 분기_카드를_고르면_조건_칸이_열린다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.False(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);

        view.SelectBranchCard(project.BranchNodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);

        // 선택지 패널·에피소드 패널은 닫힌다 — 패널이 무엇을 편집하는지 애매하면 안 된다.
        Assert.False(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.False(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);

        var combo = view.FindControl<ComboBox>("BranchConditionCombo")!;

        Assert.True(combo.IsEnabled);
        Assert.Equal("(언제나)", combo.SelectedItem);          // 아직 안 걸었다
        Assert.Contains("신뢰높음", (IEnumerable<string>)combo.ItemsSource!);
    });

    [Fact]
    public void 조건을_고르면_분기_카드의_첫머리에_적힌다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectBranchCard(project.BranchNodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.FindControl<ComboBox>("BranchConditionCombo")!.SelectedItem = "신뢰높음";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        DialogueNode branch = session.Project.FindDialogue(project.BranchNodeId)!;

        Assert.Equal("신뢰높음",
            BranchEntryCondition.Read(session.Project, branch.Id).Label);

        // 부르는 쪽은 안 바뀐다 — 조건은 다녀간 쪽에만 산다.
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        Assert.Empty(caller.TrailingTransitions);
    });

    [Fact]
    public void 되돌리기_한_번이면_조건이_걷힌다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectBranchCard(project.BranchNodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.FindControl<ComboBox>("BranchConditionCombo")!.SelectedItem = "신뢰높음";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        session.Editor.Undo();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        DialogueNode branch = session.Project.FindDialogue(project.BranchNodeId)!;

        Assert.Null(BranchEntryCondition.Read(session.Project, branch.Id).Label);
    });

    [Fact]
    public void 손으로_지은_구조는_칸이_잠기고_사유가_선다() => HeadlessUi.Run(() =>
    {
        // 작가가 대사 편집기에서 갈래를 얹었다면 덮는 순간 짝이 어긋난 대본이 된다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        DialogueNode branch = session.Project.FindDialogue(project.BranchNodeId)!;
        string firstLineId = branch.Script.ActiveLines.First().Id;

        // 첫머리 조건 하나(BeginIf + 꼬리 EndIf)가 아닌 손 구조 — 첫 줄에 조건 종료가 얹혔다.
        session.Editor.SetLineTransitions(
            branch.Id, firstLineId, [LineConditionTransition.EndIf()]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.SelectBranchCard(project.BranchNodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(view.FindControl<ComboBox>("BranchConditionCombo")!.IsEnabled);

        var blocked = view.FindControl<TextBlock>("BranchBlockedText")!;

        Assert.True(blocked.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(blocked.Text));
    });

    [Fact]
    public void 같은_줄의_분기_카드_둘은_따로_고르고_조건도_따로_건다() => HeadlessUi.Run(() =>
    {
        // 한 줄에 분기가 여럿일 수 있다 — 칩이 둘이고 저마다 고른다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        DialogueNode second = session.Editor.AddBranchMarker(caller.Id, project.BranchLineId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            2,
            view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Line>()
                .Count(line => (line.Tag as string)?.StartsWith("분기:", StringComparison.Ordinal) == true));

        view.SelectBranchCard(second.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.FindControl<ComboBox>("BranchConditionCombo")!.SelectedItem = "신뢰높음";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        DialogueNode first = session.Project.FindDialogue(project.BranchNodeId)!;
        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, second.Id).Label);
        Assert.Null(BranchEntryCondition.Read(session.Project, first.Id).Label);
    });

    [Fact]
    public void 칩_강조는_다른_칩이나_빈_판이나_카드를_고르면_풀린다() => HeadlessUi.Run(() =>
    {
        // 고른 칩은 파랗고 굵다 — 그 강조가 선택을 옮겨도 남아 있으면 무엇을 고른 것인지 판이 거짓말을 한다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        DialogueNode second = session.Editor.AddBranchMarker(caller.Id, project.BranchLineId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        double plain = Chip(canvas, project.BranchNodeId).BorderThickness.Left;

        Press(Chip(canvas, project.BranchNodeId));
        Assert.True(Chip(canvas, project.BranchNodeId).BorderThickness.Left > plain);
        Assert.Equal(plain, Chip(canvas, second.Id).BorderThickness.Left);

        Press(Chip(canvas, second.Id));
        Assert.Equal(plain, Chip(canvas, project.BranchNodeId).BorderThickness.Left);
        Assert.True(Chip(canvas, second.Id).BorderThickness.Left > plain);

        Press(canvas);
        Assert.Equal(plain, Chip(canvas, second.Id).BorderThickness.Left);

        Press(Chip(canvas, project.BranchNodeId));
        view.SelectEpisode("root");
        Assert.Equal(plain, Chip(canvas, project.BranchNodeId).BorderThickness.Left);
    });

    [Fact]
    public void 분기점은_카드_옆변에_서고_점선은_그_점에서_나간다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);
        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Border root = Card(canvas, "root");

        Ellipse knob = Assert.Single(Knobs(canvas));
        Point center = KnobCenter(knob);
        Assert.Equal(Canvas.GetLeft(root) + CardWidth, center.X, 3);
        Assert.Equal(Canvas.GetTop(root) + (CardHeight / 2), center.Y, 3);
        Assert.NotEqual(Brushes.Transparent, knob.Fill);   // 이어진 분기 — 속이 찼다

        Line line = Assert.Single(BranchLines(canvas));
        Assert.Equal(center, line.StartPoint);
    });

    [Fact]
    public void 연결만_끊은_분기점도_속_빈_점으로_남아_보인다() => HeadlessUi.Run(() =>
    {
        // [대본] 탭에서 연결을 끊어도 분기점이 판에서 사라지지 않는다 — 그 자리에 분기점이 있다는 것이 보여야 한다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        session.Editor.SetExitTarget(Detour(session, caller), null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Assert.Empty(BranchLines(canvas));
        Ellipse knob = Assert.Single(Knobs(canvas));
        Assert.Equal(Brushes.Transparent, knob.Fill);
    });

    [Fact]
    public void 점선과_분기점은_보이기만_하고_눌러도_아무것도_고르지_않는다() => HeadlessUi.Run(() =>
    {
        // 소유자(2026-11-17): 점선을 누른 것과 칩을 누른 것이 같아졌으니 점선은 눌리지 않아도 된다 — 분기점도 마찬가지다. 고르는 것은 칩이다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;

        // 점선 위에 겹친 누름 선이 없다 — 점선은 눌리지 않는다.
        Line line = Assert.Single(BranchLines(canvas));
        Assert.False(line.IsHitTestVisible);
        Assert.DoesNotContain(canvas.Children.OfType<Line>(), other =>
            other != line && other.StartPoint == line.StartPoint && other.EndPoint == line.EndPoint);

        // 분기점을 눌러도 아무것도 고르지 않고, 고른 것도 풀지 않는다.
        view.SelectEpisode("root");
        Ellipse knob = Assert.Single(Knobs(canvas));
        Press(knob);
        Release(canvas, KnobCenter(knob));

        Assert.False(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);
        Assert.True(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.Contains("분기 카드", ToolTip.GetTip(knob) as string, StringComparison.Ordinal);

        // 끊기지도 않았다.
        Assert.Equal([project.BranchNodeId], session.Project.FindDialogue(caller.Id)!.FindExtension(project.BranchLineId)!.DetourTargetNodeIds);
    });

    [Fact]
    public void 이어지지_않은_분기점은_툴팁으로_대본_탭에서_잇는다고_말한다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        session.Editor.SetExitTarget(Detour(session, caller), null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Ellipse knob = Assert.Single(Knobs(canvas));
        Assert.Contains("[대본] 탭", ToolTip.GetTip(knob) as string, StringComparison.Ordinal);

        Press(knob);
        Assert.False(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);
    });

    [Fact]
    public void 한_변의_분기점_여럿은_겹치지_않게_세로로_선다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        session.Editor.AddBranchMarker(caller.Id, project.BranchLineId);
        session.Editor.SetExitTarget(Detour(session, caller), null);   // 하나는 연결만 끊는다
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        List<Ellipse> knobs = Knobs(canvas).OrderBy(knob => KnobCenter(knob).Y).ToList();
        List<Point> centers = knobs.Select(KnobCenter).ToList();

        Assert.Equal(2, centers.Count);
        Assert.Equal(centers[0].X, centers[1].X, 3);
        Assert.True(centers[1].Y - centers[0].Y >= 10, "점이 겹친다");

        // 끊은 것(첫째)은 제자리 — 위가 속 빈 점이고 아래는 이어진 둘째다.
        Assert.Equal(Brushes.Transparent, knobs[0].Fill);
        Assert.NotEqual(Brushes.Transparent, knobs[1].Fill);
    });

    private static Border Card(Canvas canvas, string episodeId) =>
        canvas.Children.OfType<Border>().Single(border => (border.Tag as string) == episodeId);

    private static Border Chip(Canvas canvas, string nodeId) =>
        canvas.Children.OfType<Border>().Single(border =>
            border.Tag is ChapterGraphView.BranchCardMark mark && mark.NodeId == nodeId);

    private static List<Ellipse> Knobs(Canvas canvas) =>
        canvas.Children.OfType<Ellipse>()
            .Where(knob => (knob.Tag as string)?.StartsWith(ChapterGraphView.BranchKnobTag, StringComparison.Ordinal) == true)
            .ToList();

    private static Point KnobCenter(Ellipse knob) =>
        new(Canvas.GetLeft(knob) + (knob.Width / 2), Canvas.GetTop(knob) + (knob.Height / 2));

    /// <summary>그 카드의 첫 분기 포트.</summary>
    private static Vn.Authoring.Flow.ExitPort Detour(AuthoringSession session, DialogueNode caller) =>
        Vn.Authoring.Flow.NodeConnections.PortsOf(session.Project.FindDialogue(caller.Id)!, session.Project)
            .First(port => port.Kind == Vn.Authoring.Flow.ExitPortKind.Detour && port.IsConnected);

    /// <summary>판 좌표의 자리로 포인터를 옮긴다 — 포인터 이벤트의 자리는 최상위 기준으로 풀린다.</summary>
    private static void Move(Canvas canvas, Point onCanvas)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, canvas, new Pointer(0, PointerType.Mouse, true), root,
            canvas.TranslatePoint(onCanvas, root)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
            KeyModifiers.None));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Release(Canvas canvas, Point onCanvas)
    {
        var root = (Visual)TopLevel.GetTopLevel(canvas)!;
        canvas.RaiseEvent(new PointerReleasedEventArgs(
            canvas, new Pointer(0, PointerType.Mouse, true), root, canvas.TranslatePoint(onCanvas, root)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static List<Line> BranchLines(Canvas canvas) =>
        canvas.Children.OfType<Line>()
            .Where(line => (line.Tag as string)?.StartsWith("분기:", StringComparison.Ordinal) == true)
            .ToList();

    private static void Press(Control control)
    {
        var root = (Visual)TopLevel.GetTopLevel(control)!;
        control.RaiseEvent(new PointerPressedEventArgs(
            control, new Pointer(0, PointerType.Mouse, true), root, default, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static (ChapterGraphView View, AuthoringSession Session) Show(TempProject project)
    {
        var session = new AuthoringSession();
        session.Open(project.ManifestPath);

        var view = new ChapterGraphView();
        var window = new Window { Width = 1400, Height = 800, Content = view };
        window.Show();
        view.Attach(session);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        project.Ui.Own(view, window);

        return (view, session);
    }

    /// <summary>에피소드 하나에서 분기 하나를 뚫어 둔 프로젝트. 조건 `신뢰높음`이 공급돼 있다.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory;

        public OpenChapterViews Ui { get; } = new();

        public TempProject()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "vn-branch-panel", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_directory);

            var editor = new ProjectEditor(new StoryProject { Title = "분기 검증" });

            editor.EnsureChapter("ch01");
            editor.EnsureChapterBoard("ch01");
            editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");

            ChapterDocument chapter = editor.FindChapter("ch01")!;
            chapter.Stats.Add(new ChapterStat("trust", 0, 0, 10));

            // 판이 이미 섰으므로 조건은 더하는 그 자리에서 판에 공급된다.
            editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

            DialogueNode caller = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;

            BranchLineId = caller.Script.ActiveLines.First().Id;
            DialogueNode branch = editor.AddBranchMarker(caller.Id, BranchLineId);
            branch.Script.RequireLocale(branch.Script.PrimaryLocale).Entries[branch.Script.ActiveLines.First().Id] =
                new Vn.Authoring.Script.LocalizedLine("라루", "들렀다 간다");
            editor.SetBranchCardTitle(branch.Id, BranchTitle);   // 작가가 보는 제목 — ID(이름)와 따로다(2026-11-17)
            BranchName = branch.Name;
            BranchNodeId = branch.Id;

            ManifestPath = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
            ProjectStore.Save(ManifestPath, editor.Project);
        }

        public string ManifestPath { get; }

        public string BranchLineId { get; }


        /// <summary>분기 카드의 이름 — 에피소드가 아니다(2026-11-17).</summary>
        public string BranchName { get; }

        /// <summary>분기 카드의 보이는 제목 — 칩 · 인스펙터 머리글은 이것을 보인다. 이름(<see cref="BranchName"/>)은 ID다.</summary>
        public const string BranchTitle = "들르는 길";

        public string BranchNodeId { get; }

        public void Dispose()
        {
            Ui.CloseAll();

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
