namespace Vn.Authoring.Script;

/// <summary>
/// <b>카드 글 칸에서 고친 글이 어느 대본 줄을 잇는가</b> (2026-10-30 소유자 보고: *"기존 내용을 지워버릴 경우 그게 찾을 수
/// 없다고 표시되는 버그"* · *"에피소드 노드 속의 텍스트 편집하는 게 그대로 반영이 바로바로 되기를"*).
///
/// 글 칸의 글에는 줄 신원(LineId)이 안 보여서, 넣을 때 동기화(<see cref="ScriptSynchronizer"/>)가 내용만 보고 짝을 맞췄다 —
/// 한 구간에서 두 줄 이상을 고치면 "사라진 것인지 고쳐진 것인지 확신할 수 없다"며 통째로 거절했다. 그런데 칸은 처음 보일 때
/// 줄마다 어느 대본 줄인지 안다(<see cref="Rendering.ScenarioWritingLayout.RowOfLine"/>). 처음 보인 글과 고친 글을 <b>줄 단위로
/// 견줘</b> 같은 줄은 그대로, 같은 자리에서 고친 줄은 그 줄을 잇게 하고, 그 신원을 줄 끝 <c>#line:</c> 표지로 붙여 넘긴다 —
/// 동기화는 신원을 아는 줄을 닻으로 삼으므로 확신 못 할 짝이 생기지 않는다.
///
/// 규칙은 글 칸에서 사람이 하는 일 그대로다: 줄을 고치면 그 줄이고, 줄을 지우면 은퇴하고, 줄을 새로 쓰면 새 줄이다.
/// </summary>
public static class ScenarioLineIdentity
{
    private const string Tag = " #line:";

    /// <summary>
    /// 고친 글의 대사 · 선택지 줄 끝에, 그 줄이 잇는 대본 줄의 <c>#line:</c> 표지를 붙인 글.
    /// </summary>
    /// <param name="shown">칸에 처음 보인 글.</param>
    /// <param name="rowOfLine">처음 보인 글에서 대본 줄(LineId)마다 몇 번째 줄인가.</param>
    /// <param name="edited">사람이 고친 글.</param>
    public static string CarryLineIds(string shown, IReadOnlyDictionary<string, int> rowOfLine, string edited)
    {
        ArgumentNullException.ThrowIfNull(rowOfLine);

        string[] before = Rows(shown);
        string[] after = Rows(edited);
        var idOfRow = new Dictionary<int, string>();

        foreach ((string lineId, int row) in rowOfLine)
        {
            idOfRow.TryAdd(row, lineId);
        }

        string?[] carried = new string?[after.Length];

        // 같은 줄 — 가장 긴 공통 부분열. 사이의 고친 구간은 같은 자리끼리 잇는다.
        int lastBefore = -1;
        int lastAfter = -1;

        foreach ((int oldRow, int newRow) in CommonRows(before, after).Append((before.Length, after.Length)))
        {
            CarryGap(lastBefore + 1, oldRow, lastAfter + 1, newRow, idOfRow, after, carried);

            if (oldRow < before.Length && idOfRow.TryGetValue(oldRow, out string? same))
            {
                carried[newRow] = same;
            }

            lastBefore = oldRow;
            lastAfter = newRow;
        }

        for (int row = 0; row < after.Length; row++)
        {
            if (carried[row] is { } lineId && IsLineRow(after[row]) && !after[row].Contains(Tag, StringComparison.Ordinal))
            {
                after[row] = after[row].TrimEnd() + Tag + lineId;
            }
        }

        return string.Join('\n', after);
    }

    /// <summary>고친 구간 — 처음 글의 대본 줄들을 고친 글의 대사 줄들에 앞에서부터 하나씩 잇는다. 남는 쪽은 은퇴 · 새 줄이다.</summary>
    private static void CarryGap(
        int beforeStart, int beforeEnd, int afterStart, int afterEnd,
        IReadOnlyDictionary<int, string> idOfRow, string[] after, string?[] carried)
    {
        Queue<string> ids = new(Enumerable.Range(beforeStart, Math.Max(0, beforeEnd - beforeStart))
            .Where(idOfRow.ContainsKey)
            .Select(row => idOfRow[row]));

        for (int row = afterStart; row < afterEnd && ids.Count > 0; row++)
        {
            if (IsLineRow(after[row]))
            {
                carried[row] = ids.Dequeue();
            }
        }
    }

    /// <summary>대사나 선택지 줄인가 — 빈 줄과 조건 줄(<c>&lt;&lt;…&gt;&gt;</c>)은 대본 줄이 아니다.</summary>
    private static bool IsLineRow(string row)
    {
        string trimmed = row.Trim();
        return trimmed.Length > 0 && !trimmed.StartsWith("<<", StringComparison.Ordinal);
    }

    private static string[] Rows(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// <summary>두 글에서 같은 줄(앞뒤 공백은 무시)의 가장 긴 공통 부분열 — 차례대로.</summary>
    private static List<(int Before, int After)> CommonRows(string[] before, string[] after)
    {
        int[,] length = new int[before.Length + 1, after.Length + 1];

        for (int i = before.Length - 1; i >= 0; i--)
        {
            for (int j = after.Length - 1; j >= 0; j--)
            {
                length[i, j] = string.Equals(before[i].Trim(), after[j].Trim(), StringComparison.Ordinal)
                    ? length[i + 1, j + 1] + 1
                    : Math.Max(length[i + 1, j], length[i, j + 1]);
            }
        }

        var pairs = new List<(int, int)>();
        int x = 0;
        int y = 0;

        while (x < before.Length && y < after.Length)
        {
            if (string.Equals(before[x].Trim(), after[y].Trim(), StringComparison.Ordinal))
            {
                pairs.Add((x, y));
                x++;
                y++;
            }
            else if (length[x + 1, y] >= length[x, y + 1])
            {
                x++;
            }
            else
            {
                y++;
            }
        }

        return pairs;
    }
}
