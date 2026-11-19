using Vn.Authoring.Model;

namespace Vn.Authoring.Flow;

/// <summary>
/// 실행 출구의 종류 — 분기점. ⛔ 기본 출구(<c>Default</c> — 노드가 끝난 뒤의 출구)는 2026-11-17에, 갈래 출구(<c>Branch</c> — 조건 갈래가
/// 끝나면 들를 카드)는 2026-11-19에 걷혔다(usability UX28).
/// </summary>
public enum ExitPortKind
{
    /// <summary>
    /// <b>「조건 분기」 표식</b>이 뚫은 출구 (R7 P-5 · 2026-09-17). 열쇠는 그 줄의 LineId와 다녀갈 노드다 — 한 줄에
    /// 분기가 여럿일 수 있다(2026-10-29, <see cref="ExitPort.DetourKey"/>).
    ///
    /// <b>조건이 아니다.</b> 연출 그래프는 "어디서 갈라지는가"만 짚는다 — 성립하든 말든
    /// 다녀오고, 다녀온 분기 카드가 제 첫머리에서 보고 아니면 곧바로 돌아온다. 그래서 갈래와
    /// 달리 여는 전환도 닫는 전환도 없다: <b>분기 하나가 곧 포트 하나</b>다.
    /// </summary>
    Detour
}

/// <summary>
/// 그래프에서 끌어다 연결할 수 있는 실행 출력 포트 하나.
///
/// 오직 다음 실행 노드를 결정하는 출구(분기)만 표현한다.
/// </summary>
/// <param name="OpenOrdinal">
/// 이어지지 않은 분기점(<see cref="Model.DialogueLineExtension.OpenDetourPositions"/>)이면 그 줄의 분기점 전체에서 몇째
/// 자리인가. 다녀갈 곳이 없어 신원을 이 자리로 가린다 — 다른 분기를 끊어도 자리가 안 바뀐다.
/// </param>
public sealed record ExitPort(
    ExitPortKind Kind,
    string NodeId,
    string? BranchOpenLineId,
    string Label,
    string? TargetNodeId,
    int PaletteIndex,
    int? OpenOrdinal = null)
{
    public bool IsConnected => TargetNodeId is not null;

    /// <summary>이어지지 않은 분기점인가 — 분기점은 섰지만 다녀갈 곳이 없다.</summary>
    public bool IsOpenDetour => Kind == ExitPortKind.Detour && TargetNodeId is null && OpenOrdinal is not null;

    /// <summary>
    /// 이 포트를 저장할 때의 열쇠 — 분기는 줄과 다녀갈 노드다(<see cref="DetourKey"/>).
    /// </summary>
    public string? ExitKey => Kind switch
    {
        ExitPortKind.Detour when BranchOpenLineId is { } lineId && TargetNodeId is { } target => DetourKey(lineId, target),
        ExitPortKind.Detour when BranchOpenLineId is { } lineId && OpenOrdinal is { } open => DetourKey(lineId, OpenSlot(open)),
        _ => BranchOpenLineId
    };

    /// <summary>
    /// 분기 하나의 열쇠 — 줄과 다녀갈 노드 (2026-10-29). 한 줄에 분기가 여럿이라 줄만으로는 어느 분기인지 모른다.
    /// 같은 줄에 같은 노드는 두 번 들지 않으므로 이 둘이면 하나를 가린다. 선 모양(<see cref="Model.EdgeShape"/>)의
    /// 열쇠도 이것이다.
    /// </summary>
    public static string DetourKey(string lineId, string targetNodeId) => $"{lineId}>{targetNodeId}";

    /// <summary>이어지지 않은 분기점의 자리 이름 — 분기 열쇠(<see cref="DetourKey"/>)에서 다녀갈 노드 자리에 든다. 노드 Id와 겹치지 않는다.</summary>
    public static string OpenSlot(int ordinal) => $"open:{ordinal}";
}

/// <summary>
/// 노드의 실행 출력 포트 목록을 계산한다.
/// Settings 공급 연결은 여기에 섞지 않는다.
/// </summary>
public static class NodeConnections
{
    public static IReadOnlyList<ExitPort> PortsOf(
        StoryNode node,
        StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(project);

        var ports = new List<ExitPort>();

        if (node is not DialogueNode)
        {
            // 실행 출구는 DialogueNode만 가진다. SetNode는 조건을 주는 공급자라 실행 순서를
            // 결정하지 않는다. 공급 포트는 GraphProjection이 별도로 계산한다.
            return ports;
        }

        if (node is DialogueNode dialogue)
        {
            DialogueFlow flow = ConditionFlowResolver.Resolve(dialogue, project);

            // 분기의 색 번호는 그 카드의 조건 갈래 색 다음부터 — 대사 편집기에서 같은 카드의 조건 표와 분기 카드가 같은 색이 되지 않게
            // (갈래 출구 포트가 앞자리를 차지하던 때의 번호 그대로다 · ⛔ 갈래 출구는 2026-11-19에 걷혔다).
            int palette = flow.Branches.Count;

            // 「조건 분기」 표식 — 분기 하나가 곧 포트 하나다 (R7 P-5). 대본에 적힌 순서, 한 줄 안에서는 다녀오는
            // 차례 그대로 선다: 판에서 읽는 차례와 카드에서 보는 차례가 같아야 한다.
            foreach (DialogueLine line in flow.Script.Lines)
            {
                // 연결만 끊은 분기점은 제자리에 선다(다녀갈 곳 없는 포트) — 끌어 카드에 이으면 그 자리에서 분기가 된다.
                for (int slot = 0; slot < line.DetourSlots.Count; slot++)
                {
                    ports.Add(line.DetourSlots[slot] is { } detour
                        ? new ExitPort(ExitPortKind.Detour, dialogue.Id, line.LineId, "분기", detour, palette++)
                        : new ExitPort(ExitPortKind.Detour, dialogue.Id, line.LineId, "분기", null, palette++, OpenOrdinal: slot));
                }
            }
        }

        return ports;
    }

    /// <summary>프로젝트 전체의 실행 간선.</summary>
    public static IReadOnlyList<ExitPort> AllConnections(
        StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.EnumerateNodes()
            .SelectMany(node => PortsOf(node, project))
            .Where(port => port.IsConnected)
            .ToList();
    }
}
