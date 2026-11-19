using Vn.App.Services;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>
/// SetNode의 TextBox가 LostFocus에서 값을 커밋해도 그 TextBox 자체를 다시 만들지 않는다는 계약.
/// 그래프와 Dialogue 조건 선택지는 새 조건 이름을 읽어야 하므로 조건 정의 변경은
/// 단순 Content와도, 전체 Structure 변경과도 다르게 처리한다.
/// </summary>
public class ProjectRefreshPlannerTests
{
    [Fact]
    public void 에피소드_지시는_판을_다시_짓지_않고_장면_테두리만_다시_긋고_되돌리기는_구조로_받는다()
    {
        // 2026-11-19 소유자 — 「에피소드 지시를 추가하고 빼는 것이 조금 느린데」(usability UX7). ⛔ NodeMetadata — 판을 통째로 다시 지었다.
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(ProjectChangeKind.EpisodeNote, new DialogueNode(name: "장면"));

        Assert.Equal(new ProjectRefreshPlan(RebuildGraph: false, RefreshGraphPositions: false, RebuildInspector: false, RefreshSceneFrames: true), plan);

        // 되돌리면 프로젝트의 객체가 모두 새것이다 — 판이 옛 객체를 붙들지 않게 다시 짓는다.
        ProjectRefreshPlan undo = ProjectRefreshPlanner.Plan(ProjectChangeKind.EpisodeNote, null, fromHistory: true);
        Assert.True(undo.RebuildGraph);
        Assert.True(undo.RebuildInspector);
    }

    [Fact]
    public void 조건을_편집_중인_SetNode는_유지하고_그래프만_갱신한다()
    {
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(
            ProjectChangeKind.ConditionDefinition,
            new SetNode(name: "설정"));

        Assert.True(plan.RebuildGraph);
        Assert.False(plan.RefreshGraphPositions);
        Assert.False(plan.RebuildInspector);
    }

    [Fact]
    public void Dialogue가_선택되어_있으면_조건_드롭다운도_다시_계산한다()
    {
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(
            ProjectChangeKind.ConditionDefinition,
            new DialogueNode(name: "장면"));

        Assert.True(plan.RebuildGraph);
        Assert.True(plan.RebuildInspector);
    }

    [Fact]
    public void assignment_내용_변경은_컨트롤을_재생성하지_않는다()
    {
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(
            ProjectChangeKind.Content,
            new SetNode(name: "설정"));

        Assert.False(plan.RebuildGraph);
        Assert.False(plan.RefreshGraphPositions);
        Assert.False(plan.RebuildInspector);
    }


    [Fact]
    public void Dialogue_내용_변경은_편집기를_재생성하지_않고_카드_글_칸만_비춘다()
    {
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(
            ProjectChangeKind.DialogueContent,
            new DialogueNode(name: "장면"));

        Assert.False(plan.RebuildGraph);
        Assert.False(plan.RebuildInspector);
        Assert.True(plan.RefreshCardWriting);
    }

    [Fact]
    public void 되돌리기에서_온_글은_판을_다시_짓지_않고_글_칸들을_다시_채우며_그_밖은_구조로_받는다()
    {
        // 되돌리기 · 다시 실행은 객체가 모두 새것이다 — 화면이 객체를 붙들지 않는 종류만 가볍게(board-speed PF1).
        ProjectRefreshPlan text = ProjectRefreshPlanner.Plan(ProjectChangeKind.DialogueContent, new DialogueNode(name: "장면"), fromHistory: true);
        Assert.False(text.RebuildGraph);
        Assert.True(text.RefreshAllCardWriting);

        Assert.Equal(default, ProjectRefreshPlanner.Plan(ProjectChangeKind.ChapterLayout, null, fromHistory: true));
        Assert.Equal(default, ProjectRefreshPlanner.Plan(ProjectChangeKind.Content, null, fromHistory: true));

        // 자리 · 이름 · 조건은 구조로 — 접은 장면 상자처럼 지을 때의 객체로 일하는 화면이 있다.
        foreach (ProjectChangeKind kind in new[] { ProjectChangeKind.Layout, ProjectChangeKind.NodeMetadata, ProjectChangeKind.ConditionDefinition })
        {
            Assert.Equal(
                ProjectRefreshPlanner.Plan(ProjectChangeKind.Structure, null),
                ProjectRefreshPlanner.Plan(kind, null, fromHistory: true));
        }
    }

    [Fact]
    public void 구성_판의_자리_이동은_대본_판을_건드리지_않는다()
    {
        // [대본] 판은 대사 노드의 자리를 읽는다 — [구성]에서 에피소드를 옮겨도 다시 짓거나 다시 놓을 것이 없다(finishing FX3).
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(
            ProjectChangeKind.ChapterLayout,
            new DialogueNode(name: "장면"));

        Assert.Equal(default, plan);
    }

    [Fact]
    public void 그래프_좌표_이동은_자리만_다시_놓는다()
    {
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(
            ProjectChangeKind.Layout,
            new DialogueNode(name: "장면"));

        Assert.True(plan.RefreshGraphPositions);
        Assert.False(plan.RebuildInspector);
        Assert.False(plan.RefreshCardWriting);
    }

    [Fact]
    public void 구조_변경만_그래프와_Inspector를_모두_다시_만든다()
    {
        ProjectRefreshPlan plan = ProjectRefreshPlanner.Plan(
            ProjectChangeKind.Structure,
            new SetNode(name: "설정"));

        Assert.True(plan.RebuildGraph);
        Assert.True(plan.RebuildInspector);
    }
}
