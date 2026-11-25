namespace Vn.Authoring.Model;

/// <summary>
/// <b>[대본] 판의 크기</b> (2026-11-17 · settings) — 모든 챕터의 카드가 한 판(캔버스)을 나눠 쓰므로,
/// 대본이 많아지면 사람이 [⚙ 설정]에서 늘린다. 판은 왼쪽 위 (0, 0)에서 오른쪽 · 아래로 선다.
///
/// <b>프로젝트에 싣는다</b> — 대본의 양을 따라 커지는 그 프로젝트의 성질이라, 한 번 늘린 판이 누구에게나 같다. 기본이면 파일에 쓰지 않는다
/// (기본이 바뀔 때 옛 파일이 옛 기본을 굳히지 않게 — <see cref="EdgeShape"/>와 같은 규칙).
///
/// 줄일 때 카드를 판 안에 들이는 규칙은 <see cref="Graph.BoardResize"/>가 맡는다.
/// </summary>
public sealed record BoardSize(double Width, double Height)
{
    /// <summary>한 변의 가장 작은 값 — 소유자가 정한 「최소 범위」.</summary>
    public const double Minimum = 4000;

    /// <summary>한 변의 가장 큰 값.</summary>
    public const double Maximum = 60000;

    /// <summary>
    /// 카드 하나가 판에서 차지하는 기본 크기 — [대본] 판(<c>GraphEditorView</c>)이 그리는 값과 같다. 폭 · 글 칸 높이는 사람이 늘리면
    /// <see cref="NodeLayout.Width"/> · <see cref="NodeLayout.WritingHeight"/>가 정하고, 높이는 카드 머리 · 아래 선택지 칸까지 친다.
    /// </summary>
    public const double CardWidth = 210;

    /// <summary>카드 글 칸의 기본 높이.</summary>
    public const double CardWritingHeight = 88;

    /// <summary>카드 하나가 세로로 차지하는 기본 자리(글 칸이 기본 높이일 때 — 머리 · 아래 선택지 칸 포함 · 자리를 넉넉히 잡는 값이다).</summary>
    public const double CardHeight = 240;

    /// <summary>판을 따로 정하지 않은 프로젝트의 크기 — 2026-11-17 전의 고정값 그대로.</summary>
    public static BoardSize Default { get; } = new(12000, 8000);

    public bool IsDefault => Equals(Default);

    /// <summary>한 변을 범위 안의 정수로 — 숫자가 아니면 기본 쪽 변으로.</summary>
    public static double ClampSide(double value, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(Math.Round(value), Minimum, Maximum) : fallback;

    /// <summary>사람이 적은 값을 범위 안으로.</summary>
    public BoardSize Clamped() => new(ClampSide(Width, Default.Width), ClampSide(Height, Default.Height));

    /// <summary>그 노드의 카드가 판에서 차지하는 폭.</summary>
    public static double CardWidthOf(StoryNode node) => node.Layout.Width ?? CardWidth;

    /// <summary>그 노드의 카드가 판에서 차지하는 높이 — 글 칸을 늘린 만큼 더한다.</summary>
    public static double CardHeightOf(StoryNode node) =>
        CardHeight + Math.Max(0, (node.Layout.WritingHeight ?? CardWritingHeight) - CardWritingHeight);
}
