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
/// <b>챕터 그래프는 분기점을 만들지도 지우지도 않는다.</b> 그것은 연출 그래프의 일이다 — 같은 것의 주인이 둘이 되면
/// 갈린다. 챕터 그래프가 하는 것은 이미 선 분기점을 에피소드에 잇고 끊는 것뿐이다(<see cref="PointsFor"/>).
///
/// <b>간선이 아니다.</b> 챕터의 <see cref="ChapterEdge"/>는 <i>선택지</i>이고 문구·스탯변화·
/// 관문을 진다. 분기는 조작을 단순하게 하려고 그중 아무것도 지지 않는다 —
/// <b>뚫어뒀다면 반드시 행해지는 통로</b>이고, 막는 것은 다녀온 쪽의 첫머리 조건이다
/// (<see cref="BranchEntryCondition"/>).
/// </summary>
public static class ChapterBranchMarkers
{
    /// <param name="FromEpisodeId">분기가 뚫린 에피소드.</param>
    /// <param name="ToEpisodeId">다녀올 에피소드.</param>
    /// <param name="LineId">그 줄. 다녀올 카드와 함께 표식의 신원이다(한 에피소드가 분기를 여럿 가질 수 있고, 한 줄에도
    /// 여럿일 수 있다).</param>
    /// <param name="ToNodeId">다녀올 카드의 NodeId. 조건을 읽고 쓰는 쪽이 이것을 쓴다.</param>
    public sealed record Marker(
        string FromEpisodeId,
        string ToEpisodeId,
        string LineId,
        string ToNodeId);

    /// <summary>
    /// 그 챕터의 분기 전부. 챕터가 없거나 판이 없으면 빈 목록이다.
    ///
    /// <b>대본에 적힌 차례 그대로</b>(한 줄 안에서는 다녀오는 차례로) 선다 — 판에서 읽는 차례와 카드에서 보는 차례가
    /// 같아야 한다(<c>NodeConnections</c>가 분기 포트를 세우는 규칙과 같다).
    ///
    /// <b>이 챕터 안에서 끝나는 표식만</b> 낸다. 다녀올 노드가 다른 판에 있으면(구판
    /// 프로젝트의 자유 씬이 그랬다) 챕터 그래프가 그릴 카드가 없으므로 뺀다 — 없는 카드로
    /// 선을 그으면 판이 거짓말을 한다.
    /// </summary>
    public static IReadOnlyList<Marker> For(StoryProject project, string chapterId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (EpisodeNaming.BoardOf(project, chapterId) is not { } board)
        {
            return [];
        }

        var onBoard = board.Nodes.OfType<DialogueNode>()
            .ToDictionary(node => node.Id, EpisodeNaming.EpisodeIdOf, StringComparer.Ordinal);

        var markers = new List<Marker>();

        foreach (DialogueNode card in board.Nodes.OfType<DialogueNode>())
        {
            string from = EpisodeNaming.EpisodeIdOf(card);

            // 대본의 줄 차례로 훑는다 — 확장(LineExtensions)의 순서는 손댄 차례라 사람이
            // 읽는 차례가 아니다.
            foreach (ScriptLine line in card.Script.ActiveLines)
            {
                foreach (string target in card.FindExtension(line.Id)?.DetourTargetNodeIds ?? [])
                {
                    if (onBoard.TryGetValue(target, out string? to))
                    {
                        markers.Add(new Marker(from, to, line.Id, target));
                    }
                }
            }
        }

        return markers;
    }

    /// <summary>
    /// 카드 하나의 분기점 — 이어진 분기든 연결만 끊은 분기점이든.
    /// </summary>
    /// <param name="FromEpisodeId">분기점이 선 에피소드.</param>
    /// <param name="Port">그 분기점의 포트 — 잇고 끊는 것은 이것으로 한다(<see cref="Editing.ProjectEditor.SetExitTarget(ExitPort, string?)"/>).</param>
    /// <param name="ToEpisodeId">다녀올 에피소드. 이어지지 않은 분기점이면 <c>null</c>이다.</param>
    public sealed record BranchPoint(string FromEpisodeId, ExitPort Port, string? ToEpisodeId)
    {
        /// <summary>그 분기점의 열쇠 — 줄과 다녀갈 카드(이어지지 않았으면 그 줄의 몇째 빈 자리, <see cref="ExitPort.OpenSlot"/>).</summary>
        public string Key => Port.ExitKey!;

        /// <summary>이어진 분기면 그 표식.</summary>
        public Marker? Marker => ToEpisodeId is { } to && Port.TargetNodeId is { } node
            ? new Marker(FromEpisodeId, to, Port.BranchOpenLineId!, node)
            : null;
    }

    /// <summary>
    /// 그 챕터 카드들의 분기점 전부 — 포트 차례 그대로(대본의 줄 차례, 한 줄 안에서는 이어진 분기들 뒤에 이어지지 않은
    /// 분기점). [구성] 탭이 카드 옆변에 이 점들을 세우고 이어진 것만 선을 긋는다 — 연결만 끊은 분기점도 판에 남아 보인다.
    /// 다른 판으로 가는 분기는 그릴 카드가 없어 뺀다(<see cref="For"/>와 같다).
    /// </summary>
    public static IReadOnlyList<BranchPoint> PointsFor(StoryProject project, string chapterId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (EpisodeNaming.BoardOf(project, chapterId) is not { } board)
        {
            return [];
        }

        var onBoard = board.Nodes.OfType<DialogueNode>()
            .ToDictionary(node => node.Id, EpisodeNaming.EpisodeIdOf, StringComparer.Ordinal);

        var points = new List<BranchPoint>();

        foreach (DialogueNode card in board.Nodes.OfType<DialogueNode>())
        {
            string from = EpisodeNaming.EpisodeIdOf(card);

            foreach (ExitPort port in NodeConnections.PortsOf(card, project).Where(port => port.Kind == ExitPortKind.Detour))
            {
                if (port.TargetNodeId is not { } target)
                {
                    points.Add(new BranchPoint(from, port, null));
                }
                else if (onBoard.TryGetValue(target, out string? to))
                {
                    points.Add(new BranchPoint(from, port, to));
                }
            }
        }

        return points;
    }
}
