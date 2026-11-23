using Vn.Authoring.Chapters;
using Vn.Authoring.Editing;
using Vn.Authoring.Model;

namespace Vn.Authoring.Tests.Chapters;

/// <summary>
/// <b>식이 성립하지 않으면 검증 보고가 까닭과 고치는 법을 말한다</b>(2026-11-23 소유자 — 「식은 사실 일정한 양식이 있을 텐데, 이거 만약 잘못 적었을
/// 경우, 구성탭에서 검증 보고쪽에 식이 성립하지 않는 다고 띄워주고, 구체적인 예시까지 보기좋게 설명을 하는 방식으로 해주십시오」).
///
/// 못 박는 것: ① 흔히 틀리는 꼴(<c>=&gt;</c> · <c>=</c> 하나 · <c>!=</c> · <c>&amp;&amp;</c> · <c>$키</c> · 값 없음)은 그 까닭을 바로 말한다 —
/// <c>=&gt;</c>가 「없는 스탯키 'trust ='」로 읽히지 않는다 ② 조건의 진단은 「식이 성립하지 않습니다」로 시작하고, 첫 줄에만 고치는 법이 붙는다
/// ③ 고치는 법의 예시는 그 챕터의 스탯(정수는 범위 가운데 값 · 활성/비활성은 true)으로 들고 식이 한 칸으로 줄을 선다 ④ 스탯이 없으면 스탯부터
/// 더하라고 말한다 ⑤ 맞는 식 · 식 미정에는 고치는 법이 없다.
/// </summary>
public sealed class ConditionExpressionHelpTests
{
    private static readonly string[] Keys = ["trust", "met_harin"];

    [Theory]
    [InlineData("trust => 3", "'>='로 씁니다")]
    [InlineData("trust =< 3", "'<='로 씁니다")]
    [InlineData("trust = 3", "'=='로 씁니다")]
    [InlineData("trust != 3", "「같지 않다」(!= · <>)는 쓸 수 없습니다")]
    [InlineData("trust >= 3 && met_harin == true", "';'로 잇습니다")]
    [InlineData("trust >= 3, met_harin == true", "';'로 잇습니다")]
    [InlineData("trust >= 3 and met_harin == true", "';'로 잇습니다")]
    [InlineData("$trust >= 3", "$를 붙이지 않습니다. 'trust'처럼")]
    [InlineData("trust >=", "비교 오른쪽에 값이 없습니다")]
    [InlineData("trust >= 높음", "값은 정수")]
    [InlineData("호감 >= 3", "'호감'는 스탯 표에 없는 키입니다. 있는 키: trust, met_harin")]
    public void 흔히_틀리는_꼴은_그_까닭을_바로_말한다(string expression, string reason)
    {
        ConditionParseResult parsed = ConditionExpressionParser.Parse(expression, Keys);

        Assert.False(parsed.IsValid);
        ConditionParseProblem problem = Assert.Single(parsed.Problems);
        Assert.Contains(reason, problem.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'trust ='", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("trust >= 3")]
    [InlineData("trust <= 3; met_harin == true")]
    [InlineData("trust == 0")]
    [InlineData("met_harin == false")]
    public void 맞는_식은_그대로_읽힌다(string expression) =>
        Assert.True(ConditionExpressionParser.Parse(expression, Keys).IsValid);

    [Fact]
    public void 성립하지_않는_식은_까닭과_그_챕터의_스탯으로_든_예시를_보고한다()
    {
        ProjectEditor editor = World(new ChapterStat("trust", 0, 0, 10), new ChapterStat("met_harin", 0, 0, 1, ChapterStatType.Bool));
        editor.AddChapterCondition("ch01", "친해짐", "trust => 3; 호감 >= 2");

        List<ChapterDiagnostic> found = Diagnostics(editor, "친해짐");

        Assert.Equal(2, found.Count);
        Assert.All(found, item => Assert.Equal(ChapterDiagnosticSeverity.Error, item.Severity));
        Assert.All(found, item => Assert.StartsWith("조건 '친해짐' — 식이 성립하지 않습니다: ", item.Message, StringComparison.Ordinal));
        Assert.Contains("'>='로 씁니다", found[0].Message, StringComparison.Ordinal);
        Assert.Contains("'호감'는 스탯 표에 없는 키입니다", found[1].Message, StringComparison.Ordinal);

        // 고치는 법은 그 조건의 첫 줄에 한 번 — 이 챕터의 스탯으로 든다.
        Assert.Null(found[1].Help);
        string help = Assert.IsType<string>(found[0].Help);
        string[] lines = help.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        Assert.Equal("식은 「스탯 키  비교  값」입니다. 이 챕터의 스탯으로 적으면:", lines[0]);
        Assert.Contains(lines, line => line.Contains("trust >= 5", StringComparison.Ordinal) && line.EndsWith("trust 값이 5 이상일 때", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("met_harin == true", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("trust >= 5; met_harin == true", StringComparison.Ordinal) && line.Contains("둘 다 맞을 때", StringComparison.Ordinal));

        // 예시의 식은 한 칸으로 줄을 선다 — 뜻이 같은 자리에서 시작한다.
        int[] starts = lines.Where(line => line.StartsWith("    ", StringComparison.Ordinal))
            .Select(line => line.LastIndexOf("    ", StringComparison.Ordinal) + 4).ToArray();
        Assert.True(starts.Length >= 5);
        Assert.Single(starts.Distinct());

        // Describe(상태줄 · 한 줄)에는 들지 않는다.
        Assert.DoesNotContain('\n', found[0].Describe());
    }

    [Fact]
    public void 스탯이_없으면_스탯부터_더하라고_말하고_흔한_이름으로_든다()
    {
        ProjectEditor editor = World();
        editor.AddChapterCondition("ch01", "친해짐", "trust >= 3");

        ChapterDiagnostic found = Assert.Single(Diagnostics(editor, "친해짐"));

        Assert.Contains("이 챕터에는 스탯이 아직 없습니다", found.Message, StringComparison.Ordinal);
        Assert.Contains("스탯 표에서 먼저 더하고", found.Help, StringComparison.Ordinal);
        Assert.Contains("trust >= 5", found.Help, StringComparison.Ordinal);
    }

    [Fact]
    public void 맞는_식과_식_미정에는_고치는_법이_없다()
    {
        ProjectEditor editor = World(new ChapterStat("trust", 0, 0, 10));
        editor.AddChapterCondition("ch01", "맞음", "trust >= 3");
        editor.AddChapterCondition("ch01", "미정", string.Empty);

        Assert.Empty(Diagnostics(editor, "맞음"));
        ChapterDiagnostic undecided = Assert.Single(Diagnostics(editor, "미정"));
        Assert.Equal(ChapterDiagnosticSeverity.Warning, undecided.Severity);
        Assert.Null(undecided.Help);
    }

    private static ProjectEditor World(params ChapterStat[] stats)
    {
        var editor = new ProjectEditor(new StoryProject());
        editor.EnsureChapter("ch01").Stats.AddRange(stats);
        editor.AddEpisode("ch01", "root", title: "root", 0, 0, sceneId: "root");
        return editor;
    }

    private static List<ChapterDiagnostic> Diagnostics(ProjectEditor editor, string label) =>
        editor.FindChapter("ch01")!.ToGraphModel(string.Empty).Diagnostics
            .Where(item => item.Message.StartsWith($"조건 '{label}' — ", StringComparison.Ordinal))
            .ToList();
}
