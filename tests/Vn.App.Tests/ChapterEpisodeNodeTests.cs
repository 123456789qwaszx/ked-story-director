using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Tests.Fixtures;

namespace Vn.App.Tests;

/// <summary>
/// <b>챕터의 에피소드로 선 대사노드</b> — 자유 씬과 무엇이 다르고 무엇이 같은가.
///
/// ⛔ <b>이 묶음의 절반은 2026-09-16에 은퇴했다</b> (R-E · 지시서 §6.4). 옛 이름은
/// <c>ExcelNodeLockTests</c>였고, 지키던 것은 <i>"엑셀 소유 대본이 툴에서 고쳐지는 척하다
/// 다음 동기화에 증발하는 사고"</i>를 막는 잠금이었다. 그 동기화가 R-D에서 철거되면서
/// 잠금이 지킬 것이 없어졌다 — <b>대본은 어느 노드에서든 열린다</b>.
///
/// 남은 다름은 셋이고, 셋 다 대본이 아니라 <b>챕터</b>가 쥔 것이다:
/// ① 이름(챕터 `대사엔트리`가 원천 — R-F에서 뒤집힌다)
/// ② 스탯변화(챕터 간선의 것 — 2026-08-14 결정)
/// ③ 갈래 출구(연출 그래프 카드의 IF 포트 하나 — 2026-08-23 결정)
/// </summary>
public sealed class ChapterEpisodeNodeTests
{
    [Fact]
    public void 대사_잠금은_사라졌다() => HeadlessUi.Run(() =>
    {
        // ⛔ <b>은퇴의 표지다</b> (R-E · §6.4). [🔒 대사 잠김] 토글이 하던 말은 "이 대본의
        //    원본은 엑셀이라 여기서 고쳐도 다음 동기화가 되돌린다"였고, 그 동기화가
        //    R-D에서 철거됐다. 되돌릴 것이 없으므로 잠글 것도 없다.
        //
        //    이름으로 못을 박아 두는 이유 — 지운 것을 무심코 되살리는 일을 막는다.
        (DialogueNodeEditor editor, _, _) = ShowEpisodeCard();

        Assert.Null(editor.FindControl<ToggleButton>("ExcelTextLockToggle"));
    });

