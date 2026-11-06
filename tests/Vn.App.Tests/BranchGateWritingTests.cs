using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.App.Tests;

/// <summary>
/// <b>분기 카드의 조건 구조는 깨지지 않는다</b> — 분기로 다녀오는 카드에 [구성] 탭이 건 조건은 글 칸에
/// <c>&lt;&lt;if …&gt;&gt;</c> · <c>&lt;&lt;endif&gt;&gt;</c> 두 줄로 보인다.
/// <list type="bullet">
/// <item>작가가 실수로 지우거나 고쳐도 넣을 때 그 줄을 되살린다(사이의 대사는 고친 그대로).</item>
/// <item>조건 줄 위 · 아래에 쓴 줄은 조건 안쪽으로 모은다 — [구성] 탭이 「여기서 고칠 수 없습니다」로 잠기지 않는다. 이미 잠긴
/// 카드도 글 칸을 한 번 고치면 바로잡힌다.</item>
/// <item>대사 편집기에서는 조건이 걸린 첫 줄을 지우거나 옮기지 못한다(둘째 줄을 그 위로 올리는 것도).</item>
/// </list>
/// 조건은 [구성] 탭의 분기 칸에서 바꾼다.
/// </summary>
public sealed class BranchGateWritingTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-branch-gate-writing", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("하린: 다시 그린 간판, 훨씬 낫다.")]                                         // 두 줄 모두 지웠다
    [InlineData("<<if stat(\"trust\") >= 1>>\n하린: 다시 그린 간판, 훨씬 낫다.\n<<endif>>")]  // 조건을 손으로 고쳤다
    [InlineData("하린: 다시 그린 간판, 훨씬 낫다.\n<<endif>>")]                              // 첫 줄만 지웠다
    public void 조건_줄을_지우거나_고쳐도_되살리고_대사는_고친_그대로_들어간다(string typed) => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode branch, TextBox box) = Stage();
        string shown = box.Text!;
        Assert.StartsWith("<<if ", shown, StringComparison.Ordinal);
        Assert.EndsWith("<<endif>>", shown, StringComparison.Ordinal);

        box.Focus();
        box.Text = typed.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
        Release(window);

        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);
        Assert.Equal("다시 그린 간판, 훨씬 낫다.", branch.Script.Text(branch.Script.ActiveLines.First().Id).Text);
        Assert.Contains("분기 조건 줄을 지켰습니다", session.StatusMessage, StringComparison.Ordinal);

        string now = Box(window, branch).Text!;
        Assert.StartsWith("<<if ", now, StringComparison.Ordinal);
        Assert.EndsWith("<<endif>>", now, StringComparison.Ordinal);

        window.Close();
    });

    [Theory]
    [InlineData(true)]    // 조건 줄 위에 썼다
    [InlineData(false)]   // 조건 줄 아래에 썼다
    public void 조건_줄_바깥에_쓴_줄은_조건_안쪽으로_들어간다(bool above) => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode branch, TextBox box) = Stage();
        string shown = box.Text!;

        box.Focus();
        box.Text = above ? "하린: 바깥에 쓴 줄" + Environment.NewLine + shown : shown + Environment.NewLine + "하린: 바깥에 쓴 줄";
        Release(window);

        // [구성] 탭이 아는 모양 그대로다 — 잠기지 않고 조건도 그대로다.
        BranchEntryCondition.State state = BranchEntryCondition.Read(session.Project, branch.Id, session.Definition);
        Assert.Equal(("신뢰높음", true), (state.Label, state.Editable));

        // 바깥 줄은 안쪽 맨 위 · 맨 아래에 들어갔다.
        List<string> said = branch.Script.ActiveLines.Select(line => branch.Script.Text(line.Id).Text).ToList();
        Assert.Equal(above ? "바깥에 쓴 줄" : "다시 그린 간판, 생각보다 훨씬 낫다.", said[0]);
        Assert.Equal(above ? "다시 그린 간판, 생각보다 훨씬 낫다." : "바깥에 쓴 줄", said[^1]);

        string now = Box(window, branch).Text!;
        Assert.StartsWith("<<if ", now, StringComparison.Ordinal);
        Assert.EndsWith("<<endif>>", now, StringComparison.Ordinal);

        window.Close();
    });

    [Theory]
    [InlineData("{H}ASD\n{B}\n{T}", new[] { "ASD", Body })]             // if 줄 뒤에 붙였다
    [InlineData("ASD{H}\n{B}\n{T}", new[] { "ASD", Body })]             // if 줄 앞에 붙였다
    [InlineData("{H}\n{B}\n{T}SAD", new[] { Body, "SAD" })]             // endif 줄 뒤에 붙였다
    [InlineData("{H}ASD\n하린: s\n{T}SAD", new[] { "ASD", "s", "SAD" })] // 둘 다 — 보고된 모양
    public void 조건_줄과_같은_줄에_붙여_쓴_글은_떼어_조건_안쪽으로_들어간다(string typed, string[] expected) => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode branch, TextBox box) = Stage();
        string[] shown = box.Text!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        string head = shown[0];
        string tail = shown[^1];

        box.Focus();
        box.Text = typed.Replace("{H}", head, StringComparison.Ordinal)
            .Replace("{B}", shown[1], StringComparison.Ordinal)
            .Replace("{T}", tail, StringComparison.Ordinal);
        Release(window);

        BranchEntryCondition.State state = BranchEntryCondition.Read(session.Project, branch.Id, session.Definition);
        Assert.Equal(("신뢰높음", true), (state.Label, state.Editable));
        Assert.Equal(expected, branch.Script.ActiveLines.Select(line => branch.Script.Text(line.Id).Text));

        // 칸도 조건 줄이 제 줄로 선 모양이다.
        string[] now = Box(window, branch).Text!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Equal((head, tail), (now[0], now[^1]));

        window.Close();
    });

    private const string Body = "다시 그린 간판, 생각보다 훨씬 낫다.";

    [Fact]
    public void 이미_잠긴_카드도_글_칸을_한_번_고치면_바로잡힌다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode branch, TextBox box) = Stage();

        // 지키기 전에 만들어진 모양 — 조건 줄 아래에 줄이 있어 [구성] 탭이 잠겼다.
        string broken = box.Text! + "\n하린: 아래에 쓴 줄";
        Assert.True(session.Editor.ApplyScenarioText(branch.Id, broken, session.Definition).Applied);
        Dispatcher.UIThread.RunJobs();
        Assert.False(BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Editable);

        // 대사 한 글자를 고치고 나온다.
        box = Box(window, branch);
        box.Focus();
        box.Text = box.Text!.Replace("생각보다 ", string.Empty, StringComparison.Ordinal);
        Release(window);

        BranchEntryCondition.State state = BranchEntryCondition.Read(session.Project, branch.Id, session.Definition);
        Assert.True(state.Editable);
        Assert.Equal("신뢰높음", state.Label);
        Assert.Equal("아래에 쓴 줄", branch.Script.Text(branch.Script.ActiveLines.Last().Id).Text);

        window.Close();
    });

    [Fact]
    public void 대사_편집기에서는_조건이_걸린_첫_줄을_지우거나_옮기지_못한다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode branch, _) = Stage();
        ScriptLine second = session.Editor.InsertScriptLine(branch.Id);
        session.Editor.SetScriptLineText(branch.Id, second.Id, "하린", "둘째 줄");
        session.Select(branch.Id);
        Dispatcher.UIThread.RunJobs();

        // 첫 줄 — 아래로 · 삭제가 막혔다(위로는 갈 곳이 없다).
        Dictionary<string, Button> first = LineButtons(window, row: 0);
        Assert.False(first["▼ 아래로"].IsEnabled);
        Assert.False(first["✕ 삭제"].IsEnabled);

        // 둘째 줄 — 위로가 막혔다(조건 위로 올라선다). 아래로 · 삭제는 된다.
        Dictionary<string, Button> next = LineButtons(window, row: 1);
        Assert.False(next["▲ 위로"].IsEnabled);
        Assert.True(next["▼ 아래로"].IsEnabled);
        Assert.True(next["✕ 삭제"].IsEnabled);

        // 조건을 걷으면 다시 된다.
        Assert.Null(BranchEntryCondition.Set(session.Editor, branch.Id, null, session.Definition));
        Dispatcher.UIThread.RunJobs();
        Assert.True(LineButtons(window, row: 0)["✕ 삭제"].IsEnabled);

        window.Close();
    });

    [Fact]
    public void 조건_줄을_그대로_두고_대사만_고치면_알리지_않는다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession session, DialogueNode branch, TextBox box) = Stage();

        box.Focus();
        box.Text = box.Text!.Replace("생각보다 훨씬 낫다", "훨씬 낫다", StringComparison.Ordinal);
        Release(window);

        Assert.Equal("다시 그린 간판, 훨씬 낫다.", branch.Script.Text(branch.Script.ActiveLines.First().Id).Text);
        Assert.Equal("신뢰높음", BranchEntryCondition.Read(session.Project, branch.Id, session.Definition).Label);
        Assert.DoesNotContain("조건 줄을 지켰습니다", session.StatusMessage, StringComparison.Ordinal);

        window.Close();
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>root의 첫 줄 앞에 분기를 두고, 다녀갈 카드에 대사 한 줄과 조건 「신뢰높음」을 건다.</summary>
    private (MainWindow Window, AuthoringSession Session, DialogueNode Branch, TextBox Box) Stage()
    {
        (MainWindow window, AuthoringSession session) = HierarchyFixture.Open(_directory, tab: 1);
        DialogueNode root = EpisodeNaming.CardFor(session.Project, "ch01", "root")!;
        Assert.True(session.Editor.ApplyScenarioText(root.Id, "하린: 첫 줄", session.Definition).Applied);
        DialogueNode branch = session.Editor.AddBranchMarker(root.Id, root.Script.ActiveLines.First().Id);
        session.Editor.SetScriptLineText(branch.Id, branch.Script.ActiveLines.First().Id, "하린", "다시 그린 간판, 생각보다 훨씬 낫다.");

        ChapterDocument chapter = session.Editor.FindChapter("ch01")!;
        chapter.Stats.Add(new ChapterStat("trust", "신뢰", 0, 0, 10));
        session.Editor.AddChapterCondition("ch01", "신뢰높음", "trust >= 3");
        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            session.Editor, EpisodeNaming.BoardOf(session.Project, "ch01")!.Id, chapter.ToGraphModel("ch01"));
        Assert.Null(BranchEntryCondition.Set(session.Editor, branch.Id, "신뢰높음", session.Definition));
        Dispatcher.UIThread.RunJobs();

        return (window, session, branch, Box(window, branch));
    }

    private static TextBox Box(MainWindow window, DialogueNode card) =>
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Canvas>("GraphCanvas")!
            .GetLogicalDescendants().OfType<TextBox>()
            .Single(box => (box.Tag as string) == "writing:" + card.Id);

    /// <summary>대사 편집기에서 그 차례(0부터) 줄의 ＋를 열어 「줄」 칸의 단추들을 글자로 찾는다.</summary>
    private static Dictionary<string, Button> LineButtons(MainWindow window, int row)
    {
        DialogueNodeEditor editor = window.GetLogicalDescendants().OfType<DialogueNodeEditor>().Distinct().Single();
        Button plus = editor.LineHost.GetVisualDescendants().OfType<Button>().Where(button => Equals(button.Content, "＋")).ElementAt(row);
        plus.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        return editor.LineMetaPanel!.GetLogicalDescendants().OfType<Button>()
            .Where(button => button.Content is "▲ 위로" or "▼ 아래로" or "✕ 삭제")
            .ToDictionary(button => (string)button.Content!);
    }

    /// <summary>칸을 나온다 — 판의 빈 곳을 누른 것처럼(그때 초점이 가는 곳으로) 초점을 옮긴다.</summary>
    private static void Release(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.FindControl<GraphEditorView>("Graph")!.FindControl<Border>("WritingFocusSink")!.Focus();
        Dispatcher.UIThread.RunJobs();
    }
}
