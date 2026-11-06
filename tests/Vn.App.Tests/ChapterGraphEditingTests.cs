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
/// 그래프 편집이 프로젝트의 챕터에 써진다. 패널의 [적용]·[개명]·간선·조건·＋에피소드가
/// 전부 <c>ChapterDocument</c>에 남고, 모델을 다시 읽으면 그대로 나온다.
/// </summary>
public sealed class ChapterGraphEditingTests
{
    [Fact]
    public void 길의_관문_콤보가_간선에_써진다() => HeadlessUi.Run(() =>
    {
        // 보일지 말지는 간선이 정한다. 표시·해금조건은 에피소드가 아니라 길(간선)에 있고,
        // 편집도 간선 패널에서 한다.
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
    public void 간선_패널의_선택지_문구는_글자_칸에_적혀_있고_거기서_고친다() => HeadlessUi.Run(() =>
    {
        // 문구는 드롭다운으로 고르지 않는다 — 글자 칸에 지금 문구가 적혀 있고 그 자리에서 고친다. 저장은 관문과 함께 한 번이다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var labels = view.FindControl<TextBox>("EdgeLabelEditBox")!;
        Assert.True(labels.IsEnabled); // 읽기 전용이 아니다 — 여기서 고친다
        Assert.Equal("라루의 제안을 듣는다", labels.Text);

        labels.Text = "혼자 문을 연다";
        view.FindControl<ComboBox>("EdgeVisibleCombo")!.SelectedItem = "지쳐있음";
        view.ApplyEdgeFromPanel();

        ChapterEdge reread = Edge(session, "main05.02", "branch05.02A");

        Assert.Equal("혼자 문을 연다", reread.OptionLabel); // 문구가 한 저장에 함께 실렸다
        Assert.Equal("지쳐있음", reread.VisibleConditionLabel);
        Assert.Equal("신뢰높음", reread.ConditionLabel);

        // 문구는 신원의 일부다 — 바꿔도 선택이 풀리지 않고 그 길이 계속 열려 있다.
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.Equal("혼자 문을 연다", view.FindControl<TextBox>("EdgeLabelEditBox")!.Text);
    });

    [Fact]
    public void 간선_패널의_문구는_Enter로_저장되고_다른_것을_고르면_적던_문구가_그_간선에_저장된다() => HeadlessUi.Run(() =>
    {
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Enter — 그 자리에서 저장된다(사전에 없던 새 문구도 된다).
        var labels = view.FindControl<TextBox>("EdgeLabelEditBox")!;
        labels.Text = "새로 적은 문구";
        labels.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Enter
        });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("새로 적은 문구", Edge(session, "main05.02", "branch05.02A").OptionLabel);

        // 적다가(초점이 칸에 있다) 판에서 다른 간선을 고르면 — 적던 문구는 원래 간선에 들어가고 새 간선은 그대로다.
        labels.Focus();
        labels.Text = "고르기 전에 적은 문구";
        ChapterEdge other = Chapter(session).Edges.First(edge =>
            edge.FromEpisodeId != "main05.02" && !edge.HasNoOptionLabel);
        view.SelectEdgeKey(other.FromEpisodeId, other.ToEpisodeId, other.OptionLabel!);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("고르기 전에 적은 문구", Edge(session, "main05.02", "branch05.02A").OptionLabel);
        Assert.Equal(other.OptionLabel, Edge(session, other.FromEpisodeId, other.ToEpisodeId).OptionLabel);
        Assert.Equal(other.OptionLabel, labels.Text);   // 칸은 새로 고른 간선의 문구다
    });

