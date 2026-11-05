namespace Vn.Authoring.Chapters;

/// <summary>
/// 그 에피소드에 <b>도착했을 때</b> 스탯 하나가 가질 수 있는 폭. 노드에 표시되며,
/// 여러 루트가 있을 때는 스탯의 최소·최대를 보인다.
///
/// 값은 <b>도착 직후</b>다 — 그 노드로 들어오는 간선의 증감까지 커밋한 뒤. 루트가 하나면
/// 최소·최대가 같고, 갈래가 여럿이면 벌어진다. 증명이 이미 (에피소드, 스탯 벡터)로 걷고
/// 있으므로 걷는 김에 적어 두는 것이고 따로 계산하지 않는다.
/// </summary>
public sealed record ChapterStatSpan(string Key, string DisplayName, int Minimum, int Maximum)
{
    /// <summary>어느 루트로 와도 같은 값인가.</summary>
    public bool IsFixed => Minimum == Maximum;
}

/// <param name="ReachableEpisodeIds">시작 에피소드에서 어떤 플레이로든 닿을 수 있는 에피소드.</param>
/// <param name="ExplorationComplete">
/// 상태공간을 끝까지 훑었는가. 상한에 걸려 중단했으면 false이고, 그때 "도달 불가"는
/// 단정이 아니라 경고로 낮춰 보고돼 있다 — 증명하지 못한 것을 오류라고 말하지 않는다.
/// </param>
/// <param name="VisitedStates">
/// 훑은 <b>(에피소드, 스탯 벡터)</b> 상태의 수. <see cref="ChapterReachabilityProver.StateLimit"/>
/// 까지 센다.
///
/// <b>이 숫자가 축소를 할지 말지를 정한다</b>. 깃발은 0/1 정수라 <b>갈래마다
/// 다르게 켜지면 상태공간을 두 배로</b> 만든다 — 그리고 깃발은 바로 그러려고 만든다. 작가
/// 변수가 없어 그런 기억이 전부 챕터 스탯으로 들어오므로, 언젠가 상한에 닿는다.
/// <b>언제인지는 실제 대본을 돌려 봐야 안다.</b> 그래서 센다.
/// </param>
public sealed record ChapterReachabilityResult(
    IReadOnlySet<string> ReachableEpisodeIds,
    IReadOnlyList<ChapterDiagnostic> Diagnostics,
    bool ExplorationComplete,
    IReadOnlyDictionary<string, IReadOnlyList<ChapterStatSpan>>? StatSpans = null,
    int VisitedStates = 0)
{
    public bool HasErrors =>
        Diagnostics.Any(item => item.Severity == ChapterDiagnosticSeverity.Error);

    /// <summary>그 에피소드 도착 시점의 스탯 폭. 못 가는 에피소드는 비어 있다.</summary>
    public IReadOnlyList<ChapterStatSpan> SpansFor(string episodeId) =>
        StatSpans is not null && StatSpans.TryGetValue(episodeId, out IReadOnlyList<ChapterStatSpan>? spans)
            ? spans
            : [];
}

