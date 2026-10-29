using Vn.Authoring.Model;

namespace Vn.Authoring.Script;

public enum ScriptSyncKind
{
    /// <summary>화자·대사와 상대 순서가 그대로다.</summary>
    Unchanged,

    /// <summary>같은 자리의 문구가 바뀌었다. LineId는 유지하고 Revision을 올린다.</summary>
    Modified,

    /// <summary>대본에 없던 줄이다. 새 LineId를 발급한다.</summary>
    Inserted,

    /// <summary>대본에서 사라졌다. 지우지 않고 은퇴시킨다.</summary>
    Deleted,

    /// <summary>내용은 같고 위치만 달라졌다. LineId를 유지한다.</summary>
    Moved,

    /// <summary>
    /// 어느 기존 줄과 이어지는지 확신할 수 없다.
    /// <b>임의로 기존 LineId를 고르지 않는다.</b> 이 항목이 하나라도 있으면 계획을 적용하지 않는다.
    /// </summary>
    Ambiguous
}

/// <param name="LineId">
/// 이 항목이 확정한 줄의 Id. 아직 어느 줄인지 정하지 못한 새 줄만 null이다.
/// </param>
/// <param name="OldIndex">동기화 전 활성 줄 목록에서의 위치.</param>
/// <param name="NewIndex">동기화 후 줄 목록에서의 위치. 사라진 줄은 null이다.</param>
public sealed record ScriptSyncEntry(
    ScriptSyncKind Kind,
    string? LineId,
    int? OldIndex,
    int? NewIndex,
    LocalizedLine? Previous,
    LocalizedLine? Next,
    string? Message = null);

/// <summary>
/// 대본을 다시 읽었을 때 무엇이 어떻게 이어지는지에 대한 <b>검토 가능한 계획</b>.
///
/// 계획과 적용을 나눈 이유는 <see cref="HasConflicts"/> 때문이다. 확신할 수 없는 연결이
/// 하나라도 있으면 도구가 대신 결정하지 않고 여기서 멈춘다.
/// </summary>
public sealed class ScriptSyncPlan
{
    public ScriptSyncPlan(
        string scriptId,
        string locale,
        string sourceContentHash,
        IReadOnlyList<ScriptSyncEntry> entries)
    {
        ScriptId = scriptId;
        Locale = locale;
        SourceContentHash = sourceContentHash;
        Entries = entries;
    }

    public string ScriptId { get; }

    public string Locale { get; }

    public string SourceContentHash { get; }

    /// <summary>새 순서대로의 항목. 사라진 줄은 원래 있던 자리에 끼워 넣는다.</summary>
    public IReadOnlyList<ScriptSyncEntry> Entries { get; }

    public IEnumerable<ScriptSyncEntry> Conflicts =>
        Entries.Where(entry => entry.Kind == ScriptSyncKind.Ambiguous);

    public bool HasConflicts => Entries.Any(entry => entry.Kind == ScriptSyncKind.Ambiguous);

    public int Count(ScriptSyncKind kind) => Entries.Count(entry => entry.Kind == kind);

    public bool IsNoOp =>
        !HasConflicts && Entries.All(entry => entry.Kind == ScriptSyncKind.Unchanged);

    public string Summary()
    {
        string parts = string.Join(
            " · ",
            new (ScriptSyncKind Kind, string Label)[]
            {
                (ScriptSyncKind.Unchanged, "유지"),
                (ScriptSyncKind.Modified, "수정"),
                (ScriptSyncKind.Inserted, "추가"),
                (ScriptSyncKind.Deleted, "삭제"),
                (ScriptSyncKind.Moved, "이동"),
                (ScriptSyncKind.Ambiguous, "확인 필요")
            }
                .Select(pair => (pair.Label, Count: Count(pair.Kind)))
                .Where(pair => pair.Count > 0)
                .Select(pair => $"{pair.Label} {pair.Count}"));

        return parts.Length == 0 ? "변경 없음" : parts;
    }
}