    [Fact]
    public void 에피소드를_지워도_챕터의_선택지_문구는_남는다() => HeadlessUi.Run(() =>
    {
        // v9 — 사전은 챕터 전체의 어휘다. 에피소드 하나가 사라졌다고 낱말을 지우면
        // 다른 에피소드의 드롭다운에서도 사라진다.
        using var project = new TempProject();
        (ChapterGraphView _, AuthoringSession session) = Show(project);

        Assert.True(EpisodeDeleter.Delete(session.Editor, "ch05", "branch05.02A").Ok);

        ChapterGraphModel reread = Chapter(session);

        Assert.Null(reread.FindEpisode("branch05.02A"));
        Assert.DoesNotContain(reread.Edges, edge => edge.ToEpisodeId == "branch05.02A");
        Assert.Contains(reread.ChoiceOptions, option => option.Text == "라루의 제안을 듣는다");
    });

    [Fact]
    public void 에피소드를_지우면_연출_그래프의_빈_노드도_함께_지운다() => HeadlessUi.Run(() =>
    {
        // 챕터에서만 지우면 판에 <b>사라진 에피소드를 자기라고 주장하는 유령</b>이 남고,
        //    같은 Id로 다시 만들면 같은 이름의 노드가 둘이 되어 내보내기가 통째로 막힌다
        //    ("번들 이름이 겹칩니다").
        using var project = new TempProject();
        (ChapterGraphView _, AuthoringSession session) = Show(project);

        // 판과 카드는 프로젝트를 열 때 이미 섰다(AlignBoardsWithChapters) — 여기서 또 만들면
        // 같은 이름이 둘이 되어 실물이 아니라 사본을 재게 된다.
        DialogueNode ghost = session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => node.MarkedEpisodeId == "branch05.02A");

        // 대본은 딸려 있어도 적힌 글이 없으면 빈 카드다 — 아무도 손대지 않은 자리다.
        Vn.Authoring.Script.ScriptDocument blank = ghost.Script;
        Assert.DoesNotContain(blank.ActiveLines, line => blank.Text(line.Id).Text.Length > 0);

        Assert.True(EpisodeDeleter.Delete(session.Editor, "ch05", "branch05.02A").Ok);

        Assert.Null(session.Project.FindNode(ghost.Id));
    });

    [Fact]
    public void 글이_든_노드도_함께_지우고_되돌리기로_돌아온다() => HeadlessUi.Run(() =>
    {
        // 글이 든 카드도 <c>(떼어냄)</c>으로 이름만 바꿔 판에 남기지 않고 지운다 — 판 편집에도
        //    Ctrl+Z가 있어 되돌릴 자리가 있다.
        //
        // 글은 그래도 안 사라진다 — 카드가 대본을 들고 있어 되돌리면 카드와 함께 돌아온다.
        using var project = new TempProject();
        (ChapterGraphView _, AuthoringSession session) = Show(project);

        DialogueNode written = session.Project.EnumerateNodes().OfType<DialogueNode>()
            .Single(node => node.MarkedEpisodeId == "branch05.02A");

        var script = new Vn.Authoring.Script.ScriptDocument();
        script.Lines.Add(new Vn.Authoring.Script.ScriptLine("ln_keep"));
        script.RequireLocale(script.PrimaryLocale).Entries["ln_keep"] =
            new Vn.Authoring.Script.LocalizedLine("윌로", "잃으면 안 되는 줄");
        written.Script = script;

        Assert.True(EpisodeDeleter.Delete(session.Editor, "ch05", "branch05.02A").Ok);

        // 카드가 <b>사라진다</b> — `(떼어냄)`이 판에 남지 않는다.
        Assert.Null(session.Project.FindNode(written.Id));
        Assert.DoesNotContain(
            session.Project.EnumerateNodes(),
            node => node.Name.Contains("떼어냄", StringComparison.Ordinal));

        session.Editor.Undo();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 되돌리면 카드도 에피소드도 돌아오고, 글도 카드에 든 채 돌아온다.
        DialogueNode back = Assert.IsType<DialogueNode>(session.Project.FindNode(written.Id));
        Assert.Equal("잃으면 안 되는 줄", back.Script.Text("ln_keep").Text);
    });

    [Fact]
    public void 편집이_열려_있다() => HeadlessUi.Run(() =>
    {
        // 편집은 열린 채로 시작한다 — 닫을 길(잠금) 자체가 없다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<TextBox>("IdBox")!.IsEnabled);
    });

    [Fact]
    public void 열려_있을_때는_이름_칸이_아무_말도_안_한다() => HeadlessUi.Run(() =>
    {
        // 이름 칸 곁에 "이름 — 고치고 Enter" 같은 상시 설명을 두지 않는다. 개명 자리인 줄
        // 알리는 몫은 툴팁이 진다.
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
        // 머리글 "에피소드", 읽기 전용 값 줄(`대사엔트리: … · 엑셀 N행`), "길 하나 = 선택지
        // 하나…" 셋은 두지 않는다. 고칠 수도 없는 글이 상시로 자리를 먹는다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);
        view.SelectEpisode("main05.01");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(view.FindControl<TextBlock>("EpisodeFactsText"));

        string[] gone =
        [
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

        // 머리글 「에피소드」 — 칸 이름표 「에피소드ID」는 머리글이 아니라 무슨 칸인지 말하는 이름표다.
        Assert.DoesNotContain("에피소드", standing);

        // 남아야 하는 것 — 선택지 머리글과 칸 이름표는 표의 이름표라 안내가 아니다.
        Assert.Contains(standing, text => text.Contains("선택지", StringComparison.Ordinal));
        Assert.Contains("에피소드ID", standing);
    });

    [Fact]
    public void 선택지_목록에는_길을_더하거나_끊는_단추가_없다() => HeadlessUi.Run(() =>
    {
        // 선택지를 잇고 끊는 것은 [대본] 탭(카드 아래 칸)에서 한다 — 에피소드 칸의 [＋]와 줄마다의 ✕, 빈 칸 패널의 [＋]는 없다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(view.FindControl<Button>("AddNextEdgeButton"));
        Assert.Null(view.FindControl<Button>("SlotAddChoiceButton"));
        Assert.DoesNotContain(
            view.FindControl<StackPanel>("EdgeListPanel")!.GetVisualDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible && button.Content as string == "✕");
    });

    [Fact]
    public void 간선_패널에는_길_끊기와_자동_진행_체크가_없고_자동_길이면_그렇다고_말하고_칸을_잠근다() => HeadlessUi.Run(() =>
    {
        // 길을 잇고 끊고 자동으로 바꾸는 것은 [대본] 탭의 일이다 — [구성] 탭 간선 패널은 문구 · 조건 · 스탯변화만 고친다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(view.FindControl<Button>("EdgeDeleteButton"));
        Assert.Null(view.FindControl<CheckBox>("EdgeAutoCheck"));
        Assert.False(view.FindControl<TextBlock>("EdgeAutoNote")!.IsVisible);
        Assert.True(view.FindControl<TextBox>("EdgeLabelEditBox")!.IsEnabled);

        // 자동 진행 길 — 안내가 서고 문구 · 조건 칸이 잠긴다. 저장해도 자동 그대로다.
        session.Editor.UpdateEdge("ch05", "main05.01", "main05.02", matchOptionLabel: "계속", optionLabel: string.Empty, auto: true);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.SelectEdgeKey("main05.01", "main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<TextBlock>("EdgeAutoNote")!.IsVisible);
        Assert.False(view.FindControl<TextBox>("EdgeLabelEditBox")!.IsEnabled);
        Assert.False(view.FindControl<ComboBox>("EdgeConditionCombo")!.IsEnabled);

        view.ApplyEdgeFromPanel();
        Assert.True(Edge(session, "main05.01", "main05.02").Auto);
    });

    [Fact]
    public void 선택지는_챕터의_모든_문구_중에서_고른다() => HeadlessUi.Run(() =>
    {
        // 제 것만이 아니라 챕터의 모든 선택지 문구 중에서 고른다.
        // 견본의 사전에는 main05.02가 쓰는 문구 둘이 있고, main05.01의 길(「계속」)에도 그것을 적는다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEdgeKey("main05.01", "main05.02", "계속");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 문구는 간선 패널의 글자 칸에 적는다 — 다른 에피소드가 쓰는 문구를 그대로 적어도 된다.
        var labels = view.FindControl<TextBox>("EdgeLabelEditBox")!;
        Assert.Equal("계속", labels.Text);

        labels.Text = "라루의 제안을 듣는다";
        view.ApplyEdgeFromPanel();

        ChapterGraphModel reread = Chapter(session);
        ChapterEdge wired = reread.Edges.Single(candidate =>
            candidate.FromEpisodeId == "main05.01" && candidate.ToEpisodeId == "main05.02");
        Assert.Equal("라루의 제안을 듣는다", wired.OptionLabel);

        // 같은 문구를 두 에피소드가 쓰고 있어도 사전에는 한 줄뿐이다.
        Assert.Single(reread.ChoiceOptions, option => option.Text == "라루의 제안을 듣는다");
    });

    [Fact]
    public void 선택지_줄을_누르면_그_아래에_설명이_읽기_전용으로_펼쳐지고_다시_누르면_접힌다() => HeadlessUi.Run(() =>
    {
        // 에피소드 칸은 확인하는 자리다 — 펼친 설명에는 고칠 칸이 없다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        view.SelectEpisode("main05.02");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ChapterEdge target = Edge(session, "main05.02", "branch05.02A");
        view.ToggleChoice(target);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Border detail = Detail(view)!;
        List<string?> lines = detail.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("해금조건: 신뢰높음", lines);
        Assert.Contains(lines, line => line!.StartsWith("스탯변화: ", StringComparison.Ordinal));

        // 문구와 도착은 바로 위 줄이 보인다 — 설명이 되풀이하지 않는다.
        Assert.DoesNotContain(lines, line => line!.StartsWith("문구: ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line!.StartsWith("도착: ", StringComparison.Ordinal));

        // 고칠 칸이 없다 — 글자 칸 · 고르기 · 체크 · 단추 모두.
        Assert.DoesNotContain(view.FindControl<StackPanel>("EdgeListPanel")!.GetVisualDescendants(),
            control => control is TextBox or ComboBox or CheckBox or Button);
        Assert.Null(view.FindControl<TextBox>("EdgeLabelBox"));
        Assert.Null(view.FindControl<ComboBox>("EdgeTargetCombo"));

        // 다시 누르면 접힌다.
        view.ToggleChoice(target);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Null(Detail(view));
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
        // 개명이 챕터만 따라가면 카드가 옛 이름에 남아 원고가 에피소드와 끊긴다.
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
    /// 챕터 그래프에서 더한 에피소드는 곧바로 연출 그래프에 선다. 에피소드를 만드는
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

        // 첫 대사도 함께 심어져 있다 — 빈 대본은 검증이 오류로
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
    public void 탭은_없고_인스펙터에는_칸_하나만_선다() => HeadlessUi.Run(() =>
    {
        // 우측 기둥은 하이어아키 + 인스펙터다. [편집 | 화자 | 챕터] 같은 탭은 두지 않는다:
        // 고르는 것이 무엇을 보일지 정한다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.Null(view.FindControl<TabControl>("RightTabs"));
        Assert.Single(Sections(view), section => section.IsVisible);

        // 처음 고른 것은 챕터다 — 하이어아키의 챕터 줄을 고른 것과 같아 조건 · 스탯이 선다.
        Assert.True(view.FindControl<ScrollViewer>("ChapterSection")!.IsVisible);
        Assert.Equal("챕터 · ch05", view.FindControl<TextBlock>("InspectorTitle")!.Text);
    });

    [Fact]
    public void 툴바에서_다시_읽기와_픽스처가_사라졌다() => HeadlessUi.Run(() =>
    {
        // [다시 읽기]는 감시자가 있어 할 일이 없다(화면이 스스로 따라온다). 픽스처 콤보도
        // 툴바에 두지 않는다. 주의: 챕터의 픽스처(<c>ChapterDocument.Fixtures</c>)와
        // `ChapterFixtureWalker`는 남아 있다 — 화면에만 없다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);

        Assert.Null(view.FindControl<Button>("ReloadButton"));
        Assert.Null(view.FindControl<ComboBox>("FixtureCombo"));
        Assert.Null(view.FindControl<TextBlock>("FixtureStopText"));
        // [폴더 열기]도 없다.
        Assert.Null(view.FindControl<Button>("OpenFolderButton"));

        // [에피소드 삭제]도 없다 — 지우기는 하이어아키의 우클릭 「지우기」 하나다.
        Assert.Null(view.FindControl<Button>("DeleteEpisodeButton"));

        // 남아야 하는 이웃 — 지우다 같이 쓸려 나가면 여기서 걸린다.
        Assert.NotNull(view.FindControl<Button>("AddEpisodeButton"));
    });

    [Fact]
    public void 스탯_안내는_조건과_같은_말투로_어디서_고치는지_말한다() => HeadlessUi.Run(() =>
    {
        // 두 표가 같은 탭에 나란히 서 있으니 "어디서 고치나"의 대답도 같은 모양이어야
        // 눈이 한 번에 읽는다. 대답은 <b>여기서</b>다.
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
    public void 고른_것이_인스펙터를_정한다() => HeadlessUi.Run(() =>
    {
        // 하이어아키 + 인스펙터 방식 — 판에서 고른 에피소드 · 길은 그 편집을, 하이어아키의 프로젝트 ·
        // 챕터 줄은 화자 · 조건과 스탯을 보인다. 머리글이 무엇을 보는지 말한다.
        using var project = new TempProject();
        (ChapterGraphView view, _) = Show(project);
        TextBlock title = view.FindControl<TextBlock>("InspectorTitle")!;

        view.SelectEpisode("main05.02");
        Assert.True(view.FindControl<ScrollViewer>("SelectionSection")!.IsVisible);
        Assert.True(view.FindControl<StackPanel>("PropertyPanel")!.IsVisible);
        Assert.Equal("조용한 복도", title.Text);   // 「장면 - 제목」 — 장면ID가 없으면 제목만

        view.Inspect(HierarchyPick.Project);
        Assert.True(view.FindControl<ScrollViewer>("SpeakerSection")!.IsVisible);
        Assert.Equal("화자", title.Text);

        view.Inspect(new HierarchyPick(HierarchyKind.Chapter, "ch05"));
        Assert.True(view.FindControl<ScrollViewer>("ChapterSection")!.IsVisible);

        view.SelectEdgeKey("main05.02", "branch05.02A", "라루의 제안을 듣는다");
        Assert.True(view.FindControl<ScrollViewer>("SelectionSection")!.IsVisible);
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);
        Assert.StartsWith("선택지 · main05.02 → branch05.02A", title.Text, StringComparison.Ordinal);
        Assert.Single(Sections(view), section => section.IsVisible);
    });

    private static IEnumerable<ScrollViewer> Sections(ChapterGraphView view) =>
        new[] { "SelectionSection", "SpeakerSection", "ChapterSection" }
            .Select(name => view.FindControl<ScrollViewer>(name)!);

    [Fact]
    public void 대사는_선택지_아래에_펼쳐져_있다() => HeadlessUi.Run(() =>
    {
        // 대사는 별도 탭이 아니라 편집 탭의 선택지 아래에 있다. 접었다 펼 수 있다.
        //
        // 대사 미리보기는 펼친 채로 시작한다. 주의: 삼각형·본문·체크 셋이 <b>함께</b> 맞아야
        // 한다. XAML에서 체크만 켜면 핸들러가 붙기 전이라 삼각형이 ▸로 남는다.
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
        // 줄마다 카드로 가르면 읽기가 나빠진다.
        // 대사는 한 덩어리 글이고 화자는 줄 앞에 붙는다. 겹친 상자(탭 속 Expander·
        // 테두리·안쪽 스크롤)도 두지 않는다 — 접기는 제목 토글 하나가 맡는다.
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

        // 접기 토글 하나가 제목이자 스위치다 — 펼친 채로 시작한다.
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
        Assert.Contains("라루의 제안을 듣는다", view.FindControl<TextBox>("EdgeLabelEditBox")!.Text);
        Assert.Equal("신뢰높음", view.FindControl<ComboBox>("EdgeConditionCombo")!.SelectedItem);

        // 안내문을 고쳐 적용 → 간선에 써진다.
        //
        // [해금 미달이면 숨김] 체크는 없다 — 해금조건에 숨김을
        //    더한 것은 그 식을 **표시조건**에 적은 것과 결과가 같다. 같은 말이 두 번 있게 된다.
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
        // 연출 그래프와 같이 선택지 포트는 카드 아래쪽에 고정된 점 3개를 가로로 두고,
        //    들어오는 길은 카드 위쪽의 점 하나로 드나든다.
        using var project = new TempProject();
        (ChapterGraphView view, AuthoringSession session) = Show(project);

        // 셋째 길을 하나 더 낸다.
        session.Editor.AddEdge("ch05", "main05.02", "main05.end", optionLabel: "셋째 길");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var canvas = view.FindControl<Canvas>("GraphCanvas")!;

        // 문구는 간선 한가운데로 옮겼다 — 아래변의 한 칸에 남는 폭이 카드의 1/4뿐이다.
        List<Border> labels = canvas.Children.OfType<Border>()
            .Where(border => border.Tag is ChapterGraphView.EdgeLabelMark mark &&
                             (mark.Label.StartsWith("라루의 제안을 듣는다", StringComparison.Ordinal) ||
                              mark.Label is "혼자 문을 연다" or "셋째 길"))
            .ToList();

        Assert.Equal(3, labels.Count);

        // 칸은 <b>언제나 셋 이상</b>이다 — 빈 칸도 선다(고정된 점 3개). 여기에 들어오는
        // 길이 모이는 위변의 점 하나가 카드마다 더 붙는다.
        //
        // 견본 `ch05`는 에피소드 6장이고 어느 것도 나가는 길이 셋을 넘지 않는다 →
        // 칸 6×3 = 18, 입구 6 = 24. 주의: 넷을 넘기면 넷을 다 그린다(3은 상한이 아니다).
        Assert.Equal(
            24, canvas.Children.OfType<Avalonia.Controls.Shapes.Ellipse>().Count(port => port.Width == 9));

        // 라벨 클릭 = 그 길 선택 (포트 점도 같은 길로 간다).
        Press(labels.Single(border => ((ChapterGraphView.EdgeLabelMark)border.Tag!).Label.StartsWith("라루의 제안을 듣는다", StringComparison.Ordinal)));
        Assert.True(view.FindControl<StackPanel>("EdgePanel")!.IsVisible);

        Press(labels.Single(border => ((ChapterGraphView.EdgeLabelMark)border.Tag!).Label == "셋째 길"));
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
    /// 주의: 붙여넣기(<c>ApplyScenarioText</c>)로 한 번에 넣지 않는다 — 그 빈 줄이 고쳐진 것인지
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
    /// <summary>에피소드 칸의 선택지 목록에 펼쳐진 설명 — 없으면 null.</summary>
    private static Border? Detail(ChapterGraphView view) =>
        view.FindControl<StackPanel>("EdgeListPanel")!.Children.OfType<Border>()
            .SingleOrDefault(border => border.Tag as string == ChapterGraphView.ChoiceDetailTag);

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
