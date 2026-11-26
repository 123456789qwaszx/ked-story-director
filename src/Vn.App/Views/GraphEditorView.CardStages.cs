using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using Vn.App.Theming;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace Vn.App.Views;

/// <summary>
/// <b>줄인 판의 카드 단계</b>(design/board-zoom.md 3절 · 2026-11-26 소유자 — 「에피소드의 내용이 50%에서 사라지는 건 너무 과하네요. 50% 정도면 아마
/// 실제로 작가들이 에피소드의 내용을 보면서 글을 작성하려고 할 수도 있는 수준」). 카드마다 <b>화면 글자 크기</b>(그 카드의 처음 글자 크기 × 배율)로 셋 중
/// 하나에 선다 — 배율 하나로 판 전체를 끊지 않는다(글자를 키운 카드는 더 오래 글이 남는다):
///
/// <list type="bullet">
/// <item><b>글</b>(<see cref="TextFloorPx"/> 이상) — 지금 그대로.</item>
/// <item><b>글줄</b> — 글 칸 자리에 줄마다 막대(줄 수 · 길이 · 화자가 남는다), 머리 띠에 읽히는 크기의 제목, 선택지는 점과 짧은 막대.</item>
/// <item><b>덩어리</b>(<see cref="BlockBelowPx"/> 미만이거나 카드가 화면에서 <see cref="BlockHeightPx"/>보다 낮다) — 카드 전체를 종류의 색으로 채운다.</item>
/// </list>
///
/// 문턱은 오갈 때 다르다(<see cref="StageHysteresis"/>) — 문턱 근처에서 휠을 굴려도 깜빡이지 않는다. <b>카드의 치수 · 자리는 그대로다</b> — 글 칸 · 머리 줄은
/// 투명해질 뿐이고, 겹(<see cref="CardOverview"/>)은 크기를 내놓지 않는다. 글을 보이지 않는 카드는 누르면 고르고 끈다(보이지 않는 글 칸에 쓰지 않는다).
/// ⛔ 2026-11-26 1차 구현(`135a508b`)은 배율 50% · 25%로 판 전체를 한 번에 끊었다 — 50%에서 글이 사라지고 40%에서 카드가 빈 흰 상자였다.
///
/// 점 · 화살촉도 화면에서 <see cref="MarkFloorPx"/>보다 작아지지 않는다(<see cref="Marked{T}"/>) — 키우기만 하고 자리는 그대로다.
/// </summary>
public partial class GraphEditorView
{
    /// <summary>카드의 단계.</summary>
    internal enum CardStage
    {
        /// <summary>글 — 지금 그대로.</summary>
        Text,

        /// <summary>글줄 — 줄마다 막대 · 읽히는 크기의 제목.</summary>
        Lines,

        /// <summary>덩어리 — 종류의 색으로 채운다.</summary>
        Block
    }

    /// <summary>
    /// 화면 글자가 이 크기(px) 이상이면 글 단계다 — 새 카드(11)는 배율 24%까지 글이다(2026-11-26 소유자 — 「24%까지도 글은 남아있는 게 좋겠음. 그 아래로
    /// 내려가야 막대로 변경」 · ⛔ 처음에는 4px — 36%였다). 11 × 0.24 = 2.64라 소수점 어긋남에도 24%가 글이 되게 2.6으로 둔다.
    /// </summary>
    internal const double TextFloorPx = 2.6;

    /// <summary>화면 글자가 이 크기(px) 미만이면 덩어리 단계다.</summary>
    internal const double BlockBelowPx = 1.5;

    /// <summary>카드가 화면에서 이보다 낮으면(px) 글자 크기와 상관없이 덩어리 단계다.</summary>
    internal const double BlockHeightPx = 20;

    /// <summary>위 단계로 돌아갈 때는 문턱의 이 배수를 넘어야 한다 — 문턱 근처에서 깜빡이지 않게.</summary>
    internal const double StageHysteresis = 1.1;

