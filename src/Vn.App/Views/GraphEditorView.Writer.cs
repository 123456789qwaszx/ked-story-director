using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;

namespace Vn.App.Views;

/// <summary>
/// <b>작가용 글 서식</b> (2026-11-22 소유자 · <c>docs/plans/writer-formatting.md</c>) — [대본] 위쪽 줄의 서식 도구 막대와 카드 글 칸의 꾸밈.
///
/// <list type="bullet">
/// <item><b>꾸밈</b> — 모델의 서식(<see cref="DialogueNode.WriterMarks"/> · 줄의 대사 글 기준)을 글 칸 좌표로 옮겨 <see cref="WriterTextBox.Looks"/>에 준다. 칸의 줄은
/// 처음 보인 글과의 줄 짝(<see cref="ScenarioLineIdentity.RowLineIds"/>)으로 대본 줄에 잇고, 줄 안의 대사 글 자리는 파서와 같은 규칙
/// (<see cref="ScenarioTextParser.DialogueBodyRange"/>)으로 잰다. 넣지 않은 글을 쓰는 동안에는 모델의 글에서 칸의 글로 자리를 옮겨(넣을 때와 같은 셈 —
/// <see cref="WriterMarkMath.Rebase"/>) 쓰는 대로 따라간다.</item>
/// <item><b>도구 막대</b> — 글꼴 · 크기 −/+ · 굵게 · 기울임 · 취소선 · 글자색 · 바탕색 · 정렬. 마지막으로 쓴 글 칸과 그 고른 범위에 입힌다. 단추는 초점을 받지 않고,
/// 초점을 가져가는 것(글꼴 고르기 · 색 차림)을 쓰는 동안에는 칸의 고른 범위를 붙들어 두고(옅게 칠한 채 남는다), 다 쓰면 그 칸에 초점과 고른 범위를 되돌린다.
/// 넣지 않은 글이 있으면 먼저 넣는다(줄 신원이 서야 자리를 센다).</item>
/// </list>
/// 서식은 작가가 보기 위한 것이다 — 내보내기(대사 Yarn · 진행 JSON)에 나가지 않는다(WF-D1 · D2).
/// </summary>
public partial class GraphEditorView
{
    /// <summary>서식을 입힐 글 칸 — 마지막으로 초점을 쥔 카드 글 칸의 노드. 판의 빈 곳 · 카드 머리를 누르면 놓는다.</summary>
    private string? _formatNodeId;

    /// <summary>그 칸에서 마지막으로 고른 범위(글 칸 좌표) — 초점을 가져가는 도구를 쓰는 동안에도 붙들어 둔다.</summary>
    private (int Start, int End) _formatSelection;

    /// <summary>도구 막대의 상태를 맞추는 중 — 글꼴 고르기의 바뀜 알림을 사람의 고르기로 받지 않는다.</summary>
    private bool _syncingToolbar;

    private ToggleButton? _boldButton;
    private ToggleButton? _italicButton;
    private ToggleButton? _strikeButton;
    private ComboBox? _fontBox;

    /// <summary>글꼴 고르기의 목록 — 이 PC에 없는 글꼴이 서식에 들어 있으면(같이 쓰는 사람이 고른 것) 그 이름도 더해 보인다.</summary>
    private List<string> _fontItems = new();
    private TextBlock? _sizeText;
    private Border? _colorBar;
    private Border? _fillBar;
    private readonly Dictionary<WriterAlign, ToggleButton> _alignButtons = new();
    private readonly List<Control> _formatControls = new();

    /// <summary>초점을 가져가는 도구 — 색 차림들(글꼴 고르기는 <see cref="_fontBox"/>).</summary>
    private readonly List<FlyoutBase> _formatFlyouts = new();

    /// <summary>
    /// 도구 막대를 쓰는 동안 고른 범위를 붙든 글 칸 — 초점을 가져가는 도구가 열려 칸이 초점을 잃어도 범위를 지우지 않는다(Avalonia는 초점을 잃은 칸의 범위를
    /// 지운다 · 붙든 동안은 옅게 칠한 채 남는다). 칸에 초점을 되돌리면 놓는다(<see cref="ReleaseFormatSelection"/>).
    /// </summary>
    private TextBox? _selectionHold;

    /// <summary>글꼴 고르기의 첫 칸 — 설정의 「대본 글 칸」 글꼴로 되돌린다.</summary>
    private const string DefaultFontItem = "기본 글꼴";

    /// <summary>시험이 본다 — 서식 도구 막대가 켜져 있는가(입힐 글 칸이 있는가).</summary>
    internal bool FormatToolbarEnabled => _formatNodeId is not null;

    // ── 꾸밈 ──────────────────────────────────────────