/// <summary>
/// 같은 대본을 다시 읽었을 때 기존 LineId를 어디까지 이어 붙일지 계산한다.
///
/// <b>확신할 수 없는 것을 확신한 척 잇지 않는다.</b> 그것이 이 클래스의 유일한 목적이다.
/// 매번 새 Id를 발급하면 연출·녹음·번역이 전부 끊어지고, 아무 줄에나 갖다 붙이면
/// 작가가 쓰지 않은 연출이 다른 대사에 붙는다. 둘 다 조용히 일어나서는 안 된다.
///
/// 절차:
/// <list type="number">
///   <item>양쪽에서 <b>정확히 한 번씩만</b> 나타나는 문장을 닻으로 삼는다.</item>
///   <item>닻 중 순서가 유지되는 최장 부분열은 <c>Unchanged</c>, 나머지 닻은 <c>Moved</c>다.</item>
///   <item>
///     순서가 유지된 닻 사이의 구간마다 남은 줄을 맞춘다.
///     같은 문장끼리 앞에서부터 짝지어 <c>Unchanged</c>,
///     한쪽만 하나 남으면 <c>Modified</c>,
///     한쪽이 비면 <c>Inserted</c>/<c>Deleted</c>,
///     양쪽에 서로 다른 문장이 둘 이상 남으면 <c>Ambiguous</c>다.
///   </item>
/// </list>
///
/// 은퇴한 줄은 후보에서 제외한다. 사라졌던 문장이 다시 나타나면 옛 Id를 되살리지 않고
/// 새 Id를 발급한다. 그 사이에 그 Id를 가리키던 연출이 무엇을 뜻하는지 알 수 없기 때문이다.
/// </summary>
public static class ScriptSynchronizer
{
    private const char KeySeparator = '\0';

    public static ScriptSyncPlan Plan(
        ScriptDocument document,
        ParsedScript parsed,
        string? locale = null,
        Func<string>? newLineId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(parsed);

        string targetLocale = locale ?? document.PrimaryLocale;
        newLineId ??= Identifier.Line;

        List<ScriptLine> oldLines = document.ActiveLines.ToList();
        string[] oldKeys = oldLines.Select(line => KeyOf(document.Text(line.Id, targetLocale))).ToArray();
        string[] newKeys = parsed.Lines.Select(KeyOf).ToArray();

        var state = new MatchState(oldLines.Count, parsed.Lines.Count);

        // 신원을 아는 줄이 하나라도 있으면 그것이 닻이다. 내용이 같은지는 그 다음 문제다 —
        // ID가 맞는데 문장이 달라졌으면 그건 "고쳐진 줄"이지 "다른 줄"이 아니다.
        (int OldIndex, int NewIndex)[] anchors = FindIdAnchors(oldLines, parsed)
            ?? FindAnchors(oldKeys, newKeys);

        bool[] keptInOrder = MarkLongestIncreasing(anchors);

        for (int index = 0; index < anchors.Length; index++)
        {
            (int oldIndex, int newIndex) = anchors[index];
            bool sameText = string.Equals(oldKeys[oldIndex], newKeys[newIndex], StringComparison.Ordinal);

            state.Pair(oldIndex, newIndex, sameText
                ? keptInOrder[index] ? ScriptSyncKind.Unchanged : ScriptSyncKind.Moved
                : ScriptSyncKind.Modified);
        }

        // 순서가 유지된 닻만 구간을 나눈다. 이동한 닻은 구간 안에 있지만 짝이 이미 정해져 있다.
        IEnumerable<int> boundaries = anchors
            .Where((_, index) => keptInOrder[index])
            .Select(pair => pair.NewIndex)
            .Append(parsed.Lines.Count);

        int previousOld = -1;
        int previousNew = -1;

        foreach (int boundaryNew in boundaries)
        {
            int boundaryOld = boundaryNew == parsed.Lines.Count
                ? oldLines.Count
                : state.OldOf(boundaryNew)!.Value;

            MatchGap(
                document,
                targetLocale,
                oldLines,
                oldKeys,
                parsed,
                newKeys,
                previousOld + 1,
                boundaryOld,
                previousNew + 1,
                boundaryNew,
                state);

            previousOld = boundaryOld;
            previousNew = boundaryNew;
        }

        return BuildPlan(document, targetLocale, oldLines, parsed, state, newLineId);
    }

