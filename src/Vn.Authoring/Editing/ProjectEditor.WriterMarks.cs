using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Editing;

/// <summary>
/// <b>작가용 글 서식</b> (2026-11-22 소유자 · <c>docs/plans/writer-formatting.md</c>) — 작가가 보기 위한 굵게 · 기울임 · 취소선 · 글자색 · 바탕색 · 글꼴 ·
/// 크기 · 정렬. 대사 글(<see cref="LocalizedLine.Text"/>)은 순수한 글자 그대로이고 서식은 그 밖(<see cref="DialogueNode.WriterMarks"/>)에 선다 —
/// 내보내기가 읽지 않는다(WF-D2).
///
/// 바꾸기는 모두 <b>되돌리기 한 칸</b>(여러 줄이어도)이고, 바뀐 것이 없으면 적지 않는다. 알림은 <see cref="ProjectChangeKind.WriterMarks"/> + 그 카드다 —
/// [대본] 판은 그 카드의 글 칸 꾸밈만 다시 입힌다.
/// </summary>
public sealed partial class ProjectEditor
{
    /// <summary>
    /// 고른 구간들(여러 줄일 수 있다)에 한 가지 모양을 입힌다. 켜고 끄는 것(굵게 · 기울임 · 취소선)은 <b>문서 도구와 같은 판정</b>이다 — 고른 글자가 모두
    /// 이미 그 모양이면 끄고, 하나라도 아니면 모두 켠다. 은퇴한 줄 · 없는 줄 · 글 밖의 구간은 건너뛴다.
    /// </summary>
    public void ApplyWriterStyle(string nodeId, IReadOnlyList<WriterRange> ranges, WriterStyleChange change)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(change);

        DialogueNode node = RequireDialogue(nodeId);
        ScriptDocument script = node.Script;
        var targets = new List<(string LineId, int Length, int Start, int End)>();

        foreach (WriterRange range in ranges)
        {
            if (script.FindLine(range.LineId) is not { IsRetired: false })
            {
                continue;
            }

            int length = script.Text(range.LineId).Text.Length;
            int start = Math.Clamp(range.Start, 0, length);
            int end = Math.Clamp(range.Start + range.Length, start, length);

            if (end > start)
            {
                targets.Add((range.LineId, length, start, end));
            }
        }

        if (targets.Count == 0)
        {
            return;
        }

        bool on = WriterMarkMath.IsToggle(change.Kind) &&
                  !targets.All(target => WriterMarkMath.IsAll(
                      node.WriterMarks.GetValueOrDefault(target.LineId), target.Length, target.Start, target.End,
                      look => WriterMarkMath.Has(look, change.Kind)));

        var next = new Dictionary<string, List<WriterSpan>>(StringComparer.Ordinal);

        foreach ((string lineId, int length, int start, int end) in targets)
        {
            IEnumerable<WriterSpan> now = next.TryGetValue(lineId, out List<WriterSpan>? pending)
                ? pending
                : node.WriterMarks.GetValueOrDefault(lineId) ?? [];
            next[lineId] = WriterMarkMath.Apply(now, length, start, end, look => WriterMarkMath.Change(look, change, on));
        }

        if (next.All(pair => SameSpans(node.WriterMarks.GetValueOrDefault(pair.Key), pair.Value)))
        {
            return;
        }