    /// <summary>글을 보이지 않는 단계의 카드 제목이 화면에서 이보다 작아지지 않는다(px).</summary>
    internal const double CardTitleMinPx = 12;

    /// <summary>점 · 화살촉이 화면에서 이보다 작아지지 않는다(지름 px).</summary>
    internal const double MarkFloorPx = 4;

    /// <summary>덩어리 카드에 제목을 쓰는 가장 작은 화면 크기(px).</summary>
    private const double BlockTitleWidthPx = 48;

    private const double BlockTitleHeightPx = 18;

    /// <summary>글을 보이지 않는 단계의 겹(<see cref="CardOverview"/>)의 Tag — 시험이 찾는다.</summary>
    internal const string CardOverviewTag = "card-overview";

    /// <summary>글줄 막대(본문)의 Tag.</summary>
    internal const string CardBarsTag = "card-bars";

    /// <summary>글줄 막대(화자)의 Tag.</summary>
    internal const string CardSpeakerBarsTag = "card-bars-speaker";

    /// <summary>
    /// 다음 단계 — 지금 단계에서 화면 글자 크기(px)와 카드의 화면 높이(px)로. 아래로는 문턱에서, 위로는 문턱 × <see cref="StageHysteresis"/>에서 바뀐다.
    /// </summary>
    internal static CardStage NextStage(CardStage current, double screenFont, double screenHeight)
    {
        bool block = screenFont < BlockBelowPx || screenHeight < BlockHeightPx;
        bool unblock = screenFont >= BlockBelowPx * StageHysteresis && screenHeight >= BlockHeightPx * StageHysteresis;
        bool belowText = screenFont < TextFloorPx;
        bool aboveText = screenFont >= TextFloorPx * StageHysteresis;

        return current switch
        {
            CardStage.Text => block ? CardStage.Block : belowText ? CardStage.Lines : CardStage.Text,
            CardStage.Lines => block ? CardStage.Block : aboveText ? CardStage.Text : CardStage.Lines,
            _ => !unblock ? CardStage.Block : aboveText ? CardStage.Text : CardStage.Lines
        };
    }

    /// <summary>그 카드의 처음 글자 크기(판 단위) — 단계를 재는 잣대. 시험이 본다.</summary>
    internal double CardBaseFont(string nodeId) =>
        _session?.Project.FindDialogue(nodeId) is { } node ? CardSize(node) : ScriptFont.Size;

    /// <summary>그 카드의 지금 단계 — 시험이 본다. 판에 없는 카드는 글 단계로 친다.</summary>
    internal CardStage CardStageOf(string nodeId) => FindCard(nodeId)?.Stage ?? CardStage.Text;

    private CardStage StageFor(NodeCard card, CardStage current) =>
        NextStage(current, CardBaseFont(card.NodeId) * _zoom, CardHeightOf(card) * _zoom);

    /// <summary>지은 카드를 제 단계로 세운다 — 글 단계에서 출발한다(새로 지은 카드는 글 그대로다).</summary>
    private void InitCardStage(NodeCard card)
    {
        card.Stage = StageFor(card, CardStage.Text);

        if (card.Stage != CardStage.Text)
        {
            ApplyCardStage(card);
        }
    }

    /// <summary>
    /// 배율이 바뀌었다 — 카드마다 단계를 다시 재고(바뀐 카드만 다시 칠한다 · 글을 보이지 않는 카드는 제목 크기를 맞춘다), 선택지 칸 · 선 · 점 · 카드 테두리 ·
    /// 이름표를 지금 배율에 맞춘다.
    /// </summary>
    private void ApplyZoomDrawing()
    {
        foreach (NodeCard card in _cards)
        {
            CardStage next = StageFor(card, card.Stage);

            if (next != card.Stage)
            {
                card.Stage = next;
                ApplyCardStage(card);
            }
            else if (next != CardStage.Text)
            {
                SizeOverview(card);
            }
        }

        foreach (Panel chip in _railVisuals.OfType<Panel>().Where(visual => visual.Classes.Contains(SlotChipClass)))
        {
            ApplySlotStage(chip);
        }

        FloorLines();
        FloorMarks();

        foreach (NodeCard card in _cards)
        {
            PaintCardEdge(card, CardIsPicked(card.NodeId));
        }

        PlaceFrameLabels();
    }