    /// <summary>
    /// 그 카드(모르면 모든 카드) 글 칸의 꾸밈과 정렬을 모델로 다시 입힌다 — 서식이 바뀌었을 때(<see cref="Vn.Authoring.Editing.ProjectChangeKind.WriterMarks"/>) 셸이
    /// 부른다. 판도 칸의 글도 그대로다.
    /// </summary>
    internal void RefreshWriterMarks(string? nodeId)
    {
        foreach ((string id, TextBox box) in _writingBoxes)
        {
            if (nodeId is null || string.Equals(id, nodeId, StringComparison.Ordinal))
            {
                UpdateLooks(id, box);
            }
        }

        RefreshFormatToolbar();
    }

    /// <summary>이 카드가 서식을 그리는 글 칸을 받는가 — 서식(글자 구간이나 왼쪽이 아닌 정렬)이 있을 때만.</summary>
    private bool WantsWriterBox(string nodeId) =>
        _session?.Project.FindDialogue(nodeId) is { } node && (node.WriterMarks.Count > 0 || node.WriterAlign != WriterAlign.Left);

    /// <summary>
    /// 한 글 칸의 꾸밈과 정렬을 지금 모델과 칸의 글로. 칸의 종류가 서식 유무와 맞지 않으면(서식이 처음 생겼다 · 다 걷혔다) 그 카드를 다시 지어 칸을 바꾼다 —
    /// 다시 짓기가 쓰던 글 · 커서 · 초점을 지킨다(RebuildCard). 지금 도는 처리가 끝난 뒤에 한다.
    /// </summary>
    private void UpdateLooks(string nodeId, TextBox box)
    {
        if (_session?.Project.FindDialogue(nodeId) is not { } node)
        {
            return;
        }

        if ((box is WriterTextBox) != WantsWriterBox(nodeId))
        {
            Dispatcher.UIThread.Post(() => UiGuard.Run(_session, "글 칸 바꾸기", () =>
            {
                if (ReferenceEquals(_writingBoxes.GetValueOrDefault(nodeId), box) && (box is WriterTextBox) != WantsWriterBox(nodeId))
                {
                    RebuildCard(nodeId);
                    RefreshFormatToolbar();
                }
            }));
            return;
        }

        if (box is not WriterTextBox writer)
        {
            return;
        }

        // 왼쪽은 값을 두지 않는다 — 보통 글 칸(dev)과 같은 기본값이다.
        if (node.WriterAlign == WriterAlign.Left)
        {
            writer.ClearValue(TextBox.TextAlignmentProperty);
        }
        else
        {
            TextAlignment align = node.WriterAlign == WriterAlign.Center ? TextAlignment.Center : TextAlignment.Right;

            if (writer.TextAlignment != align)
            {
                writer.TextAlignment = align;
            }
        }

        writer.Looks = node.WriterMarks.Count == 0
            ? []
            : LooksOf(node, writer.Text ?? string.Empty, _writingLayouts.GetValueOrDefault(nodeId), _session.Project.Speakers);
    }

    /// <summary>
    /// 모델의 서식을 글 칸 좌표로 — 칸의 줄마다 잇는 대본 줄을 찾고, 그 줄의 대사 글 자리에 서식을 놓는다. 칸의 대사 글이 모델의 글과 다르면(넣지 않은 글)
    /// 모델의 글에서 칸의 글로 자리를 옮긴다.
    /// </summary>
    internal static List<BoxLook> LooksOf(DialogueNode node, string text, ScenarioWritingLayout? layout, IReadOnlyList<Speaker> speakers)
    {
        var looks = new List<BoxLook>();

        if (layout is null || node.WriterMarks.Count == 0)
        {
            return looks;
        }

        List<(int Start, string Text)> rows = Rows(text);
        IReadOnlyList<string?> ids = ScenarioLineIdentity.RowLineIds(layout.Text, layout.RowOfLine, text);

        for (int row = 0; row < rows.Count && row < ids.Count; row++)
        {
            if (ids[row] is not { } lineId ||
                !node.WriterMarks.TryGetValue(lineId, out IReadOnlyList<WriterSpan>? spans) ||
                ScenarioTextParser.DialogueBodyRange(rows[row].Text, speakers) is not { } body)
            {
                continue;
            }

            string model = node.Script.Text(lineId).Text;
            string shown = rows[row].Text.Substring(body.Start, body.Length);
            IReadOnlyList<WriterSpan> placed = string.Equals(model, shown, StringComparison.Ordinal)
                ? spans
                : WriterMarkMath.Rebase(spans, model, shown);
            int offset = rows[row].Start + body.Start;

            foreach (WriterSpan span in placed)
            {
                looks.Add(new BoxLook(offset + span.Start, span.Length, span.Look));
            }
        }

        return looks;
    }

