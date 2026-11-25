using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Rendering;
using Vn.Authoring.Script;
using Vn.App.Strings;
using Vn.Authoring.Strings;

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
    /// <summary>크기 칸 — 고른 글자의 크기를 보이고, 숫자를 적어 Enter면 그 크기로(WF-D20).</summary>
    private TextBox? _sizeEntry;

    /// <summary>줄 간격 칸 — 카드의 줄 간격(%)을 보이고, 숫자를 적어 Enter면 그 값으로(WF-D20).</summary>
    private TextBox? _lineSpacingEntry;

    /// <summary>서식 도구의 줄 간격 한 칸(%) — −/+ 한 번에.</summary>
    private const int LineSpacingStep = 10;
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
    private static string DefaultFontItem => Ui.Format_DefaultFont;

    /// <summary>시험이 본다 — 서식 도구 막대가 켜져 있는가(입힐 글 칸이 있는가).</summary>
    internal bool FormatToolbarEnabled => _formatNodeId is not null;

    // ── 꾸밈 ──────────────────────────────────────────

    /// <summary>
    /// 그 카드(모르면 모든 카드) 글 칸의 꾸밈과 정렬을 모델로 다시 입힌다 — 서식이 바뀌었을 때(<see cref="Vn.Authoring.Editing.ProjectChangeKind.WriterMarks"/>) 셸이
    /// 부른다. 판도 칸의 글도 그대로다.
    /// </summary>
    internal void RefreshWriterMarks(string? nodeId)
    {
        var swaps = new List<string>();

        foreach ((string id, TextBox box) in _writingBoxes)
        {
            if ((nodeId is null || string.Equals(id, nodeId, StringComparison.Ordinal)) && !DrawLooks(id, box))
            {
                swaps.Add(id);
            }
        }

        SwapWritingBoxesLater(swaps);
        RefreshFormatToolbar();
    }

    /// <summary>
    /// 이 카드가 서식을 그리는 글 칸을 받는가 — 서식(글자 구간이나 왼쪽이 아닌 정렬)이 있거나, 색을 정한 화자의 대사가 있을 때만(WF-D15).
    /// </summary>
    private bool WantsWriterBox(string nodeId) =>
        _session?.Project is { } project && project.FindDialogue(nodeId) is { } node &&
        (node.WriterMarks.Count > 0 || node.WriterAlign != WriterAlign.Left || SpeaksInColor(project, node));

    /// <summary>이 카드에 이름 색을 정한 화자의 대사가 있는가.</summary>
    private static bool SpeaksInColor(StoryProject project, DialogueNode node) =>
        project.Speakers.Any(Dressed) &&
        node.Script.ActiveLines.Any(line => project.FindSpeaker(node.Script.Text(line.Id).Speaker) is { } speaker && Dressed(speaker));

    /// <summary>이름을 꾸민 화자인가 — 이름 색(WF-D15)이나 이름 바탕색(WF-D22)을 정했다.</summary>
    private static bool Dressed(Speaker speaker) => speaker.Color is not null || speaker.Fill is not null;

    /// <summary>
    /// 칸의 종류가 서식 유무와 맞지 않는 카드들을 다시 지어 칸을 바꾼다 — 지금 도는 처리가 끝난 뒤에. 하나면 그 카드만(RebuildCard), 여럿이면(화자 이름 색 하나가 여러
    /// 카드를 바꾼다) 판을 한 번 다시 짓는다 — 그대로인 카드는 판에 둔다(<see cref="CanKeep"/>가 칸의 종류도 본다). 다시 짓기가 쓰던 글 · 커서 · 초점을 지킨다.
    /// </summary>
    private void SwapWritingBoxesLater(IReadOnlyList<string> nodeIds)
    {
        if (nodeIds.Count == 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => UiGuard.Run(_session, Ui.Format_ChangeWritingBox, () =>
        {
            List<string> still = nodeIds
                .Where(id => _writingBoxes.GetValueOrDefault(id) is { } box && (box is WriterTextBox) != WantsWriterBox(id))
                .ToList();

            if (still.Count == 1)
            {
                RebuildCard(still[0]);
            }
            else if (still.Count > 1)
            {
                Rebuild();
            }

            if (still.Count > 0)
            {
                RefreshFormatToolbar();
            }
        }));
    }

    /// <summary>
    /// 한 글 칸의 꾸밈과 정렬을 지금 모델과 칸의 글로. 칸의 종류가 서식 유무와 맞지 않으면(서식이 처음 생겼다 · 다 걷혔다) 그 카드를 다시 지어 칸을 바꾼다 —
    /// 다시 짓기가 쓰던 글 · 커서 · 초점을 지킨다(RebuildCard). 지금 도는 처리가 끝난 뒤에 한다.
    /// </summary>
    private void UpdateLooks(string nodeId, TextBox box)
    {
        if (!DrawLooks(nodeId, box))
        {
            SwapWritingBoxesLater([nodeId]);
        }
    }

    /// <summary>칸에 꾸밈과 정렬을 입힌다 — 칸의 종류가 맞지 않으면 아무것도 하지 않고 거짓(바꿀 칸이다).</summary>
    private bool DrawLooks(string nodeId, TextBox box)
    {
        if (_session?.Project.FindDialogue(nodeId) is not { } node)
        {
            return true;
        }

        if ((box is WriterTextBox) != WantsWriterBox(nodeId))
        {
            return false;
        }

        // 카드의 바탕 꼴 — 줄 간격을 서식 도구로 바꾸면 여기서 입는다(WF-D19 · 서식이 없는 칸도).
        ScriptFont.Apply(box, node.WriterBase);

        if (box is not WriterTextBox writer)
        {
            return true;
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

        writer.Looks = LooksOf(node, writer.Text ?? string.Empty, _writingLayouts.GetValueOrDefault(nodeId), _session.Project.Speakers);
        return true;
    }

    /// <summary>
    /// 모델의 서식을 글 칸 좌표로 — 칸의 줄마다 잇는 대본 줄을 찾고, 그 줄의 대사 글 자리에 서식을 놓는다. 칸의 대사 글이 모델의 글과 다르면(넣지 않은 글)
    /// 모델의 글에서 칸의 글로 자리를 옮긴다. 화자 이름은 그 화자의 이름 색으로(WF-D15) — 칸의 글을 그대로 읽으므로 넣기 전의 줄에도 선다.
    /// </summary>
    internal static List<BoxLook> LooksOf(DialogueNode node, string text, ScenarioWritingLayout? layout, IReadOnlyList<Speaker> speakers)
    {
        var looks = new List<BoxLook>();
        List<(int Start, string Text)> rows = Rows(text);
        IReadOnlyList<string?> ids = layout is null || node.WriterMarks.Count == 0
            ? []
            : ScenarioLineIdentity.RowLineIds(layout.Text, layout.RowOfLine, text);
        bool colored = speakers.Any(Dressed);

        for (int row = 0; row < rows.Count; row++)
        {
            // 화자 이름 — 이름 색 · 이름 바탕색(WF-D15 · WF-D22).
            if (colored &&
                ScenarioTextParser.SpeakerNameRange(rows[row].Text, speakers) is { } name &&
                speakers.FirstOrDefault(speaker => string.Equals(speaker.Name, name.Name, StringComparison.Ordinal)) is { } speaker &&
                Dressed(speaker))
            {
                looks.Add(new BoxLook(rows[row].Start + name.Start, name.Length, new WriterLook(Color: speaker.Color, Fill: speaker.Fill)));
            }

            if (row >= ids.Count ||
                ids[row] is not { } lineId ||
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
            PlaceholderText = Ui.Settings_Font,
            [ToolTip.TipProperty] = Ui.Format_FontForSelectedTextOr
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

        bar.Children.Add(FormatButton("−", Ui.Format_SmallerShrinksSelectedTextBy, () => ApplyFormat(new WriterStyleChange(WriterStyleKind.Grow, Step: -1))));
        _sizeEntry = ValueEntry(
            36,
            Ui.Format_SizeOfSelectedTextPt,
            ApplyTypedSize);
        bar.Children.Add(_sizeEntry);
        bar.Children.Add(FormatButton("+", Ui.Format_LargerGrowsSelectedTextBy, () => ApplyFormat(new WriterStyleChange(WriterStyleKind.Grow, Step: 1))));
        bar.Children.Add(Separator());

        _boldButton = ToggleFormat(new TextBlock { Text = "B", FontWeight = FontWeight.Bold }, Ui.Format_BoldTurnsItOffIf, WriterStyleKind.Bold);
        _italicButton = ToggleFormat(new TextBlock { Text = "I", FontStyle = FontStyle.Italic }, Ui.Format_ItalicSlantsSelectedTextOr, WriterStyleKind.Italic);
        _strikeButton = ToggleFormat(new TextBlock { Text = "S", TextDecorations = TextDecorations.Strikethrough }, Ui.Format_StrikethroughDrawsLineThroughSelected, WriterStyleKind.Strike);
        bar.Children.Add(_boldButton);
        bar.Children.Add(_italicButton);
        bar.Children.Add(_strikeButton);

        (Button colorButton, _colorBar) = ColorButton(Ui.Format_Text, Ui.Format_TextColorForSelectedText, WriterStyleKind.Color);
        (Button fillButton, _fillBar) = ColorButton(null, Ui.Format_BackgroundColorBehindSelectedText, WriterStyleKind.Fill);
        bar.Children.Add(colorButton);
        bar.Children.Add(fillButton);
        bar.Children.Add(Separator());

        foreach ((WriterAlign align, string tip) in new[]
                 {
                     (WriterAlign.Left, Ui.Format_AlignLeftAllTextIn),
                     (WriterAlign.Center, Ui.Format_AlignCenterAllTextIn),
                     (WriterAlign.Right, Ui.Format_AlignRightAllTextIn)
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

        // 줄 간격 — 카드 단위(정렬처럼 · 2026-11-23 소유자 「서식도구에 줄 간격 조절을 새로 넣는게 맞습니다」 · WF-D19).
        bar.Children.Add(Separator());
        bar.Children.Add(LineSpacingIcon());
        bar.Children.Add(FormatButton("−", Ui.Format_TighterLineSpacingAllText, () => ApplyLineSpacing(-LineSpacingStep)));
        _lineSpacingEntry = ValueEntry(
            48,
            Ui.Format_ThisCardSLineSpacing,
            ApplyTypedLineSpacing);
        bar.Children.Add(_lineSpacingEntry);
        bar.Children.Add(FormatButton("+", Ui.Format_LooserLineSpacingAllText, () => ApplyLineSpacing(LineSpacingStep)));

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

    /// <summary>
    /// 값을 적는 칸(크기 · 줄 간격 · WF-D20 — 2026-11-23 소유자 「정확히 텍스트 입력을 해서 특정값으로 변경하는 것도 필요하겠습니다」). 누르면 칸의 고른 범위를
    /// 붙든 채 초점을 받고 글이 모두 골라진다. Enter면 <paramref name="commit"/>이 적은 글을 받아 입히고 칸으로 초점을 돌려준다. Esc나 다른 데를 누르면 적던
    /// 것은 버리고 지금 값을 다시 보인다.
    /// </summary>
    private TextBox ValueEntry(double width, string tip, Action<string> commit)
    {
        var entry = new TextBox
        {
            Width = width,
            MinWidth = 0,
            FontSize = 11,
            Padding = new Thickness(4, 2),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            AcceptsReturn = false,
            [ToolTip.TipProperty] = tip
        };

        entry.GotFocus += (_, _) => Dispatcher.UIThread.Post(entry.SelectAll, DispatcherPriority.Background);
        entry.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                string typed = entry.Text ?? string.Empty;
                UiGuard.Run(_session, Ui.Format_FormatValue, () => commit(typed));
                LeaveValueEntry();
            }
            else if (args.Key == Key.Escape)
            {
                args.Handled = true;
                LeaveValueEntry();
            }
        }, RoutingStrategies.Tunnel);

        // 다른 데로 갔다 — 적던 것은 버리고, 칸에 돌아가지 않았으면 붙든 범위를 놓는다(초점이 옮겨 간 뒤에 본다).
        entry.LostFocus += (_, _) => Dispatcher.UIThread.Post(() => UiGuard.Run(_session, Ui.Format_FormatValue, () =>
        {
            if (entry.IsFocused)
            {
                return;
            }

            RefreshFormatToolbar();

            if (!FormatToolOpen && FormatTarget() is not { Box.IsFocused: true })
            {
                ReleaseFormatSelection();
            }
        }), DispatcherPriority.Background);

        _formatControls.Add(entry);
        return entry;
    }

    /// <summary>
    /// 값 칸을 떠난다 — 글 칸이 입힐 곳이면 그 칸으로(고른 범위째), 카드만 골랐으면 하이어아키로 초점을 옮긴다(칸에 초점이 남으면 다른 카드를 골라도 칸의 값이
    /// 따라오지 않는다). 그 뒤 지금 값을 다시 보인다.
    /// </summary>
    private void LeaveValueEntry()
    {
        if (FormatTarget() is null)
        {
            Hierarchy.Focus();
        }

        RestoreFormatFocus();
        RefreshFormatToolbar();
    }

    /// <summary>적은 글에서 숫자 하나 — 「12」 · 「12pt」 · 「150%」 · 「12.5」(반올림). 숫자가 아니면 null.</summary>
    internal static int? TypedNumber(string typed)
    {
        string text = typed.Trim().TrimEnd('%').Trim();

        if (text.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2].Trim();
        }

        return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) &&
               double.IsFinite(value) && Math.Abs(value) < 100000
            ? (int)Math.Round(value)
            : null;
    }

    /// <summary>크기 칸에 적은 값 — 고른 글자 모두를 그 크기(pt)로. 서식의 크기 차는 카드의 처음 크기에 대한 차라 그 차로 바꾸고, 끝(처음 크기 −6 ~ +36)으로 당긴다.</summary>
    private void ApplyTypedSize(string typed)
    {
        if (_session is not { } session || (FormatTarget() is null && CardTargets().Count == 0))
        {
            return;
        }

        if (TypedNumber(typed) is not { } points)
        {
            session.SetStatus(string.Format(Ui.Format_IsnTSizeTypeNumber, typed.Trim()));
            return;
        }

        // 카드만 골랐다 — 카드의 기본 크기가 그 값이다(고른 답 「카드의 기본 크기」 · 일부만 키운 글자는 그 차를 지킨다 · WF-D21).
        if (FormatTarget() is not { } target)
        {
            IReadOnlyList<DialogueNode> cards = CardTargets();
            double size = Math.Clamp(points, ScriptFont.MinSize, ScriptFont.MaxSize);
            session.Editor.SetWriterBase(cards.Select(card => card.Id).ToList(), basis => basis with { Size = size });
            if (size != points)
            {
                session.SetStatus(string.Format(Ui.Format_CardSBaseSizeIs, ScriptFont.MinSize, ScriptFont.MaxSize, size));
            }
            else if (cards.Count > 1)
            {
                session.SetStatus(Plural.Format(Ui.Format_SetBaseSizeOfSelected, cards.Count, size));
            }

            return;
        }

        int cardSize = (int)Math.Round(target.Box.FontSize);
        int grow = Math.Clamp(points - cardSize, WriterMarkMath.MinGrow, WriterMarkMath.MaxGrow);

        if (cardSize + grow != points)
        {
            session.SetStatus(string.Format(Ui.Format_ThisCardSTextSize, cardSize + WriterMarkMath.MinGrow, cardSize + WriterMarkMath.MaxGrow, cardSize + grow));
        }

        ApplyFormat(new WriterStyleChange(WriterStyleKind.Size, Step: grow));
    }

    /// <summary>줄 간격 칸에 적은 값 — 그 카드의 줄 간격(%)을 그 값으로, 설정과 같은 범위(100 ~ 250%)로 당긴다.</summary>
    private void ApplyTypedLineSpacing(string typed)
    {
        if (_session is not { } session || (FormatTarget() is null && CardTargets().Count == 0))
        {
            return;
        }

        if (TypedNumber(typed) is not { } percent)
        {
            session.SetStatus(string.Format(Ui.Format_IsnTLineSpacingType, typed.Trim()));
            return;
        }

        // 카드마다 그 카드에서 가장 좁은 값 ~ 설정의 끝으로 당긴다(WF-D23) — 좁은 쪽으로 당겼으면 가장 좁은 값을, 넓은 쪽이면 끝을 말한다.
        IReadOnlyList<DialogueNode> cards = FormatTarget() is { } target
            ? session.Project.FindDialogue(target.NodeId) is { } node ? [node] : []
            : CardTargets();

        if (cards.Count == 0)
        {
            return;
        }

        bool narrowed = cards.Any(card => LineSpacingWithin(card, card.WriterBase, percent) > percent);
        session.Editor.SetWriterBase(cards.Select(card => card.Id).ToList(), (card, basis) => basis with { LineSpacing = LineSpacingWithin(card, basis, percent) });

        if (narrowed)
        {
            SayNarrowest(cards);
        }
        else if (percent > ScriptFont.MaxLineSpacing)
        {
            session.SetStatus(string.Format(Ui.Format_LineSpacingGoesUpTo, ScriptFont.MaxLineSpacing));
        }
        else if (cards.Count > 1)
        {
            session.SetStatus(Plural.Format(Ui.Format_SetLineSpacingOfSelected, cards.Count, percent));
        }
    }

    /// <summary>줄 간격 그림 — 세 줄과 그 왼쪽의 위아래 화살표(문서 도구의 그 그림).</summary>
    private static Control LineSpacingIcon() => new Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse("M2.5,1.5 V11.5 M0.5,3.5 L2.5,1.5 L4.5,3.5 M0.5,9.5 L2.5,11.5 L4.5,9.5 M7,2.5 H13 M7,6.5 H13 M7,10.5 H13"),
        Stroke = Brushes.Black,
        StrokeThickness = 1.1,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Width = 14,
        Height = 13,
        Margin = new Thickness(2, 0),
        VerticalAlignment = VerticalAlignment.Center,
        [ToolTip.TipProperty] = Ui.Format_LineSpacingAllTextIn
    };

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

    /// <summary>색 단추 하나(글자색 · 바탕색) — 차림(<see cref="WriterColorChooser"/>)과 그 플라이아웃 · 단추 밑의 색 띠(마지막에 고른 색). 시험이 들여다본다.</summary>
    internal sealed record ColorMenu(Flyout Flyout, Border Bar, WriterColorChooser Chooser);

    private readonly Dictionary<WriterStyleKind, ColorMenu> _colorMenus = new();

    /// <summary>시험이 본다 — 글자색(<see cref="WriterStyleKind.Color"/>) · 바탕색(<see cref="WriterStyleKind.Fill"/>)의 색 차림.</summary>
    internal ColorMenu ColorMenuOf(WriterStyleKind kind) => _colorMenus[kind];

    /// <summary>
    /// 이 프로젝트에 쓴 색 — 서식의 글자색(화자 이름 색도 글자색이다 · WF-D15) 또는 바탕색(화자 이름 바탕색도 · WF-D22). 색 차림의 「이 프로젝트에 쓴 색」(WF-D14).
    /// </summary>
    internal static IEnumerable<WriterRgb> ProjectColors(StoryProject project, bool fill)
    {
        IEnumerable<WriterRgb> marks = WriterMarkMath.UsedColors(project.EnumerateNodes().OfType<DialogueNode>(), fill);
        return marks.Concat(project.Speakers.Select(speaker => fill ? speaker.Fill : speaker.Color).OfType<WriterRgb>()).Distinct();
    }

    /// <summary>글자색 · 바탕색 — 단추 밑의 색 띠가 마지막에 쓴 색이고, 누르면 색 차림이 열린다(WF-D14).</summary>
    private (Button Button, Border Bar) ColorButton(string? glyph, string tip, WriterStyleKind kind)
    {
        bool fill = kind == WriterStyleKind.Fill;
        var chooser = new WriterColorChooser(fill, fill ? Ui.Format_ClearBackgroundColor : Ui.Format_ClearTextColor);
        var bar = new Border { Height = 3, Margin = new Thickness(0, 1, 0, 0), Background = WriterPalette.Brush(chooser.Last) };
        var flyout = new Flyout { Content = chooser.Content, Placement = PlacementMode.BottomEdgeAlignedLeft };
        _colorMenus[kind] = new ColorMenu(flyout, bar, chooser);

        // 고른 색을 입힌다 — 차림을 닫고(닫힌 뒤 칸에 초점과 범위가 돌아온다) 띠가 그 색이 된다.
        chooser.Picked += rgb =>
        {
            if (rgb is { } picked)
            {
                bar.Background = WriterPalette.Brush(picked);
                chooser.Last = picked;
            }

            flyout.Hide();
            ApplyFormat(new WriterStyleChange(kind, Color: rgb));
        };

        // 열 때마다 — 이 프로젝트에 쓴 색을 다시 세고(많이 쓴 차례), 직접 고르기는 마지막에 고른 색에서 시작한다.
        flyout.Opening += (_, _) =>
        {
            if (_session is { } session)
            {
                chooser.Prepare(ProjectColors(session.Project, fill));
            }
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
        UiGuard.Run(_session, Ui.Format_TextFormatting, () =>
        {
            if (FormatTarget() is null && CardTargets() is { Count: > 0 } cards)
            {
                ApplyCardFormat(cards, change);
                return;
            }

            if (FormatTarget() is not { } target || _session is not { } session)
            {
                return;
            }

            if (target.End <= target.Start)
            {
                session.SetStatus(Ui.Format_SelectTextToFormatFirst);
                return;
            }

            (string nodeId, TextBox box, int start, int end) = target;

            // 처음부터 굵은 카드(WF-D16)에서는 일부만 가늘게 할 수 없다 — 서식의 굵게는 그 위에 더하는 것이다.
            if (change.Kind == WriterStyleKind.Bold && session.Project.FindDialogue(nodeId)?.WriterBase is { Bold: true })
            {
                session.SetStatus(Ui.Format_ThisCardIsBoldFrom);
                return;
            }

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
                session.SetStatus(Ui.Format_SelectionHasNoDialogueText);
                return;
            }

            session.Editor.ApplyWriterStyle(nodeId, ranges, change);
            _formatSelection = (start, end);
        });

        // 입혔든 못 입혔든 칸에 초점과 범위를 되돌린다(붙든 범위도 놓는다 — 도구가 아직 열려 있으면 닫힐 때).
        RestoreFormatFocus();
    }

    /// <summary>
    /// 카드만 골랐을 때(여러 장도) 한 가지 모양을 입힌다(WF-D21 · 2026-11-23 소유자 — 「도구 막대 전부」) — 크기 −/+는 카드의 기본 크기를 1pt씩(고른 답
    /// 「카드의 기본 크기」 — 일부만 키운 글자는 그 차를 지킨다), 그 밖은 카드들의 글 전체에(<see cref="Vn.Authoring.Editing.ProjectEditor.ApplyWriterStyleToCards"/>).
    /// 되돌리기 한 칸이고, 여러 장이면 상태줄이 몇 장인지 말한다.
    /// </summary>
    private void ApplyCardFormat(IReadOnlyList<DialogueNode> cards, WriterStyleChange change)
    {
        if (_session is not { } session)
        {
            return;
        }

        List<string> ids = cards.Select(card => card.Id).ToList();

        if (change.Kind == WriterStyleKind.Grow)
        {
            session.Editor.SetWriterBase(ids, basis => basis with
            {
                Size = Math.Clamp(Math.Round(basis.Size) + change.Step, ScriptFont.MinSize, ScriptFont.MaxSize)
            });
        }
        else
        {
            session.Editor.ApplyWriterStyleToCards(ids, change);

            // 처음부터 굵은 카드는 가늘게 할 수 없다(WF-D16) — 굵게를 끈 뒤에도 그 카드는 굵다.
            if (change.Kind == WriterStyleKind.Bold && cards.Any(card => card.WriterBase is { Bold: true }) &&
                cards.All(card => card.Script.ActiveLines.All(line => !(card.WriterMarks.GetValueOrDefault(line.Id)?.Any(span => span.Look.Bold) ?? false))))
            {
                session.SetStatus(Ui.Format_CardsThatAreBoldFrom);
                return;
            }
        }

        if (cards.Count > 1)
        {
            session.SetStatus(Plural.Format(Ui.Format_AppliedToSelectedCardsOne, cards.Count));
        }
    }

    /// <summary>카드 글 칸의 좌우 정렬 — 고른 글자가 없어도 그 칸이면 된다. 카드만 골랐으면 그 카드들 모두(WF-D21).</summary>
    private void ApplyAlign(WriterAlign align)
    {
        UiGuard.Run(_session, Ui.Format_TextAlignment, () =>
        {
            if (FormatTarget() is { } target)
            {
                _session?.Editor.SetWriterAlign(target.NodeId, align);
                RestoreFormatFocus();
            }
            else if (CardTargets() is { Count: > 0 } cards)
            {
                _session?.Editor.SetWriterAlign(cards.Select(card => card.Id).ToList(), align);
            }

            RefreshFormatToolbar();
        });
    }

    /// <summary>
    /// 카드 글 칸의 줄 간격을 한 칸 좁히거나 넓힌다 — 그 카드의 처음 값(<see cref="DialogueNode.WriterBase"/>)을 바꾼다(WF-D19). 고른 글자가 없어도 그 칸이면
    /// 된다. 설정과 같은 범위(100 ~ 250%)로 당긴다.
    /// </summary>
    private void ApplyLineSpacing(int step)
    {
        UiGuard.Run(_session, Ui.Format_LineSpacing, () =>
        {
            if (FormatTarget() is { } target && _session?.Project.FindDialogue(target.NodeId) is { } node)
            {
                int now = CardLineSpacing(node);
                int next = LineSpacingWithin(node, node.WriterBase, now + step);

                if (next != node.WriterBase?.LineSpacing)
                {
                    _session.Editor.SetWriterLineSpacing(target.NodeId, next);
                }

                if (step < 0 && next == now)
                {
                    SayNarrowest([node]);
                }

                RestoreFormatFocus();
            }
            else if (CardTargets() is { Count: > 0 } cards)
            {
                // 카드마다 제 줄 간격(보이는 값)에서 한 칸 — 그 카드에서 가장 좁은 값에서 멈춘다(WF-D21 · WF-D23).
                bool stopped = step < 0 && cards.All(card => CardLineSpacing(card) == ScriptFont.MinLineSpacingOf(card.WriterBase, NarrowestLineOf(card)));
                _session?.Editor.SetWriterBase(cards.Select(card => card.Id).ToList(), (card, basis) => basis with
                {
                    LineSpacing = LineSpacingWithin(card, basis, ScriptFont.ShownLineSpacing(basis, NarrowestLineOf(card)) + step)
                });

                if (stopped)
                {
                    SayNarrowest(cards);
                }
            }

            RefreshFormatToolbar();
        });
    }

    /// <summary>카드의 줄 간격(%) — 보이는 값(그 카드에서 가장 좁은 값 아래로 내려가지 않는다 · <see cref="ScriptFont.ShownLineSpacing"/> · WF-D23 · WF-D24).</summary>
    private int CardLineSpacing(DialogueNode node) => ScriptFont.ShownLineSpacing(node.WriterBase, NarrowestLineOf(node));

    /// <summary>
    /// 그 카드에서 줄 간격을 <paramref name="wanted"/>(%)로 — 그 카드에서 가장 좁은 값 ~ 설정의 끝으로 당긴다(WF-D23). 가장 좁은 값은 판에 선 그 카드 글 칸의
    /// 가장 낮게 내려갈 수 있는 줄로 잰다 — 글자를 줄인 줄이 있으면 더 좁다(WF-D24).
    /// </summary>
    private int LineSpacingWithin(DialogueNode node, WriterBase? basis, int wanted) =>
        Math.Clamp(wanted, ScriptFont.MinLineSpacingOf(basis, NarrowestLineOf(node)), ScriptFont.MaxLineSpacing);

    /// <summary>
    /// 판에 선 그 카드 글 칸에서 가장 낮게 내려갈 수 있는 줄의 높이(px · <see cref="WriterTextPresenter.NarrowestLine"/>) — 줄인 글자만 든 줄이 없거나 칸이 서
    /// 있지 않으면(접힌 장면) null — 그 카드의 처음 크기로 잰다.
    /// </summary>
    private double? NarrowestLineOf(DialogueNode node) =>
        _writingBoxes.GetValueOrDefault(node.Id) is WriterTextBox { Presenter: { } presenter } ? presenter.NarrowestLine() : null;

    /// <summary>
    /// 가장 좁은 값에서 멈췄다고 말한다 — 「최소 줄 간격입니다」(2026-11-23 소유자 — 「이 카드의 글꼴, 크기에서는 줄 간격 135%가 가장 좁습니다 이 문구를,
    /// 그냥 최소 줄 간격입니다로 고쳐줘」). ⛔ 그 전에는 그 값과 까닭(숫자만 내려가고 화면은 그대로)을 말했다.
    /// </summary>
    private void SayNarrowest(IReadOnlyList<DialogueNode> cards) => _session?.SetStatus(Ui.Format_ThisIsMinimumLineSpacing);

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
    private bool FormatToolOpen =>
        _fontBox?.IsDropDownOpen == true || _formatFlyouts.Exists(flyout => flyout.IsOpen) || _sizeEntry?.IsFocused == true || _lineSpacingEntry?.IsFocused == true;

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

        if (target is null)
        {
            RefreshCardToolbar(CardTargets());
            return;
        }

        foreach (Control control in _formatControls)
        {
            control.IsEnabled = true;
        }

        WriterToolbar.SetValue(ToolTip.TipProperty, null);

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
            // 처음부터 굵은 카드(설정의 굵게로 만든 카드 · WF-D16)는 모두 굵다.
            bool cardBold = target is { } bolded && (int)bolded.Box.FontWeight >= (int)FontWeight.SemiBold;
            _boldButton!.IsChecked = All(look => look.Bold || cardBold);
            _italicButton!.IsChecked = All(look => look.Italic);
            _strikeButton!.IsChecked = All(look => look.Strike);

            List<int> grows = chosen.Select(look => look.Grow).Distinct().ToList();
            double cardSize = target?.Box.FontSize ?? ScriptFont.Size;   // 그 카드의 처음 크기(칸이 입고 있다)
            // 적는 중인 칸은 건드리지 않는다(그사이 다른 알림이 와도 적던 글이 지워지지 않게).
            if (!_sizeEntry!.IsFocused)
            {
                _sizeEntry.Text = target is null ? string.Empty : grows.Count == 1 ? $"{cardSize + grows[0]:0}" : chosen.Count == 0 ? $"{cardSize:0}" : string.Empty;
            }

            List<string?> fonts = chosen.Select(look => look.Font).Distinct().ToList();

            if (fonts is [{ } missing] && !_fontItems.Contains(missing, StringComparer.Ordinal))
            {
                _fontItems = [.. _fontItems, missing];
                _fontBox.ItemsSource = _fontItems;
            }

            _fontBox.SelectedItem = fonts.Count == 1 ? fonts[0] ?? DefaultFontItem : chosen.Count == 0 && target is not null ? DefaultFontItem : null;

            DialogueNode? card = target is { } shown ? _session?.Project.FindDialogue(shown.NodeId) : null;
            WriterAlign align = card?.WriterAlign ?? WriterAlign.Left;

            foreach ((WriterAlign key, ToggleButton button) in _alignButtons)
            {
                button.IsChecked = target is not null && key == align;
            }

            if (!_lineSpacingEntry!.IsFocused)
            {
                _lineSpacingEntry.Text = card is null ? string.Empty : $"{CardLineSpacing(card)}%";
            }
        }
        finally
        {
            _syncingToolbar = false;
        }
    }

    /// <summary>
    /// 카드만 골랐을 때의 도구 막대 모습(WF-D21) — 고른 카드들의 글 전체가 모두 그 모양이면 B · I · S가 눌려 있고, 기본 크기 · 줄 간격 · 정렬 · 글꼴이 하나면
    /// 그것을 보인다(섞였으면 빈 칸). 카드가 없으면 모두 꺼진다.
    /// </summary>
    private void RefreshCardToolbar(IReadOnlyList<DialogueNode> cards)
    {
        foreach (Control control in _formatControls)
        {
            control.IsEnabled = cards.Count > 0;
        }

        WriterToolbar.SetValue(ToolTip.TipProperty, cards.Count == 0 ? Ui.Format_TurnsOnWhenYouSelect : null);

        bool any = false;
        bool bold = true;
        bool italic = true;
        bool strike = true;
        var fonts = new HashSet<string?>();

        foreach (DialogueNode card in cards)
        {
            foreach (ScriptLine line in card.Script.ActiveLines)
            {
                int length = card.Script.Text(line.Id).Text.Length;

                foreach (WriterLook look in WriterMarkMath.Expand(card.WriterMarks.GetValueOrDefault(line.Id), length))
                {
                    any = true;
                    bold &= look.Bold || card.WriterBase is { Bold: true };
                    italic &= look.Italic;
                    strike &= look.Strike;
                    fonts.Add(look.Font);
                }
            }
        }

        _syncingToolbar = true;

        try
        {
            _boldButton!.IsChecked = any && bold;
            _italicButton!.IsChecked = any && italic;
            _strikeButton!.IsChecked = any && strike;

            List<double> sizes = cards.Select(CardSize).Distinct().ToList();

            if (!_sizeEntry!.IsFocused)
            {
                _sizeEntry.Text = sizes is [var size] ? $"{size:0}" : string.Empty;
            }

            if (fonts is { Count: 1 } && fonts.First() is { } missing && !_fontItems.Contains(missing, StringComparer.Ordinal))
            {
                _fontItems = [.. _fontItems, missing];
                _fontBox!.ItemsSource = _fontItems;
            }

            _fontBox!.SelectedItem = cards.Count == 0 ? null : fonts.Count == 1 ? fonts.First() ?? DefaultFontItem : fonts.Count == 0 ? DefaultFontItem : null;

            List<WriterAlign> aligns = cards.Select(card => card.WriterAlign).Distinct().ToList();

            foreach ((WriterAlign key, ToggleButton button) in _alignButtons)
            {
                button.IsChecked = aligns is [var align] && key == align;
            }

            List<int> spacings = cards.Select(CardLineSpacing).Distinct().ToList();

            if (!_lineSpacingEntry!.IsFocused)
            {
                _lineSpacingEntry.Text = spacings is [var spacing] ? $"{spacing}%" : string.Empty;
            }
        }
        finally
        {
            _syncingToolbar = false;
        }
    }

    /// <summary>카드의 기본 크기(pt) — 적힌 처음 크기(설정의 범위로 당긴다). 안 적힌 카드는 이 PC의 설정.</summary>
    private static double CardSize(DialogueNode node) =>
        Math.Clamp(Math.Round(node.WriterBase?.Size ?? ScriptFont.Size), ScriptFont.MinSize, ScriptFont.MaxSize);

    /// <summary>
    /// 글 칸을 고르지 않았을 때 서식 도구가 입힐 카드들(WF-D21 — 2026-11-23 소유자 「글 칸을 정확히 눌러야 활성화 되다보니 현재는 조정이 사실상 불가능」) —
    /// 하이어아키에서 고른 줄들에 든 카드(<see cref="PickedCards"/>). 글 칸이 입힐 곳이면(<see cref="FormatTarget"/>) 빈 목록이다.
    /// </summary>
    private IReadOnlyList<DialogueNode> CardTargets() => FormatTarget() is null ? PickedCards() : [];

    /// <summary>
    /// 하이어아키에서 고른 줄들(여러 줄도 · <see cref="StoryHierarchy.Selections"/>)에 든 카드 — 에피소드 줄은 그 카드, 장면 줄은 그 장면의 에피소드 카드들, 챕터
    /// 줄은 그 챕터의 에피소드 카드들, 프로젝트 줄은 모든 에피소드 카드, 분기 카드 줄은 그 분기 카드. 판에서 카드를 고르면 하이어아키가 그 줄을 비추므로 같다.
    /// 겹치면 한 번, 하이어아키 차례(챕터 · 에피소드 차례)다. 나중의 한꺼번에 바꾸기(빈 선택지 숨기기 · 글 서식)가 함께 쓴다(2026-11-23 소유자).
    /// </summary>
    internal IReadOnlyList<DialogueNode> PickedCards()
    {
        if (_session is not { } session)
        {
            return [];
        }

        var cards = new List<DialogueNode>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(DialogueNode? card)
        {
            if (card is not null && seen.Add(card.Id))
            {
                cards.Add(card);
            }
        }

        void AddEpisodes(string chapterId, string? sceneId)
        {
            if (session.Editor.FindChapter(chapterId) is not { } chapter)
            {
                return;
            }

            foreach (ChapterEpisode episode in chapter.Episodes)
            {
                if (sceneId is null || string.Equals(episode.SceneId, sceneId, StringComparison.Ordinal))
                {
                    Add(EpisodeNaming.CardFor(session.Project, chapterId, episode.EpisodeId));
                }
            }
        }

        IReadOnlyList<HierarchyPick> picks = Hierarchy.Selections;

        if (picks.Count == 0 && session.SelectedNode is DialogueNode selected)
        {
            Add(selected);
        }

        foreach (HierarchyPick pick in picks)
        {
            switch (pick.Kind)
            {
                case HierarchyKind.Project:
                    foreach (ChapterDocument chapter in session.Project.Chapters)
                    {
                        AddEpisodes(chapter.ChapterId, sceneId: null);
                    }

                    break;

                case HierarchyKind.Chapter when pick.ChapterId is { } chapterId:
                    AddEpisodes(chapterId, sceneId: null);
                    break;

                case HierarchyKind.Scene when pick.ChapterId is { } chapterId && pick.SceneId is { } sceneId:
                    AddEpisodes(chapterId, sceneId);
                    break;

                case HierarchyKind.Episode when pick.ChapterId is { } chapterId && pick.EpisodeId is { } episodeId:
                    Add(EpisodeNaming.CardFor(session.Project, chapterId, episodeId));
                    break;

                case HierarchyKind.Branch when pick.NodeId is { } nodeId:
                    Add(session.Project.FindDialogue(nodeId));
                    break;
            }
        }

        return cards;
    }

    /// <summary>시험이 본다 — 도구 막대의 줄 간격 글(「140%」 · 입힐 칸이 없으면 빈 글).</summary>
    internal string? FormatLineSpacingText => _lineSpacingEntry?.Text;

    /// <summary>시험이 쓴다 — 도구 막대의 크기 칸 · 줄 간격 칸.</summary>
    internal (TextBox? Size, TextBox? LineSpacing) FormatValueEntries => (_sizeEntry, _lineSpacingEntry);

    /// <summary>시험이 본다 — 도구 막대의 단추 상태(굵게 · 기울임 · 취소선 · 크기 글).</summary>
    internal (bool? Bold, bool? Italic, bool? Strike, string? Size, string? Font) FormatToolbarState =>
        (_boldButton?.IsChecked, _italicButton?.IsChecked, _strikeButton?.IsChecked, _sizeEntry?.Text, _fontBox?.SelectedItem as string);
}