    private static ScriptSyncPlan BuildPlan(
        ScriptDocument document,
        string locale,
        List<ScriptLine> oldLines,
        ParsedScript parsed,
        MatchState state,
        Func<string> newLineId)
    {
        // 사라지거나 확정하지 못한 옛 줄은 바로 앞에서 짝이 맞은 새 줄 뒤에 끼워 넣는다.
        // 그래야 계획을 위에서 아래로 읽을 때 원래 있던 자리가 보인다.
        var unmatchedAfterNew = new Dictionary<int, List<int>>();

        foreach (int oldIndex in state.UnmatchedOld())
        {
            int anchor = state.PrecedingNewIndex(oldIndex);

            if (!unmatchedAfterNew.TryGetValue(anchor, out List<int>? bucket))
            {
                bucket = new List<int>();
                unmatchedAfterNew[anchor] = bucket;
            }

            bucket.Add(oldIndex);
        }

        var entries = new List<ScriptSyncEntry>();

        // 새 줄이 신원(#line:)을 선언하고 왔으면 그 ID를 그대로 발급한다 — 입력이 신원을 밝혔는데
        // 다른 ID를 지어 주면 "태그가 곧 신원"이라는 계약이 조용히 깨지고, 엑셀 쪽 B열과 대본이
        // 서로 다른 이름으로 갈라진다. 단 문서가 이미 아는 ID(은퇴 포함)나 이 계획 안에서 이미
        // 쓴 ID는 채택하지 않는다 — 되살리거나 겹치게 하지 않는다.
        var usedIds = document.Lines.Select(line => line.Id).ToHashSet(StringComparer.Ordinal);

        string IssueLineId(ParsedScriptLine parsedLine)
        {
            string id = parsedLine.LineId is { Length: > 0 } declared && usedIds.Add(declared)
                ? declared
                : newLineId();

            usedIds.Add(id);
            return id;
        }

        void AppendUnmatchedOld(int anchorNew)
        {
            if (!unmatchedAfterNew.TryGetValue(anchorNew, out List<int>? bucket))
            {
                return;
            }

            foreach (int oldIndex in bucket)
            {
                bool ambiguous = state.IsAmbiguousOld(oldIndex);

                entries.Add(new ScriptSyncEntry(
                    ambiguous ? ScriptSyncKind.Ambiguous : ScriptSyncKind.Deleted,
                    oldLines[oldIndex].Id,
                    oldIndex,
                    null,
                    document.Text(oldLines[oldIndex].Id, locale),
                    null,
                    ambiguous
                        ? "이 줄이 사라진 것인지 아래의 새 문장으로 고쳐진 것인지 확신할 수 없습니다."
                        : "대본에서 사라졌습니다. 지우지 않고 은퇴 상태로 남깁니다."));
            }
        }

        AppendUnmatchedOld(-1);

        for (int newIndex = 0; newIndex < parsed.Lines.Count; newIndex++)
        {
            ParsedScriptLine parsedLine = parsed.Lines[newIndex];
            var next = new LocalizedLine(parsedLine.Speaker, parsedLine.Text);
            int? oldIndex = state.OldOf(newIndex);
            ScriptSyncKind kind = state.KindOf(newIndex);

            string? lineId = oldIndex is { } resolved
                ? oldLines[resolved].Id
                : kind == ScriptSyncKind.Inserted
                    ? IssueLineId(parsedLine)
                    : null;

            entries.Add(new ScriptSyncEntry(
                kind,
                lineId,
                oldIndex,
                newIndex,
                oldIndex is { } previous ? document.Text(oldLines[previous].Id, locale) : null,
                next,
                state.MessageOf(newIndex)));

            AppendUnmatchedOld(newIndex);
        }

        return new ScriptSyncPlan(
            document.Id,
            locale,
            parsed.ContentHash,
            entries);
    }