    /// <summary>카드 하나를 제 단계로 칠한다 — 글 칸 · 머리 줄 · 겹 · 분기 점 · 테두리.</summary>
    private void ApplyCardStage(NodeCard card)
    {
        bool text = card.Stage == CardStage.Text;

        if (card.WritingBox is { } box)
        {
            box.Opacity = text ? 1 : 0;
            box.IsHitTestVisible = text;
        }

        card.TitleRow.Opacity = text ? 1 : 0;
        card.TitleRow.IsHitTestVisible = text;

        if (text)
        {
            if (card.Overview is { } shown)
            {
                shown.Root.IsVisible = false;
            }
        }
        else if (EnsureOverview(card) is { } overview)
        {
            overview.Root.IsVisible = true;
            overview.Lines.IsVisible = card.Stage == CardStage.Lines;
            overview.Fill.IsVisible = card.Stage == CardStage.Block;
            overview.BlockRoom.IsVisible = card.Stage == CardStage.Block;
            SizeOverview(card);

            if (card.Stage == CardStage.Lines)
            {
                card.BarsDirty = true;
                RefreshBarsIfDirty(card);
            }
        }

        PlaceLineKnobs(card);   // 덩어리 카드는 분기 점을 걷는다
        PaintCardEdge(card, CardIsPicked(card.NodeId));   // 그림자는 글을 보이는 카드에만
    }

    // ── 겹 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 글을 보이지 않는 단계에서 카드 위에 얹는 겹. 카드의 안 여백을 거슬러 테두리 안쪽까지 닿고(머리 띠와 같다), 누름을 받지 않으며, <b>크기를 내놓지
    /// 않는다</b>(캔버스와 크기 없는 면뿐이다 — 카드의 치수가 그대로다). 글줄 단계의 머리(표 · 제목 · 분기 조건의 색 점)와 막대, 덩어리 단계의 면과 제목.
    /// </summary>
    private sealed class CardOverview
    {
        public required Border Root { get; init; }

        /// <summary>글줄 단계의 한 겹 — 막대 위에 머리가 얹힌다.</summary>
        public required Canvas Lines { get; init; }

        public required Border Header { get; init; }

        public required TextBlock Icon { get; init; }

        public required TextBlock Title { get; init; }

        /// <summary>분기로 다녀오는 카드의 조건 — 띠 오른쪽의 색 점(설계 9절 7번).</summary>
        public Ellipse? Condition { get; init; }

        public required ShapePath Body { get; init; }

        public required ShapePath Speaker { get; init; }

        /// <summary>덩어리 단계의 면.</summary>
        public required Border Fill { get; init; }

        public required Canvas BlockRoom { get; init; }

        public required TextBlock BlockTitle { get; init; }
    }

