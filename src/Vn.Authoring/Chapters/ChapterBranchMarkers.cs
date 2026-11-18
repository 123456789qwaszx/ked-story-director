using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 한 챕터에 뚫린 <b>분기 표식</b>을 모으는 유일한 자리.
///
/// 표식은 <see cref="DialogueLineExtension.DetourTargetNodeIds"/> — <b>대사 줄</b>에 산다(한 줄에 여럿일 수 있다).
/// 연출 그래프가 [＋ 분기]로 뚫고, 챕터 그래프는 그것을 <b>읽어서 그린다</b>.
///
/// <b>챕터 그래프는 분기점을 만들지도 지우지도 잇고 끊지도 않는다.</b> 그것은 연출 그래프의 일이다 — 같은 것의 주인이
/// 둘이 되면 갈린다. 챕터 그래프는 분기점을 보이고(<see cref="PointsFor"/>) 그 분기의 조건을 정한다.
///
/// <b>간선이 아니다.</b> 챕터의 <see cref="ChapterEdge"/>는 <i>선택지</i>이고 문구·스탯변화·
/// 관문을 진다. 분기는 조작을 단순하게 하려고 그중 아무것도 지지 않는다 —
/// <b>뚫어뒀다면 반드시 행해지는 통로</b>이고, 막는 것은 다녀온 쪽의 첫머리 조건이다
/// (<see cref="BranchEntryCondition"/>).
///
/// <b>다녀올 곳은 분기 카드다</b>(2026-11-17 — <see cref="DialogueNode.IsBranchCard"/>). 분기점은 에피소드 카드에도, 분기 카드에도(분기 카드가
/// 또 들른다) 선다 — 그래서 출발은 카드(NodeId)로 짚고, 에피소드면 그 Id를 함께 싣는다. 다녀올 곳은 늘 분기 카드다(그 밖은 읽을 때 거절한다).
/// </summary>
public static class ChapterBranchMarkers
{
    /// <param name="FromNodeId">분기점이 선 카드.</param>
    /// <param name="FromEpisodeId">그 카드가 이 챕터의 에피소드면 그 Id — 분기 카드면 <c>null</c>.</param>
    /// <param name="LineId">그 줄. 다녀올 카드와 함께 표식의 신원이다(한 카드가 분기를 여럿 가질 수 있고, 한 줄에도
    /// 여럿일 수 있다).</param>
    /// <param name="ToNodeId">다녀올 카드의 NodeId. 조건을 읽고 쓰는 쪽이 이것을 쓴다.</param>
    /// <param name="ToName">다녀올 분기 카드의 이름.</param>
    public sealed record Marker(
        string FromNodeId,
        string? FromEpisodeId,
        string LineId,
        string ToNodeId,
        string ToName);

    /// <summary>
    /// 그 챕터의 분기 전부. 챕터가 없거나 판이 없으면 빈 목록이다.
    ///
    /// <b>대본에 적힌 차례 그대로</b>(한 줄 안에서는 다녀오는 차례로) 선다 — 판에서 읽는 차례와 카드에서 보는 차례가
    /// 같아야 한다(<c>NodeConnections</c>가 분기 포트를 세우는 규칙과 같다). 분기는 늘 같은 판의 분기 카드로 간다(저장 검증이 지킨다).
    /// </summary>
    public static IReadOnlyList<Marker> For(StoryProject project, string chapterId) =>
        PointsFor(project, chapterId).Select(point => point.Marker).OfType<Marker>().ToList();

    /// <summary>
    /// 카드 하나의 분기점 — 이어진 분기든 연결만 끊은 분기점이든.
    /// </summary>
    /// <param name="FromNodeId">분기점이 선 카드.</param>
    /// <param name="FromEpisodeId">그 카드가 에피소드면 그 Id — 분기 카드면 <c>null</c>.</param>
    /// <param name="Port">그 분기점의 포트 — 차례 · 색 번호 · 신원(<see cref="Key"/>)이 여기서 나온다.</param>
    /// <param name="ToName">다녀올 분기 카드의 이름(<see cref="Marker.ToName"/>). 이어지지 않은 분기점이면 <c>null</c>이다.</param>
    public sealed record BranchPoint(string FromNodeId, string? FromEpisodeId, ExitPort Port, string? ToName)
    {
        /// <summary>그 분기점의 열쇠 — 줄과 다녀갈 카드(이어지지 않았으면 그 줄의 몇째 빈 자리, <see cref="ExitPort.OpenSlot"/>).</summary>
        public string Key => Port.ExitKey!;

        /// <summary>이어진 분기면 그 표식.</summary>
        public Marker? Marker => ToName is { } to && Port.TargetNodeId is { } node
            ? new Marker(FromNodeId, FromEpisodeId, Port.BranchOpenLineId!, node, to)
            : null;
    }

    /// <summary>
    /// 그 챕터 카드들의 분기점 전부 — 판의 카드 차례, 카드 안에서는 포트 차례 그대로(대본의 줄 차례, 한 줄 안에서는 이어진
    /// 분기들 뒤에 이어지지 않은 분기점). [구성] 탭이 카드 옆변에 이 점들을 세우고 이어진 것만 선을 긋는다 — 연결만 끊은
    /// 분기점도 판에 남아 보인다. ⛔ 다른 판으로 가는 분기를 빼던 갈래는 2026-11-18에 걷혔다(old-shapes O8 — 분기는 같은 판의 분기 카드에만 잇는다).
    /// </summary>
    public static IReadOnlyList<BranchPoint> PointsFor(StoryProject project, string chapterId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (EpisodeNaming.BoardOf(project, chapterId) is not { } board)
        {
            return [];
        }

        ChapterDocument? chapter = project.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, chapterId, StringComparison.Ordinal));
        var onBoard = board.Nodes.OfType<DialogueNode>().ToDictionary(node => node.Id, StringComparer.Ordinal);

        var points = new List<BranchPoint>();

        foreach (DialogueNode card in onBoard.Values)
        {
            string? from = chapter is null ? null : EpisodeNaming.EpisodeFor(chapter, card)?.EpisodeId;

            foreach (ExitPort port in NodeConnections.PortsOf(card, project).Where(port => port.Kind == ExitPortKind.Detour))
            {
                points.Add(new BranchPoint(card.Id, from, port, port.TargetNodeId is { } target ? onBoard[target].Name : null));
            }
        }

        return points;
    }
}
