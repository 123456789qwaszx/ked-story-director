namespace Vn.Authoring.Model;

/// <summary>
/// <b>작가용 글 서식의 색</b> — 실제 RGB 값이다(2026-11-23 소유자 — 「글자 색깔과 바탕색의 종류가 조금 제한 적인게 아쉬운데, 사용한 색깔과 더불어서
/// RGB를 원하는 걸 선택할 수 있는 것도 해주십시오」 · writer-formatting WF-D14). 파일에는 <c>"#RRGGBB"</c>로 적는다. ⛔ 그 전에는 정해 둔 열 가지의
/// 이름(<c>"red"</c>)만 적었다 — 그 이름은 읽을 때 <see cref="WriterSwatch"/>의 값으로 바꾼다.
/// </summary>
public readonly record struct WriterRgb(byte R, byte G, byte B)
{
    /// <summary><c>#RRGGBB</c>(대문자).</summary>
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => Hex;

    /// <summary><c>#RRGGBB</c> · <c>RRGGBB</c>(대소문자 무관) — 그 밖은 null.</summary>
    public static WriterRgb? Parse(string? text)
    {
        string hex = (text ?? string.Empty).Trim().TrimStart('#');

        if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out uint value))
        {
            return null;
        }

        return new WriterRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
}

/// <summary>
/// 색 차림의 <b>정해 둔 열 가지</b> — 같은 이름이 글자색에서는 진한 색(흰 카드 바탕에서 읽힌다), 바탕색에서는 옅은 색(검은 글자가 읽힌다)이다.
/// <see cref="Key"/>는 옛 파일의 색 이름이다(WF-D14 전).
/// </summary>
public sealed record WriterSwatch(string Key, string Name, WriterRgb Text, WriterRgb Fill)
{
    public static IReadOnlyList<WriterSwatch> All { get; } =
    [
        new("red", "빨강", new(0xD3, 0x2F, 0x2F), new(0xFF, 0xCD, 0xD2)),
        new("orange", "주황", new(0xE6, 0x5C, 0x00), new(0xFF, 0xE0, 0xB2)),
        new("yellow", "노랑", new(0xB2, 0x8A, 0x00), new(0xFF, 0xF5, 0x9D)),
        new("green", "초록", new(0x2E, 0x7D, 0x32), new(0xC8, 0xE6, 0xC9)),
        new("teal", "청록", new(0x00, 0x79, 0x6B), new(0xB2, 0xEB, 0xF2)),
        new("blue", "파랑", new(0x15, 0x65, 0xC0), new(0xBB, 0xDE, 0xFB)),
        new("purple", "보라", new(0x6A, 0x1B, 0x9A), new(0xE1, 0xBE, 0xE7)),
        new("pink", "분홍", new(0xC2, 0x18, 0x5B), new(0xF8, 0xBB, 0xD0)),
        new("brown", "갈색", new(0x6D, 0x4C, 0x41), new(0xD7, 0xCC, 0xC8)),
        new("gray", "회색", new(0x75, 0x75, 0x75), new(0xE0, 0xE0, 0xE0))
    ];

    /// <summary>글자색이면 <see cref="Text"/>, 바탕색이면 <see cref="Fill"/>.</summary>
    public WriterRgb Of(bool fill) => fill ? Fill : Text;
}

/// <summary>카드 글 칸의 좌우 정렬 — 카드 하나에 하나(줄마다가 아니다). 왼쪽이 기본이다.</summary>
public enum WriterAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// <b>글자 한 토막의 모양</b> — 작가가 보기 위한 것이다(WF-D1 · 게임에 나가지 않는다).
/// </summary>
/// <param name="Font">글꼴 이름 — null이면 카드의 처음 글꼴(<see cref="DialogueNode.WriterBase"/>). 그 글꼴이 없는 PC에서는 기본 글꼴로 보인다.</param>
/// <param name="Grow">
/// 글자 크기 차(pt) — 카드의 처음 크기(<see cref="DialogueNode.WriterBase"/>)에 더한다. 「남보다 크게」라는 뜻을 적는다 — 처음 크기가 안 적힌 옛 카드는
/// 그 PC의 설정 크기에 더한다.
/// </param>
public sealed record WriterLook(
    bool Bold = false,
    bool Italic = false,
    bool Strike = false,
    WriterRgb? Color = null,
    WriterRgb? Fill = null,
    string? Font = null,
    int Grow = 0)
{
    /// <summary>아무 모양도 없다.</summary>
    public static WriterLook Plain { get; } = new();

    public bool IsPlain => this == Plain;
}

