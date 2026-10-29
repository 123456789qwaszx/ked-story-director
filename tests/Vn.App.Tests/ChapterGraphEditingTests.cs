using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Path = System.IO.Path;
using Vn.App.Services;
using Vn.App.Views;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// G-2 v2 — 그래프 편집이 프로젝트의 챕터에 써진다. 패널의 [적용]·[개명]·간선·조건·＋에피소드가
/// 전부 <c>ChapterDocument</c>에 남고, 모델을 다시 읽으면 그대로 나온다.
/// (2026-10-29 · D2: 전에는 챕터 워크북으로 왕복했다.)
/// </summary>
public sealed class ChapterGraphEditingTests
{
    [Fact]
    public void 길의_관문_콤보가_간선에_써진다() => HeadlessUi.Run(() =>
    {
        // v8 (2026-08-16 소유자) — "보일지 말지는 이제 간선이 정한다". 표시·해금조건이
        // 에피소드에서 길(간선)로 옮겨 왔고, 편집도 간선 패널에서 한다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        // 견본의 이 길에는 해금조건 '신뢰높음'이 걸려 있다.
        Assert.Equal("신뢰높음", view.FindControl<ComboBox>("EdgeConditionCombo")!.SelectedItem);

        view.FindControl<ComboBox>("EdgeVisibleCombo")!.SelectedItem = "지쳐있음";
        view.ApplyEdgeFromPanel();

        ChapterEdge reread = Edge(session, "main05.02", "branch05.02A");

        Assert.Equal("지쳐있음", reread.VisibleConditionLabel);
        Assert.Equal("신뢰높음", reread.ConditionLabel); // 안 바꾼 값은 그대로

        // 에피소드를 고르면 그 패널로 넘어간다 — 간선 패널은 닫힌다.
        view.SelectEpisode("branch05.02A");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.False(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
    });

    [Fact]
    public void 간선_패널의_선택지도_챕터의_모든_문구에서_고른다() => HeadlessUi.Run(() =>
    {
        // v9 (2026-08-17 소유자) — "간선에서도 선택지 드롭다운이 되도록." 그래프에서 길을
        // 눌러 연 패널에서도 문구를 고르고, [적용] 한 번에 관문과 함께 저장된다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var labels = view.FindControl<ComboBox>("EdgeLabelEditBox")!;
        Assert.True(labels.IsEnabled); // 읽기 전용이 아니다 — 여기서 고친다
        Assert.Equal("라루의 제안을 듣는다", labels.SelectedItem);
        // 목록은 챕터 전체 사전이다 — 다른 에피소드가 쓰는 문구까지 담긴다.
        Assert.Contains("혼자 문을 연다", (IEnumerable<string>)labels.ItemsSource!);

        labels.SelectedItem = "혼자 문을 연다";
        view.FindControl<ComboBox>("EdgeVisibleCombo")!.SelectedItem = "지쳐있음";
        view.ApplyEdgeFromPanel();

        ChapterEdge reread = Edge(session, "main05.02", "branch05.02A");

        Assert.Equal("혼자 문을 연다", reread.OptionLabel); // 문구가 한 저장에 함께 실렸다
        Assert.Equal("지쳐있음", reread.VisibleConditionLabel);
        Assert.Equal("신뢰높음", reread.ConditionLabel);

        // 문구는 신원의 일부다 — 바꿔도 선택이 풀리지 않고 그 길이 계속 열려 있다.
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.Equal("혼자 문을 연다", view.FindControl<ComboBox>("EdgeLabelEditBox")!.SelectedItem);
    });

    [Fact]
    public void 에피소드를_지워도_챕터의_선택지_문구는_남는다() => HeadlessUi.Run(() =>
    {
        // v9 — 사전은 챕터 전체의 어휘다. 에피소드 하나가 사라졌다고 낱말을 지우면
        // 다른 에피소드의 드롭다운에서도 사라진다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEpisode("branch05.02A");
        view.DeleteSelectedEpisode();

        ChapterGraphModel reread = Chapter(session);

        Assert.Null(reread.FindEpisode("branch05.02A"));
        Assert.DoesNotContain(reread.Edges, edge => edge.ToEpisodeId == "branch05.02A");
        Assert.Contains(reread.ChoiceOptions, option => option.Text == "라루의 제안을 듣는다");
    });

    [Fact]
    public void 에피소드를_지우면_연출_그래프의_빈_노드도_함께_지운다() => HeadlessUi.Run(() =>
    {
        // ⛔ 이것이 "번들 이름이 겹칩니다"의 뿌리였다 (2026-08-25). 챕터에서만 지우면
        //    판에 <b>사라진 에피소드를 자기라고 주장하는 유령</b>이 남고, 같은 Id로 다시
        //    만들면 같은 이름의 노드가 둘이 되어 내보내기가 통째로 막힌다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        // 판과 카드는 프로젝트를 열 때 이미 섰다(AlignBoardsWithChapters) — 여기서 또 만들면
        // 같은 이름이 둘이 되어 실물이 아니라 사본을 재게 된다.
        DialogueNode ghost = session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => node.MarkedEpisodeId == "branch05.02A");

        // 대본은 딸려 있어도 적힌 글이 없으면 빈 카드다 — 아무도 손대지 않은 자리다.
        Vn.Authoring.Script.ScriptDocument blank = ghost.Script;
        Assert.DoesNotContain(blank.ActiveLines, line => blank.Text(line.Id).Text.Length > 0);

        view.SelectEpisode("branch05.02A");
        view.DeleteSelectedEpisode();

        Assert.Null(session.Project.FindNode(ghost.Id));
    });

