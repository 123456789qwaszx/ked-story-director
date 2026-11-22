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
