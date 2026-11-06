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
    });

    private const double CardWidth = 190;
    private const double CardHeight = 74;

    [Fact]
    public void 다녀갈_에피소드가_왼쪽에_있으면_분기는_왼변으로_나가_그_카드의_오른변으로_든다() => HeadlessUi.Run(() =>
    {
        // 다녀갈 카드가 자기가 나온 에피소드보다 왼쪽에 있으면 왼쪽으로 나온다.
        // 연출 그래프의 분기 포트와 같은 규칙(EdgeSide.GoesLeft)이다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        session.Editor.MoveEpisode("ch01", project.BranchEpisodeId, -400, 120);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        Line drawn = Assert.Single(
            canvas.Children.OfType<Line>(),
            line => (line.Tag as string)?.StartsWith("분기:", StringComparison.Ordinal) == true);
        Border root = canvas.Children.OfType<Border>().Single(border => (border.Tag as string) == "root");
        Border target = canvas.Children.OfType<Border>().Single(border => (border.Tag as string) == project.BranchEpisodeId);

        Assert.True(Canvas.GetLeft(target) < Canvas.GetLeft(root), "다녀갈 카드가 왼쪽에 섰다");
        Assert.Equal(Canvas.GetLeft(root), drawn.StartPoint.X, 3);
        Assert.Equal(Canvas.GetTop(root) + (CardHeight / 2), drawn.StartPoint.Y, 3);
        Assert.Equal(Canvas.GetLeft(target) + CardWidth, drawn.EndPoint.X, 3);
        Assert.Equal(Canvas.GetTop(target) + (CardHeight / 2), drawn.EndPoint.Y, 3);

        // 다시 오른쪽으로 옮기면 오른변으로 나간다.
        session.Editor.MoveEpisode("ch01", project.BranchEpisodeId, 600, 120);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        drawn = Assert.Single(
            canvas.Children.OfType<Line>(),
            line => (line.Tag as string)?.StartsWith("분기:", StringComparison.Ordinal) == true);
        root = canvas.Children.OfType<Border>().Single(border => (border.Tag as string) == "root");   // 다시 그려 새 카드다
        target = canvas.Children.OfType<Border>().Single(border => (border.Tag as string) == project.BranchEpisodeId);
        Assert.Equal(Canvas.GetLeft(root) + CardWidth, drawn.StartPoint.X, 3);
        Assert.Equal(Canvas.GetLeft(target), drawn.EndPoint.X, 3);
    });

    [Fact]
    public void 분기를_고르면_조건_칸이_열린다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.False(view.FindControl<StackPanel>("BranchPanel")!.IsVisible);

        view.SelectBranch(project.BranchKey);
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
    public void 조건을_고르면_다녀갈_에피소드의_첫머리에_적힌다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectBranch(project.BranchKey);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.FindControl<ComboBox>("BranchConditionCombo")!.SelectedItem = "신뢰높음";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        DialogueNode branch = EpisodeNaming.CardFor(session.Project, "ch01", project.BranchEpisodeId)!;

        Assert.Equal("신뢰높음",
            BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);

        // 부르는 쪽은 안 바뀐다 — 조건은 다녀간 쪽에만 산다.
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        Assert.Empty(caller.TrailingTransitions);
    });

    [Fact]
    public void 되돌리기_한_번이면_조건이_걷힌다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectBranch(project.BranchKey);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.FindControl<ComboBox>("BranchConditionCombo")!.SelectedItem = "신뢰높음";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        session.Editor.Undo();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        DialogueNode branch = EpisodeNaming.CardFor(session.Project, "ch01", project.BranchEpisodeId)!;

        Assert.Null(BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);
    });

    [Fact]
    public void 손으로_지은_구조는_칸이_잠기고_사유가_선다() => HeadlessUi.Run(() =>
    {
        // 작가가 대사 편집기에서 갈래를 얹었다면 덮는 순간 짝이 어긋난 대본이 된다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        DialogueNode branch = EpisodeNaming.CardFor(session.Project, "ch01", project.BranchEpisodeId)!;
        string firstLineId = branch.Script.ActiveLines.First().Id;

        // 첫머리 조건 하나(BeginIf + 꼬리 EndIf)가 아닌 손 구조 — 첫 줄에 조건 종료가 얹혔다.
        session.Editor.SetLineTransitions(
            branch.Id, firstLineId, [LineConditionTransition.EndIf()]);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.SelectBranch(project.BranchKey);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(view.FindControl<ComboBox>("BranchConditionCombo")!.IsEnabled);

        var blocked = view.FindControl<TextBlock>("BranchBlockedText")!;

        Assert.True(blocked.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(blocked.Text));
    });

    [Fact]
    public void 같은_줄의_분기_둘은_따로_고르고_조건도_따로_건다() => HeadlessUi.Run(() =>
    {
        // 한 줄에 분기가 여럿일 수 있다. 줄만으로 고르면 둘째가 첫째를 덮어 점선 하나만 고를 수 있게 된다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        DialogueNode second = session.Editor.AddBranchMarker(caller.Id, project.BranchLineId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            2,
            view.FindControl<Canvas>("GraphCanvas")!.Children.OfType<Line>()
                .Count(line => (line.Tag as string)?.StartsWith("분기:", StringComparison.Ordinal) == true));

        view.SelectBranch(Vn.Authoring.Flow.ExitPort.DetourKey(project.BranchLineId, second.Id));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.FindControl<ComboBox>("BranchConditionCombo")!.SelectedItem = "신뢰높음";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        DialogueNode first = EpisodeNaming.CardFor(session.Project, "ch01", project.BranchEpisodeId)!;
        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, second.Id, session.Definition).Label);
        Assert.Null(BranchEntryCondition.Read(session.Project, first.Id, session.Definition).Label);
    });

    [Fact]
    public void 분기_강조는_다른_분기나_빈_판이나_카드를_고르면_풀린다() => HeadlessUi.Run(() =>
    {
        // 분기 점선을 고르면 굵고 파래진다 — 그 강조가 선택을 옮겨도 남아 있으면 무엇을 고른 것인지 판이 거짓말을 한다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);
        DialogueNode caller = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        session.Editor.AddBranchMarker(caller.Id, project.BranchLineId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;
        List<Line> branches = BranchLines(canvas);
        Assert.Equal(2, branches.Count);
        double plain = branches[0].StrokeThickness;

        // 첫째를 누른다 — 그것만 굵다.
        Press(HitOf(canvas, branches[0]));
        Assert.True(branches[0].StrokeThickness > plain);
        Assert.Equal(plain, branches[1].StrokeThickness);

        // 둘째를 누른다 — 첫째는 풀린다.
        Press(HitOf(canvas, branches[1]));
        Assert.Equal(plain, branches[0].StrokeThickness);
        Assert.True(branches[1].StrokeThickness > plain);

        // 빈 판을 누른다 — 둘 다 풀린다.
        Press(canvas);
        Assert.All(branches, line => Assert.Equal(plain, line.StrokeThickness));
        Assert.All(branches, line => Assert.Equal(((ISolidColorBrush)branches[0].Stroke!).Color, ((ISolidColorBrush)line.Stroke!).Color));

        // 다시 고르고 카드를 고른다 — 풀린다.
        Press(HitOf(canvas, branches[0]));
        view.SelectEpisode("root");
        Assert.Equal(plain, branches[0].StrokeThickness);
    });

    private static List<Line> BranchLines(Canvas canvas) =>
        canvas.Children.OfType<Line>()
            .Where(line => (line.Tag as string)?.StartsWith("분기:", StringComparison.Ordinal) == true)
            .ToList();

    /// <summary>점선에 겹친 굵은 누름 선 — 사람이 실제로 누르는 것이다.</summary>
    private static Line HitOf(Canvas canvas, Line branch) =>
        canvas.Children.OfType<Line>().Single(line =>
            line != branch && line.StartPoint == branch.StartPoint && line.EndPoint == branch.EndPoint &&
            line.Stroke == Brushes.Transparent);

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
            editor.AddEpisode("ch01", "root", title: "root", 0, 0);

            ChapterDocument chapter = editor.FindChapter("ch01")!;
            chapter.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));

            // 판이 이미 섰으므로 조건은 더하는 그 자리에서 판에 공급된다.
            editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");

            DialogueNode caller = EpisodeNaming.CardFor(editor.Project, "ch01", "root")!;

            BranchLineId = caller.Script.ActiveLines.First().Id;
            DialogueNode branch = editor.AddBranchMarker(caller.Id, BranchLineId);
            BranchEpisodeId = EpisodeNaming.EpisodeIdOf(branch);
            BranchKey = Vn.Authoring.Flow.ExitPort.DetourKey(BranchLineId, branch.Id);

            ManifestPath = Path.Combine(_directory, "project" + ProjectManifestJson.FileExtension);
            ProjectStore.Save(ManifestPath, editor.Project);
        }

        public string ManifestPath { get; }

        public string BranchLineId { get; }

        /// <summary>분기 하나의 열쇠 — 줄과 다녀갈 카드(한 줄에 분기가 여럿일 수 있다).</summary>
        public string BranchKey { get; }

        public string BranchEpisodeId { get; }

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
