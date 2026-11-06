using Vn.Authoring.Definition;
using Vn.Authoring.Model;

namespace Vn.Authoring.Flow;

public enum ExitPortKind
{
    /// <summary>실행 가능한 노드 전체가 끝난 뒤의 출구.</summary>
    Default,

    /// <summary>특정 조건 갈래를 지났을 때의 출구.</summary>
    Branch,

    /// <summary>
    /// 챕터 간선(선택지) 하나를 골랐을 때의 출구 (v9, 2026-08-17). <b>열쇠는 선택지 문구</b>다 —
    /// 대본에 OPTION이 없어도(v9에서는 없는 것이 정상) 선택지마다 자유 씬을 달 수 있다.
    ///
    /// 대본의 줄에 매이지 않으므로 대본을 고쳐도 사라지지 않는다(문구의 주인은 챕터다).
    /// </summary>
    Choice,

    /// <summary>
    /// <b>「조건 분기」 표식</b>이 뚫은 출구 (R7 P-5 · 2026-09-17). 열쇠는 그 줄의 LineId와 다녀갈 노드다 — 한 줄에
    /// 분기가 여럿일 수 있다(2026-10-29, <see cref="ExitPort.DetourKey"/>).
    ///
    /// <b>조건이 아니다.</b> 연출 그래프는 "어디서 갈라지는가"만 짚는다 — 성립하든 말든
    /// 다녀오고, 다녀온 자유 씬이 제 첫머리에서 보고 아니면 곧바로 돌아온다. 그래서 갈래와
    /// 달리 여는 전환도 닫는 전환도 없다: <b>분기 하나가 곧 포트 하나</b>다.
    /// </summary>
    Detour
}

/// <summary>
/// 그래프에서 끌어다 연결할 수 있는 실행 출력 포트 하나.
///
/// Settings link는 실행 포트가 아니며 <see cref="StoryProject.Links"/>에 별도로 저장된다.
/// 이 타입은 오직 다음 실행 노드를 결정하는 기본/조건 출구만 표현한다.
/// </summary>
/// <param name="ChoiceText">챕터 간선 선택지(<see cref="ExitPortKind.Choice"/>)의 원문 라벨. 간선과 라벨 짝을 맞추는 열쇠다.</param>
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
    string? ChoiceText = null,
    int? OpenOrdinal = null)
{
    public bool IsConnected => TargetNodeId is not null;

    /// <summary>이어지지 않은 분기점인가 — 분기점은 섰지만 다녀갈 곳이 없다.</summary>
    public bool IsOpenDetour => Kind == ExitPortKind.Detour && TargetNodeId is null && OpenOrdinal is not null;

    /// <summary>
    /// 이 포트를 저장할 때의 열쇠. 갈래는 여는 줄의 LineId, 선택지는 <b>문구</b>다(v9), 분기는 줄과 다녀갈 노드다
    /// (<see cref="DetourKey"/>). 기본 출구는 열쇠가 없다 — 노드에 자리가 하나뿐이다.
    /// </summary>
    public string? ExitKey => Kind switch
    {
        ExitPortKind.Choice => ChoiceText,
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
        StoryProject project,
        GameDefinition? definition = null)
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
            DialogueFlow flow = ConditionFlowResolver.Resolve(dialogue, project, definition);
            AvailableConditionCatalog available = AvailableConditionResolver.Resolve(
                project,
                dialogue.Id,
                definition);

            foreach (ConditionBranch branch in flow.Branches)
            {
                ports.Add(new ExitPort(
                    ExitPortKind.Branch,
                    dialogue.Id,
                    branch.OpenLineId,
                    LabelFor(branch, project, definition, available),
                    branch.ExitTargetNodeId,
                    branch.PaletteIndex));
            }

            // 「조건 분기」 표식 — 분기 하나가 곧 포트 하나다 (R7 P-5). 대본에 적힌 순서, 한 줄 안에서는 다녀오는
            // 차례 그대로 선다: 판에서 읽는 차례와 카드에서 보는 차례가 같아야 한다.
            foreach (DialogueLine line in flow.Script.Lines)
            {
                // 연결만 끊은 분기점은 제자리에 선다(다녀갈 곳 없는 포트) — 끌어 카드에 이으면 그 자리에서 분기가 된다.
                for (int slot = 0; slot < line.DetourSlots.Count; slot++)
                {
                    ports.Add(line.DetourSlots[slot] is { } detour
                        ? new ExitPort(ExitPortKind.Detour, dialogue.Id, line.LineId, "분기", detour, ports.Count)
                        : new ExitPort(ExitPortKind.Detour, dialogue.Id, line.LineId, "분기", null, ports.Count, OpenOrdinal: slot));
                }
            }

            // ~~기본 출구는 엑셀노드만 가진다~~ (2026-08-21) — <b>걷혔다</b>
            //    (R7 P-6 · 결정 ⑤ · 2026-09-17). 그 관문이 곧 엑셀노드/자유노드의 구분이었고,
            //    구분이 없어졌으므로 <b>대사 노드는 전부 기본 출구를 가진다</b>.
            //
            // 이 규칙은 `StoryNode.EffectiveDefaultExit`에도 <b>따로 한 벌</b> 있었다 —
            //    같은 규칙이 두 곳에 살면 갈린다. 그쪽도 함께 걷었다.
            ports.Add(new ExitPort(
                ExitPortKind.Default,
                dialogue.Id,
                null,
                "기본",
                dialogue.DefaultExitTargetNodeId,
                -1));
        }

        return ports;
    }

    /// <summary>프로젝트 전체의 실행 간선. Settings link는 포함하지 않는다.</summary>
    public static IReadOnlyList<ExitPort> AllConnections(
        StoryProject project,
        GameDefinition? definition = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.EnumerateNodes()
            .SelectMany(node => PortsOf(node, project, definition))
            .Where(port => port.IsConnected)
            .ToList();
    }

    private static string LabelFor(
        ConditionBranch branch,
        StoryProject project,
        GameDefinition? definition,
        AvailableConditionCatalog available)
    {
        AvailableCondition? condition = available.Find(branch.ConditionId);
        string name;

        if (condition is not null)
        {
            name = AvailableConditionResolver.LayeredLabel(condition);
        }
        else
        {
            AvailableCondition? known = AvailableConditionResolver.FindKnown(
                project,
                definition,
                branch.ConditionId);
            name = known is null
                ? "알 수 없는 조건"
                : AvailableConditionResolver.UnavailableLabel(known, branch.ConditionId);
        }

        return branch.BranchIndexInChain == 0
            ? name
            : $"{name} (elseif)";
    }
}
