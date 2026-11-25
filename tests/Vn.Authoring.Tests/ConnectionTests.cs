using Vn.Authoring.Flow;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests;

/// <summary>
/// 그래프의 포트·간선은 저장되지 않고 대본(분기점)에서 계산된다. 실행 출구는 <b>분기점뿐</b>이다 — ⛔ 조건 갈래마다 서던 출구 포트(갈래 출구)는
/// 2026-11-19에 걷혔다(usability UX28 — 그것을 보던 시험 일곱도 함께).
/// </summary>
public class ConnectionTests
{
    [Fact]
    public void 조건_갈래는_출구_포트를_세우지_않는다()
    {
        var sample = new Sample();
        sample.BuildSpecExample();

        Assert.Empty(NodeConnections.PortsOf(sample.Dialogue, sample.Project));
        Assert.Empty(NodeConnections.AllConnections(sample.Project));
    }

    [Fact]
    public void 없는_노드로는_연결되지_않는다()
    {
        var sample = new Sample();
        string line = sample.Line("하린: 한 줄");
        long before = sample.Editor.Revision;

        sample.Editor.SetExitTarget(sample.Dialogue.Id, ExitPortKind.Detour, line, "nd_없는것");

        Assert.Equal(before, sample.Editor.Revision);
        Assert.Empty(NodeConnections.PortsOf(sample.Dialogue, sample.Project));
    }
}