    private static void MatchGap(
        ScriptDocument document,
        string locale,
        List<ScriptLine> oldLines,
        string[] oldKeys,
        ParsedScript parsed,
        string[] newKeys,
        int oldStart,
        int oldEnd,
        int newStart,
        int newEnd,
        MatchState state)
    {
        List<int> remainingOld = Enumerable.Range(oldStart, Math.Max(0, oldEnd - oldStart))
            .Where(state.IsOldFree)
            .ToList();
        List<int> remainingNew = Enumerable.Range(newStart, Math.Max(0, newEnd - newStart))
            .Where(state.IsNewFree)
            .ToList();

        // 1. 같은 문장끼리 앞에서부터 짝짓는다. 본문이 같으므로 어느 쪽을 골라도 대사는 같다.
        foreach (int newIndex in remainingNew.ToList())
        {
            int match = remainingOld.FirstOrDefault(
                oldIndex => string.Equals(oldKeys[oldIndex], newKeys[newIndex], StringComparison.Ordinal),
                -1);

            if (match < 0)
            {
                continue;
            }

            state.Pair(match, newIndex, ScriptSyncKind.Unchanged);
            remainingOld.Remove(match);
            remainingNew.Remove(newIndex);
        }

        // 2. 양쪽에 하나씩만 남았다면 같은 자리의 문구 수정이다.
        if (remainingOld.Count == 1 && remainingNew.Count == 1)
        {
            state.Pair(remainingOld[0], remainingNew[0], ScriptSyncKind.Modified);
            return;
        }

        // 3. 한쪽이 비었다면 순수한 추가 또는 삭제다.
        if (remainingOld.Count == 0)
        {
            foreach (int newIndex in remainingNew)
            {
                state.Classify(newIndex, ScriptSyncKind.Inserted);
            }

            return;
        }

        if (remainingNew.Count == 0)
        {
            return;
        }

        // 4. 양쪽에 서로 다른 문장이 둘 이상 남았다. 어느 줄이 어느 줄의 수정인지 알 수 없다.
        string candidates = string.Join(
            ", ",
            remainingOld.Select(index =>
                $"{oldLines[index].Id}({Preview(document.Text(oldLines[index].Id, locale))})"));

        foreach (int newIndex in remainingNew)
        {
            ParsedScriptLine line = parsed.Lines[newIndex];

            state.Classify(
                newIndex,
                ScriptSyncKind.Ambiguous,
                $"'{Preview(new LocalizedLine(line.Speaker, line.Text))}'가 기존 줄 " +
                $"[{candidates}] 중 어느 것의 수정인지 확신할 수 없습니다. " +
                "한쪽을 먼저 확정하거나 대본을 나누어 동기화해 주세요.");
        }

        foreach (int oldIndex in remainingOld)
        {
            state.MarkAmbiguousOld(oldIndex);
        }
    }

    /// <summary>
    /// <c>LineId</c>가 같은 줄끼리의 짝. 새 순서대로 돌려준다.
    ///
    /// <b>이것이 있으면 내용 기반 닻보다 언제나 낫다.</b> 내용 매칭은 "같은 문장이 양쪽에 정확히
    /// 한 번씩"일 때만 확신할 수 있어서, 비슷한 대사가 여러 줄이면 <c>Ambiguous</c>가 나고
    /// 동기화가 통째로 거부된다. 입력이 신원을 알고 있으면(<c>#line:</c> 태그 — 엑셀 시절에는 시트의 열) 그 일이 일어나서는 안 된다.
    ///
    /// 신원을 아는 줄이 하나도 없으면 null을 돌려주고 호출자가 내용 기반으로 되돌아간다 —
    /// 평평한 대본 파일 가져오기는 지금까지와 똑같이 동작한다.
    ///
    /// 같은 ID가 두 번 나오면 첫 번째만 닻으로 쓴다. 나머지는 자유롭게 남아 내용 매칭을 타고,
    /// 그래도 확신할 수 없으면 <c>Ambiguous</c>로 잡힌다 — 임의로 고르지 않는다.
    /// </summary>
    private static (int OldIndex, int NewIndex)[]? FindIdAnchors(
        List<ScriptLine> oldLines, ParsedScript parsed)
    {
        if (!parsed.Lines.Any(line => !string.IsNullOrEmpty(line.LineId)))
        {
            return null;
        }

        var oldPositions = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int index = 0; index < oldLines.Count; index++)
        {
            oldPositions[oldLines[index].Id] = index;
        }

        var anchors = new List<(int OldIndex, int NewIndex)>();
        var used = new HashSet<int>();

        for (int index = 0; index < parsed.Lines.Count; index++)
        {
            if (parsed.Lines[index].LineId is { Length: > 0 } id &&
                oldPositions.TryGetValue(id, out int oldIndex) &&
                used.Add(oldIndex))
            {
                anchors.Add((oldIndex, index));
            }
        }