/// <summary>
/// 정적 도달성 증명 (§0.5 보증 2) — "작가가 무엇을 저장하든, 특정 에피소드로 절대 못 가는
/// 상태를 만들 수 없다"의 장치.
///
/// 상태 = (에피소드, 스탯 정수 벡터). 스탯이 2~5개·정수·`최소~최대` 유한 범위라 상태공간이
/// 유한하고 <b>완전 탐색이 된다</b> — 경계값 버그를 막으려던 정수 고정(G-3)이 이 증명을
/// 가능하게 만들었다. float이면 여기서 결정 불가능이다.
///
/// 스탯은 <b>간선을 탈 때</b> 그 간선의 증감(<see cref="ChapterEdge.StatChanges"/>)만큼 변한다 —
/// 근사 없는 정확 전이다(<see cref="ApplyDeltas"/>).
///
/// 조건이 묻는 것은 <b>스탯뿐이고</b>, 스탯은 걷는 도중에 정해진다 — 그래서 한 번만 걷는다.
/// 도달 가능 집합 자체를 참조하는 조건이 없으므로 고정점 반복이 필요 없다.
///
/// <para>
/// 도달성은 저작 시각의 일이라 이 증명기가 정본이고, 맞출 외부 구현이 없으므로 자유롭게 고친다.
/// </para>
/// <para>
/// <b>사본은 일부러 안 고친다.</b> 이 저장소의
/// <c>src/Ked.Progression/Reachability/ChapterReachability.cs</c>는
/// <c>ChapterReachabilityEquivalenceTests</c>로 이쪽과 대조된다 — 지문
/// (<c>runtime-sync.txt</c>)에는 없으므로 <b>고칠 수는 있지만 안 고친다</b>.
/// <b>상태 벡터 축소</b>는 도달 가능 집합을 한 글자도 바꾸면 안 되는 변경이고, 그러면
/// <b>안 건드린 사본이 정확히 옳은 기준</b>이다: 이쪽만 고쳐서 대조가 초록이면 그것이
/// 기계적 증거다. 양쪽을 함께 고치면 같은 실수를 두 번 쓰고 테스트는 초록이 된다.
/// (아래 인접 색인도 같은 이유로 이쪽에만 둔다.)
///
/// <b>한계</b>: 사본은 <b>같은 사람이 함께 고칠 사본</b>이라 <b>독립된</b> 증인이 아니다.
/// 같은 오해는 두 곳에 똑같이 들어가고, 잡는 것은 <b>한쪽만 고친 실수</b>뿐이다. 그래도
/// 그것이 축소에서 제일 있을 법한 실수라 값을 한다.
/// </para>
/// <para>
/// 화면이 이 결과를 (내용해시 → 결과) 캐시 뒤에서 부르는 것은 <b>상관없다</b> — 캐시는
/// 화면의 사정이고 답을 바꾸지 않는다.
/// </para>
/// </summary>
public static class ChapterReachabilityProver
{
    /// <summary>
    /// 완전 탐색 상한. 스탯 5개 × 범위 0~10이라도 이 안에 넉넉히 든다.
    ///
    /// <b>전체 곱이 아니라 실제로 닿는 조합만 센다</b> — 간선을 타야 상태가 생긴다.
    /// 그래서 값의 범위가 넓어도 대개 남는데, <b>깃발이 합류 지점에서 만나면</b> 곱해진다.
    /// </summary>
    internal const int StateLimit = 250_000;

    /// <summary>
    /// 스탯 증감의 원천은 <b>간선</b> 하나다. 에피소드 안에서는 스탯이 변하지 않으므로,
    /// 과대근사 없이 간선의 정확값으로 전이한다 — 증명이 근사가 아니다.
    /// </summary>
    public static ChapterReachabilityResult Prove(ChapterGraphModel chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var diagnostics = new List<ChapterDiagnostic>();

        if (chapter.Episodes.Count == 0)
        {
            return new ChapterReachabilityResult(
                new HashSet<string>(StringComparer.Ordinal), diagnostics, ExplorationComplete: true);
        }

        // 시작 규칙은 모델의 것 하나를 쓴다 — 내보내기의 StartEpisodeId와 갈리면 안 된다.
        ChapterEpisode start = chapter.StartEpisode!;

        int[] maxSeen = chapter.Stats.Select(stat => stat.Initial).ToArray();
        int[] minSeen = chapter.Stats.Select(stat => stat.Initial).ToArray();

        // 에피소드별 도착 시점 폭 — 걷는 김에 적는다.
        var spans = new Dictionary<string, (int[] Min, int[] Max)>(StringComparer.Ordinal);

        HashSet<string> reachable;
        bool complete;

        // 한 번만 걷는다. 조건이 참조하는 것은 스탯뿐이고, 스탯은 걷는 도중에 정해지므로
        // 두 번째 바퀴가 새로 찾을 것이 없다.
        int visited;
        (reachable, complete, visited) = Explore(chapter, start, maxSeen, minSeen, spans);

        ReportIncompleteExploration(chapter, complete, visited, diagnostics);
        ReportUnreachable(chapter, reachable, maxSeen, minSeen, complete, diagnostics);

        return new ChapterReachabilityResult(
            reachable, diagnostics, complete, BuildSpans(chapter, spans), visited);
    }

