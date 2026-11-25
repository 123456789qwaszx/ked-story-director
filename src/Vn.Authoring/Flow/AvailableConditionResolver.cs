using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Flow;

/// <summary>
/// 카드가 볼 수 있는 조건 하나의 읽기 모델 — <b>그 카드의 챕터 조건</b>이다. 편집 화면과 흐름 해석기는 이 중립적인 형태만 본다.
/// <see cref="Id"/>는 챕터 조건의 이름이다 — 대본의 줄 전환이 이 글자로 가리킨다(이름을 바꾸면 전환도 함께 간다 · <c>ProjectEditor.RenameChapterCondition</c>).
/// <see cref="Expression"/>은 대사 안에서 묻는 Yarn 식(<c>stat("trust") &gt;= 3</c>)이다.
/// ⛔ 조건의 출처(<c>SourceKind</c> — 옛 「X 설정」 노드의 작가 조건 · 게임 전역 조건)를 가르던 칸은 걷혔다 — 조건의 자리는 챕터 하나다
/// (old-shapes O6 · <c>definition-split-orders.md</c> S2).
/// </summary>
public sealed record AvailableCondition(
    string Id,
    string Name,
    string Expression)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

public sealed class AvailableConditionCatalog
{
    private readonly Dictionary<string, AvailableCondition> _byId;

    public AvailableConditionCatalog(IReadOnlyList<AvailableCondition> conditions)
    {
        Conditions = conditions;
        _byId = conditions.ToDictionary(condition => condition.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<AvailableCondition> Conditions { get; }

    public AvailableCondition? Find(string? conditionId)
    {
        return conditionId is not null && _byId.TryGetValue(conditionId, out AvailableCondition? condition)
            ? condition
            : null;
    }
}

/// <summary>
/// 대사 카드가 쓸 수 있는 조건 — <b>그 카드가 선 판의 챕터 조건</b>이다(범위는 챕터 · 2026-08-17 소유자: "시나리오 작가가 만든 조건, 변수 등은
/// 챕터 단위로 기록이 되어서 사용됐으면 해").
///
/// <b>사본이 없다</b> — 조건은 [구성] 탭의 챕터 조건 하나뿐이고, 대사가 묻는 Yarn 식은 쓸 때마다 그 식에서 옮긴다
/// (<see cref="ConditionYarnTranslator"/>). 그래서 식을 고치면 다음에 그리거나 내보낼 때 그대로 따라간다.
/// ⛔ 2026-11-25까지는 판마다 숨은 「조건 공급 노드」(<c>SetNode</c> — 이름 「챕터 {판} 조건」)가 옮긴 사본을 들고, 줄 전환은 그 사본의 Id를
/// 가리켰다. 두 벌을 코드가 맞추느라 식을 고쳐도 사본이 옛 식에 머무는 버그가 났다(condition-supply C1 — 소유자 「이 노드가 필요한가?」).
/// </summary>
public static class AvailableConditionResolver
{
    /// <summary>
    /// <b>식 미정</b>인 조건이 대사에서 서는 식 — 항상 통과한다(writer-fields W1의 규칙: 식이 빈 조건은 관문이 없는 것과 같다).
    /// 빈 식을 그대로 내면 <c>&lt;&lt;if &gt;&gt;</c>가 되어 Yarn 문법 오류다.
    /// </summary>
    public const string UndecidedYarn = "true";

    public static AvailableConditionCatalog Resolve(
        StoryProject project,
        string dialogueNodeId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.FindDialogue(dialogueNodeId) is not { } node ||
            project.FindFileContainingNode(node.Id) is not { } board ||
            project.Chapters.FirstOrDefault(chapter =>
                string.Equals(chapter.ChapterId, board.Name, StringComparison.Ordinal)) is not { } chapter)
        {
            return new AvailableConditionCatalog(Array.Empty<AvailableCondition>());
        }

        return Of(chapter);
    }

    /// <summary>그 챕터의 조건 전부 — 챕터에 적힌 순서. 이름이 비었거나 겹치는 행은 뺀다(검증이 짚는다).</summary>
    public static AvailableConditionCatalog Of(ChapterDocument chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var conditions = new List<AvailableCondition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (ChapterCondition condition in chapter.Conditions)
        {
            string label = condition.Label?.Trim() ?? string.Empty;

            if (label.Length == 0 || !seen.Add(label))
            {
                continue;
            }

            conditions.Add(new AvailableCondition(label, label, YarnOf(chapter, condition)));
        }

        return new AvailableConditionCatalog(conditions);
    }

    /// <summary>
    /// 대사 안에서 묻는 식 — 식 미정이면 <see cref="UndecidedYarn"/>, 옮길 수 있으면 Yarn 식, 틀린 식이면 적힌 그대로다(검증 보고가 짚고, 진행
    /// JSON · Yarn은 막힌다 — 글 칸에는 작가가 적은 글자가 보인다).
    /// </summary>
    public static string YarnOf(ChapterDocument chapter, ChapterCondition condition)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(condition);

        if (string.IsNullOrWhiteSpace(condition.Expression))
        {
            return UndecidedYarn;
        }

        return ConditionYarnTranslator.Translate(chapter.Reparse(condition)).Yarn ?? condition.Expression.Trim();
    }

    /// <summary>
    /// 그 카드의 챕터와 상관없이 그 이름의 조건이 어느 챕터에든 있는지 찾는다 — 다른 챕터에 있는 조건과 아예 없는 조건을 가른다.
    /// </summary>
    public static AvailableCondition? FindKnown(
        StoryProject project,
        string? conditionId)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrEmpty(conditionId))
        {
            return null;
        }

        foreach (ChapterDocument chapter in project.Chapters)
        {
            if (Of(chapter).Find(conditionId) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    public static string UnavailableLabel(AvailableCondition? known, string conditionId)
    {
        string name = known?.DisplayName ?? conditionId;
        return string.Format(Msg.Diag_Unavailable, name);
    }

    /// <summary>
    /// 화면에 세울 이름 — 조건의 이름 그대로다.
    ///
    /// ⛔ 2026-11-17 소유자: 챕터 조건 앞의 「[기획]」을 걷었다. 2026-08-17에는 작가가 고칠 수 없는 조건이 왜 보이는지를 이름으로
    /// 말하려고 붙였지만, 이제 조건 이름은 작가가 [대본] 탭에서 짓고 분기에 직접 건다(two-graphs.md §4) — 작가의 것이다.
    /// </summary>
    public static string LayeredLabel(AvailableCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return condition.DisplayName;
    }
}