        return anchors.ToArray();
    }

    /// <summary>양쪽에서 정확히 한 번씩만 나타나는 문장의 짝. 새 순서대로 돌려준다.</summary>
    private static (int OldIndex, int NewIndex)[] FindAnchors(string[] oldKeys, string[] newKeys)
    {
        Dictionary<string, int> oldCounts = CountKeys(oldKeys);
        Dictionary<string, int> newCounts = CountKeys(newKeys);
        var oldPositions = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int index = 0; index < oldKeys.Length; index++)
        {
            if (oldCounts[oldKeys[index]] == 1)
            {
                oldPositions[oldKeys[index]] = index;
            }
        }

        var anchors = new List<(int OldIndex, int NewIndex)>();

        for (int index = 0; index < newKeys.Length; index++)
        {
            if (newCounts[newKeys[index]] == 1 &&
                oldPositions.TryGetValue(newKeys[index], out int oldIndex))
            {
                anchors.Add((oldIndex, index));
            }
        }

        return anchors.ToArray();
    }

    /// <summary>
    /// 새 순서로 정렬된 닻 중 옛 위치가 증가하는 최장 부분열을 표시한다.
    /// 표시되지 않은 닻은 줄이 옮겨졌다는 뜻이다.
    /// </summary>
    private static bool[] MarkLongestIncreasing((int OldIndex, int NewIndex)[] anchors)
    {
        var kept = new bool[anchors.Length];

        if (anchors.Length == 0)
        {
            return kept;
        }

        var tails = new List<int>();
        var previous = new int[anchors.Length];

        for (int index = 0; index < anchors.Length; index++)
        {
            int low = 0;
            int high = tails.Count;

            while (low < high)
            {
                int middle = (low + high) / 2;

                if (anchors[tails[middle]].OldIndex < anchors[index].OldIndex)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            previous[index] = low > 0 ? tails[low - 1] : -1;

            if (low == tails.Count)
            {
                tails.Add(index);
            }
            else
            {
                tails[low] = index;
            }
        }

        for (int index = tails[^1]; index >= 0; index = previous[index])
        {
            kept[index] = true;
        }

        return kept;
    }

    private static Dictionary<string, int> CountKeys(string[] keys)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string key in keys)
        {
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private static string KeyOf(LocalizedLine line) => line.Speaker + KeySeparator + line.Text;

    private static string KeyOf(ParsedScriptLine line) => line.Speaker + KeySeparator + line.Text;

    private static string Preview(LocalizedLine line)
    {
        string text = string.IsNullOrEmpty(line.Speaker) ? line.Text : $"{line.Speaker}: {line.Text}";
        return text.Length <= 20 ? text : text[..20] + "…";
    }

    /// <summary>맞춰 가는 도중의 짝과 분류. 계획을 만드는 동안에만 살아 있다.</summary>
    private sealed class MatchState
    {
        private readonly int?[] _oldToNew;
        private readonly int?[] _newToOld;
        private readonly ScriptSyncKind?[] _kinds;
        private readonly string?[] _messages;
        private readonly bool[] _ambiguousOld;

        public MatchState(int oldCount, int newCount)
        {
            _oldToNew = new int?[oldCount];
            _newToOld = new int?[newCount];
            _kinds = new ScriptSyncKind?[newCount];
            _messages = new string?[newCount];
            _ambiguousOld = new bool[oldCount];
        }

        public void Pair(int oldIndex, int newIndex, ScriptSyncKind kind)
        {
            _oldToNew[oldIndex] = newIndex;
            _newToOld[newIndex] = oldIndex;
            _kinds[newIndex] = kind;
        }

        public void Classify(int newIndex, ScriptSyncKind kind, string? message = null)
        {
            _kinds[newIndex] = kind;
            _messages[newIndex] = message;
        }

        public void MarkAmbiguousOld(int oldIndex) => _ambiguousOld[oldIndex] = true;

        public bool IsAmbiguousOld(int oldIndex) => _ambiguousOld[oldIndex];

        public bool IsOldFree(int oldIndex) => _oldToNew[oldIndex] is null;

        public bool IsNewFree(int newIndex) => _newToOld[newIndex] is null;

        public int? OldOf(int newIndex) => _newToOld[newIndex];

        public string? MessageOf(int newIndex) => _messages[newIndex];

        public ScriptSyncKind KindOf(int newIndex)
        {
            return _kinds[newIndex]
                ?? throw new InvalidOperationException(
                    $"동기화 계획이 {newIndex}번째 줄을 분류하지 못했습니다.");
        }

        public IEnumerable<int> UnmatchedOld()
        {
            for (int index = 0; index < _oldToNew.Length; index++)
            {
                if (_oldToNew[index] is null)
                {
                    yield return index;
                }
            }
        }

        public int PrecedingNewIndex(int oldIndex)
        {
            for (int index = oldIndex - 1; index >= 0; index--)
            {
                if (_oldToNew[index] is { } newIndex)
                {
                    return newIndex;
                }
            }

            return -1;
        }
    }
}