    /// <summary>
    /// 상한에 걸렸다는 것을 <b>챕터 단위로 한 번 크게</b> 말한다.
    ///
    /// 상한에 걸리면 에피소드마다 붙는 문장이 <i>"경로가 없습니다"</i>(오류)에서
    /// <i>"경로를 찾지 못했습니다(탐색이 상한에서 중단됨)"</i>(경고)로 바뀐다.
    /// <b>뜻은 정반대인데 화면은 거의 같아 보인다</b> — 「없음을 증명했다」와 「못 찾았다」다.
    /// 게다가 도달 불가가 하나도 없으면 그 문장마저 없어, 상한에 걸린 사실을 <b>아무도 말하지 않는다</b>.
    ///
    /// <b>오류가 아니라 알림이다.</b> 챕터가 틀린 것이 아니라 <b>이 증명기가 답을 못
    /// 낸 것</b>이므로, 오류로 올려 내보내기를 막으면 멀쩡한 챕터가 커진 죄로 출시를 못 한다.
    /// 그러나 <b>조용히 넘기지도 않는다</b>(규칙 14 — 판정을 못 한 사실을 삼키지 않는다).
    /// </summary>
    private static void ReportIncompleteExploration(
        ChapterGraphModel chapter,
        bool complete,
        int visited,
        List<ChapterDiagnostic> diagnostics)
    {
        if (complete)
        {
            return;
        }

        diagnostics.Add(new ChapterDiagnostic(
            ChapterDiagnosticSeverity.Info,
            ChapterDiagnosticCode.ReachabilityExplorationIncomplete,
            chapter.SourcePath,
            ChapterSheetNames.Episodes,
            EpisodeId: null,
            $"이 챕터의 도달성은 **증명하지 못했습니다** — 상태 {visited:N0}개에서 탐색을 " +
            "멈췄습니다. 아래 「도달 불가」는 단정이 아니라 **찾지 못했다**는 뜻입니다. " +
            "예/아니오 스탯은 갈래마다 다르게 켜지면 상태 수를 두 배로 만듭니다 — " +
            "간선의 표시조건·해금조건이 읽지 않는 예/아니오 스탯이 많으면 그것이 원인입니다."));
    }

    // ── 탐색 ────────────────────────────────────────────────────────────────

