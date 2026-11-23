namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>조건 식을 고치는 법</b> — 식이 성립하지 않을 때 검증 보고가 그 아래에 세우는 여러 줄 글(<see cref="ChapterDiagnostic.Help"/>).
/// 2026-11-23 소유자 — 「식은 사실 일정한 양식이 있을 텐데, 이거 만약 잘못 적었을 경우, 구성탭에서 검증 보고쪽에 식이 성립하지 않는 다고 띄워주고,
/// 구체적인 예시까지 보기좋게 설명을 하는 방식으로 해주십시오」.
///
/// 예시는 <b>그 챕터의 스탯으로</b> 든다 — 정수 스탯은 그 범위 가운데 값으로 넘고 · 못 미치고 · 꼭 같은 꼴, 활성/비활성 스탯은
/// <c>== true</c>, 둘이 다 있으면 <c>;</c>로 이은 꼴. 스탯이 아직 없으면 스탯부터 더하라고 말하고 흔한 이름으로 든다. 식의 왼쪽(예시)은 줄을 맞춰
/// 고정폭 글꼴에서 한 칸으로 선다.
/// </summary>
public static class ConditionExpressionHelp
{
    /// <summary>스탯이 없을 때 드는 정수 스탯 이름.</summary>
    internal const string SampleNumber = "trust";

    /// <summary>스탯이 없을 때 드는 활성/비활성 스탯 이름.</summary>
    internal const string SampleFlag = "met_harin";

    public static string Examples(IReadOnlyList<ChapterStat> stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        ChapterStat? number = stats.FirstOrDefault(stat => stat.Type == ChapterStatType.Int);
        ChapterStat? flag = stats.FirstOrDefault(stat => stat.Type == ChapterStatType.Bool);
        bool sample = number is null && flag is null;

        if (sample)
        {
            number = new ChapterStat(SampleNumber, 0, 0, 10);
            flag = new ChapterStat(SampleFlag, 0, 0, 1, ChapterStatType.Bool);
        }

        var rows = new List<(string Expression, string Meaning)>();

        if (number is not null)
        {
            int middle = Middle(number);
            rows.Add(($"{number.Key} >= {middle}", $"{number.Key} 값이 {middle} 이상일 때"));
            rows.Add(($"{number.Key} < {middle}", $"{number.Key} 값이 {middle} 미만일 때"));
            rows.Add(($"{number.Key} == {number.Minimum}", $"{number.Key} 값이 꼭 {number.Minimum}일 때"));
        }

        if (flag is not null)
        {
            rows.Add(($"{flag.Key} == true", $"{flag.Key} 스탯이 켜져 있을 때 (활성/비활성 스탯)"));
            rows.Add(($"{flag.Key} == false", $"{flag.Key} 스탯이 꺼져 있을 때"));
        }

        if (number is not null && flag is not null)
        {
            rows.Add(($"{number.Key} >= {Middle(number)}; {flag.Key} == true", "둘 다 맞을 때 — 여럿은 ;로 잇습니다"));
        }

        int width = rows.Max(row => row.Expression.Length);
        var text = new System.Text.StringBuilder();

        text.AppendLine(sample
            ? "식은 「스탯 키  비교  값」입니다. 이 챕터에는 스탯이 아직 없습니다 — 「조건 및 스탯」의 스탯 표에서 먼저 더하고, 예컨대 이렇게 적습니다:"
            : "식은 「스탯 키  비교  값」입니다. 이 챕터의 스탯으로 적으면:");

        foreach ((string expression, string meaning) in rows)
        {
            text.Append("    ").Append(expression.PadRight(width)).Append("    ").AppendLine(meaning);
        }

        text.Append("비교는 >= · <= · > · < · == 다섯 가지, 값은 정수만(활성/비활성 스탯은 true · false)입니다. 스탯 키는 스탯 표에 있는 그대로 적습니다.");
        return text.ToString();
    }

    /// <summary>예시 값 — 범위의 가운데(최소보다는 크게). 범위가 비었으면 최소+1.</summary>
    private static int Middle(ChapterStat stat) =>
        stat.Maximum > stat.Minimum
            ? Math.Max(stat.Minimum + 1, stat.Minimum + ((stat.Maximum - stat.Minimum + 1) / 2))
            : stat.Minimum + 1;
}