        Mutate(ProjectChangeKind.WriterMarks, () =>
        {
            foreach ((string lineId, List<WriterSpan> spans) in next)
            {
                SetLineMarks(node, lineId, spans);
            }
        }, nodeId);
    }

    /// <summary>
    /// <b>카드들의 글 전체</b>에 한 가지 모양을 입힌다 — 하이어아키 · 판에서 카드(여러 장도)만 골랐을 때의 서식 도구(2026-11-23 소유자 — 「도구 막대 전부」 ·
    /// writer-formatting WF-D21). 카드마다 은퇴하지 않은 모든 줄의 대사 글 전체가 구간이다(화자 이름 · 조건 줄은 대사 글이 아니라 들지 않는다). 켜고 끄는 것은
    /// <b>모든 카드를 하나로</b> 판정한다 — 모두 이미 그 모양이면 끄고, 하나라도 아니면 모두 켠다. 굵게는 처음부터 굵은 카드(<see cref="WriterBase.Bold"/>)를 이미
    /// 굵은 것으로 센다. 되돌리기 한 칸이고, 바뀐 것이 없으면 적지 않는다. 크기(<see cref="WriterStyleKind.Grow"/> · <see cref="WriterStyleKind.Size"/>)는 여기서도
    /// 글자마다의 차다 — 카드의 기본 크기는 <see cref="SetWriterBase"/>.
    /// </summary>
    public void ApplyWriterStyleToCards(IReadOnlyList<string> nodeIds, WriterStyleChange change)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentNullException.ThrowIfNull(change);

        var cards = new List<(DialogueNode Node, List<(string LineId, int Length)> Lines)>();

        foreach (string nodeId in nodeIds.Distinct(StringComparer.Ordinal))
        {
            DialogueNode node = RequireDialogue(nodeId);
            List<(string, int)> lines = node.Script.ActiveLines
                .Select(line => (line.Id, node.Script.Text(line.Id).Text.Length))
                .Where(line => line.Length > 0)
                .ToList();

            if (lines.Count > 0)
            {
                cards.Add((node, lines));
            }
        }

        if (cards.Count == 0)
        {
            return;
        }

        bool on = WriterMarkMath.IsToggle(change.Kind) &&
                  !cards.All(card =>
                      (change.Kind == WriterStyleKind.Bold && card.Node.WriterBase is { Bold: true }) ||
                      card.Lines.All(line => WriterMarkMath.IsAll(
                          card.Node.WriterMarks.GetValueOrDefault(line.LineId), line.Length, 0, line.Length,
                          look => WriterMarkMath.Has(look, change.Kind))));

        var next = new List<(DialogueNode Node, string LineId, List<WriterSpan> Spans)>();

        foreach ((DialogueNode node, List<(string LineId, int Length)> lines) in cards)
        {
            foreach ((string lineId, int length) in lines)
            {
                List<WriterSpan> spans = WriterMarkMath.Apply(
                    node.WriterMarks.GetValueOrDefault(lineId) ?? [], length, 0, length, look => WriterMarkMath.Change(look, change, on));

                if (!SameSpans(node.WriterMarks.GetValueOrDefault(lineId), spans))
                {
                    next.Add((node, lineId, spans));
                }
            }
        }

        if (next.Count == 0)
        {
            return;
        }

        string? only = next.Select(item => item.Node.Id).Distinct(StringComparer.Ordinal).Count() == 1 ? next[0].Node.Id : null;
        Mutate(ProjectChangeKind.WriterMarks, () =>
        {
            foreach ((DialogueNode node, string lineId, List<WriterSpan> spans) in next)
            {
                SetLineMarks(node, lineId, spans);
            }
        }, only);
    }

    /// <summary>
    /// 카드들의 처음 값(<see cref="DialogueNode.WriterBase"/> — 기본 크기 · 줄 간격 등)을 한 번에 바꾼다 — 서식 도구가 카드(여러 장도)를 골랐을 때(WF-D21).
    /// 카드마다 <paramref name="change"/>가 새 값을 낸다. 처음 값이 안 적힌 카드는 새 카드의 처음 값(<see cref="NewCardWriterBase"/>) 위에서 — 그것도 없으면
    /// 건너뛴다. 되돌리기 한 칸이고, 바뀐 것이 없으면 적지 않는다.
    /// </summary>
    public void SetWriterBase(IReadOnlyList<string> nodeIds, Func<WriterBase, WriterBase> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        SetWriterBase(nodeIds, (_, basis) => change(basis));
    }

    /// <summary>
    /// <see cref="SetWriterBase(IReadOnlyList{string}, Func{WriterBase, WriterBase})"/>와 같고, 새 값을 낼 때 그 카드를 함께 본다 — 카드마다 끝이 다른 값(그 카드의
    /// 가장 좁은 줄 간격 · WF-D24)을 당길 때.
    /// </summary>
    public void SetWriterBase(IReadOnlyList<string> nodeIds, Func<DialogueNode, WriterBase, WriterBase> change)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentNullException.ThrowIfNull(change);

        var next = new List<(DialogueNode Node, WriterBase Value)>();

        foreach (string nodeId in nodeIds.Distinct(StringComparer.Ordinal))
        {
            DialogueNode node = RequireDialogue(nodeId);

            if ((node.WriterBase ?? NewCardWriterBase?.Invoke()) is not { } basis)
            {
                continue;
            }

            WriterBase value = change(node, basis);

            if (node.WriterBase is null || value != node.WriterBase)
            {
                next.Add((node, value));
            }
        }

        if (next.Count == 0)
        {
            return;
        }

        Mutate(ProjectChangeKind.WriterMarks, () =>
        {
            foreach ((DialogueNode node, WriterBase value) in next)
            {
                node.WriterBase = value;
            }
        }, next.Count == 1 ? next[0].Node.Id : null);
    }

    /// <summary>카드들의 좌우 정렬을 한 번에(WF-D21) — 되돌리기 한 칸. 같은 카드는 적지 않는다.</summary>
    public void SetWriterAlign(IReadOnlyList<string> nodeIds, WriterAlign align)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        List<DialogueNode> changing = nodeIds.Distinct(StringComparer.Ordinal).Select(RequireDialogue).Where(node => node.WriterAlign != align).ToList();

        if (changing.Count == 0)
        {
            return;
        }

        Mutate(ProjectChangeKind.WriterMarks, () =>
        {
            foreach (DialogueNode node in changing)
            {
                node.WriterAlign = align;
            }
        }, changing.Count == 1 ? changing[0].Id : null);
    }

    /// <summary>카드 글 칸의 좌우 정렬. 같으면 적지 않는다.</summary>
    public void SetWriterAlign(string nodeId, WriterAlign align)
    {
        DialogueNode node = RequireDialogue(nodeId);

        if (node.WriterAlign == align)
        {
            return;
        }

        Mutate(ProjectChangeKind.WriterMarks, () => node.WriterAlign = align, nodeId);
    }

    /// <summary>
    /// 카드 글 칸의 줄 간격(글자 크기에 대한 %) — 그 카드의 처음 값(<see cref="DialogueNode.WriterBase"/>)을 바꾼다(writer-formatting WF-D19). 같으면 적지
    /// 않는다. 처음 값이 안 적힌 카드는 새 카드의 처음 값(<see cref="NewCardWriterBase"/>) 위에 적는다.
    /// </summary>
    public void SetWriterLineSpacing(string nodeId, int lineSpacing)
    {
        DialogueNode node = RequireDialogue(nodeId);

        if (lineSpacing <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineSpacing), lineSpacing, "줄 간격은 0보다 커야 합니다.");
        }

        WriterBase basis = node.WriterBase ?? NewCardWriterBase?.Invoke() ??
            throw new InvalidOperationException($"카드 '{nodeId}'에 처음 글꼴 · 크기가 없어 줄 간격을 적을 수 없습니다.");

        if (node.WriterBase is not null && basis.LineSpacing == lineSpacing)
        {
            return;
        }

        WriterBase next = basis with { LineSpacing = lineSpacing };
        Mutate(ProjectChangeKind.WriterMarks, () => node.WriterBase = next, nodeId);
    }

    /// <summary>
    /// 대사 글이 바뀌는 두 자리(<see cref="ApplyScriptSync"/> · <see cref="SetScriptLineText"/>)가 <b>같은 변경 안에서</b> 부른다 — 서식이 같은 글자를 따라간다.
    /// 기본 언어의 글일 때만이다(서식은 기본 언어의 글에 붙는다).
    /// </summary>
    private static void RebaseWriterMarks(DialogueNode node, string locale, string lineId, string? before, string? after)
    {
        if (!string.Equals(locale, node.Script.PrimaryLocale, StringComparison.Ordinal) ||
            !node.WriterMarks.TryGetValue(lineId, out IReadOnlyList<WriterSpan>? spans) ||
            string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        SetLineMarks(node, lineId, WriterMarkMath.Rebase(spans, before ?? string.Empty, after ?? string.Empty));
    }

    private static void SetLineMarks(DialogueNode node, string lineId, List<WriterSpan> spans)
    {
        if (spans.Count == 0)
        {
            node.WriterMarks.Remove(lineId);
        }
        else
        {
            node.WriterMarks[lineId] = spans;
        }
    }

    private static bool SameSpans(IReadOnlyList<WriterSpan>? now, IReadOnlyList<WriterSpan> next) =>
        (now ?? []).SequenceEqual(next);
}
