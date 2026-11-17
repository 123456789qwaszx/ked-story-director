using System.Text.Json.Nodes;
using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Rendering;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>「조건 분기」 표식</b> — 분기 하나가 곧 포트 하나 (R7 P-5 · `docs/plans/R7.md`). 한 줄에 여럿일 수 있다(2026-10-29 · `docs/plans/multi-branch.md`).
///
/// ⛔ <b>조건이 아니다.</b> 연출 그래프는 "어디서 갈라지는가"만 짚는다 — 성립하든 말든
/// 다녀오고, 다녀온 자유 씬이 제 첫머리에서 보고 아니면 곧바로 돌아온다.
///
/// ⚠ 갈래(<c>BeginIf</c>…)와 달리 <b>구역이 아니다</b>. 줄 사이에 끼는 카드 한 장이라
/// 여는 전환도 닫는 전환도 없다 — 그래서 전환이 아니라 <b>줄에 붙는 표식</b>이다.
/// </summary>
public sealed class BranchMarkerTests
{
    [Fact]
    public void 표식_하나가_포트_하나를_뚫는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();

        Assert.DoesNotContain(Ports(editor, node), port => port.Kind == ExitPortKind.Detour);

        node.RequireExtension(lineId).DetourTargetNodeIds.Add(Target(editor).Id);

        ExitPort port = Assert.Single(Ports(editor, node), item => item.Kind == ExitPortKind.Detour);