    /// <summary>겹을 처음 필요할 때 짓는다. 카드 안의 한 겹(<c>Frame</c>)이 없으면 짓지 않는다.</summary>
    private static CardOverview? EnsureOverview(NodeCard card)
    {
        if (card.Overview is { } built)
        {
            return built;
        }

        if (card.Frame is not { } frame)
        {
            return null;
        }

        Color ink = card.WritingBox?.Foreground is ISolidColorBrush { Color: var boxInk } ? boxInk : Colors.Black;

        var body = new ShapePath { Fill = ink.Brush(70), IsHitTestVisible = false, Tag = CardBarsTag };
        var speaker = new ShapePath { Fill = ink.Brush(150), IsHitTestVisible = false, Tag = CardSpeakerBarsTag };

        var icon = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), IsHitTestVisible = false };

        if (card.TitleRow.Children.OfType<TextBlock>().FirstOrDefault(text => Grid.GetColumn(text) == CardTitleRow.IconColumn) is { } shownIcon)
        {
            icon.Bind(TextBlock.TextProperty, shownIcon.GetObservable(TextBlock.TextProperty));
            icon.Bind(TextBlock.ForegroundProperty, shownIcon.GetObservable(TextBlock.ForegroundProperty));
        }

        var title = new TextBlock
        {
            Classes = { "card-title" },   // 테마의 제목 글꼴
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        title.Bind(TextBlock.TextProperty, card.Title.GetObservable(TextBlock.TextProperty));
        title.Bind(TextBlock.ForegroundProperty, card.Title.GetObservable(TextBlock.ForegroundProperty));

        Ellipse? condition = card.BranchPalette is int palette
            ? new Ellipse { Fill = BranchPalette.Accent(palette), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false }
            : null;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(title, 1);
        row.Children.Add(icon);
        row.Children.Add(title);

        if (condition is not null)
        {
            Grid.SetColumn(condition, 2);
            row.Children.Add(condition);
        }

        // 머리는 카드의 머리 띠 색이다(고르면 띠와 함께 바뀐다) — 띠가 없는 테마는 카드의 바탕.
        var header = new Border { Child = row, IsHitTestVisible = false };

        if (card.Band is { } band)
        {
            header.Bind(Border.BackgroundProperty, band.GetObservable(Border.BackgroundProperty));
        }
        else
        {
            header.Background = card.Style.Background.Brush();
        }

        var lines = new Canvas { IsHitTestVisible = false, Children = { body, speaker, header } };

        // 덩어리 — 종류의 색(테마의 Block… — 장면 바탕과 대비 3:1 이상 · 기본 테마는 종의 색).
        var fill = new Border { Background = card.Style.Block.Brush(), IsHitTestVisible = false };
        var blockTitle = new TextBlock
        {
            Classes = { "card-title" },
            FontWeight = FontWeight.SemiBold,
            Foreground = Skin.OnAccent.Brush(),
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false
        };
        blockTitle.Bind(TextBlock.TextProperty, card.Title.GetObservable(TextBlock.TextProperty));
        var blockRoom = new Canvas { IsHitTestVisible = false, Children = { blockTitle } };

        var root = new Border
        {
            Margin = new Thickness(-CardPadding),
            CornerRadius = new CornerRadius(Math.Max(0, card.Style.Radius - 1)),
            ClipToBounds = true,
            IsHitTestVisible = false,
            IsVisible = false,
            Tag = CardOverviewTag,
            Child = new Panel { Children = { fill, blockRoom, lines } }
        };

        frame.Children.Add(root);
        card.Overview = new CardOverview
        {
            Root = root,
            Lines = lines,
            Header = header,
            Icon = icon,
            Title = title,
            Condition = condition,
            Body = body,
            Speaker = speaker,
            Fill = fill,
            BlockRoom = blockRoom,
            BlockTitle = blockTitle
        };

        return card.Overview;
    }

    /// <summary>
    /// 겹의 크기를 지금 배율에 맞춘다 — 글줄 단계는 제목이 화면에서 <see cref="CardTitleMinPx"/> 이상이고 머리 띠가 그 제목에 맞춰 자란다(막대는 띠 아래로
    /// 숨는다), 덩어리 단계는 카드가 충분히 크면 가운데에 제목을 쓴다.
    /// </summary>
    private void SizeOverview(NodeCard card)
    {
        if (card.Overview is not { } overview)
        {
            return;
        }

        double zoom = Math.Max(_zoom, 0.01);
        double border = card.Visual.BorderThickness.Left;
        double width = Math.Max(0, card.Visual.Width - (border * 2));
        double height = Math.Max(0, CardHeightOf(card) - (border * 2));

        if (card.Stage == CardStage.Lines)
        {
            double font = Math.Max(card.Title.FontSize, CardTitleMinPx / zoom);
            double pad = Math.Max(CardPadding, 4 / zoom);
            overview.Title.FontSize = font;
            overview.Icon.FontSize = font * 0.8;
            overview.Header.Width = width;
            overview.Header.Height = Math.Min(height, Math.Max(CardPadding + TitleHeight + (HeaderGap / 2), (font * 1.35) + pad));
            overview.Header.Padding = new Thickness(pad, 0);

            if (overview.Condition is { } condition)
            {
                condition.Width = condition.Height = 8 / zoom;
                condition.Margin = new Thickness(4 / zoom, 0, 0, 0);
            }
        }
        else if (card.Stage == CardStage.Block)
        {
            TextBlock title = overview.BlockTitle;
            bool roomy = width * zoom >= BlockTitleWidthPx && height * zoom >= BlockTitleHeightPx;
            title.IsVisible = roomy;

            if (roomy)
            {
                title.FontSize = CardTitleMinPx / zoom;
                title.MaxWidth = Math.Max(0, width - (8 / zoom));
                title.Measure(Size.Infinity);
                Canvas.SetLeft(title, (width - title.DesiredSize.Width) / 2);
                Canvas.SetTop(title, (height - title.DesiredSize.Height) / 2);
            }
        }
    }

    /// <summary>화자 머리 — 줄 처음의 「이름:」.</summary>
    private static readonly Regex SpeakerPrefix = new(@"^\s*[^:\s][^:\n]{0,24}:", RegexOptions.CultureInvariant);

    /// <summary>글줄 막대를 다시 지어야 하면 짓는다 — 글 칸이 배치를 마친 뒤에(그래야 줄의 자리를 안다).</summary>
    private void RefreshBarsIfDirty(NodeCard card)
    {
        if (!card.BarsDirty || card.Stage != CardStage.Lines || card.Overview is not { } overview)
        {
            return;
        }

        if (card.WritingBox is not { } box ||
            box.FindDescendantOfType<TextPresenter>() is not { } presenter ||
            box.Bounds.Height <= 0 ||
            presenter.TranslatePoint(default, card.Visual) is not { } origin ||
            box.TranslatePoint(default, card.Visual) is not { } boxAt)
        {
            return;   // 아직 배치 전이다 — 다음 배치 뒤에 다시 온다
        }

        card.BarsDirty = false;

        // 카드 좌표 → 겹 좌표(겹은 테두리 안쪽에서 시작한다).
        double border = card.Visual.BorderThickness.Left;
        var shift = new Vector(-border, -border);
        Point start = origin + shift;
        string text = box.Text ?? string.Empty;
        TextLayout layout = presenter.TextLayout;

        var bodyGeometry = new StreamGeometry();
        var speakerGeometry = new StreamGeometry();

        using (StreamGeometryContext bodyPen = bodyGeometry.Open())
        using (StreamGeometryContext speakerPen = speakerGeometry.Open())
        {
            double y = 0;

            foreach (TextLine line in layout.TextLines)
            {
                double lineHeight = line.Height;

                if (line.Width > 0.5)
                {
                    double bar = lineHeight * 0.5;
                    double top = start.Y + y + ((lineHeight - bar) / 2);
                    double left = start.X + line.Start;
                    double right = left + line.Width;
                    double split = left;
                    int first = Math.Clamp(line.FirstTextSourceIndex, 0, text.Length);

                    // 대사의 첫 줄이면 화자 머리(「이름:」)를 진하게 — 누가 몇 줄 말하는지가 남는다.
                    if (first == 0 || text[first - 1] == '\n')
                    {
                        int length = Math.Clamp(line.Length, 0, text.Length - first);
                        Match speaker = SpeakerPrefix.Match(text.Substring(first, length));

                        if (speaker.Success)
                        {
                            split = Math.Clamp(start.X + layout.HitTestTextPosition(first + speaker.Length).X, left, right);
                            AddBar(speakerPen, left, top, split - left, bar);
                            split += bar * 0.6;   // 화자와 대사 사이의 틈
                        }
                    }

                    AddBar(bodyPen, split, top, right - split, bar);
                }

                y += lineHeight;
            }
        }

        // 글 칸 밖(스크롤된 줄)은 그리지 않는다.
        var clip = new RectangleGeometry(new Rect(boxAt + shift, box.Bounds.Size));
        overview.Body.Data = bodyGeometry;
        overview.Body.Clip = clip;
        overview.Speaker.Data = speakerGeometry;
        overview.Speaker.Clip = clip;
    }

    private static void AddBar(StreamGeometryContext pen, double x, double y, double width, double height)
    {
        if (width <= 0.5)
        {
            return;
        }

        pen.BeginFigure(new Point(x, y), isFilled: true);
        pen.LineTo(new Point(x + width, y));
        pen.LineTo(new Point(x + width, y + height));
        pen.LineTo(new Point(x, y + height));
        pen.EndFigure(isClosed: true);
    }

    // ── 선택지 칸 ───────────────────────────────────────────────────────────

    /// <summary>선택지 칸(점 + 문구 한 줄)이 매달린 카드 — 칸은 그 카드의 단계를 따른다.</summary>
    private static readonly AttachedProperty<string?> SlotOwnerProperty =
        AvaloniaProperty.RegisterAttached<GraphEditorView, Control, string?>("SlotOwner");

    /// <summary>줄인 판에서 바꾼 선택지 칸의 한 조각과 그 전의 모양 — 다시 글 단계가 되면 돌려준다.</summary>
    private sealed record SlotPartLook(double Opacity, bool HitTest, IBrush? Foreground, IBrush? Background);

    private readonly ConditionalWeakTable<Control, SlotPartLook> _slotLooks = new();

    /// <summary>
    /// 선택지 칸 하나를 그 카드의 단계로 — 글줄이면 문구가 그 길이만큼의 막대가 되고 빈 칸의 [+]가 걷힌다(「문구를 적어 주세요」는 빨간 작은 칸 그대로 —
    /// 빠진 것이 보인다), 덩어리면 점까지 걷혀 길(선)만 남는다. 칸의 폭은 그대로다(테두리 · 길의 셈이 칸의 폭을 쓴다). 적고 있는 칸은 건드리지 않는다.
    /// </summary>
    private void ApplySlotStage(Panel chip)
    {
        CardStage stage = chip.GetValue(SlotOwnerProperty) is { } owner ? CardStageOf(owner) : CardStage.Text;

        foreach (Control part in chip.Children)
        {
            if (part is Ellipse knob)
            {
                knob.Opacity = stage == CardStage.Block ? 0 : 1;
                knob.IsHitTestVisible = stage != CardStage.Block;
                ApplyMark(knob);
                continue;
            }

            if (part is TextBox || part.IsKeyboardFocusWithin)
            {
                continue;
            }

            if (stage == CardStage.Text)
            {
                if (_slotLooks.TryGetValue(part, out SlotPartLook? look))
                {
                    part.Opacity = look.Opacity;
                    part.IsHitTestVisible = look.HitTest;

                    if (part is TextBlock words)
                    {
                        words.Foreground = look.Foreground;
                        words.Background = look.Background;
                    }

                    _slotLooks.Remove(part);
                }

                continue;
            }

            if (!_slotLooks.TryGetValue(part, out SlotPartLook? was))
            {
                was = new SlotPartLook(part.Opacity, part.IsHitTestVisible, (part as TextBlock)?.Foreground, (part as TextBlock)?.Background);
                _slotLooks.Add(part, was);
            }

            part.IsHitTestVisible = false;

            if (stage == CardStage.Block || Equals(part.Tag, SlotPlaceholderTag))
            {
                part.Opacity = 0;   // 덩어리 — 다 걷는다 · 빈 칸의 [+] — 글줄에서도 걷는다
                continue;
            }

            part.Opacity = was.Opacity;

            // 문구 — 그 길이만큼의 막대(글자색을 옅게 깐 바탕 · 글자는 투명).
            if (part is TextBlock label && was.Foreground is ISolidColorBrush { Color: var color })
            {
                label.Foreground = Brushes.Transparent;
                label.Background = color.Brush(90);
            }
        }
    }

    // ── 점 · 화살촉 ─────────────────────────────────────────────────────────

    /// <summary>점 · 화살촉의 판 크기(지름) — 화면에서 <see cref="MarkFloorPx"/>보다 작아지지 않게 키운다. NaN이면 키우지 않는다.</summary>
    private static readonly AttachedProperty<double> MarkSizeProperty =
        AvaloniaProperty.RegisterAttached<GraphEditorView, Control, double>("MarkSize", double.NaN);

    /// <summary>키우는 축 — 점은 가운데, 화살촉은 끝.</summary>
    private static readonly AttachedProperty<RelativePoint> MarkOriginProperty =
        AvaloniaProperty.RegisterAttached<GraphEditorView, Control, RelativePoint>("MarkOrigin", RelativePoint.Center);

    /// <summary>점 · 화살촉에 판 크기를 적는다 — 판에 붙을 때 · 배율이 바뀔 때 키움을 맞춘다(<see cref="ApplyMark"/>).</summary>
    private static T Marked<T>(T control, double size, RelativePoint? origin = null) where T : Control
    {
        control.SetValue(MarkSizeProperty, size);
        control.SetValue(MarkOriginProperty, origin ?? RelativePoint.Center);
        return control;
    }

    /// <summary>점 · 화살촉의 지금 배수 — 화면 바닥에 모자라면 키운다. 포인터를 올린 선택지 점은 1.5배 더(<see cref="HoverKnob"/>).</summary>
    private double MarkScale(Control control)
    {
        double size = control.GetValue(MarkSizeProperty);
        double scale = double.IsNaN(size) || size <= 0 ? 1 : Math.Max(1, MarkFloorPx / (size * Math.Max(_zoom, 0.01)));
        return ReferenceEquals(control, _hoveredKnob) ? scale * 1.5 : scale;
    }

    /// <summary>점 · 화살촉의 키움을 지금 배율에 맞춘다 — 그리기만 키운다(자리 · 판의 셈은 그대로). 철도를 따라 옮기는 중이면 그 옮김과 겹친다.</summary>
    private void ApplyMark(Control control)
    {
        if (double.IsNaN(control.GetValue(MarkSizeProperty)))
        {
            return;
        }

        Vector follow = control.RenderTransform is TransformGroup group && group.Children.OfType<TranslateTransform>().FirstOrDefault() is { } move
            ? new Vector(move.X, move.Y)
            : control.RenderTransform is TranslateTransform alone ? new Vector(alone.X, alone.Y) : default;
        SetMarkTransform(control, follow);
    }

    /// <summary>판에 붙은 점 · 화살촉 모두를 지금 배율에 맞춘다.</summary>
    private void FloorMarks()
    {
        foreach (Control control in GraphCanvas.Children)
        {
            ApplyMark(control);
        }
    }

    /// <summary>철도를 따라 옮기는 그림(<see cref="FollowRails"/>) — 점 · 화살촉이면 키움과 함께 건다.</summary>
    private void FollowOffset(Control visual, Vector by)
    {
        if (double.IsNaN(visual.GetValue(MarkSizeProperty)))
        {
            visual.RenderTransform = by == default ? null : new TranslateTransform(by.X, by.Y);
            return;
        }

        SetMarkTransform(visual, by);
    }

    private void SetMarkTransform(Control control, Vector follow)
    {
        double scale = MarkScale(control);
        bool grown = Math.Abs(scale - 1) > 0.0001;
        control.RenderTransformOrigin = control.GetValue(MarkOriginProperty);

        if (!grown)
        {
            control.RenderTransform = follow == default ? null : new TranslateTransform(follow.X, follow.Y);
        }
        else if (follow == default)
        {
            control.RenderTransform = new ScaleTransform(scale, scale);
        }
        else
        {
            control.RenderTransform = new TransformGroup
            {
                Children = { new ScaleTransform(scale, scale), new TranslateTransform(follow.X, follow.Y) }
            };
        }
    }
}