    [Fact]
    public void 글이_든_노드도_함께_지우고_되돌리기로_돌아온다() => HeadlessUi.Run(() =>
    {
        // ⛔ <b>2026-09-18에 뒤집혔다</b> (소유자: *"노드가 지워지는게 아니라 떼어냄으로
        //    변하는데?"*). 전에는 글이 든 카드를 지우지 않고 <c>(떼어냄)</c>으로 이름만
        //    바꿔 판에 남겼다. 근거는 *"툴이 임의로 지우면 되돌릴 자리가 없다(판 편집에는
        //    Ctrl+Z가 없다)"*였는데, <b>같은 날 Ctrl+Z가 붙어 그 전제가 없어졌다</b>.
        //
        // ⚠ 글은 그래도 안 사라진다 — 대본은 원래 안 지우고, 되돌리면 카드가 다시 찾는다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        DialogueNode written = session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => node.MarkedEpisodeId == "branch05.02A");

        var script = new Vn.Authoring.Script.ScriptDocument(name: "남은 대본");
        script.Lines.Add(new Vn.Authoring.Script.ScriptLine("ln_keep"));
        script.RequireLocale(script.PrimaryLocale).Entries["ln_keep"] =
            new Vn.Authoring.Script.LocalizedLine("윌로", "잃으면 안 되는 줄");
        written.Script = script;

        view.SelectEpisode("branch05.02A");
        view.DeleteSelectedEpisode();

        // 카드가 <b>사라진다</b> — `(떼어냄)`이 판에 남지 않는다.
        Assert.Null(session.Project.FindNode(written.Id));
        Assert.DoesNotContain(
            session.Project.EnumerateNodes(),
            node => node.Name.Contains("떼어냄", StringComparison.Ordinal));

        session.Editor.Undo();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 되돌리면 카드도 에피소드도 돌아오고, 글은 애초에 안 사라졌다.
        DialogueNode back = Assert.IsType<DialogueNode>(session.Project.FindNode(written.Id));
        Assert.Equal("잃으면 안 되는 줄", back.Script.Text("ln_keep").Text);
    });

    [Fact]
    public void 편집이_열려_있다() => HeadlessUi.Run(() =>
    {
        // ⚠ 이것이 2026-08-24에 뒤집힌 기본값이다 — 전에는 닫힌 채로 시작했다. 2026-10-29부터는
        //    닫을 길(엑셀 잠금) 자체가 없다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<TextBox>("IdBox")!.IsEnabled);
        Assert.True(view.FindControl<Button>("AddNextEdgeButton")!.IsVisible);

        var delete = view.FindControl<Button>("DeleteEpisodeButton")!;
        Assert.True(delete.IsVisible);
        Assert.True(delete.IsEnabled);
    });

