using Vn.Authoring.Strings;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>자동 길의 규칙</b> — 네 가지.
///
/// 자동 진행(<see cref="ChapterEdge.Auto"/>)은 플레이어 입력 없이 다음 에피소드로 잇는 길이다 — 장면을 넘어도 된다. 그러려면
/// 갈릴 데가 없어야 한다: 유일한 간선 · 조건 없음 · 스탯변화 없음 · 문구 없음. ⛔ 「같은 장면」 규칙은 2026-11-18에 런타임과 함께
/// 걷혔다(auto-edge-across-scenes — 장면 경계의 일(무대 비우기 · 확정 · 저장)은 묻는 선택지로 넘을 때와
/// 같고 박스만 뜨지 않는다).
///
/// <b>왜 따로 떼어 냈나</b>: 판의 주인은 워크북이 아니라 프로젝트다. 이 검사가 워크북을
/// 읽는 길에만 있으면 <b>툴이 소유한 챕터에서는 조용히 안 돈다</b>. 내보내기 관문은 코어가
/// 거부하므로 잘못된 것이 나가지는 않지만, 기획자는 <b>고치는 중에</b> 못 본다 —
/// 관문에서야 알면 어디를 고칠지 되짚어야 한다.
///
/// <b>새 규칙을 만들지 않는다.</b> 앞의 셋은 코어의 <c>ChapterInvariants.VerifyAuto</c>와 같고(문구 없음은 코어가 빈 문구를
/// 자동 길에만 허용하는 것의 반대편이다), 여기서 하는 일은 그것을 <b>더 일찍, 셀을 짚어</b> 말해 주는 것뿐이다.
///
/// <see cref="ChapterDocument.StatDiagnostics"/>와 같은 부류다 — <b>파일의 흠이 아니라
/// 값의 흠</b>이라 주인이 바뀌어도 남는다. 다만 그쪽은 두 벌로 갈라져 있고 이쪽은 한 벌이다:
/// 검사가 두 자리에 있으면 언젠가 한쪽만 고쳐진다.
/// </summary>
public static class ChapterAutoEdgeCheck
{
    /// <summary>
    /// 이 챕터의 자동 길들을 본다. 깨진 것이 없으면 빈 목록이다.
    /// </summary>
    /// <param name="path">진단이 사람에게 짚어 줄 파일 — 읽은 자리가 아니라 <b>낼</b> 자리다.</param>
    public static IReadOnlyList<ChapterDiagnostic> Of(IReadOnlyList<ChapterEdge> edges, string path)
    {
        ArgumentNullException.ThrowIfNull(edges);

        var diagnostics = new List<ChapterDiagnostic>();

        var outgoing = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (ChapterEdge edge in edges)
        {
            outgoing[edge.FromEpisodeId] = outgoing.GetValueOrDefault(edge.FromEpisodeId) + 1;
        }

        foreach (ChapterEdge edge in edges.Where(item => item.Auto))
        {
            void Error(ChapterDiagnosticCode code, string message) =>
                diagnostics.Add(new ChapterDiagnostic(
                    ChapterDiagnosticSeverity.Error, code, path, ChapterSheetNames.Edges,
                    null,
                    message));

            if (outgoing.GetValueOrDefault(edge.FromEpisodeId) != 1)
            {
                Error(ChapterDiagnosticCode.AutoEdgeHasSiblings,
                    string.Format(Msg.Diag_AutoPathMustBeEpisode, edge.FromEpisodeId, edge.ToEpisodeId));
            }

            if (edge.HasGate)
            {
                Error(ChapterDiagnosticCode.AutoEdgeHasConditions,
                    Msg.Diag_AutoPathCanTHave);
            }

            if (edge.StatChanges.Count > 0)
            {
                Error(ChapterDiagnosticCode.AutoEdgeHasStatChanges,
                    Msg.Diag_AutoPathCanTHave2);
            }

            if (!edge.HasNoOptionLabel)
            {
                Error(ChapterDiagnosticCode.AutoEdgeHasChoiceLabel,
                    Msg.Diag_AutoPathSChoiceText);
            }
        }

        return diagnostics;
    }
}
