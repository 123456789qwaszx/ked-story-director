using System.Text;

namespace Vn.Authoring.Rendering;

/// <summary>
/// 문서를 사람이 읽고 쓰는 글로 편다 — <b>시나리오 표기</b>(카드 안 글 칸, D4) 하나다.
/// Segment 목록과 원본 매핑은 그대로 두고, 사람이 보는 표기만 바꾼다.
///
/// ⛔ Yarn · 녹음 대본 · 번역 대본 표기는 2026-10-29에 걷혔다 — 그것을 고르던 Script Preview 탭이
/// 유일한 독자였다(소유자: "더 이상 필요없으니 완전히 제거하십시오"). 게임용 Yarn은 ked-story-compositor가 낸다.
/// </summary>
public static class DocumentPreviewFormatter
{
    public static string Format(RenderedDocument document) => FormatWithRows(document).Text;

    /// <summary>
    /// 펼친 글과, 대본의 줄(LineId)마다 그 줄이 선 <b>글의 몇 번째 줄</b>인가(0부터) — 대사 줄과 선택지 줄만 든다.
    /// 연출 그래프 카드가 분기 점을 그 대사의 높이에 세울 때 쓴다(2026-10-30).
    /// </summary>
    public static (string Text, IReadOnlyDictionary<string, int> RowOfLine) FormatWithRows(RenderedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Options.Format == DocumentOutputFormat.Scenario
            ? FormatScenario(document)
            : throw new ArgumentOutOfRangeException(
                nameof(document),
                document.Options.Format,
                "글로 펴는 문서는 시나리오 표기뿐입니다.");
    }

    private static (string Text, IReadOnlyDictionary<string, int> RowOfLine) FormatScenario(RenderedDocument document)
    {
        var builder = new StringBuilder();
        var rowOfLine = new Dictionary<string, int>(StringComparer.Ordinal);
        int row = 0;

        foreach (RenderedSegment segment in document.Segments)
        {
            if (segment.Kind is RenderedSegmentKind.DialogueLine or RenderedSegmentKind.ChoiceOption &&
                segment.Source.LineId is { Length: > 0 } lineId)
            {
                rowOfLine.TryAdd(lineId, row);
            }

            int before = builder.Length;

            switch (segment.Kind)
            {
                case RenderedSegmentKind.NodeHeader:
                    builder.Append("[장면] ")
                        .Append(string.IsNullOrWhiteSpace(segment.Text)
                            ? segment.Source.NodeId
                            : segment.Text)
                        .Append('\n');
                    break;

                // 조건은 실제 Yarn 문법으로 쓴다 (X11). ScenarioOnly가 편집·붙여넣기
                // 입력면이 되므로(X12) 표기 문법과 파싱 문법이 같아야 왕복이 성립한다.
                // 조립은 YarnSyntax 재사용 — Preview·이미터와 같은 규칙 하나다.
                case RenderedSegmentKind.ConditionBegin:
                    YarnSyntax.AppendCondition(builder, "if", segment.Expression);
                    builder.Append('\n');
                    break;

                case RenderedSegmentKind.ConditionElseIf:
                    YarnSyntax.AppendCondition(builder, "elseif", segment.Expression);
                    builder.Append('\n');
                    break;

                case RenderedSegmentKind.ConditionEnd:
                    builder.Append("<<endif>>\n");
                    break;

                case RenderedSegmentKind.DialogueLine:
                    AppendDialogue(builder, document.Options, segment, lineIdPrefix: null);
                    break;

                case RenderedSegmentKind.ChoiceOption:
                    builder.Append("-> ").Append(segment.Text ?? string.Empty).Append('\n');
                    break;

                case RenderedSegmentKind.Warning:
                    builder.Append("[주의] ").Append(segment.Text).Append('\n');
                    break;

                case RenderedSegmentKind.NodeFooter:
                    break;
            }

            for (int at = before; at < builder.Length; at++)
            {
                if (builder[at] == '\n')
                {
                    row++;
                }
            }
        }

        return (builder.ToString(), rowOfLine);
    }

    private static void AppendDialogue(
        StringBuilder builder,
        DocumentOutputOptions options,
        RenderedSegment segment,
        string? lineIdPrefix)
    {
        if (options.IncludeLineId && segment.Source.LineId is { Length: > 0 } lineId)
        {
            if (lineIdPrefix is null)
            {
                builder.Append('[').Append(lineId).Append("] ");
            }
            else
            {
                builder.Append('[')
                    .Append(lineIdPrefix)
                    .Append(' ')
                    .Append(lineId)
                    .Append("] ");
            }
        }

        if (options.IncludeSpeaker && !string.IsNullOrWhiteSpace(segment.Speaker))
        {
            builder.Append(segment.Speaker).Append(": ");
        }

        if (options.IncludeDialogueText)
        {
            builder.Append(segment.Text ?? string.Empty);
        }

        builder.Append('\n');
    }

}