    [Fact]
    public void 편집_폼은_선택지를_먼저_묻는다() => HeadlessUi.Run(() =>
    {
        // 2026-08-24 소유자 재지시 — 같은 날 오전의 "도착이 먼저" 결정을 되돌렸다.
        // 목록의 줄이 `문구 → 도착`으로 읽히므로, 그 줄을 눌러 열리는 폼도 같은 순서여야
        // 눈이 자리를 다시 찾지 않는다. 화살표도 →로 돌아온다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.Equal(0, Grid.GetColumn(view.FindControl<ComboBox>("EdgeLabelBox")!));
        Assert.Equal(2, Grid.GetColumn(view.FindControl<ComboBox>("EdgeTargetCombo")!));

        // 화살표가 관계를 말한다 — 칸만 바꾸고 ←를 남겨 두면 뜻이 거꾸로 읽힌다.
        Assert.Equal("→", view.FindControl<Grid>("EdgeFormPanel")!
            .Children.OfType<TextBlock>().Single().Text);
    });

    [Fact]
    public void 열려_있을_때는_이름_칸이_아무_말도_안_한다() => HeadlessUi.Run(() =>
    {
        // 2026-08-24 소유자 "이런 쓸모없는 설명들을 제거해줘" — 하루 전 이 자리에 붙였던
        // "이름 — 고치고 Enter"를 걷었다. 개명 자리인 줄 알리는 몫은 툴팁이 진다.
        // (잠겼을 때 사유를 적던 칸도 2026-10-29에 엑셀 잠금 관문과 함께 사라졌다.)
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);
        view.SelectEpisode("main05.01");

        Assert.True(view.FindControl<TextBox>("IdBox")!.IsEnabled);

        // 툴팁은 남는다 — 물을 때만 뜨고 자리를 안 먹는다.
        var tip = ToolTip.GetTip(view.FindControl<TextBox>("IdBox")!) as string;
        Assert.Contains("Enter", tip!);
    });

    [Fact]
    public void 상시_안내들이_편집_패널에서_사라졌다() => HeadlessUi.Run(() =>
    {
        // 2026-08-24 소유자 — 머리글 "에피소드", 읽기 전용 값 줄(`대사엔트리: … · 엑셀 N행`),
        // "길 하나 = 선택지 하나…" 셋을 걷었다. 고칠 수도 없는 글이 상시로 자리를 먹었다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);
        view.SelectEpisode("main05.01");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(view.FindControl<TextBlock>("EpisodeFactsText"));

        string[] gone =
        [
            "에피소드",
            "길 하나 = 선택지 하나",
            "대사엔트리"
        ];

        List<string> standing = view.FindControl<StackPanel>("PropertyPanel")!
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsVisible)
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        foreach (string phrase in gone)
        {
            Assert.DoesNotContain(standing, text => text.Contains(phrase, StringComparison.Ordinal));
        }

        // 남아야 하는 것 — 선택지 머리글은 표의 이름표라 안내가 아니다.
        Assert.Contains(standing, text => text.Contains("선택지", StringComparison.Ordinal));
    });

    [Fact]
    public void 선택지는_챕터의_모든_문구_중에서_고른다() => HeadlessUi.Run(() =>
    {
        // v9 (2026-08-17 소유자) — "자기 것만 고르는 게 아니라 모든 선택지 중에서 자유자재로."
        // 견본의 사전에는 main05.02가 쓰는 문구 둘이 있고, main05.01에서도 그것을 고른다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEpisode("main05.01");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // [＋] — 빈 폼이 열린다. 아직 아무것도 안 쓴다.
        view.FindControl<Button>("AddNextEdgeButton")!.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<Grid>("EdgeFormPanel")!.IsVisible);
        Assert.Equal("잇기", view.FindControl<Button>("AddEdgeButton")!.Content);

        // 문구 드롭다운은 다른 에피소드가 쓰는 문구까지 전부 담는다.
        var labels = view.FindControl<ComboBox>("EdgeLabelBox")!;
        Assert.Contains("라루의 제안을 듣는다", (IEnumerable<string>)labels.ItemsSource!);

        labels.SelectedItem = "라루의 제안을 듣는다";
        view.FindControl<ComboBox>("EdgeTargetCombo")!.SelectedItem = "main05.end";
        view.SubmitEdgeForm();

        ChapterGraphModel reread = Chapter(session);
        ChapterEdge wired = reread.Edges.Single(candidate =>
            candidate.FromEpisodeId == "main05.01" && candidate.ToEpisodeId == "main05.end");
        Assert.Equal("라루의 제안을 듣는다", wired.OptionLabel);

        // 같은 문구를 두 에피소드가 쓰고 있어도 사전에는 한 줄뿐이다.
        Assert.Single(reread.ChoiceOptions, option => option.Text == "라루의 제안을 듣는다");
    });

    [Fact]
    public void 선택지_줄을_클릭하면_그_아래에서_문구와_도착을_고친다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ChapterEdge target = Edge(session, "main05.02", "branch05.02A");

        view.OpenEdgeForm(target, rowIndex: 0);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<Grid>("EdgeFormPanel")!.IsVisible);
        Assert.Equal("수정", view.FindControl<Button>("AddEdgeButton")!.Content);
        Assert.Equal("라루의 제안을 듣는다", view.FindControl<ComboBox>("EdgeLabelBox")!.SelectedItem);

        // 문구를 바꿔 저장 — 도착은 그대로다.
        view.FindControl<ComboBox>("EdgeLabelBox")!.SelectedItem = "혼자 문을 연다";
        view.SubmitEdgeForm();

        ChapterEdge moved = Chapter(session).Edges.Single(edge => edge.ToEpisodeId == "branch05.02A");
        Assert.Equal("혼자 문을 연다", moved.OptionLabel);
        Assert.Equal("신뢰높음", moved.ConditionLabel); // 관문은 길에 붙어 따라온다
    });

    [Fact]
    public void 에피소드를_더하고_개명하면_챕터에_남는다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.AddEpisodeFromToolbar();

        ChapterGraphModel afterAdd = Chapter(session);
        Assert.NotNull(afterAdd.FindEpisode("new01"));

        // 다시 그린 판에서 골라 개명한다 — 자리표시 Id를 사람이 정한 이름으로.
        view.SelectEpisode("new01");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.FindControl<TextBox>("IdBox")!.Text = "main05.05";
        view.RenameSelectedEpisode();

        ChapterGraphModel afterRename = Chapter(session);
        Assert.Null(afterRename.FindEpisode("new01"));
        Assert.NotNull(afterRename.FindEpisode("main05.05"));
    });

    [Fact]
    public void 개명하면_대사_노드가_함께_따라간다() => HeadlessUi.Run(() =>
    {
        // 개명이 챕터만 따라가면 카드가 옛 이름에 남아 원고가 에피소드와 끊긴다
        // (실사례는 2026-08-15 — 그때는 옛 이름의 대본 워크북이 원고를 든 채 남았다).
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.AddEpisodeFromToolbar(); // new01 + 카드
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 대사 노드는 에피소드를 더한 그 자리에서 함께 섰다 — <c>AddNextEpisode</c>가 카드까지 만든다.
        Assert.Contains(session.Project.EnumerateNodes().OfType<DialogueNode>(),
            node => node.MarkedEpisodeId == "new01");

        view.SelectEpisode("new01");
        view.FindControl<TextBox>("IdBox")!.Text = "ep_renamed";
        view.RenameSelectedEpisode();

        // 대사 노드도 새 이름이다 — 새로 만들지 않고 이름만 바꿔 연출·신원이 보존된다.
        // 표식(MarkedEpisodeId)도 함께 간다 — 옛 Id로 남으면 시나리오 그래프가
        // 챕터 밖 노드로 보고 레일을 끊는다.
        Assert.Contains(session.Project.EnumerateNodes().OfType<DialogueNode>(),
            node => node.Name == "ep_renamed" && node.MarkedEpisodeId == "ep_renamed");
        Assert.DoesNotContain(session.Project.EnumerateNodes().OfType<DialogueNode>(),
            node => node.Name == "new01");
    });

    /// <summary>
    /// 2026-08-22 소유자 보고 — *"챕터그래프에서 에피소드를 추가했는데 그게 즉각적으로
    /// 연출그래프에 반영이 안 돼. 노드를 더블클릭해서 엑셀을 열어서 엑셀파일을
    /// 생성시켜야지 그제야 노드가 추가되는 버그."*
    ///
    /// 그때는 노드를 세우는 것이 대본 동기화뿐이었다. 2026-09-18부터는 에피소드를 만드는
    /// 명령(<c>AddEpisode</c>·<c>AddNextEpisode</c>)이 <b>카드까지 함께</b> 만든다 — 기다릴 것이 없다.
    /// </summary>
    [Fact]
    public void 에피소드를_더하면_그_자리에서_대사_노드가_선다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        Assert.DoesNotContain(session.Project.EnumerateNodes().OfType<DialogueNode>(),
            node => node.MarkedEpisodeId == "new01");

        view.AddEpisodeFromToolbar();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs(); // QueueReload 한 차례

        // 더한 그 자리에서 노드가 서 있다 — 에피소드와 카드가 한 변경이다.
        DialogueNode node = Assert.Single(
            session.Project.EnumerateNodes().OfType<DialogueNode>(),
            item => item.MarkedEpisodeId == "new01");

        // 첫 대사도 함께 심어져 있다 (2026-08-26 소유자) — 빈 대본은 검증이 오류로
        // 막으므로, 방금 더한 에피소드가 그 오류부터 들고 시작하지 않는다.
        Assert.True(
            node.Script is { } document &&
            document.ActiveLines.Any(),
            "새 에피소드의 대사 노드에 기본 대사 한 줄이 있어야 한다");
    });

    [Fact]
    public void 선택한_노드의_자식으로_에피소드가_생기고_간선이_이어진다() => HeadlessUi.Run(() =>
    {
        // v3 — [＋ 에피소드]는 선택된 노드의 다음으로 만든다. 흐름 연결이 기본값이다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEpisode("main05.end");
        view.AddEpisodeFromToolbar();

        ChapterGraphModel reread = Chapter(session);
        ChapterEpisode added = reread.FindEpisode("new01")!;

        // 간선이 함께 이어졌고, 대사엔트리는 EpisodeId로 자동이다.
        Assert.Contains(reread.Edges, edge =>
            edge.FromEpisodeId == "main05.end" && edge.ToEpisodeId == "new01");
        Assert.Equal("new01", added.DialogueEntry);

        // 새 노드가 선택되어 바로 [개명]할 수 있다 — 판을 다시 세워도 선택이 남는다.
        view.Refresh();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("new01", view.FindControl<TextBox>("IdBox")!.Text);
    });

    [Fact]
    public void 탭_순서는_편집_화자_챕터다() => HeadlessUi.Run(() =>
    {
        // 2026-08-24 소유자 "편집/화자/챕터가 되도록 챕터를 가장 뒤에 보내" — [편집]은
        // 고른 하나를 다루는 자리라 첫째이고, [챕터]는 판 전체의 읽기 전용 표라 가장 뒤다.
        //
        // ⚠ 첫 화면은 순서가 아니라 `SelectedItem`이 정한다 — 아래 짝 테스트가 그것을
        //    따로 붙든다. 둘을 한 테스트에 묶으면 순서를 바꾸는 날 무엇이 깨졌는지 흐려진다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        string[] headers = view.FindControl<TabControl>("RightTabs")!
            .Items.OfType<TabItem>()
            .Select(tab => (string)tab.Header!)
            .ToArray();

        Assert.Equal(["편집", "화자", "챕터"], headers);
    });

    [Fact]
    public void 툴바에서_다시_읽기와_픽스처가_사라졌다() => HeadlessUi.Run(() =>
    {
        // 2026-08-24 소유자 "꽤 오래 다뤘는데, 단 한 번도 안 썼어."
        //
        // [다시 읽기]는 감시자가 선 뒤로 할 일이 없었고(화면이 스스로 따라온다), 픽스처
        // 콤보는 2026-08-16부터 숨어 있기만 했다. ⚠ 챕터의 픽스처(<c>ChapterDocument.Fixtures</c>)와
        // `ChapterFixtureWalker`는 안 지웠다 — 화면만 걷었다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.Null(view.FindControl<Button>("ReloadButton"));
        Assert.Null(view.FindControl<ComboBox>("FixtureCombo"));
        Assert.Null(view.FindControl<TextBlock>("FixtureStopText"));
        // [폴더 열기]도 2026-08-26에 사라졌다 (소유자).
        Assert.Null(view.FindControl<Button>("OpenFolderButton"));

        // 남아야 하는 이웃 — 지우다 같이 쓸려 나가면 여기서 걸린다.
        Assert.NotNull(view.FindControl<Button>("AddEpisodeButton"));
        Assert.NotNull(view.FindControl<Button>("DeleteEpisodeButton"));
    });

    [Fact]
    public void 스탯_안내는_조건과_같은_말투로_어디서_고치는지_말한다() => HeadlessUi.Run(() =>
    {
        // 두 표가 같은 탭에 나란히 서 있으니 "어디서 고치나"의 대답도 같은 모양이어야
        // 눈이 한 번에 읽는다 (2026-08-24 소유자).
        //
        // ⚠ <b>대답이 2026-09-17에 바뀌었다</b>: *"챕터 엑셀의 시트에서"* → <b>여기서</b>.
        //    같은 말투여야 한다는 규칙은 그대로고, 가리키는 곳만 옮겨 왔다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        List<string> lines = view.FindControl<StackPanel>("ConditionSection")!
            .Children.OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(lines, text => text.Contains("엑셀", StringComparison.Ordinal));
        Assert.Contains(lines, text => text.Contains("여기서 고칩니다", StringComparison.Ordinal));
        Assert.Contains(lines, text => text.Contains("여기서 만들고 고칩니다", StringComparison.Ordinal));
    });

    [Fact]
    public void 처음_보이는_탭은_편집이고_선택이_탭을_끌고_다니지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-08-16 소유자 — 처음 열었을 때 손이 가는 곳은 지금 고른 하나(편집)이지
        // 챕터 전체 표가 아니다. 그리고 무언가를 골라도 보던 탭이 유지된다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        var tabs = view.FindControl<TabControl>("RightTabs")!;
        var conditionTab = view.FindControl<TabItem>("ConditionTab")!;
        var editTab = view.FindControl<TabItem>("EditTab")!;

        Assert.Same(editTab, tabs.SelectedItem);

        // 노드를 골라도 탭은 그대로다.
        view.SelectEpisode("main05.02");
        Assert.Same(editTab, tabs.SelectedItem);

        // 챕터 탭을 보며 다른 노드로 갈아타도 그대로다.
        tabs.SelectedItem = conditionTab;
        view.SelectEpisode("main05.01");
        Assert.Same(conditionTab, tabs.SelectedItem);

        // 간선을 골라도 마찬가지다.
        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Assert.Same(conditionTab, tabs.SelectedItem);
    });

    [Fact]
    public void 대사는_선택지_아래에_펼쳐져_있다() => HeadlessUi.Run(() =>
    {
        // 2026-08-16 소유자 — 별도 탭이던 대사를 편집 탭의 선택지 아래로 합쳤다
        // ("에피소드 노드의 정보량이 부족하다"). 접었다 펼 수 있다.
        //
        // 2026-08-24 소유자 "대사 미리보기는, 펼쳐둔 걸 디폴트로 보이도록 해줘" — 시작이
        // 뒤집혔다. ⚠ 삼각형·본문·체크 셋이 <b>함께</b> 맞아야 한다. XAML에서 체크만 켜면
        // 핸들러가 붙기 전이라 삼각형이 ▸로 남는다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.Null(view.FindControl<TabItem>("DialogueTab"));       // 탭은 사라졌다
        Assert.Null(view.FindControl<Expander>("DialogueExpander")); // 탭 속 상자도 사라졌다

        var toggle = view.FindControl<ToggleButton>("DialogueToggle")!;
        Assert.True(toggle.IsChecked);              // 펼친 채로 시작한다
        Assert.Equal("▾  대사", toggle.Content);    // 삼각형도 같이 뒤집혔다

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 고르자마자 그 에피소드의 대사가 서 있다 — 펼치는 손이 필요 없다.
        Assert.True(view.FindControl<SelectableTextBlock>("DialoguePreviewText")!.IsVisible);
        Assert.False(string.IsNullOrEmpty(view.FindControl<TextBlock>("DialoguePreviewHeader")!.Text));

        // 접는 길도 그대로다.
        toggle.IsChecked = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(view.FindControl<SelectableTextBlock>("DialoguePreviewText")!.IsVisible);
        Assert.Equal("▸  대사", toggle.Content);
    });

    [Fact]
    public void 대사는_한_덩어리로_흐르고_상자를_겹치지_않는다() => HeadlessUi.Run(() =>
    {
        // 2026-08-16 소유자 재지시 — 줄마다 카드로 가르니 읽기가 더 나빠졌다(철회).
        // 대사는 한 덩어리 글이고 화자는 줄 앞에 붙는다. 겹친 상자(탭 속 Expander·
        // 테두리·안쪽 스크롤)도 걷어냈다 — 접기는 제목 토글 하나가 맡는다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        // 미리보기의 원천은 카드의 대본이다 — 대사 한 줄과 화자 없는 줄 하나를 적어 둔다.
        WriteLines(session, "main05.02", ("윌로", "첫 줄"), ("", "라루의 제안을 듣는다"));

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 줄 카드 패널은 사라졌다.
        Assert.Null(view.FindControl<StackPanel>("DialoguePreviewPanel"));

        var text = view.FindControl<SelectableTextBlock>("DialoguePreviewText")!;
        Assert.Contains("윌로: 첫 줄", text.Text!);

        // 접기 토글 하나가 제목이자 스위치다 — 펼친 채로 시작한다(2026-08-24).
        var toggle = view.FindControl<ToggleButton>("DialogueToggle")!;
        Assert.True(toggle.IsChecked);
        Assert.True(text.IsVisible);

        toggle.IsChecked = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(text.IsVisible);
    });

    [Fact]
    public void 간선을_선택하면_패널이_차고_적용이_간선에_써진다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");

        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.False(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        // 문구 칸에 그 길의 문구가 선다.
        Assert.Contains("라루의 제안을 듣는다",
            (string)view.FindControl<ComboBox>("EdgeLabelEditBox")!.SelectedItem!);
        Assert.Equal("신뢰높음", view.FindControl<ComboBox>("EdgeConditionCombo")!.SelectedItem);

        // 안내문을 고쳐 적용 → 간선에 써진다.
        //
        // ⛔ [해금 미달이면 숨김] 체크는 2026-08-24에 없앴다 (소유자) — 해금조건에 숨김을
        //    더한 것은 그 식을 **표시조건**에 적은 것과 결과가 같다. 같은 말이 두 번 있었다.
        Assert.Null(view.FindControl<CheckBox>("EdgeHideCheck"));

        view.FindControl<TextBox>("EdgeLockedMsgBox")!.Text = "신뢰를 더 쌓아야 한다";
        view.ApplyEdgeFromPanel();

        ChapterEdge edge = Edge(session, "main05.02", "branch05.02A");

        Assert.Equal("신뢰를 더 쌓아야 한다", edge.LockedMessage);
        Assert.Equal("신뢰높음", edge.ConditionLabel);  // 안 바꾼 필드는 그대로
    });

    [Fact]
    public void 선택지_포트는_카드_아래에_가로로_선다() => HeadlessUi.Run(() =>
    {
        // ⛔ <b>v4까지 포트는 카드 오른변이었다</b>. 연출 그래프는 반대(선택지가 아래, 분기가
        //    오른쪽)였고, 소유자가 2026-09-18에 그것을 짚었다: *"아래쪽으로 고정된 점 3개를
        //    가로로 두고, 그 포트가 노드위쪽의 점 하나로 출입이 되도록."*
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        // 셋째 길을 하나 더 낸다.
        session.Editor.AddEdge("ch05", "main05.02", "main05.end", optionLabel: "셋째 길");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;

        // 문구는 간선 한가운데로 옮겼다 — 아래변의 한 칸에 남는 폭이 카드의 1/4뿐이다.
        List<Border> labels = canvas.Children.OfType<Border>()
            .Where(border => border.Child is TextBlock text &&
                             (text.Text ?? "").StartsWith("라루의 제안을 듣는다") ||
                             (border.Child as TextBlock)?.Text is "혼자 문을 연다" or "셋째 길")
            .ToList();

        Assert.Equal(3, labels.Count);

        // 칸은 <b>언제나 셋 이상</b>이다 — 빈 칸도 선다(고정된 점 3개). 여기에 들어오는
        // 길이 모이는 위변의 점 하나가 카드마다 더 붙는다.
        //
        // 견본 `ch05`는 에피소드 6장이고 어느 것도 나가는 길이 셋을 넘지 않는다 →
        // 칸 6×3 = 18, 입구 6 = 24. ⚠ 넷을 넘기면 넷을 다 그린다(3은 상한이 아니다).
        Assert.Equal(
            24, canvas.Children.OfType<Avalonia.Controls.Shapes.Ellipse>().Count(port => port.Width == 9));

        // 라벨 클릭 = 그 길 선택 (포트 점도 같은 길로 간다).
        Press(labels.Single(border => ((TextBlock)border.Child!).Text!.StartsWith("라루의 제안을 듣는다")));
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        Press(labels.Single(border => ((TextBlock)border.Child!).Text == "셋째 길"));
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        static void Press(Control control) => control.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            control, new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            control, default, 0,
            new Avalonia.Input.PointerPointProperties(
                Avalonia.Input.RawInputModifiers.LeftMouseButton,
                Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
            Avalonia.Input.KeyModifiers.None));
    });

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>프로젝트가 든 ch05 — 화면이 쓴 것이 남았는지 여기서 다시 읽는다.</summary>
    private static ChapterGraphModel Chapter(AuthoringSession session) =>
        session.Editor.FindChapter("ch05")!.ToGraphModel("ch05");

    private static ChapterEdge Edge(AuthoringSession session, string from, string to) =>
        Chapter(session).Edges.Single(edge => edge.FromEpisodeId == from && edge.ToEpisodeId == to);

    /// <summary>
    /// 그 에피소드 카드에 대사를 줄마다 적는다 — 작가가 카드에서 하는 일. 첫 줄은 카드가 설 때
    /// 함께 선 빈 줄이다.
    ///
    /// ⚠ 붙여넣기(<c>ApplyScenarioText</c>)로 한 번에 넣지 않는다 — 그 빈 줄이 고쳐진 것인지
    /// 사라진 것인지 판정이 서지 않아 붙여넣기가 통째로 멈춘다.
    /// </summary>
    private static void WriteLines(
        AuthoringSession session, string episodeId, params (string Speaker, string Text)[] lines)
    {
        DialogueNode card = EpisodeNaming.CardFor(session.Project, "ch05", episodeId)!;

        for (int index = 0; index < lines.Length; index++)
        {
            string lineId = index == 0
                ? card.Script.ActiveLines.First().Id
                : session.Editor.InsertScriptLine(card.Id).Id;

            session.Editor.SetScriptLineText(card.Id, lineId, lines[index].Speaker, lines[index].Text);
        }

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

        // 이 창은 테스트가 끝날 때 TempProject가 닫는다 — 안 닫으면 어셈블리가 나눠 쓰는
        // 디스패처에 뷰가 살아남아 다음 테스트를 흔든다.
        project.Ui.Own(view, window);

        return (view, session);
    }

    /// <summary>견본 챕터(ch05 복도)를 든 프로젝트. 판과 카드는 열 때 선다.</summary>
    private sealed class TempProject : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "vn-chapter-editing", Guid.NewGuid().ToString("N"));

        /// <summary>이 테스트가 띄운 화면. 폴더를 지우기 <b>전에</b> 닫는다.</summary>
        public OpenChapterViews Ui { get; } = new();

        public TempProject()
        {
            // 실제 프로젝트의 모양 — 시나리오 파일이 하나는 있다.
            ManifestPath = SampleProject.Corridor(_directory, "편집 검증", project =>
                project.Files.Add(new StoryFile("sf_main", "본편", "story/main.vnstory.json")));
        }

        public string ManifestPath { get; }

        public void Dispose()
        {
            // 순서가 규칙이다 — 화면을 먼저 닫고 폴더를 지운다.
            Ui.CloseAll();

            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