/// <summary>
/// <b>카드 글 칸의 처음 글꼴 · 글자 크기</b>(2026-11-23 소유자 — 「설정 내부에 대본 글칸속의 글꼴과 글자 크기는 이제 처음 생성시에 기본값으로만 사용하는
/// 게 맞겠습니다. 지금처럼 즉각 갱신되는게 아니라요」) — 카드를 만들 때 그 PC의 설정 「대본 글 칸」 값을 적어 두고, 뒤에 설정을 바꿔도 그 카드는 그대로다.
/// 줄 간격도 같다(WF-D19).
/// </summary>
/// <param name="Font">글꼴 이름 — 빈 글이면 테마의 기본 글꼴. 그 글꼴이 없는 PC에서는 기본 글꼴로 보인다.</param>
/// <param name="Size">글자 크기(pt).</param>
/// <param name="Bold">
/// 카드 글 전체를 굵게(2026-11-23 소유자 — 「설정 창에서 원한다면, Bold로써 굵게 하는 걸 기본값으로 쓸 수도 있게」 · WF-D16). 서식의 굵게(<see cref="WriterLook.Bold"/>)는
/// 이 위에 더하는 것이라, 처음부터 굵은 카드에서 일부만 가늘게 할 수는 없다.
/// </param>
/// <param name="Color">카드 글의 기본 글자색(같은 날 — 「글자 색깔도 같이」) — 없으면 테마의 글자색. 서식의 글자색 · 화자 이름 색이 그 위에 선다.</param>
/// <param name="LineSpacing">
/// 줄 간격 — 글자 크기에 대한 %(같은 날 소유자 — 「줄 간격을 새 카드의 기본값으로 바꿉시다」 · 「서식도구에 줄 간격 조절을 새로 넣는게 맞습니다」 · WF-D19).
/// 카드를 만들 때 설정의 값이 적히고, 서식 도구로 그 카드만 바꾼다. null은 그 값이 생기기 전에 적힌 카드다 — 열 때 그 PC의 설정으로 적힌다(<see cref="WriterBaseFill"/>).
/// </param>
public sealed record WriterBase(string Font, double Size, bool Bold = false, WriterRgb? Color = null, int? LineSpacing = null);

/// <summary>대사 글의 한 구간 <c>[Start, Start + Length)</c>에 입힌 모양 — 자리는 그 줄 기본 언어 <c>Text</c>의 UTF-16 자리다(화자는 밖이다).</summary>
public sealed record WriterSpan(int Start, int Length, WriterLook Look)
{
    public int End => Start + Length;
}

/// <summary>한 줄(LineId)의 대사 글에서 고른 구간 — 화면이 고른 범위를 이것들로 바꿔 편집기에 넘긴다.</summary>
public sealed record WriterRange(string LineId, int Start, int Length);

/// <summary>도구 막대의 한 번 — 무엇을 바꾸는가.</summary>
public enum WriterStyleKind
{
    /// <summary>굵게 — 켜고 끄기(고른 글자가 모두 굵으면 끈다).</summary>
    Bold,

    /// <summary>기울임 — 켜고 끄기.</summary>
    Italic,

    /// <summary>취소선 — 켜고 끄기.</summary>
    Strike,

    /// <summary>글자색 — <see cref="WriterStyleChange.Color"/>로(null이면 없앤다).</summary>
    Color,

    /// <summary>바탕색 — <see cref="WriterStyleChange.Color"/>로(null이면 없앤다).</summary>
    Fill,

    /// <summary>글꼴 — <see cref="WriterStyleChange.Font"/>로(null이면 기본으로).</summary>
    Font,

    /// <summary>크기 — 글자마다 <see cref="WriterStyleChange.Step"/>만큼 키우거나 줄인다.</summary>
    Grow,

    /// <summary>
    /// 크기를 정한 값으로 — 고른 글자 모두의 크기 차를 <see cref="WriterStyleChange.Step"/>으로 맞춘다(카드의 처음 크기에 대한 차 · 끝으로 당긴다). 도구 막대의
    /// 크기 칸에 숫자를 적어 넣을 때다(2026-11-23 소유자 — 「정확히 텍스트 입력을 해서 특정값으로 변경하는 것도 필요하겠습니다」 · WF-D20).
    /// </summary>
    Size
}