    /// <summary>
    /// 글 칸의 고른 범위 <c>[start, end)</c>를 대본 줄의 구간들로 — 줄마다, 대사 글 부분만(화자 · 조건 줄 · 빈 줄은 빠진다). 칸의 대사 글이 모델의 글과 같은
    /// 줄만 든다(넣지 못한 줄 · 아직 대본 줄이 아닌 새 줄은 자리를 셀 수 없다).
    /// </summary>
    internal static List<WriterRange> RangesOf(DialogueNode node, string text, ScenarioWritingLayout layout, IReadOnlyList<Speaker> speakers, int start, int end)
    {
        var ranges = new List<WriterRange>();
        List<(int Start, string Text)> rows = Rows(text);
        IReadOnlyList<string?> ids = ScenarioLineIdentity.RowLineIds(layout.Text, layout.RowOfLine, text);

        for (int row = 0; row < rows.Count && row < ids.Count; row++)
        {
            if (ids[row] is not { } lineId ||
                ScenarioTextParser.DialogueBodyRange(rows[row].Text, speakers) is not { } body)
            {
                continue;
            }

            int bodyStart = rows[row].Start + body.Start;
            int from = Math.Max(start, bodyStart);
            int to = Math.Min(end, bodyStart + body.Length);

            if (to <= from ||
                !string.Equals(node.Script.Text(lineId).Text, rows[row].Text.Substring(body.Start, body.Length), StringComparison.Ordinal))
            {
                continue;
            }

            ranges.Add(new WriterRange(lineId, from - bodyStart, to - from));
        }

        return ranges;
    }

    /// <summary>칸의 글을 줄로 — 줄마다 칸 안의 시작 자리와 줄의 글(줄 끝 <c>\r</c>은 뺀다).</summary>
    private static List<(int Start, string Text)> Rows(string text)
    {
        var rows = new List<(int Start, string Text)>();
        int start = 0;

        while (true)
        {
            int newline = text.IndexOf('\n', start);
            int end = newline < 0 ? text.Length : newline;
            string row = text[start..end];
            rows.Add((start, row.EndsWith('\r') ? row[..^1] : row));

            if (newline < 0)
            {
                return rows;
            }

            start = newline + 1;
        }
    }

    // ── 입힐 칸 ──────────────────────────────────────────

    /// <summary>글 칸이 초점을 받거나 고른 범위가 바뀌었다 — 그 칸이 입힐 곳이다.</summary>
    private void TrackFormatTarget(string nodeId, TextBox box)
    {
        if (!box.IsFocused || !ReferenceEquals(_writingBoxes.GetValueOrDefault(nodeId), box))
        {
            return;
        }

        (int, int) selection = (Math.Min(box.SelectionStart, box.SelectionEnd), Math.Max(box.SelectionStart, box.SelectionEnd));

        if (string.Equals(_formatNodeId, nodeId, StringComparison.Ordinal) && _formatSelection == selection)
        {
            return;
        }

        // 글자를 칠 때마다 커서가 옮겨 범위가 바뀐다 — 빈 범위에서 빈 범위로(치는 중)는 도구 막대의 모습이 같으니 맞추지 않는다.
        bool sameLook = string.Equals(_formatNodeId, nodeId, StringComparison.Ordinal) &&
                        _formatSelection.Start == _formatSelection.End && selection.Item1 == selection.Item2;
        _formatNodeId = nodeId;
        _formatSelection = selection;

        if (!sameLook)
        {
            RefreshFormatToolbarLater();
        }
    }

    /// <summary>도구 막대를 맞추는 일이 미뤄져 있는가 — 한 번만 돈다.</summary>
    private bool _toolbarRefreshQueued;