    [Fact]
    public void 챕터_에피소드도_줄을_더한다() => HeadlessUi.Run(() =>
    {
        // ⛔ 지시서 §6.4가 이름으로 짚은 줄이다 — `AddLineButton.IsEnabled = !_excelOwned`.
        //    (짝이던 [텍스트 반영]은 2026-10-29에 Script Preview 탭과 함께 걷혔다.)
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId) = ShowEpisodeCard();

        Assert.True(editor.FindControl<Button>("AddLineButton")!.IsEnabled);

        DialogueNode node = session.Project.FindDialogue(nodeId)!;
        int before = session.Project.FindScript(node.ScriptId)!.ActiveLines.Count();

        editor.FindControl<Button>("AddLineButton")!
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + 1, session.Project.FindScript(node.ScriptId)!.ActiveLines.Count());
    });

    [Fact]
    public void 챕터_에피소드의_본문과_화자가_열렸다() => HeadlessUi.Run(() =>
    {
        // 옛 판은 이 셋이 <b>잠겼음</b>을 지켰다. 잠긴 이유가 사라졌으니 지킬 것이 뒤집힌다.
        (DialogueNodeEditor editor, _, _) = ShowEpisodeCard();
        var host = editor.FindControl<StackPanel>("LineHost")!;

        // 본문 칸 — 캐럿이 선다. (화자 자동완성 내부의 TextBox는 제외.)
        List<TextBox> bodies = host.GetVisualDescendants().OfType<TextBox>()
            .Where(box => box.FindAncestorOfType<AutoCompleteBox>() is null)
            .ToList();
        Assert.NotEmpty(bodies);
        Assert.All(bodies, box => Assert.False(box.IsReadOnly));
        Assert.All(bodies, box => Assert.True(box.IsHitTestVisible));

        // 화자 칸과 그 옆 ▾ — 함께 열린다.
        List<AutoCompleteBox> speakers = host.GetVisualDescendants().OfType<AutoCompleteBox>().ToList();
        Assert.NotEmpty(speakers);
        Assert.All(speakers, box => Assert.True(box.IsEnabled));

        List<Button> picks = host.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Content as string == "▾")
            .ToList();
        Assert.NotEmpty(picks);
        Assert.All(picks, button => Assert.True(button.IsEnabled));
    });

    [Fact]
    public void 이름만은_아직_챕터의_것이다() => HeadlessUi.Run(() =>
    {
        // ⚠ 잠긴 이유가 바뀌었다 — 대본의 주인 때문이 아니라, <b>이름의 원천이 챕터의
        //    에피소드</b>이기 때문이다. 개명은 [챕터 그래프]의 [이름] 칸에서 하고, 그쪽은
        //    챕터와 카드를 함께 간다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId) = ShowEpisodeCard();

        Assert.True(editor.FindControl<TextBox>("NameBox")!.IsReadOnly);

        // 우회로도 안 바뀐다 — 문이 둘이면 빗장도 둘이어야 한다.
        string before = session.Project.FindDialogue(nodeId)!.Name;

        editor.FindControl<TextBox>("NameBox")!.Text = "손으로 고친 이름";
        editor.FindControl<Button>("AddLineButton")!.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, session.Project.FindDialogue(nodeId)!.Name);
    });

    [Fact]
    public void 고친_대사가_프로젝트에_남는다() => HeadlessUi.Run(() =>
    {
        // ⚠ <b>이 테스트는 두 번 뒤집혔다</b>. 첫 판은
        //    `풀고_고친_대사가_엑셀_셀까지_간다`였다 — 고친 글을 엑셀 셀에 먼저 쓰고
        //    성공했을 때만 노드를 고치는 순서였고, 그 이유가 <i>"노드만 고치면 다음
        //    동기화가 지운다"</i>였다.
        //
        //    다시 읽는 동기화가 없어졌으므로 지울 것이 없다 — 고친 글은 프로젝트에 남는다.
        //    그리고 2026-09-16에 <b>잠금을 푸는 절차가 사라졌다</b>(R-E) — 그냥 고친다.
        (DialogueNodeEditor editor, AuthoringSession session, string nodeId) = ShowEpisodeCard();

        var host = editor.FindControl<StackPanel>("LineHost")!;
        TextBox body = host.GetVisualDescendants().OfType<TextBox>()
            .First(box => box.FindAncestorOfType<AutoCompleteBox>() is null);

        Type(editor, body, "연출 그래프에서 고친 대사");

        DialogueNode node = session.Project.FindDialogue(nodeId)!;

        Assert.Contains(
            session.Project.FindScript(node.ScriptId)!.Locales
                .SelectMany(locale => locale.Entries.Values),
            line => line.Text == "연출 그래프에서 고친 대사");
    });

    // ⛔ `엑셀이_잡고_있어도_노드는_고쳐진다`는 2026-10-29(D2)에 걷혔다 — 엑셀 연동이 사라져
    //    붙들릴 대본 파일이 없다.

    // ⛔ `다른_노드로_옮기면_다시_잠긴다`는 2026-09-16에 은퇴했다 (R-E). 노드를 옮길 때마다
    //    다시 잠그던 규율은 "여는 것은 잠깐의 예외여야 한다"에서 나왔는데, 이제 예외가
    //    아니라 기본이다 — 다시 잠글 상태가 없다.

    // ⛔ `엑셀노드의_분기_이후_레일은_표시뿐이다`는 2026-09-16에 은퇴했다 (규격 v15 — R-C).
    //    그 테스트는 <b>엑셀노드에 조건 갈래가 있다</b>를 전제로 레일이 단추가 아님을 지켰는데,
    //    대본에서 조건 블록이 폐지되면서 평평화가 `<<if>>`를 더는 내지 않는다 — 엑셀 출처
    //    노드는 갈래를 가질 길 자체가 없어졌다. 막을 것이 없어진 빗장이라 함께 걷는다.

    [Fact]
    public void 출구_후보에_에피소드_카드가_없다() => HeadlessUi.Run(() =>
    {
        // 소유자 결정 (2026-08-14) — 에피소드 사이 흐름은 챕터 간선(기획자) 소유다.
        // 자유 노드의 출구로 에피소드 카드를 고를 수 있으면 챕터 장부(표시/해금·스탯 환산·
        // cleared)를 지나치는 뒷길이 생기고, Yarn 점프라 "복귀"도 처음부터 다시 재생된다.
        (DialogueNodeEditor editor, AuthoringSession session, string episodeNodeId) = ShowEpisodeCard();

        // 자유 노드는 챕터가 아닌 판에 산다 — 챕터 판의 대사 노드는 전부 에피소드다(R7 P-6).
        string scratch = session.Editor.AddStoryFile("낙서판").Id;
        DialogueNode free = session.Editor.AddDialogueNode(scratch, name: "곁가지");
        session.Editor.AddDialogueNode(scratch, name: "곁가지2");

        // ⚠ [기본 출구] 편집 구역은 2026-08-22에 사라졌다 (소유자) — 같은 값을 판의
        // 레일 칩이 편집하므로 창구를 하나로 줄였다. 후보 규칙은 갈래(detour) 출구가
        // 그대로 물려받았고, 여기서 지키는 것은 그 규칙이다.
        editor.Show(episodeNodeId);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(editor.FindControl<Grid>("DefaultExitControls"));
        Assert.Null(editor.FindControl<ComboBox>("DefaultExitCombo"));
        Assert.Null(editor.FindControl<TextBlock>("DefaultExitSubtitle"));

        // 갈래 출구의 후보 — 자유 노드만 남고 에피소드 카드는 빠진다.
        List<StoryNode> targets = editor.ExitTargetsProbe(free.Id);

        Assert.Contains(targets, target => target.Name == "곁가지2");
        Assert.DoesNotContain(targets, target =>
            target is DialogueNode { MarkedEpisodeId: not null });
    });

    [Fact]
    public void 에피소드_카드로_향하는_출구는_검증이_크게_말한다() => HeadlessUi.Run(() =>
    {
        // 편집기는 후보에서 빼지만, 이미 있는 연결(옛 프로젝트)은 막지 않고 경고한다.
        (_, AuthoringSession session, string episodeNodeId) = ShowEpisodeCard();

        string fileId = session.EnsureChapterBoard("ch05");
        DialogueNode free = session.Editor.AddDialogueNode(fileId, name: "우회로");

        // 갈래(detour) 출구가 에피소드 카드를 가리키면 여전히 크게 말한다.
        free.BranchExits["ln_legacy"] = episodeNodeId;

        // ⚠ 2026-09-17에 뒤집혔다 (R7 P-6 · 결정 ⑤). 전에는 커스텀 노드의 기본 출구가
        //    죽은 값이라 경고도 안 냈다 — 그 관문이 곧 에피소드/자유노드의 구분이었고,
        //    구분이 없어졌으므로 이 배선도 살아나 <b>갈래 출구와 나란히</b> 짚인다.
        free.DefaultExitTargetNodeId = episodeNodeId;

        ChapterGraphModel chapter = Chapter(session);

        var warnings = ChapterBoardSupply.WarnExitsIntoLandingCards(session.Editor, fileId, chapter);

        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, item =>
            Assert.Equal(ChapterDiagnosticSeverity.Warning, item.Severity));
        Assert.Contains(warnings, item => item.Message.Contains("기본 출구", StringComparison.Ordinal));

        ChapterDiagnostic warning = Assert.Single(
            warnings, item => item.Message.Contains("갈래 출구", StringComparison.Ordinal));
        Assert.Contains("우회로", warning.Message);
        Assert.Contains("챕터 간선", warning.Message);
    });

    [Fact]
    public void 자유_노드는_그대로_편집된다() => HeadlessUi.Run(() =>
    {
        var session = new AuthoringSession();
        using var project = new TempProject();
        session.Open(project.ManifestPath);

        // 자유 노드는 챕터가 아닌 판에 산다 — 챕터 판에 세우면 에피소드가 된다(R7 P-6).
        string scratch = session.Editor.AddStoryFile("낙서판").Id;
        DialogueNode free = session.Editor.AddDialogueNode(scratch, name: "자유씬");

        var editor = new DialogueNodeEditor();
        var window = new Window { Width = 1200, Height = 800, Content = editor };
        window.Show();
        editor.Attach(session);
        editor.Show(free.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(editor.FindControl<TextBox>("NameBox")!.IsReadOnly);
        Assert.True(editor.FindControl<Button>("AddLineButton")!.IsEnabled);
    });

    [Fact]
    public void 자유_노드가_챕터_조건을_바로_쓴다() => HeadlessUi.Run(() =>
    {
        // 2단계 4번 — 작가가 설정노드를 손으로 잇지 않아도, 판 위의 자유 노드가
        // 조건 드롭다운에서 챕터 라벨(A 계층)을 바로 고를 수 있어야 한다.
        // (R7 P-6부터 챕터 판에 세운 노드는 에피소드 카드가 된다 — 공급은 판 전체가 본다.)
        (_, AuthoringSession session, _) = ShowEpisodeCard();

        string fileId = session.EnsureChapterBoard("ch05");
        DialogueNode free = session.Editor.AddDialogueNode(fileId, name: "자유씬");

        ChapterGraphModel chapter = Chapter(session);

        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            session.Editor, fileId, chapter);

        Vn.Authoring.Flow.AvailableConditionCatalog available =
            Vn.Authoring.Flow.AvailableConditionResolver.Resolve(
                session.Project, free.Id, session.Definition);

        Assert.Contains(available.Conditions, condition => condition.Name == "신뢰높음");

        // 멱등 — 두 번 불러도 공급 노드·조건이 늘지 않는다.
        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            session.Editor, fileId, chapter);

        Assert.Single(session.Project.EnumerateNodes().OfType<SetNode>(),
            node => node.Name == "챕터 ch05 조건");
    });

    [Fact]
    public void 챕터_조건_공급노드는_시나리오_그래프에_보이지_않는다() => HeadlessUi.Run(() =>
    {
        // A 계층 격리 (2026-08-15 소유자) — 챕터의 조건 식(스탯 변수)은 기획자의 자료다.
        // 공급 설정노드가 시나리오 그래프에 카드·링크로 서 있으면 작가에게 노출된다.
        // 데이터(공급·드롭다운 라벨)는 살아 있되, 화면에서는 존재하지 않는다.
        (_, AuthoringSession session, _) = ShowEpisodeCard();

        string fileId = session.EnsureChapterBoard("ch05");
        DialogueNode free = session.Editor.AddDialogueNode(fileId, name: "자유씬");

        ChapterGraphModel chapter = Chapter(session);

        ChapterBoardSupply.SupplyChapterConditionsToBoard(
            session.Editor, fileId, chapter);

        SetNode supply = session.Project.EnumerateNodes().OfType<SetNode>()
            .Single(node => node.Name == "챕터 ch05 조건");

        // 펼친 판 — 카드도, 조건 공급 간선도 없다.
        Vn.Authoring.Graph.GraphProjection expanded = Vn.Authoring.Graph.GraphProjectionBuilder.Build(
            session.Project,
            session.Project.Files.Select(file => file.Id).ToHashSet(StringComparer.Ordinal));

        Assert.DoesNotContain(expanded.Items.OfType<Vn.Authoring.Graph.ExpandedNodeProjection>(),
            item => item.NodeId == supply.Id);
        Assert.DoesNotContain(expanded.Connections,
            connection => connection.SourceNodeId == supply.Id || connection.TargetNodeId == supply.Id);

        // 접힌 파일 프록시의 행 목록에도 없다.
        Vn.Authoring.Graph.GraphProjection collapsed = Vn.Authoring.Graph.GraphProjectionBuilder.Build(
            session.Project, new HashSet<string>(StringComparer.Ordinal));

        Assert.DoesNotContain(
            collapsed.Items.OfType<Vn.Authoring.Graph.CollapsedFileProjection>()
                .SelectMany(proxy => proxy.Nodes),
            entry => entry.NodeId == supply.Id);

        // 공급 자체는 살아 있다 — 작가는 라벨만 본다.
        Vn.Authoring.Flow.AvailableConditionCatalog available =
            Vn.Authoring.Flow.AvailableConditionResolver.Resolve(
                session.Project, free.Id, session.Definition);

        Assert.Contains(available.Conditions, condition => condition.Name == "신뢰높음");
    });
    // ⛔ <b>「자유 노드가 스탯을 set으로 바꾸면 경고한다」는 2026-09-17에 걷혔다</b>
    //    (작가 변수 폐지). 대사에서 스탯에 `<<set>>`을 걸 길 자체가 없어져, 경고가
    //    지키던 뒷길이 <b>구조적으로 불가능</b>해졌다.

    /// <summary>
    /// 칸에 글을 치고 <b>초점을 진짜로 옮긴다</b>. 이름 칸으로 옮기는 것은 그것이 늘 있고
    /// 읽기 전용이라 아무 일도 안 일으키기 때문이다.
    /// </summary>
    private static void Type(DialogueNodeEditor editor, TextBox box, string text)
    {
        box.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        box.Text = text;

        editor.FindControl<TextBox>("NameBox")!.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static ChapterGraphModel Chapter(AuthoringSession session) =>
        session.Editor.FindChapter(SampleChapters.Corridor)!.ToGraphModel(SampleChapters.Corridor);

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 견본 챕터를 든 프로젝트를 열고, <c>main05.02</c>의 카드에 대사 두 줄을 채워 편집기에 띄운다.
    /// 카드는 세션이 열 때 이미 서 있다(<c>ProjectEditor.AlignBoardsWithChapters</c>).
    /// </summary>
    private static (DialogueNodeEditor Editor, AuthoringSession Session, string NodeId) ShowEpisodeCard()
    {
        var session = new AuthoringSession();
        var project = new TempProject();
        session.Open(project.ManifestPath);

        DialogueNode card = EpisodeNaming.CardFor(session.Project, SampleChapters.Corridor, "main05.02")!;

        // 대사 두 줄. 새 카드는 빈 줄 하나로 서는데, 그 줄을 먼저 채워야 붙여넣기가 그것을
        // 지울 후보로 보지 않는다(ScenarioPasteTests와 같은 준비).
        ScriptDocument script = session.Project.FindScript(card.ScriptId)!;
        session.Editor.SetScriptLineText(script.Id, script.ActiveLines.First().Id, "윌로", "첫 줄");

        ScenarioPasteOutcome pasted = session.Editor.ApplyScenarioText(
            card.Id, "윌로: 첫 줄\n라루: 둘째 줄", session.Definition, confirmDeletes: true);

        Assert.True(pasted.Applied, string.Join(" / ", pasted.Problems));

        var editor = new DialogueNodeEditor();
        var window = new Window { Width = 1200, Height = 800, Content = editor };
        window.Show();
        editor.Attach(session);
        editor.Show(card.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (editor, session, card.Id);
    }

    /// <summary>견본 챕터(ch05 복도)를 든 임시 프로젝트.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "vn-episode-node", Guid.NewGuid().ToString("N"));

        public TempProject()
        {
            ManifestPath = SampleProject.Corridor(_directory, "에피소드 카드 검증");
        }

        public string ManifestPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