public sealed record WriterStyleChange(WriterStyleKind Kind, WriterRgb? Color = null, string? Font = null, int Step = 0)
{
    public static WriterStyleChange Toggle(WriterStyleKind kind) => new(kind);
}

/// <summary>
/// 처음 글꼴 · 크기가 안 적힌 카드(<see cref="DialogueNode.WriterBase"/>가 null — 그 값이 생기기 전에 만든 카드)에 그 값을 적는다. 앱이 파일을 열 때 ·
/// 새 프로젝트를 만들 때 그 PC의 설정으로 부른다 — 되돌리기 기록 없이, 프로젝트를 편집기에 넘기기 전에(열자마자 「저장 안 한 변경」이 되지 않는다).
/// 적은 값은 다음 저장에 파일로 간다(writer-formatting WF-D13 — 2026-11-23 소유자: 기존 카드는 「지금 설정으로 굳힘」).
/// </summary>
public static class WriterBaseFill
{
    /// <summary>적은 카드 수 — 처음 값이 아예 없는 카드와, 줄 간격만 없는 카드(줄 간격이 처음 값이 되기 전에 적힌 카드 · WF-D19 — 줄 간격만 적는다).</summary>
    public static int Fill(StoryProject project, WriterBase value)
    {
        int filled = 0;

        foreach (DialogueNode node in project.EnumerateNodes().OfType<DialogueNode>())
        {
            if (node.WriterBase is null)
            {
                node.WriterBase = value;
                filled++;
            }
            else if (node.WriterBase.LineSpacing is null && value.LineSpacing is { } spacing)
            {
                node.WriterBase = node.WriterBase with { LineSpacing = spacing };
                filled++;
            }
        }

        return filled;
    }
}

/// <summary>
/// <b>서식 구간 셈</b> — 화면을 모르는 순수 함수들. 구간 목록은 늘 <b>정규형</b>이다: 겹치지 않고 자리 차례이며, 모양 없는 구간이 없고,
/// 이웃한 같은 모양은 하나로 합친다. 셈은 글자마다의 모양 배열로 펴서 하고 다시 접는다(대사 한 줄은 짧다).
/// </summary>
public static class WriterMarkMath
{
    /// <summary>크기 차의 끝 — 설정 크기(9 ~ 48)에 더해 너무 작거나 크지 않게.</summary>
    public const int MinGrow = -6;

    public const int MaxGrow = 36;

    /// <summary>글자마다의 모양 — 길이 <paramref name="length"/>. 구간이 글 밖으로 나가면 잘린다.</summary>
    public static WriterLook[] Expand(IEnumerable<WriterSpan>? spans, int length)
    {
        var looks = new WriterLook[Math.Max(0, length)];
        Array.Fill(looks, WriterLook.Plain);

        foreach (WriterSpan span in spans ?? [])
        {
            int from = Math.Max(0, span.Start);
            int to = Math.Min(looks.Length, span.Start + Math.Max(0, span.Length));

            for (int at = from; at < to; at++)
            {
                looks[at] = span.Look;
            }
        }

        return looks;
    }

    /// <summary>글자마다의 모양을 정규형 구간 목록으로 접는다.</summary>
    public static List<WriterSpan> Collapse(IReadOnlyList<WriterLook> looks)
    {
        var spans = new List<WriterSpan>();
        int run = 0;

        for (int at = 1; at <= looks.Count; at++)
        {
            if (at < looks.Count && looks[at] == looks[run])
            {
                continue;
            }

            if (!looks[run].IsPlain)
            {
                spans.Add(new WriterSpan(run, at - run, looks[run]));
            }

            run = at;
        }

        return spans;
    }

    /// <summary>정규형으로 — 글 밖을 자르고, 겹치면 뒤의 것이 이기고, 빈 · 모양 없는 구간을 버리고, 이웃한 같은 모양을 합친다.</summary>
    public static List<WriterSpan> Normalize(IEnumerable<WriterSpan>? spans, int length) =>
        Collapse(Expand(spans, length));

    /// <summary>구간 <c>[start, end)</c>의 글자마다 모양을 바꾼다.</summary>
    public static List<WriterSpan> Apply(IEnumerable<WriterSpan>? spans, int length, int start, int end, Func<WriterLook, WriterLook> change)
    {
        WriterLook[] looks = Expand(spans, length);

        for (int at = Math.Max(0, start); at < Math.Min(length, end); at++)
        {
            looks[at] = change(looks[at]);
        }

        return Collapse(looks);
    }