    /// <summary>
    /// 칸이 커서 · 고른 범위를 옮길 때마다(글자를 칠 때마다) 도구 막대를 맞추는 것은 <b>칸의 입력 처리가 다 끝난 뒤</b>에 한 번만 한다 — 글 칸이 글자를 넣고
    /// 커서를 옮기는 도중(한글 조합 포함)에 다른 컨트롤을 건드리지 않는다.
    /// </summary>
    private void RefreshFormatToolbarLater()
    {
        if (_toolbarRefreshQueued)
        {
            return;
        }

        _toolbarRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _toolbarRefreshQueued = false;
            RefreshFormatToolbar();
        }, DispatcherPriority.Background);
    }

    /// <summary>입힐 칸을 놓는다 — 판의 빈 곳 · 카드 머리를 눌렀다.</summary>
    private void ReleaseFormatTarget()
    {
        ReleaseFormatSelection();

        if (_formatNodeId is null)
        {
            return;
        }

        _formatNodeId = null;
        RefreshFormatToolbar();
    }

    // ── 도구 막대 ──────────────────────────────────────────

    /// <summary>위쪽 줄에 서식 도구 막대를 세운다(한 번).</summary>
    private void BuildFormatToolbar()
    {
        Panel bar = WriterToolbar;
        bar.Children.Clear();

        // 도구를 누르는 순간 칸의 고른 범위를 붙든다 — 누르기가 초점을 옮기기 전에(터널).
        bar.AddHandler(PointerPressedEvent, (_, _) => HoldFormatSelection(), RoutingStrategies.Tunnel, handledEventsToo: true);

        _fontBox = new ComboBox
        {
            Width = 132,
            FontSize = 11,
            PlaceholderText = "글꼴",
            [ToolTip.TipProperty] = "글꼴 — 고른 글자에. 「기본 글꼴」은 이 카드의 처음 글꼴(카드를 만들 때 설정의 「대본 글 칸」 글꼴)입니다. 그 글꼴이 없는 PC에서는 기본 글꼴로 보입니다."
        };
        _fontItems = FontNames();
        _fontBox.ItemsSource = _fontItems;
        _fontBox.SelectionChanged += (_, _) =>
        {
            if (_syncingToolbar || _fontBox.SelectedItem is not string picked)
            {
                return;
            }

            ApplyFormat(new WriterStyleChange(WriterStyleKind.Font, Font: picked == DefaultFontItem ? null : picked));
        };
        _fontBox.DropDownClosed += (_, _) => RestoreFormatFocusLater();
        bar.Children.Add(_fontBox);

        bar.Children.Add(FormatButton("−", "글자 작게 — 고른 글자를 1pt 줄입니다", () => ApplyFormat(new WriterStyleChange(WriterStyleKind.Grow, Step: -1))));
        _sizeText = new TextBlock
        {
            MinWidth = 22,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            [ToolTip.TipProperty] = "고른 글자의 크기(pt) — 이 카드의 처음 크기에 더한 값입니다. 크기가 섞였으면 비어 있습니다."
        };
        bar.Children.Add(_sizeText);
        bar.Children.Add(FormatButton("+", "글자 크게 — 고른 글자를 1pt 키웁니다", () => ApplyFormat(new WriterStyleChange(WriterStyleKind.Grow, Step: 1))));
        bar.Children.Add(Separator());

        _boldButton = ToggleFormat(new TextBlock { Text = "B", FontWeight = FontWeight.Bold }, "굵게 — 고른 글자가 모두 굵으면 풉니다", WriterStyleKind.Bold);
        _italicButton = ToggleFormat(new TextBlock { Text = "I", FontStyle = FontStyle.Italic }, "기울임 — 한글 글꼴은 대부분 기울지 않습니다(영문은 기웁니다)", WriterStyleKind.Italic);
        _strikeButton = ToggleFormat(new TextBlock { Text = "S", TextDecorations = TextDecorations.Strikethrough }, "취소선 — 고른 글자에 줄을 긋습니다", WriterStyleKind.Strike);
        bar.Children.Add(_boldButton);
        bar.Children.Add(_italicButton);
        bar.Children.Add(_strikeButton);

        (Button colorButton, _colorBar) = ColorButton("가", "글자색 — 고른 글자에", WriterStyleKind.Color);
        (Button fillButton, _fillBar) = ColorButton(null, "바탕색 — 고른 글자 뒤에", WriterStyleKind.Fill);
        bar.Children.Add(colorButton);
        bar.Children.Add(fillButton);
        bar.Children.Add(Separator());

        foreach ((WriterAlign align, string tip) in new[]
                 {
                     (WriterAlign.Left, "왼쪽 정렬 — 이 카드의 글 전체"),
                     (WriterAlign.Center, "가운데 정렬 — 이 카드의 글 전체"),
                     (WriterAlign.Right, "오른쪽 정렬 — 이 카드의 글 전체")
                 })
        {
            var button = new ToggleButton
            {
                Content = AlignIcon(align),
                Focusable = false,
                Padding = new Thickness(6, 1),
                MinWidth = 26,
                [ToolTip.TipProperty] = tip
            };
            button.Click += (_, _) => ApplyAlign(align);
            _alignButtons[align] = button;
            _formatControls.Add(button);
            bar.Children.Add(button);
        }

        _formatControls.Add(_fontBox);
        RefreshFormatToolbar();
    }

    /// <summary>정렬 그림 — 긴 줄과 짧은 줄이 번갈아 그 쪽에 붙는다(문서 도구의 그 그림).</summary>
    private static Control AlignIcon(WriterAlign align)
    {
        var lines = new System.Text.StringBuilder();

        for (int row = 0; row < 4; row++)
        {
            double length = row % 2 == 0 ? 12 : 7;
            double left = align switch
            {
                WriterAlign.Center => (12 - length) / 2,
                WriterAlign.Right => 12 - length,
                _ => 0
            };
            double y = 1.5 + (row * 3);
            lines.Append(System.FormattableString.Invariant($"M{left},{y} H{left + length} "));
        }

        return new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(lines.ToString()),
            Stroke = Brushes.Black,
            StrokeThickness = 1.3,
            Width = 12,
            Height = 12,
            Margin = new Thickness(0, 2)
        };
    }

    /// <summary>바탕색 그림 — 형광 펜(비스듬한 펜 끝).</summary>
    private static Control MarkerIcon() => new Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse("M3,10 L8,3 L11,5.5 L6,12 L3,12 Z M8,3 L9.5,1 L12.5,3.5 L11,5.5"),
        Stroke = Brushes.Black,
        StrokeThickness = 1.1,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Width = 13,
        Height = 13,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private static Border Separator() => new() { Width = 1, Margin = new Thickness(4, 2), Background = new SolidColorBrush(Color.FromArgb(0x33, 0x55, 0x55, 0x55)) };

    private Button FormatButton(string glyph, string tip, Action run)
    {
        var button = new Button
        {
            Content = glyph,
            Focusable = false,
            Padding = new Thickness(6, 1),
            MinWidth = 24,
            FontSize = 12,
            [ToolTip.TipProperty] = tip
        };
        button.Click += (_, _) => run();
        _formatControls.Add(button);
        return button;
    }

    private ToggleButton ToggleFormat(TextBlock glyph, string tip, WriterStyleKind kind)
    {
        glyph.FontSize = 12;
        var button = new ToggleButton
        {
            Content = glyph,
            Focusable = false,
            Padding = new Thickness(6, 1),
            MinWidth = 26,
            [ToolTip.TipProperty] = tip
        };

        // 눌린 모습은 고른 글자의 모양이 정한다 — 단추가 제멋대로 켜지고 꺼지지 않게 입힌 뒤 다시 맞춘다.
        button.Click += (_, _) =>
        {
            ApplyFormat(WriterStyleChange.Toggle(kind));
            RefreshFormatToolbar();
        };
        _formatControls.Add(button);
        return button;
    }

    /// <summary>
    /// 색 차림 하나(글자색 · 바탕색) — 정해 둔 열 가지 · 「없음」 · <b>이 프로젝트에 쓴 색</b> · <b>직접 고르기(RGB)</b>(2026-11-23 소유자 — 「글자 색깔과 바탕색의
    /// 종류가 조금 제한 적인게 아쉬운데, 사용한 색깔과 더불어서 RGB를 원하는 걸 선택할 수 있는 것도」 · writer-formatting WF-D14). 시험이 들여다본다.
    /// </summary>
    internal sealed class ColorMenu
    {
        internal required Flyout Flyout { get; init; }

        /// <summary>단추 밑의 색 띠 — 마지막에 고른 색.</summary>
        internal required Border Bar { get; init; }

        /// <summary>이 프로젝트에 쓴 색(정해 둔 열 가지 밖) — 차림을 열 때마다 다시 센다. 없으면 머리와 함께 숨는다.</summary>
        internal required WrapPanel Used { get; init; }

        internal required TextBlock UsedTitle { get; init; }

        /// <summary>직접 고르기 — <c>#RRGGBB</c> 칸 · R · G · B 막대 · 미리 보기 · [입히기].</summary>
        internal required TextBox Hex { get; init; }

        internal required Slider[] Channels { get; init; }

        internal required Border Preview { get; init; }

        internal required Button Apply { get; init; }

        /// <summary>마지막에 고른 색 — 직접 고르기가 이 색에서 시작한다.</summary>
        internal WriterRgb Last { get; set; }
    }

    private readonly Dictionary<WriterStyleKind, ColorMenu> _colorMenus = new();

    /// <summary>시험이 본다 — 글자색(<see cref="WriterStyleKind.Color"/>) · 바탕색(<see cref="WriterStyleKind.Fill"/>)의 색 차림.</summary>
    internal ColorMenu ColorMenuOf(WriterStyleKind kind) => _colorMenus[kind];

    /// <summary>글자색 · 바탕색 — 단추 밑의 색 띠가 마지막에 쓴 색이고, 누르면 색 차림이 열린다.</summary>
    private (Button Button, Border Bar) ColorButton(string? glyph, string tip, WriterStyleKind kind)
    {
        bool fill = kind == WriterStyleKind.Fill;
        WriterRgb first = WriterSwatch.All[fill ? 2 : 0].Of(fill);   // 바탕은 노랑, 글자는 빨강부터
        var bar = new Border { Height = 3, Margin = new Thickness(0, 1, 0, 0), Background = WriterPalette.Brush(first) };
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };

        // 고른 색을 입힌다 — 차림을 닫고(닫힌 뒤 칸에 초점과 범위가 돌아온다) 띠가 그 색이 된다.
        void Pick(WriterRgb? rgb)
        {
            if (rgb is { } picked)
            {
                bar.Background = WriterPalette.Brush(picked);
                _colorMenus[kind].Last = picked;
            }

            flyout.Hide();
            ApplyFormat(new WriterStyleChange(kind, Color: rgb));
        }

        Button Swatch(WriterRgb rgb, string name)
        {
            var swatch = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                Padding = new Thickness(0),
                Focusable = false,
                Background = WriterPalette.Brush(rgb),
                Tag = rgb,
                [ToolTip.TipProperty] = name
            };
            swatch.Click += (_, _) => Pick(rgb);
            return swatch;
        }

        var palette = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 5 * 28 };

        foreach (WriterSwatch swatch in WriterSwatch.All)
        {
            palette.Children.Add(Swatch(swatch.Of(fill), swatch.Name));
        }

        var none = new Button
        {
            Content = "없음",
            FontSize = 11,
            Margin = new Thickness(2),
            Focusable = false,
            [ToolTip.TipProperty] = fill ? "바탕색을 걷습니다" : "글자색을 걷습니다"
        };
        none.Click += (_, _) => Pick(null);
        palette.Children.Add(none);

        var usedTitle = new TextBlock { Text = "이 프로젝트에 쓴 색", FontSize = 11, Margin = new Thickness(2, 6, 2, 1), Opacity = 0.7 };
        var used = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 5 * 28 };

        // 직접 고르기 — 막대는 초점을 받지 않는다(초점이 막대에 있으면 Delete가 고른 카드를 지운다 · 글 칸 밖의 Delete). 16진수 칸은 글 칸이라 괜찮다.
        var preview = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)),
            Background = WriterPalette.Brush(first)
        };
        var hex = new TextBox
        {
            Width = 86,
            FontSize = 12,
            Text = first.Hex,
            [ToolTip.TipProperty] = "#RRGGBB — 16진수로 적고 Enter로 입힙니다"
        };
        var channels = new Slider[3];
        var values = new TextBlock[3];
        var rows = new StackPanel { Spacing = 0 };
        bool syncing = false;

        WriterRgb FromChannels() => new((byte)Math.Round(channels[0].Value), (byte)Math.Round(channels[1].Value), (byte)Math.Round(channels[2].Value));

        void Show(WriterRgb rgb, bool keepHex)
        {
            syncing = true;

            try
            {
                byte[] parts = [rgb.R, rgb.G, rgb.B];

                for (int index = 0; index < 3; index++)
                {
                    channels[index].Value = parts[index];
                    values[index].Text = parts[index].ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                if (!keepHex)
                {
                    hex.Text = rgb.Hex;
                }

                preview.Background = WriterPalette.Brush(rgb);
            }
            finally
            {
                syncing = false;
            }
        }

        foreach ((int index, string label) in new[] { (0, "R"), (1, "G"), (2, "B") })
        {
            channels[index] = new Slider
            {
                Minimum = 0,
                Maximum = 255,
                Width = 96,
                SmallChange = 1,
                LargeChange = 16,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            values[index] = new TextBlock { Width = 26, FontSize = 11, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            channels[index].ValueChanged += (_, _) =>
            {
                if (!syncing)
                {
                    Show(FromChannels(), keepHex: false);
                }
            };
            rows.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { new TextBlock { Text = label, Width = 10, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, channels[index], values[index] }
            });
        }

        hex.TextChanged += (_, _) =>
        {
            if (!syncing && WriterRgb.Parse(hex.Text) is { } typed)
            {
                Show(typed, keepHex: true);
            }
        };

        var apply = new Button { Content = "입히기", FontSize = 11, Focusable = false, [ToolTip.TipProperty] = "고른 글자에 이 색을 입힙니다" };
        apply.Click += (_, _) => Pick(WriterRgb.Parse(hex.Text) ?? FromChannels());
        hex.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Pick(WriterRgb.Parse(hex.Text) ?? FromChannels());
            }
        };

        flyout.Content = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                palette,
                usedTitle,
                used,
                new TextBlock { Text = "직접 고르기 (RGB)", FontSize = 11, Margin = new Thickness(2, 6, 2, 1), Opacity = 0.7 },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(2, 0), Children = { preview, hex, apply } },
                rows
            }
        };

        var menu = new ColorMenu
        {
            Flyout = flyout,
            Bar = bar,
            Used = used,
            UsedTitle = usedTitle,
            Hex = hex,
            Channels = channels,
            Preview = preview,
            Apply = apply,
            Last = first
        };
        _colorMenus[kind] = menu;
        Show(first, keepHex: false);

        // 열 때마다 — 이 프로젝트에 쓴 색을 다시 세고(정해 둔 열 가지 밖 · 많이 쓴 차례 열 개), 직접 고르기는 마지막에 고른 색에서 시작한다.
        flyout.Opening += (_, _) =>
        {
            used.Children.Clear();

            if (_session is { } session)
            {
                HashSet<WriterRgb> standard = WriterSwatch.All.Select(swatch => swatch.Of(fill)).ToHashSet();

                foreach (WriterRgb rgb in WriterMarkMath.UsedColors(session.Project.EnumerateNodes().OfType<DialogueNode>(), fill)
                             .Where(rgb => !standard.Contains(rgb))
                             .Take(10))
                {
                    used.Children.Add(Swatch(rgb, rgb.Hex));
                }
            }

            usedTitle.IsVisible = used.IsVisible = used.Children.Count > 0;
            Show(menu.Last, keepHex: false);
        };
        flyout.Closed += (_, _) => RestoreFormatFocusLater();
        _formatFlyouts.Add(flyout);

        var button = new Button
        {
            Focusable = false,
            Padding = new Thickness(6, 1),
            MinWidth = 26,
            Flyout = flyout,
            Content = new StackPanel
            {
                Children =
                {
                    glyph is null ? MarkerIcon() : new TextBlock { Text = glyph, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center },
                    bar
                }
            },
            [ToolTip.TipProperty] = tip
        };
        _formatControls.Add(button);
        return (button, bar);
    }

    /// <summary>글꼴 고르기의 목록 — 「기본 글꼴」 다음에 이 PC의 글꼴(이름 차례).</summary>
    private static List<string> FontNames()
    {
        var names = new List<string> { DefaultFontItem };

        try
        {
            names.AddRange(FontManager.Current.SystemFonts
                .Select(family => family.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !name.StartsWith('$'))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.CurrentCulture));
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // 글꼴 목록을 못 읽는 환경 — 기본 글꼴만 둔다.
        }

        return names;
    }

    /// <summary>
    /// 지금 입힐 칸과 고른 범위 — 칸이 판을 다시 지어 바뀌었으면 새 칸이다. 고른 범위는 붙들어 둔 것(초점을 가져가는 도구를 쓰는 동안 칸이 범위를 잃어도)이다.
    /// </summary>
    private (string NodeId, TextBox Box, int Start, int End)? FormatTarget()
    {
        if (_formatNodeId is not { } nodeId || _session is null || !_writingBoxes.TryGetValue(nodeId, out TextBox? box))
        {
            return null;
        }

        int length = box.Text?.Length ?? 0;
        int start = Math.Clamp(_formatSelection.Start, 0, length);
        int end = Math.Clamp(_formatSelection.End, start, length);

        // 칸이 초점을 쥐고 있으면 칸의 범위가 지금 것이다.
        if (box.IsFocused)
        {
            start = Math.Min(box.SelectionStart, box.SelectionEnd);
            end = Math.Max(box.SelectionStart, box.SelectionEnd);
        }

        return (nodeId, box, start, end);
    }

    /// <summary>
    /// 고른 글자에 한 가지 모양을 입힌다 — 넣지 않은 글을 먼저 넣고(줄 신원이 서야 자리를 센다), 고른 범위를 대본 줄의 구간들로 바꿔 편집기에 넘긴다.
    /// 편집기가 켜고 끄기를 판정한다(모두 켜져 있으면 끈다). 끝나면 칸에 초점과 고른 범위를 되돌린다.
    /// </summary>
    internal void ApplyFormat(WriterStyleChange change)
    {
        UiGuard.Run(_session, "글 서식", () =>
        {
            if (FormatTarget() is not { } target || _session is not { } session)
            {
                return;
            }

            if (target.End <= target.Start)
            {
                session.SetStatus("서식을 입힐 글자를 먼저 고르세요 — 글 칸에서 글자를 끌어 고릅니다.");
                return;
            }

            (string nodeId, TextBox box, int start, int end) = target;

            if (PendingWriting(nodeId, box) is { } pending)
            {
                _liveWriting.Stop();
                ApplyWriting(nodeId, box, pending, live: true);
                box = _writingBoxes.GetValueOrDefault(nodeId) ?? box;
            }

            if (session.Project.FindDialogue(nodeId) is not { } node || _writingLayouts.GetValueOrDefault(nodeId) is not { } layout)
            {
                return;
            }

            List<WriterRange> ranges = RangesOf(node, box.Text ?? string.Empty, layout, session.Project.Speakers, start, end);

            if (ranges.Count == 0)
            {
                session.SetStatus("고른 범위에 서식을 입힐 대사 글이 없습니다 — 화자 이름 · 조건 줄에는 입히지 않고, 넣지 못한 줄은 넣은 뒤에 입힙니다.");
                return;
            }

            session.Editor.ApplyWriterStyle(nodeId, ranges, change);
            _formatSelection = (start, end);
        });

        // 입혔든 못 입혔든 칸에 초점과 범위를 되돌린다(붙든 범위도 놓는다 — 도구가 아직 열려 있으면 닫힐 때).
        RestoreFormatFocus();
    }

    /// <summary>카드 글 칸의 좌우 정렬 — 고른 글자가 없어도 그 칸이면 된다.</summary>
    private void ApplyAlign(WriterAlign align)
    {
        UiGuard.Run(_session, "글 정렬", () =>
        {
            if (FormatTarget() is { } target)
            {
                _session?.Editor.SetWriterAlign(target.NodeId, align);
                RestoreFormatFocus();
            }

            RefreshFormatToolbar();
        });
    }

    /// <summary>
    /// 입힐 칸에 초점과 고른 범위를 되돌린다 — 도구 막대를 쓴 뒤. 초점을 가져가는 도구가 다 닫혔으면 붙든 범위도 놓는다.
    /// </summary>
    private void RestoreFormatFocus()
    {
        if (FormatTarget() is { } target && !target.Box.IsFocused)
        {
            (int start, int end) = (target.Start, target.End);
            target.Box.Focus();
            target.Box.SelectionStart = start;
            target.Box.SelectionEnd = end;
        }

        if (!FormatToolOpen)
        {
            ReleaseFormatSelection();
        }
    }

    /// <summary>
    /// 초점을 가져간 도구(글꼴 고르기 · 색 차림)를 닫은 뒤 — 칸에 초점과 범위를 되돌리는 일은 <b>입력 처리가 다 끝난 뒤</b>에 한다. 윈도에서는 도구의 창이
    /// 닫히며 생기는 초점 알림이 입력으로 늦게 오는데, 그보다 먼저 되돌리면 그 알림이 칸의 초점을 다시 가져가 범위가 풀렸다(2026-11-23 소유자 — 「여러 글자를
    /// 드래그 한 뒤에 글꼴을 바꾸면, 드래그 했던게 모두 풀리는 버그」). 그사이 사람이 다른 칸을 눌렀으면 입힐 칸이 그 칸으로 바뀌어 있어 끌어오지 않는다.
    /// </summary>
    private void RestoreFormatFocusLater() => Dispatcher.UIThread.Post(RestoreFormatFocus, DispatcherPriority.Background);

    /// <summary>초점을 가져가는 도구가 열려 있는가.</summary>
    private bool FormatToolOpen => _fontBox?.IsDropDownOpen == true || _formatFlyouts.Exists(flyout => flyout.IsOpen);

    /// <summary>도구 막대를 누른다 — 입힐 칸이 초점을 쥐고 있으면 그 고른 범위를 붙든다(<see cref="_selectionHold"/>).</summary>
    private void HoldFormatSelection()
    {
        if (FormatTarget() is { Box: { IsFocused: true } box })
        {
            box.ClearSelectionOnLostFocus = false;
            _selectionHold = box;
        }
    }

    /// <summary>붙든 범위를 놓는다 — 칸이 초점을 쥐지 않았으면(사람이 다른 데로 갔다) 보통 칸처럼 범위를 걷는다.</summary>
    private void ReleaseFormatSelection()
    {
        if (_selectionHold is not { } box)
        {
            return;
        }

        _selectionHold = null;
        box.ClearValue(TextBox.ClearSelectionOnLostFocusProperty);

        if (!box.IsFocused)
        {
            box.ClearSelection();
        }
    }

    /// <summary>시험이 본다 — 도구 막대가 칸의 고른 범위를 붙들고 있는가.</summary>
    internal bool HoldsFormatSelection => _selectionHold is not null;

    /// <summary>도구 막대가 고른 글자의 지금 모양을 보인다 — 모두 굵으면 B가 눌려 있고, 크기 · 글꼴이 하나면 그것을 보인다.</summary>
    private void RefreshFormatToolbar()
    {
        if (_fontBox is null)
        {
            return;
        }

        (string NodeId, TextBox Box, int Start, int End)? target = FormatTarget();

        foreach (Control control in _formatControls)
        {
            control.IsEnabled = target is not null;
        }

        WriterToolbar.SetValue(ToolTip.TipProperty, target is null ? "카드의 글 칸에서 글자를 고르면 켜집니다" : null);

        IReadOnlyList<BoxLook> looks = target?.Box is WriterTextBox writer ? writer.Looks : [];
        List<WriterLook> chosen = target is { } picked && picked.End > picked.Start
            ? Enumerable.Range(picked.Start, picked.End - picked.Start)
                .Select(at => looks.LastOrDefault(look => at >= look.Start && at < look.End).Look ?? WriterLook.Plain)
                .ToList()
            : [];

        bool All(Func<WriterLook, bool> has) => chosen.Count > 0 && chosen.All(has);

        _syncingToolbar = true;

        try
        {
            _boldButton!.IsChecked = All(look => look.Bold);
            _italicButton!.IsChecked = All(look => look.Italic);
            _strikeButton!.IsChecked = All(look => look.Strike);

            List<int> grows = chosen.Select(look => look.Grow).Distinct().ToList();
            double cardSize = target?.Box.FontSize ?? ScriptFont.Size;   // 그 카드의 처음 크기(칸이 입고 있다)
            _sizeText!.Text = target is null ? string.Empty : grows.Count == 1 ? $"{cardSize + grows[0]:0}" : chosen.Count == 0 ? $"{cardSize:0}" : string.Empty;

            List<string?> fonts = chosen.Select(look => look.Font).Distinct().ToList();

            if (fonts is [{ } missing] && !_fontItems.Contains(missing, StringComparer.Ordinal))
            {
                _fontItems = [.. _fontItems, missing];
                _fontBox.ItemsSource = _fontItems;
            }

            _fontBox.SelectedItem = fonts.Count == 1 ? fonts[0] ?? DefaultFontItem : chosen.Count == 0 && target is not null ? DefaultFontItem : null;

            WriterAlign align = target is { } shown && _session?.Project.FindDialogue(shown.NodeId) is { } node ? node.WriterAlign : WriterAlign.Left;

            foreach ((WriterAlign key, ToggleButton button) in _alignButtons)
            {
                button.IsChecked = target is not null && key == align;
            }
        }
        finally
        {
            _syncingToolbar = false;
        }
    }

    /// <summary>시험이 본다 — 도구 막대의 단추 상태(굵게 · 기울임 · 취소선 · 크기 글).</summary>
    internal (bool? Bold, bool? Italic, bool? Strike, string? Size, string? Font) FormatToolbarState =>
        (_boldButton?.IsChecked, _italicButton?.IsChecked, _strikeButton?.IsChecked, _sizeText?.Text, _fontBox?.SelectedItem as string);
}