    private static (HashSet<string> Reachable, bool Complete, int Visited) Explore(
        ChapterGraphModel chapter,
        ChapterEpisode start,
        int[] maxSeen,
        int[] minSeen,
        Dictionary<string, (int[] Min, int[] Max)> spans)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal) { start.EpisodeId };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<(string EpisodeId, int[] Stats)>();

        // 출발지별 간선 목록을 <b>한 번</b> 만든다. 상태를 하나 꺼낼 때마다 `chapter.Edges`
        //    전체를 문자열 비교로 훑으면 상태 25만 × 간선 수만큼의 비교가 된다.
        //    같은 간선을 같은 순서로 보므로 답은 같다.
        var outgoing = new Dictionary<string, List<ChapterEdge>>(StringComparer.Ordinal);

        foreach (ChapterEdge edge in chapter.Edges)
        {
            if (!outgoing.TryGetValue(edge.FromEpisodeId, out List<ChapterEdge>? list))
            {
                list = [];
                outgoing[edge.FromEpisodeId] = list;
            }

            list.Add(edge);
        }

        int[] initial = chapter.Stats.Select(stat => stat.Initial).ToArray();
        queue.Enqueue((start.EpisodeId, initial));
        visited.Add(StateKey(start.EpisodeId, initial));
        Observe(initial, maxSeen, minSeen);
        ObserveAt(start.EpisodeId, initial, spans);

        while (queue.Count > 0)
        {
            if (visited.Count > StateLimit)
            {
                return (reachable, false, visited.Count);
            }

            (string episodeId, int[] stats) = queue.Dequeue();

            foreach (ChapterEdge edge in
                     outgoing.TryGetValue(episodeId, out List<ChapterEdge>? edges) ? edges : [])
            {
                ChapterEpisode? target = chapter.FindEpisode(edge.ToEpisodeId);

                if (target is null)
                {
                    continue; // 없는 도착지는 구조 검증이 이미 오류로 잡았다.
                }

                // 관문 판정은 커밋 전 값으로 — 플레이어가 선택지를 보는 시점의 값이다.
                // 관문은 전부 간선이 갖는다 — 표시조건과 해금조건 둘 다 서야 탄다.
                if (!Satisfied(chapter, edge.VisibleConditionLabel, stats) ||
                    !Satisfied(chapter, edge.ConditionLabel, stats))
                {
                    continue;
                }

                // 간선을 타는 순간 증감이 1회 커밋된다 — 근사 없는 정확 전이.
                int[] next = ApplyDeltas(chapter, stats, edge.StatChanges);
                Observe(next, maxSeen, minSeen);
                ObserveAt(target.EpisodeId, next, spans); // 도착 시점 값 = 증감 커밋 뒤

                reachable.Add(target.EpisodeId);
                string key = StateKey(target.EpisodeId, next);

                if (visited.Add(key))
                {
                    queue.Enqueue((target.EpisodeId, next));
                }
            }
        }

        return (reachable, true, visited.Count);
    }

    /// <summary>
    /// 간선의 스탯 변화 커밋. `최소~최대`는 탐색 경계라 밖은 잘라낸다.
    ///
    /// <b>지정(Set)은 지금 값을 보지 않는다</b> — 깃발을 켜는 것이 "이전에
    /// 켜져 있었는가"에 따라 달라지면 그건 켜는 게 아니다. 그래서 이 함수는 단조롭지 않다.
    /// </summary>
    internal static int[] ApplyDeltas(
        ChapterGraphModel chapter, int[] stats, IReadOnlyList<StatDelta> deltas)
    {
        if (deltas.Count == 0)
        {
            return stats;
        }

        var next = (int[])stats.Clone();

        foreach (StatDelta delta in deltas)
        {
            for (int index = 0; index < chapter.Stats.Count; index++)
            {
                if (string.Equals(chapter.Stats[index].Key, delta.Key, StringComparison.Ordinal))
                {
                    int raw = delta.IsSet ? delta.Amount : next[index] + delta.Amount;

                    next[index] = Math.Clamp(
                        raw,
                        chapter.Stats[index].Minimum,
                        chapter.Stats[index].Maximum);
                    break;
                }
            }
        }

        return next;
    }

    // 판정의 단일 구현은 ChapterGateJudge다 — 워커와 한 벌(compositor의 무대 프리뷰도 같은 것을 쓴다).
    // 미정의·깨진 라벨(Broken)도 "지나갈 수 없음"이다: 깨진 조건을 통과시켜 도달 가능을
    // 부풀리지 않는다.
    private static bool Satisfied(
        ChapterGraphModel chapter,
        string? conditionLabel,
        int[] stats) =>
        ChapterGateJudge.Judge(chapter, conditionLabel, stats) == ChapterGateVerdict.Open;

    // 간선 없이 관문 만족 가능성만으로 도달 가능하다고 세는 에피소드 종류는 없다 — 계약에
    //    그런 종류가 없으므로, 툴만 그렇게 세면 <b>증명과 플레이가 갈린다</b>.
    //    이 증명기가 있는 이유가 정확히 그것을 없애는 것이다.

    // ── 보고 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 도달 불가를 <b>원인 조건까지 지목해</b> 보고한다. 기본은 오류이고,
    /// `도달불가 허용`이 켜진 에피소드는 알림으로 낮춘다 — 의도임이 표시된 것이므로.
    /// </summary>
    private static void ReportUnreachable(
        ChapterGraphModel chapter,
        IReadOnlySet<string> reachable,
        int[] maxSeen,
        int[] minSeen,
        bool complete,
        List<ChapterDiagnostic> diagnostics)
    {
        foreach (ChapterEpisode episode in chapter.Episodes.Where(episode =>
                     !reachable.Contains(episode.EpisodeId)))
        {
            string cause = DiagnoseCause(chapter, episode, reachable, maxSeen, minSeen);

            ChapterDiagnosticSeverity severity = episode.AllowUnreachable
                ? ChapterDiagnosticSeverity.Info
                : complete
                    ? ChapterDiagnosticSeverity.Error
                    : ChapterDiagnosticSeverity.Warning;

            string prefix = episode.AllowUnreachable
                ? $"'{episode.EpisodeId}'는 도달 불가이지만 `도달불가 허용`이 켜져 있습니다"
                : complete
                    ? $"'{episode.EpisodeId}'에 도달할 수 있는 경로가 없습니다"
                    : $"'{episode.EpisodeId}'에 도달하는 경로를 찾지 못했습니다(탐색이 상한에서 중단됨)";

            diagnostics.Add(new ChapterDiagnostic(
                severity,
                ChapterDiagnosticCode.EpisodeUnreachable,
                chapter.SourcePath,
                ChapterSheetNames.Episodes,
                episode.EpisodeId,
                $"{prefix}. {cause}"));
        }
    }

    private static string DiagnoseCause(
        ChapterGraphModel chapter,
        ChapterEpisode episode,
        IReadOnlySet<string> reachable,
        int[] maxSeen,
        int[] minSeen)
    {
        List<ChapterEdge> incoming = chapter.Edges
            .Where(edge => string.Equals(edge.ToEpisodeId, episode.EpisodeId, StringComparison.Ordinal))
            .ToList();

        if (incoming.Count == 0)
        {
            return "이 에피소드로 들어오는 간선이 `간선` 시트에 없습니다.";
        }

        if (incoming.All(edge => !reachable.Contains(edge.FromEpisodeId)))
        {
            return $"들어오는 간선의 출발점({string.Join(", ", incoming.Select(edge => edge.FromEpisodeId))})부터 " +
                "도달 불가입니다.";
        }

        // 관문이 되는 조건들에서 만족 불가능한 항을 찾는다 (전부 간선의 것).
        var labels = incoming.Select(edge => edge.ConditionLabel)
            .Concat(incoming.Select(edge => edge.VisibleConditionLabel))
            .Where(label => !string.IsNullOrEmpty(label))
            .Distinct(StringComparer.Ordinal);

        foreach (string? label in labels)
        {
            ChapterCondition? condition = chapter.FindCondition(label!);

            if (condition is null)
            {
                continue;
            }

            foreach (ConditionTerm term in condition.Parsed)
            {
                int index = IndexOfStat(chapter, term.Key);

                if (index < 0)
                {
                    continue;
                }

                if (term.Comparison is ConditionComparison.AtLeast or ConditionComparison.Above &&
                    maxSeen[index] < term.Value + (term.Comparison == ConditionComparison.Above ? 1 : 0))
                {
                    string op = term.Comparison == ConditionComparison.Above ? ">" : ">=";
                    return $"조건 '{label}'의 '{term.Key} {op} {term.Value}'가 원인입니다 — " +
                        $"어떤 경로로도 {term.Key}는 최대 {maxSeen[index]}까지밖에 오르지 않습니다.";
                }

                if (term.Comparison is ConditionComparison.AtMost or ConditionComparison.Below &&
                    minSeen[index] > term.Value - (term.Comparison == ConditionComparison.Below ? 1 : 0))
                {
                    string op = term.Comparison == ConditionComparison.Below ? "<" : "<=";
                    return $"조건 '{label}'의 '{term.Key} {op} {term.Value}'가 원인입니다 — " +
                        $"어떤 경로로도 {term.Key}는 최소 {minSeen[index]} 아래로 내려가지 않습니다.";
                }
            }
        }

        return "관문 조건들의 조합을 만족하는 경로가 없습니다.";
    }

    private static void Observe(int[] stats, int[] maxSeen, int[] minSeen)
    {
        for (int index = 0; index < stats.Length; index++)
        {
            maxSeen[index] = Math.Max(maxSeen[index], stats[index]);
            minSeen[index] = Math.Min(minSeen[index], stats[index]);
        }
    }

    /// <summary>그 에피소드에 이 값으로 도착했다 — 에피소드별 폭을 넓힌다.</summary>
    private static void ObserveAt(
        string episodeId, int[] stats, Dictionary<string, (int[] Min, int[] Max)> spans)
    {
        if (!spans.TryGetValue(episodeId, out (int[] Min, int[] Max) span))
        {
            spans[episodeId] = ((int[])stats.Clone(), (int[])stats.Clone());
            return;
        }

        for (int index = 0; index < stats.Length; index++)
        {
            span.Min[index] = Math.Min(span.Min[index], stats[index]);
            span.Max[index] = Math.Max(span.Max[index], stats[index]);
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ChapterStatSpan>> BuildSpans(
        ChapterGraphModel chapter, Dictionary<string, (int[] Min, int[] Max)> spans)
    {
        var built = new Dictionary<string, IReadOnlyList<ChapterStatSpan>>(StringComparer.Ordinal);

        foreach ((string episodeId, (int[] min, int[] max)) in spans)
        {
            var list = new List<ChapterStatSpan>(chapter.Stats.Count);

            for (int index = 0; index < chapter.Stats.Count && index < min.Length; index++)
            {
                ChapterStat stat = chapter.Stats[index];
                list.Add(new ChapterStatSpan(stat.Key, stat.DisplayName, min[index], max[index]));
            }

            built[episodeId] = list;
        }

        return built;
    }

    private static int IndexOfStat(ChapterGraphModel chapter, string key)
    {
        for (int index = 0; index < chapter.Stats.Count; index++)
        {
            if (string.Equals(chapter.Stats[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string StateKey(string episodeId, int[] stats) =>
        episodeId + "|" + string.Join(',', stats);

    private sealed class IntArrayComparer : IEqualityComparer<int[]>
    {
        public static IntArrayComparer Instance { get; } = new();

        public bool Equals(int[]? left, int[]? right) =>
            left is not null && right is not null && left.AsSpan().SequenceEqual(right);

        public int GetHashCode(int[] value)
        {
            var hash = new HashCode();

            foreach (int item in value)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }
    }
}
