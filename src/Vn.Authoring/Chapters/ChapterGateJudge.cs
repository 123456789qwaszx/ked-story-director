namespace Vn.Authoring.Chapters;

/// <summary>관문 하나의 판정 결과 — 지나갈 수 있는가만이 아니라 <b>왜 못 가는가</b>까지 가른다.</summary>
public enum ChapterGateVerdict
{
    /// <summary>지나갈 수 있다 (라벨 없음 포함 — 관문 없는 길은 언제나 열려 있다).</summary>
    Open,

    /// <summary>조건이 지금 스탯으로 서지 않는다.</summary>
    Blocked,

    /// <summary>라벨이 `조건` 시트에 없거나 식이 깨졌다 — 구조 검증의 오류이지 스탯의 문제가 아니다.</summary>
    Broken
}

/// <summary>
/// 간선 관문(표시·해금조건) 판정의 <b>단일 구현</b> (2026-08-27) — 도달성 증명·픽스처
/// 워커가 이 하나를 본다(무대 프리뷰도 보던 것인데 2026-10-29에 compositor로 갔다). 예전에는 증명기와 워커가 같은 판정을 각자
/// 들고 있었다(규칙이 두 벌이면 한쪽만 고쳐질 때 증명과 플레이가 갈린다).
///
/// 깨진 라벨(<see cref="ChapterGateVerdict.Broken"/>)은 "지나갈 수 없음"이다 —
/// 깨진 조건을 통과시켜 도달 가능을 부풀리지 않는다(증명기의 원래 규칙 그대로).
/// </summary>
public static class ChapterGateJudge
{
    /// <param name="stats"><paramref name="chapter"/>의 `스탯` 시트 순서와 같은 자리의 현재 값.</param>
    public static ChapterGateVerdict Judge(ChapterGraphModel chapter, string? conditionLabel, int[] stats)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(stats);

        if (string.IsNullOrEmpty(conditionLabel))
        {
            return ChapterGateVerdict.Open;
        }

        if (chapter.FindCondition(conditionLabel) is not { IsValid: true } condition)
        {
            return ChapterGateVerdict.Broken;
        }

        foreach (ConditionTerm term in condition.Parsed)
        {
            if (!CompareStat(chapter, term, stats))
            {
                return ChapterGateVerdict.Blocked;
            }
        }

        return ChapterGateVerdict.Open;
    }

    private static bool CompareStat(ChapterGraphModel chapter, ConditionTerm term, int[] stats)
    {
        int index = -1;

        for (int position = 0; position < chapter.Stats.Count; position++)
        {
            if (string.Equals(chapter.Stats[position].Key, term.Key, StringComparison.Ordinal))
            {
                index = position;
                break;
            }
        }

        if (index < 0)
        {
            return false; // 미등록 스탯키 — 구조 검증이 이미 오류로 잡았다.
        }

        return term.Comparison switch
        {
            ConditionComparison.AtLeast => stats[index] >= term.Value,
            ConditionComparison.AtMost => stats[index] <= term.Value,
            ConditionComparison.Above => stats[index] > term.Value,
            ConditionComparison.Below => stats[index] < term.Value,
            _ => stats[index] == term.Value
        };
    }
}