    /// <summary>구간의 모든 글자가 그 모양을 가졌는가 — 빈 구간은 거짓.</summary>
    public static bool IsAll(IEnumerable<WriterSpan>? spans, int length, int start, int end, Func<WriterLook, bool> has)
    {
        WriterLook[] looks = Expand(spans, length);
        int from = Math.Max(0, start);
        int to = Math.Min(length, end);

        if (to <= from)
        {
            return false;
        }

        for (int at = from; at < to; at++)
        {
            if (!has(looks[at]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>글이 <paramref name="before"/>에서 <paramref name="after"/>로 바뀌었다 — 구간의 자리를 옮긴다</b> (writer-formatting 3.3).
    /// 두 글의 글자를 앞에서부터 짝짓고(가장 긴 공통 부분열) 짝이 선 글자는 제 모양을 그대로 가져간다. 새로 들어온 글자는:
    /// <list type="bullet">
    /// <item>그 자리에서 지운 글자가 없으면(넣기) <b>바로 앞 글자의 모양</b>을 잇는다 — 구간 끝에 이어 친 글자 · 구간 안에 친 글자는 그 모양이고, 구간 앞에
    /// 바로 친 글자는 아니다(워드 · 한글과 같다). 글 맨 앞에 친 글자는 모양이 없다.</item>
    /// <item>지운 글자가 있으면(바꿔 쓰기) 지운 글자들이 <b>모두 같은 모양</b>일 때만 그 모양을 잇는다(그 구간 안에서 고쳐 썼다). 아니면 모양 없이 들어온다.</item>
    /// </list>
    /// 한 번에 여러 군데를 고쳐도(쓰다 멈추기 전에 두 곳을 고쳤다) 고친 곳마다 따로 셈한다 — 사이의 서식이 잘리지 않는다. 아주 긴 줄은 셈이 무거워
    /// 앞 · 뒤 공통 부분만 보는 셈으로 갈음한다(가운데 서식이 잘릴 수 있다 — 글은 깨지지 않는다).
    /// </summary>
    public static List<WriterSpan> Rebase(IEnumerable<WriterSpan>? spans, string before, string after)
    {
        before ??= string.Empty;
        after ??= string.Empty;
        WriterLook[] old = Expand(spans, before.Length);

        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return Collapse(old);
        }

        int[] match = (long)before.Length * after.Length <= MaxAlignCells
            ? Align(before, after)
            : AlignEnds(before, after);
        var looks = new WriterLook[after.Length];
        int lastOld = -1;

        for (int at = 0; at < after.Length;)
        {
            if (match[at] >= 0)
            {
                looks[at] = old[match[at]];
                lastOld = match[at];
                at++;
                continue;
            }

            // 새로 들어온 글자들 — 다음 짝까지. 그 사이에서 지운 옛 글자는 (lastOld, nextOld).
            int gapEnd = at;

            while (gapEnd < after.Length && match[gapEnd] < 0)
            {
                gapEnd++;
            }

            int nextOld = gapEnd < after.Length ? match[gapEnd] : before.Length;
            WriterLook inserted;

            if (nextOld - lastOld <= 1)
            {
                inserted = lastOld >= 0 ? old[lastOld] : WriterLook.Plain;
            }
            else
            {
                inserted = old[lastOld + 1];

                for (int gone = lastOld + 2; gone < nextOld; gone++)
                {
                    if (old[gone] != inserted)
                    {
                        inserted = WriterLook.Plain;
                        break;
                    }
                }
            }

            for (; at < gapEnd; at++)
            {
                looks[at] = inserted;
            }
        }

        return Collapse(looks);
    }

    /// <summary>글자 짝짓기를 온전히 하는 끝 — 줄 하나가 이보다 길면(두 글 길이의 곱) 앞 · 뒤만 본다.</summary>
    private const long MaxAlignCells = 400_000;

    /// <summary>새 글의 글자마다 짝이 된 옛 글의 자리(없으면 -1) — 가장 긴 공통 부분열, 앞에서부터 짝짓는다.</summary>
    private static int[] Align(string before, string after)
    {
        int[,] longest = new int[before.Length + 1, after.Length + 1];

        for (int i = before.Length - 1; i >= 0; i--)
        {
            for (int j = after.Length - 1; j >= 0; j--)
            {
                longest[i, j] = before[i] == after[j]
                    ? longest[i + 1, j + 1] + 1
                    : Math.Max(longest[i + 1, j], longest[i, j + 1]);
            }
        }

        int[] match = new int[after.Length];
        Array.Fill(match, -1);
        int a = 0;
        int b = 0;

        while (a < before.Length && b < after.Length)
        {
            if (before[a] == after[b] && longest[a, b] == longest[a + 1, b + 1] + 1)
            {
                match[b] = a;
                a++;
                b++;
            }
            else if (longest[a, b + 1] >= longest[a + 1, b])
            {
                b++;   // 새 글자 — 넣었다
            }
            else
            {
                a++;   // 옛 글자 — 지웠다
            }
        }

        return match;
    }

    /// <summary>긴 줄의 갈음 — 공통 앞부분과 공통 뒷부분(앞을 먼저 최대로)만 짝짓는다.</summary>
    private static int[] AlignEnds(string before, string after)
    {
        int shorter = Math.Min(before.Length, after.Length);
        int head = 0;

        while (head < shorter && before[head] == after[head])
        {
            head++;
        }

        int tail = 0;

        while (tail < shorter - head && before[before.Length - 1 - tail] == after[after.Length - 1 - tail])
        {
            tail++;
        }

        int[] match = new int[after.Length];
        Array.Fill(match, -1);

        for (int at = 0; at < head; at++)
        {
            match[at] = at;
        }

        for (int at = 0; at < tail; at++)
        {
            match[after.Length - 1 - at] = before.Length - 1 - at;
        }

        return match;
    }

    /// <summary>한 번의 바꾸기가 글자 하나의 모양을 어떻게 바꾸는가 — 켜고 끄기는 <paramref name="on"/>(편집기가 고른 범위 전체로 정한다).</summary>
    public static WriterLook Change(WriterLook look, WriterStyleChange change, bool on) => change.Kind switch
    {
        WriterStyleKind.Bold => look with { Bold = on },
        WriterStyleKind.Italic => look with { Italic = on },
        WriterStyleKind.Strike => look with { Strike = on },
        WriterStyleKind.Color => look with { Color = change.Color },
        WriterStyleKind.Fill => look with { Fill = change.Color },
        WriterStyleKind.Font => look with { Font = string.IsNullOrWhiteSpace(change.Font) ? null : change.Font.Trim() },
        WriterStyleKind.Grow => look with { Grow = Math.Clamp(look.Grow + change.Step, MinGrow, MaxGrow) },
        WriterStyleKind.Size => look with { Grow = Math.Clamp(change.Step, MinGrow, MaxGrow) },
        _ => look
    };

    /// <summary>켜고 끄는 종류인가 — 그 판정(모두 켜져 있으면 끈다)이 필요한 것.</summary>
    public static bool IsToggle(WriterStyleKind kind) => kind is WriterStyleKind.Bold or WriterStyleKind.Italic or WriterStyleKind.Strike;

    /// <summary>켜고 끄는 종류의 「켜져 있다」.</summary>
    public static bool Has(WriterLook look, WriterStyleKind kind) => kind switch
    {
        WriterStyleKind.Bold => look.Bold,
        WriterStyleKind.Italic => look.Italic,
        WriterStyleKind.Strike => look.Strike,
        _ => false
    };

    /// <summary>
    /// 그 카드들의 서식에 쓰인 글자색(<paramref name="fill"/>이 거짓) · 바탕색 — 많이 쓴 차례(글자 수), 같으면 값 차례. 색 차림의 「이 프로젝트에 쓴 색」이다
    /// (writer-formatting WF-D14 — 같이 쓰는 사람이 쓴 색도 보인다).
    /// </summary>
    public static IReadOnlyList<WriterRgb> UsedColors(IEnumerable<DialogueNode> nodes, bool fill)
    {
        var counts = new Dictionary<WriterRgb, int>();

        foreach (DialogueNode node in nodes)
        {
            foreach (IReadOnlyList<WriterSpan> spans in node.WriterMarks.Values)
            {
                foreach (WriterSpan span in spans)
                {
                    if ((fill ? span.Look.Fill : span.Look.Color) is { } rgb)
                    {
                        counts[rgb] = counts.GetValueOrDefault(rgb) + span.Length;
                    }
                }
            }
        }

        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key.Hex, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .ToList();
    }
}