        Assert.Equal(lineId, port.BranchOpenLineId);
        Assert.Equal(Target(editor).Id, port.TargetNodeId);
        Assert.True(port.IsConnected);
    }

    [Fact]
    public void 조건이_없어도_포트가_선다()
    {
        // 갈래는 `ConditionId`가 있어야 열리지만 이 표식은 조건을 아예 안 진다 —
        // 갈지 말지는 다녀온 곳이 정한다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        node.RequireExtension(lineId).DetourTargetNodeIds.Add(Target(editor).Id);

        Assert.DoesNotContain(
            node.LineExtensions.SelectMany(extension => extension.Transitions),
            transition => transition.OpensBranch);

        Assert.Single(Ports(editor, node), item => item.Kind == ExitPortKind.Detour);
    }

    [Fact]
    public void 목표는_그_줄이_지고_갈래_출구_장부에_안_적힌다()
    {
        // ⛔ 갈래 출구 장부(`BranchExits`)에 적으면 <b>여는 전환이 없는 고아</b>가 되고,
        //    그 칸은 저장할 때 전환 안에 실리므로 통째로 사라진다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();

        editor.SetExitTarget(node.Id, ExitPortKind.Detour, lineId, Target(editor).Id);

        Assert.Equal([Target(editor).Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Empty(node.BranchExits);
    }

    [Fact]
    public void 끊으면_표식도_지워진다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        editor.SetExitTarget(node.Id, ExitPortKind.Detour, lineId, Target(editor).Id);

        editor.SetExitTarget(node.Id, ExitPortKind.Detour, lineId, null);

        Assert.Empty(node.FindExtension(lineId)?.DetourTargetNodeIds ?? []);
        Assert.DoesNotContain(Ports(editor, node), port => port.Kind == ExitPortKind.Detour);
    }

    [Fact]
    public void 저장하고_다시_열어도_남는다()
    {
        // 저장이 안 되면 다음에 열었을 때 포트가 통째로 사라진다 — 뚫어 둔 자리가 없어진다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        node.RequireExtension(lineId).DetourTargetNodeIds.Add(Target(editor).Id);

        StoryProject reopened = ProjectSnapshotCodec.Decode(
            ProjectSnapshotCodec.Encode(editor.Project));

        DialogueNode again = reopened.EnumerateNodes().OfType<DialogueNode>()
            .First(item => string.Equals(item.Id, node.Id, StringComparison.Ordinal));

        Assert.Equal([Target(editor).Id], again.FindExtension(lineId)!.DetourTargetNodeIds);
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    private static IReadOnlyList<ExitPort> Ports(ProjectEditor editor, DialogueNode node) =>
        NodeConnections.PortsOf(node, editor.Project);

    private static DialogueNode Target(ProjectEditor editor) =>
        editor.Project.EnumerateNodes().OfType<DialogueNode>()
            .First(node => node.Name == "곁가지");

    /// <summary>대사 한 줄짜리 에피소드 노드와, 다녀올 분기 카드 하나(「곁가지」 — 아직 아무도 부르지 않는다).</summary>
    private static (ProjectEditor Editor, DialogueNode Node, string LineId) World()
    {
        var project = new StoryProject();
        var editor = new ProjectEditor(project);

        editor.EnsureChapter("ch01");
        string fileId = editor.EnsureChapterBoard("ch01");
        editor.AddEpisode("ch01", "root", title: "root", 0, 0);

        DialogueNode node = editor.AddDialogueNode(fileId, 0, 0, "root");
        node.MarkedEpisodeId = "root";

        string lineId = node.Script.ActiveLines.First().Id;

        // 분기 카드는 「분기 추가」로만 선다 — 세우고 분기점만 걷으면 판에 남는다.
        DialogueNode side = editor.AddBranchMarker(node.Id, lineId, name: "곁가지");
        editor.RemoveBranchMarker(node.Id, lineId, side.Id);

        return (editor, node, lineId);
    }

    // ── 대본에 나가는 모양 (R7 P-5, 이미터) ────────────────────────────────

    [Fact]
    public void 표식은_조건_없이_detour_한_줄로_나간다()
    {
        // ⛔ `<<if>>`로 감싸지 않는다 — 연출 그래프는 "어디서 갈라지는가"만 짚고, 갈지
        //    말지는 다녀온 곳이 제 첫머리에서 정한다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        node.RequireExtension(lineId).DetourTargetNodeIds.Add(Target(editor).Id);

        RenderedDocument document = Rendered(editor, node);

        // ⚠ 조각은 노드 <b>Id</b>를 든다 — compositor의 번들 이미터가 제 이름 규칙으로 옮긴다.
        RenderedSegment detour = Assert.Single(
            document.Segments, segment => segment.Kind == RenderedSegmentKind.BranchDetour);
        Assert.Equal(Target(editor).Id, detour.TargetNodeId);
        Assert.DoesNotContain(document.Segments, segment => segment.Kind == RenderedSegmentKind.ConditionBegin);
    }

    [Fact]
    public void 표식이_없으면_아무것도_안_나간다()
    {
        (ProjectEditor editor, DialogueNode node, _) = World();

        Assert.DoesNotContain(
            Rendered(editor, node).Segments, segment => segment.Kind == RenderedSegmentKind.BranchDetour);
    }

    /// <summary>작업 상태를 문서 조각으로 편 것 — 게임용 Yarn은 compositor가 이 조각을 그대로 옮긴다.</summary>
    private static RenderedDocument Rendered(ProjectEditor editor, DialogueNode node) =>
        WorkingDialoguePreview.Compose(editor.Project, node.Id);

    // ── 「분기 추가」 (R7 P-5, 편집 명령) ───────────────────────────────────

    [Fact]
    public void 한_번에_표식과_분기_카드가_함께_선다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();

        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        Assert.Equal([made.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Single(Ports(editor, node), port => port.Kind == ExitPortKind.Detour);

        // 분기 카드다(2026-11-17) — 챕터의 에피소드가 아니다. ⛔ R7 ⑤(2026-09-17)부터 이날까지는 「그냥 에피소드」였다(표식 · 에피소드 목록).
        Assert.True(made.IsBranchCard);
        Assert.Null(made.MarkedEpisodeId);
        Assert.DoesNotContain(
            editor.Project.Chapters.Single().Episodes,
            episode => string.Equals(episode.EpisodeId, made.Name, StringComparison.Ordinal));

        // 같은 판에 선다 — 다녀오는 길이 판을 넘지 않는다.
        Assert.Equal(
            editor.Project.FindFileContainingNode(node.Id)!.Id,
            editor.Project.FindFileContainingNode(made.Id)!.Id);
    }

    [Fact]
    public void 분기_카드는_장면을_저장하지_않는다()
    {
        // 분기 카드의 장면은 부르는 에피소드를 따라 읽는다(판의 장면 테두리 · 대사 Yarn · 장면 파일 — 그 규칙은 투영과 내보내기의 시험이 본다).
        // 저장해 두면 부르는 쪽 장면이 바뀔 때 둘이 갈린다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        editor.UpdateEpisode("ch01", "root", sceneId: "sc_아침");

        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        Assert.DoesNotContain(editor.Project.Chapters.Single().Episodes, episode => episode.EpisodeId == made.Name);
    }

    [Fact]
    public void 분기_카드는_도달성_오류도_경고도_없다()
    {
        // 분기 카드는 에피소드가 아니라 도달성 증명(런타임의 오라클 — 에피소드와 간선만) 밖이다. 도달할 수 있는 에피소드(root — 시작)가
        // 부르므로 분기 카드 검증(ChapterBranchCards)도 말이 없다. ⛔ 2026-11-17 전에는 에피소드로 세우고 `도달불가 허용` 깃발로 오류를 낮췄다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();

        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);
        made.Script.RequireLocale(made.Script.PrimaryLocale).Entries[made.Script.ActiveLines.First().Id] = new LocalizedLine("라루", "들렀다 간다");
        ChapterDocument chapter = editor.Project.Chapters.Single();

        ChapterValidationResult result = ChapterValidator.Validate(chapter.ToGraphModel(chapter.ChapterId), editor.Project);

        Assert.DoesNotContain(result.All, item => item.Message.Contains(made.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void 손으로_세운_카드는_에피소드고_이어지지_않았다고_말한다()
    {
        // 작가가 판에 그냥 세운 카드는 <b>에피소드</b>이고 아직 안 이은 것이므로 짚어 줘야 한다 — 말없이 두면 고아 에피소드가 조용히
        //    쌓인다. 작업 중이라 경고다(EpisodeNotConnected — 2026-11-17). 분기 카드는 「분기 추가」 · 빈 곳에 놓은 분기점으로만 선다.
        (ProjectEditor editor, _, _) = World();

        DialogueNode made = editor.AddDialogueNode(
            editor.Project.Files[0].Id, name: "혼자선카드");
        ChapterDocument chapter = editor.Project.Chapters.Single();

        ChapterDiagnostic said = Assert.Single(
            ChapterValidator.Validate(chapter.ToGraphModel(chapter.ChapterId), editor.Project).All,
            item => item.Code == ChapterDiagnosticCode.EpisodeNotConnected);
        Assert.Equal((made.Name, ChapterDiagnosticSeverity.Warning), (said.EpisodeId, said.Severity));
    }

    [Fact]
    public void 분기_카드에는_선택지_칸이_없다()
    {
        // 분기로 들른 카드의 선택지는 게임에 나오지 않는다 — 런타임은 지금 에피소드의 대사를 끝까지 튼 뒤 지금 에피소드의 선택지를 띄운다
        // (2026-11-17 소유자). ⛔ R7 ⑤(2026-09-17)부터 이날까지는 「에피소드 선택지 3칸 바로 그것」이었다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        Assert.Equal(0, Graph.GraphProjectionBuilder
            .Build(editor.Project, new HashSet<string>(editor.Project.Files.Select(file => file.Id)))
            .Items.OfType<Graph.ExpandedNodeProjection>()
            .Single(item => string.Equals(item.NodeId, made.Id, StringComparison.Ordinal))
            .OutputPorts.Count(port => port.Kind == Graph.GraphOutputPortKind.Choice));
    }

    [Fact]
    public void 되돌리기_한_번에_셋이_함께_돌아간다()
    {
        // ⛔ 갈라 두면 표식만 사라지고 <b>아무도 안 부르는 씬</b>이 판에 남는다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();

        int before = editor.Project.Files[0].Nodes.Count;
        editor.AddBranchMarker(node.Id, lineId);

        editor.Undo();

        Assert.Equal(before, editor.Project.Files[0].Nodes.Count);
        Assert.Empty(editor.Project.EnumerateNodes().OfType<DialogueNode>()
            .First(item => item.Id == node.Id)
            .FindExtension(lineId)?.DetourTargetNodeIds ?? []);
    }

    [Fact]
    public void 한_자리에_여럿을_두면_차례로_다녀온다()
    {
        // ⛔ 2026-10-29까지는 줄 하나에 하나였다(칸이 하나뿐이었다). 소유자: *"하나의 줄에 여러개의 분기가 있을 수
        //    있게"* — 둘째는 첫째 <b>뒤에</b> 서고, 대본에도 그 차례로 `<<detour>>`가 나간다. 각자 제 첫머리 조건으로
        //    들어갈지 정한다(`docs/plans/multi-branch.md`).
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode first = editor.AddBranchMarker(node.Id, lineId);
        DialogueNode second = editor.AddBranchMarker(node.Id, lineId);

        Assert.Equal([first.Id, second.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Equal(
            [first.Id, second.Id],
            Ports(editor, node).Where(port => port.Kind == ExitPortKind.Detour).Select(port => port.TargetNodeId));
        Assert.Equal(
            [first.Id, second.Id],
            Rendered(editor, node).Segments
                .Where(segment => segment.Kind == RenderedSegmentKind.BranchDetour)
                .Select(segment => segment.TargetNodeId));
        Assert.Equal(
            [first.Id, second.Id],
            ChapterBranchMarkers.For(editor.Project, "ch01").Select(marker => marker.ToNodeId));

        // 두 카드는 서로 다른 빈자리에 선다.
        Assert.NotEqual((first.Layout.X, first.Layout.Y), (second.Layout.X, second.Layout.Y));
    }

    [Fact]
    public void 포트로_바꾸면_그_분기_하나만_바뀌고_선_모양이_따라간다()
    {
        // 분기의 신원은 줄과 다녀갈 노드다 — 줄만으로는 어느 분기인지 모른다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode first = editor.AddBranchMarker(node.Id, lineId);
        DialogueNode second = editor.AddBranchMarker(node.Id, lineId);
        ExitPort firstPort = Ports(editor, node).Single(port => port.TargetNodeId == first.Id);
        var bend = new EdgeShape(EdgeShapeKind.Curve, 0, 40);
        editor.SetExitShape(firstPort, bend);

        Assert.NotEqual(
            firstPort.ExitKey,
            Ports(editor, node).Single(port => port.TargetNodeId == second.Id).ExitKey);

        // 다른 곳으로 이으면 제자리에서 바뀐다 — 다녀오는 차례도, 굽혀 둔 선 모양도 그대로다.
        editor.SetExitTarget(firstPort, Target(editor).Id);
        Assert.Equal([Target(editor).Id, second.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        ExitPort moved = Ports(editor, node).Single(port => port.TargetNodeId == Target(editor).Id);
        Assert.Equal(bend, node.EdgeShapes[EdgeShape.KeyOf(moved)]);

        // 끊으면 그 하나만 걷히고 모양도 걷힌다.
        editor.SetExitTarget(moved, null);
        Assert.Equal([second.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Empty(node.EdgeShapes);

        // 줄만 주고 끊으면 그 줄의 분기를 모두 끊는다.
        editor.SetExitTarget(node.Id, ExitPortKind.Detour, lineId, null);
        Assert.Empty(node.FindExtension(lineId)?.DetourTargetNodeIds ?? []);
    }

    [Fact]
    public void 분기점은_에피소드에_잇지_않는다()
    {
        // 들렀다 돌아오는 동안 그 에피소드의 선택지는 게임에 나오지 않는다(2026-11-17) — 분기점은 분기 카드에만 잇는다. 줄로 이어도,
        // 포트로 이어도, 이어지지 않은 분기점을 이어도 같다. 아무것도 바뀌지 않는다(되돌리기 칸도 없다).
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);
        editor.AddEpisode("ch01", "다음", title: "다음", 600, 0);
        DialogueNode episode = EpisodeNaming.CardFor(editor.Project, "ch01", "다음")!;
        long before = editor.Revision;

        Assert.False(editor.CanBranchTo(episode.Id));
        Assert.True(editor.CanBranchTo(made.Id));

        editor.SetExitTarget(node.Id, ExitPortKind.Detour, lineId, episode.Id);
        editor.SetExitTarget(Ports(editor, node).Single(port => port.TargetNodeId == made.Id), episode.Id);

        Assert.Equal([made.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.Equal(before, editor.Revision);
    }

    [Fact]
    public void 이어지지_않은_분기점을_빈_곳에_놓으면_새_분기_카드가_선다_되돌리기_한_칸()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);
        editor.SetExitTarget(Ports(editor, node).Single(port => port.TargetNodeId == made.Id), null);
        ExitPort open = Ports(editor, node).Single(port => port.IsOpenDetour);
        long before = editor.Revision;

        DialogueNode created = editor.AddBranchCardAt(open, 900, 300)!;

        Assert.True(created.IsBranchCard);
        Assert.Equal((900d, 300d), (created.Layout.X, created.Layout.Y));
        Assert.Equal([created.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.DoesNotContain(Ports(editor, node), port => port.IsOpenDetour);
        Assert.Equal(before + 1, editor.Revision);

        editor.Undo();
        Assert.Null(editor.Project.FindNode(created.Id));
        Assert.Single(Ports(editor, (DialogueNode)editor.Project.FindNode(node.Id)!), port => port.IsOpenDetour);

        // 이어진 분기점이면 아무것도 안 한다.
        (ProjectEditor other, DialogueNode otherNode, string otherLine) = World();
        DialogueNode linked = other.AddBranchMarker(otherNode.Id, otherLine);
        Assert.Null(other.AddBranchCardAt(Ports(other, otherNode).Single(port => port.TargetNodeId == linked.Id), 0, 0));
    }

    [Fact]
    public void 분기_카드는_저장하고_다시_열어도_분기_카드고_에피소드로_올리지_않는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        string text = StoryFileJson.Write(editor.Project.FindFileContainingNode(made.Id)!);
        Assert.Contains("\"branchCard\": true", text, StringComparison.Ordinal);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(text, "branchCard").Count - 1);   // 곁가지와 이것 — 에피소드 카드에는 안 쓴다

        StoryProject reopened = ProjectSnapshotCodec.Decode(ProjectSnapshotCodec.Encode(editor.Project));
        Assert.True(((DialogueNode)reopened.FindNode(made.Id)!).IsBranchCard);

        // 열 때 판의 카드를 에피소드로 올리는 자리(LiftFreeScenes)가 분기 카드를 지나친다.
        var again = new ProjectEditor(reopened);
        again.EnsureChapterBoard("ch01");
        Assert.DoesNotContain(again.Project.Chapters.Single().Episodes, episode => episode.EpisodeId == made.Name);
        Assert.Null(EpisodeNaming.CardFor(again.Project, "ch01", made.Name));
    }

    [Fact]
    public void 같은_줄에_같은_곳은_두_번_들지_않는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        editor.SetExitTarget(node.Id, ExitPortKind.Detour, lineId, made.Id);

        Assert.Equal([made.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
    }

    [Fact]
    public void 하나면_문자열로_여럿이면_배열로_적는다()
    {
        // 하나뿐이면 단일 분기 형식(문자열)과 같은 바이트다 — 분기가 하나인 판은 다시 써도 그대로다. 여럿이면 배열이다.
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode first = editor.AddBranchMarker(node.Id, lineId);

        string single = StoryFileJson.Write(editor.Project.FindFileContainingNode(node.Id)!);
        Assert.Contains($"\"detour\": \"{first.Id}\"", single, StringComparison.Ordinal);

        DialogueNode second = editor.AddBranchMarker(node.Id, lineId);
        string several = StoryFileJson.Write(editor.Project.FindFileContainingNode(node.Id)!);
        StoryFile reread = StoryFileJson.Read(several);

        Assert.Equal(
            [first.Id, second.Id],
            reread.Nodes.OfType<DialogueNode>().Single(item => item.Id == node.Id)
                .FindExtension(lineId)!.DetourTargetNodeIds);
    }

    [Fact]
    public void 분기_하나를_지우면_같은_줄의_다른_분기는_남는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode first = editor.AddBranchMarker(node.Id, lineId);
        DialogueNode second = editor.AddBranchMarker(node.Id, lineId);

        editor.RemoveBranchMarker(node.Id, lineId, first.Id);

        Assert.Equal([second.Id], node.FindExtension(lineId)!.DetourTargetNodeIds);
        Assert.NotNull(editor.Project.FindNode(first.Id));   // 카드는 남는다(2026-11-17)
        Assert.NotNull(editor.Project.FindNode(second.Id));
    }

    [Fact]
    public void 그_노드의_줄이_아니면_거절한다()
    {
        (ProjectEditor editor, DialogueNode node, _) = World();

        Assert.Throws<InvalidOperationException>(
            () => editor.AddBranchMarker(node.Id, "ln_아무거나"));
    }

    // ── 「분기 지우기」 (2026-10-29 소유자) ─────────────────────────────────

    [Fact]
    public void 분기_지우기는_분기만_걷고_함께_선_분기_카드는_남긴다()
    {
        // 2026-11-17 소유자 — 같은 단추가 카드의 출신(함께 선 것 · 사람이 이은 것)에 따라 카드를 지우기도 남기기도 하면 결과를
        //    미리 알 수 없다. 분기 지우기는 분기점과 선만 걷는다. (2026-10-29에는 함께 선 카드도 걷었다.)
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);

        BranchMarkerRemoval removal = Assert.IsType<BranchMarkerRemoval>(editor.RemoveBranchMarker(node.Id, lineId, made.Id));

        Assert.Equal(EpisodeNaming.ShownNameOf(editor.Project, made), removal.KeptNodeName);
        Assert.Contains("판에 남아 있습니다", removal.Describe(), StringComparison.Ordinal);
        Assert.Empty(node.FindExtension(lineId)?.DetourTargetNodeIds ?? []);
        Assert.True(((DialogueNode)editor.Project.FindNode(made.Id)!).IsBranchCard);
        Assert.DoesNotContain(Ports(editor, node), port => port.Kind == ExitPortKind.Detour);
    }

    [Fact]
    public void 분기_지우기는_되돌리기_한_번에_돌아온다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);
        editor.RemoveBranchMarker(node.Id, lineId, made.Id);

        editor.Undo();

        // 되돌리기는 스냅샷을 새로 푼다 — 노드는 Id로 다시 찾는다.
        var restored = (DialogueNode)editor.Project.FindNode(node.Id)!;
        Assert.Equal([made.Id], restored.FindExtension(lineId)?.DetourTargetNodeIds ?? []);
        Assert.True(((DialogueNode)editor.Project.FindNode(made.Id)!).IsBranchCard);
    }

    [Fact]
    public void 다른_분기도_다녀가는_분기_카드는_남는다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        DialogueNode made = editor.AddBranchMarker(node.Id, lineId);
        string otherLine = editor.InsertScriptLine(node.Id).Id;
        editor.SetExitTarget(node.Id, ExitPortKind.Detour, otherLine, made.Id);

        editor.RemoveBranchMarker(node.Id, lineId, made.Id);

        Assert.NotNull(editor.Project.FindNode(made.Id));
        Assert.Equal([made.Id], node.FindExtension(otherLine)?.DetourTargetNodeIds ?? []);
    }

    [Fact]
    public void 분기가_없는_자리는_아무것도_안_한다()
    {
        (ProjectEditor editor, DialogueNode node, string lineId) = World();
        long before = editor.Revision;

        Assert.Null(editor.RemoveBranchMarker(node.Id, lineId, "nd_없는_분기"));
        Assert.Equal(before, editor.Revision);
    }
}
