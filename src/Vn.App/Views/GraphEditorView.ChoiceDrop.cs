using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Vn.App.Theming;
using Vn.Authoring.Chapters;
using Vn.Authoring.Graph;
using Vn.Authoring.Text;
using Vn.App.Strings;

namespace Vn.App.Views;

/// <summary>
/// <b>선택지 점을 끄는 동안 「놓으면 무엇이 되는지」를 미리 보인다</b> (2026-11-22 소유자 — 「다른 에피소드 노드에 끌었을 때 동작이 있다는 사실을
/// 알기 어렵다 · 같은 챕터라는 조건도 있다 · 이어진 선택지를 빈 곳에 끌면 끊긴다는 것도 해 보기 전까지 모른다」 · usability UX36).
///
/// 끄는 동안 포인터 아래를 판정해(<see cref="JudgeChoiceDrop"/>) 세 곳에 같은 말을 한다 — ① 포인터 옆 꼬리표(짧게 · 색), ② 끄는 선의 색과 모양,
/// ③ 아래 띠(까닭까지). 놓을 카드에는 테두리가 선다. 판정은 놓았을 때의 처리(<c>JoinChoice</c> · <c>Unjoin</c>)와 <b>같은 규칙</b>을 같은 차례로 본다 —
/// 미리 보인 것과 놓은 결과가 갈리면 이 기능은 거짓말이 된다. 끌기 전에는 점에 포인터를 올리면 점이 커지고 꼬리표가 「끌 수 있다」를 말한다.
///
/// <b>장면 입구를 둘로 만드는 잇기는 받지 않는다</b>(같은 날 소유자 — 「검증 오류가 나올 건데 이을 수 있다는 게 이상하다」). 이미 시작점이 있는 다른
/// 장면의 시작점 아닌 카드에 놓으면 못 이음으로 판정하고, 놓아도 잇지 않는다(<see cref="SceneEntryRefusal"/> — 판정과 놓기가 같은 것을 부른다).
/// 그 장면의 시작점, 아직 밖에서 들어오는 길이 없는 장면, 같은 장면 안의 카드는 그대로 잇는다.
/// </summary>
public partial class GraphEditorView
{
    /// <summary>놓으면 일어날 일의 종류.</summary>
    internal enum ChoiceDropKind
    {
        /// <summary>새로 잇는다(안 이은 칸).</summary>
        Join,

        /// <summary>이어진 칸의 도착을 그 카드로 옮긴다.</summary>
        Move,

        /// <summary>이어진 칸을 끊는다(빈 곳 · 제 카드).</summary>
        Unjoin,

        /// <summary>바뀌는 것이 없다(안 이은 칸을 빈 곳에).</summary>
        Nothing,

        /// <summary>이미 그 에피소드로 이어져 있다.</summary>
        Same,

        /// <summary>받지 않는다 — 다른 챕터 · 분기 카드 · 에피소드가 아님 · 같은 문구의 길이 이미 있음.</summary>
        Refuse
    }

    /// <summary>판정 — 꼬리표(짧은 말), 아래 띠(까닭까지), 놓을 카드.</summary>
    internal sealed record ChoiceDrop(ChoiceDropKind Kind, string Badge, string Hint, string? TargetNodeId);

    internal static IBrush DropJoinBrush => Skin.Join.Brush();
    internal static IBrush DropCutBrush => Skin.Danger.Brush();
    internal static IBrush DropIdleBrush => Skin.Neutral.Brush();

    /// <summary>포인터 옆 꼬리표 — 끄는 동안과 점에 포인터를 올렸을 때. 판(<c>GraphCanvas</c>) 위에 서되 배율을 거슬러 늘 같은 크기다.</summary>
    private Border? _dropBadge;
    private TextBlock? _dropBadgeText;

    /// <summary>놓을 카드의 테두리 — 펼친 카드일 때만(접은 상자의 줄은 꼬리표만 말한다).</summary>
    private Rectangle? _dropFrame;

    /// <summary>지금 보이는 판정 — 같으면 꼬리표 · 선 · 띠를 다시 고치지 않는다(자리만 따라간다).</summary>
    private ChoiceDrop? _shownDrop;

    /// <summary>점에 포인터를 올려 커진 점 — 내려오면 되돌린다.</summary>
    private Control? _hoveredKnob;

    /// <summary>테스트의 손잡이 — 지금 꼬리표가 말하는 것. 꼬리표가 없으면 null.</summary>
    internal string? DropBadgeText =>
        _dropBadge is not null && GraphCanvas.Children.Contains(_dropBadge) ? _dropBadgeText?.Text : null;

    /// <summary>테스트의 손잡이 — 지금 테두리가 감싼 카드. 없으면 null.</summary>
    internal string? DropFrameNodeId => _dropFrame is not null ? _shownDrop?.TargetNodeId : null;

    /// <summary>테스트의 손잡이 — 끄는 선의 붓.</summary>
    internal IBrush? ConnectingLineBrush => _connectingLine?.Stroke;

    /// <summary>
    /// 그 칸을 그 자리(<paramref name="droppedNodeId"/> — <c>NodeAt</c>이 찾은 노드, 빈 곳이면 null)에 놓으면 무엇이 되는가.
    /// 차례와 규칙은 놓을 때의 처리(<c>OnCanvasPointerReleased</c> → <c>JoinChoice</c>)와 같다.
    /// </summary>
    internal ChoiceDrop JudgeChoiceDrop(GraphChoicePort choice, string sourceNodeId, string? droppedNodeId)
    {
        // 제 카드에 놓는 것은 빈 곳에 놓는 것과 같다(놓을 때의 처리가 그렇게 받는다).
        string? target = droppedNodeId is null || string.Equals(droppedNodeId, sourceNodeId, StringComparison.Ordinal)
            ? null
            : droppedNodeId;

        if (target is null)
        {
            if (choice.IsEmpty)
            {
                return new ChoiceDrop(
                    ChoiceDropKind.Nothing,
                    Ui.Board_NothingChanges,
                    Ui.Board_DroppingHereChangesNothingDrop,
                    null);
            }

            string arrival = ArrivalName(choice);
            return new ChoiceDrop(
                ChoiceDropKind.Unjoin,
                Ui.Board_Disconnect,
                string.Format(Ui.Board_DroppingHereDisconnectsPathTo, arrival, Josa.으로(arrival)) +
                (choice.Label.Length > 0 ? string.Format(Ui.Board_TextStaysInSlotSo, choice.Label, Josa.은는(choice.Label)) : string.Empty) +
                Ui.Board_ToKeepItDropOn,
                null);
        }

        if (_session is null)
        {
            return new ChoiceDrop(ChoiceDropKind.Nothing, Ui.Board_NothingChanges, string.Empty, null);
        }

        string name = ShownName(target, target);

        if (EpisodeAt(target) is not var (chapterId, episodeId))
        {
            return IsBranchCard(target)
                ? new ChoiceDrop(
                    ChoiceDropKind.Refuse,
                    Ui.Board_CanTConnectToBranch,
                    string.Format(Ui.Board_IsBranchCardChoicesConnect, name, Josa.은는(name)),
                    target)
                : new ChoiceDrop(
                    ChoiceDropKind.Refuse,
                    Ui.Board_NotEpisode,
                    string.Format(Ui.Board_IsnTEpisodeChoicesConnect, name, Josa.은는(name)),
                    target);
        }

        // ⚠ 간선은 챕터 안의 길이다 — 두 챕터를 잇지 못한다.
        if (!string.Equals(chapterId, choice.ChapterId, StringComparison.Ordinal))
        {
            return new ChoiceDrop(
                ChoiceDropKind.Refuse,
                Ui.Board_AnotherChapter,
                string.Format(Ui.Board_IsEpisodeInAnotherChapter, name, Josa.은는(name), chapterId),
                target);
        }

        if (string.Equals(choice.ToEpisodeId, episodeId, StringComparison.Ordinal))
        {
            return new ChoiceDrop(
                ChoiceDropKind.Same,
                Ui.Board_AlreadyConnected,
                string.Format(Ui.Board_AlreadyConnectedToDroppingHere, name, Josa.으로(name)),
                target);
        }

        string label = LabelOf(choice);

        // 칸들은 문구 없이 안 이은 칸에만 묻는다(자동 길 자리인가) — 끄는 걸음마다 부르는 자리다.
        bool auto = JoinsAsAuto(choice, choice.IsEmpty && label.Length == 0 ? SlotsOf(sourceNodeId) : [], label);

        if (_session.Editor.FindChapter(chapterId) is not { } chapter)
        {
            return new ChoiceDrop(ChoiceDropKind.Nothing, Ui.Board_NothingChanges, string.Empty, target);
        }

        if (HasSameEdge(chapter, choice, episodeId, label))
        {
            return new ChoiceDrop(
                ChoiceDropKind.Refuse,
                Ui.Board_SameChoiceExists,
                label.Length > 0
                    ? string.Format(Ui.Board_ThisEpisodeAlreadyHasChoice, name, Josa.으로(name), label)
                    : string.Format(Ui.Board_ThisEpisodeAlreadyHasPath, name, Josa.으로(name)),
                target);
        }

        if (SceneEntryRefusal(choice, chapterId, episodeId) is { } refusal)
        {
            return new ChoiceDrop(ChoiceDropKind.Refuse, Ui.Board_SceneWouldGetTwoEntrances, refusal + Ui.Board_DroppingHereChangesNothing, target);
        }

        if (choice.IsEmpty)
        {
            return new ChoiceDrop(
                ChoiceDropKind.Join,
                auto ? string.Format(Ui.Board_ConnectAsAutoPath, name) : string.Format(Ui.Board_Connect, name),
                auto
                    ? string.Format(Ui.Board_DroppingHereMakesAutoPath, name, Josa.으로(name))
                    : label.Length == 0
                        ? string.Format(Ui.Board_DroppingHereConnectsBlank, name)
                        : string.Format(Ui.Board_DroppingHereConnectsTo, label, Josa.을를(label), name),
                target);
        }

        string was = ArrivalName(choice);
        return new ChoiceDrop(
            ChoiceDropKind.Move,
            string.Format(Ui.Board_Reconnect, name),
            auto
                ? string.Format(Ui.Board_DroppingHereSendsAutoPath, was, name, Josa.으로(name))
                : label.Length == 0
                    ? string.Format(Ui.Board_DroppingHereSendsBlank, was, name, Josa.으로(name))
                    : string.Format(Ui.Board_DroppingHereSendsToInstead, label, Josa.이가(label), was, name, Josa.으로(name)),
            target);
    }

    /// <summary>
    /// 그 칸을 그 에피소드에 이으면 같은 신원(출발 · 도착 · 문구)의 길이 둘이 되는가 — 옮기면 지금 길(그 칸이 진 간선)은 빼고 본다(편집기
    /// <c>SetEdgeRoute</c>가 그 간선을 빼고 신원 겹침을 보듯이). 문구 없이 같은 에피소드로 두 번 이은 것이 흔한 경우다(2026-11-26 — 먼저 잇고 나중에
    /// 적기). 판정과 놓기(<c>JoinChoice</c>)가 이것 하나를 부른다.
    /// </summary>
    private static bool HasSameEdge(ChapterDocument chapter, GraphChoicePort choice, string toEpisodeId, string label)
    {
        int replacing = ReplacedEdgeIndex(chapter, choice);

        return chapter.Edges
            .Where((_, index) => index != replacing)
            .Any(edge =>
                string.Equals(edge.FromEpisodeId, choice.FromEpisodeId, StringComparison.Ordinal) &&
                string.Equals(edge.ToEpisodeId, toEpisodeId, StringComparison.Ordinal) &&
                string.Equals(edge.OptionLabel?.Trim() ?? string.Empty, label.Trim(), StringComparison.Ordinal));
    }

    /// <summary>그 칸이 진 간선의 자리 — 안 이은 칸이거나 찾지 못하면 -1.</summary>
    private static int ReplacedEdgeIndex(ChapterDocument chapter, GraphChoicePort choice) =>
        choice.IsEmpty
            ? -1
            : chapter.Edges.FindIndex(edge =>
                string.Equals(edge.FromEpisodeId, choice.FromEpisodeId, StringComparison.Ordinal) &&
                string.Equals(edge.ToEpisodeId, choice.ToEpisodeId, StringComparison.Ordinal) &&
                string.Equals(edge.OptionLabel?.Trim() ?? string.Empty, choice.Label.Trim(), StringComparison.Ordinal));

    /// <summary>
    /// <b>그 칸을 그 에피소드에 이으면 도착 장면의 입구가 늘어 둘 이상이 되는가</b> — 그렇다면 받지 않는 까닭(아니면 null).
    /// 장면은 한 자리(시작점 ⌂)에서만 시작한다(<see cref="ChapterSceneEntryCheck"/> — [구성] 검증 보고의 오류 · 내보내기를 막는다).
    ///
    /// 이은 뒤의 간선으로 그 장면의 입구를 세어(<see cref="ChapterSceneEntryCheck.IsEntry"/>) <b>늘었고 둘 이상</b>일 때만 거절한다 — 그래서
    /// 그 장면의 시작점에 잇기 · 아직 입구가 없는 장면에 잇기 · 같은 장면 안의 길은 받고, 그 장면의 하나뿐인 입구 길을 같은 장면의 다른 카드로
    /// 옮기는 것(시작점이 옮겨 갈 뿐이다)도 받는다. 이미 입구가 둘인 옛 파일에서 늘리지 않는 잇기도 받는다. 판정(<see cref="JudgeChoiceDrop"/>)과
    /// 놓기(<c>JoinChoice</c>)가 이것 하나를 부른다.
    /// </summary>
    private string? SceneEntryRefusal(GraphChoicePort choice, string chapterId, string toEpisodeId)
    {
        if (_session?.Editor.FindChapter(chapterId) is not { } chapter)
        {
            return null;
        }

        ChapterEpisode? from = chapter.Episodes.FirstOrDefault(episode => string.Equals(episode.EpisodeId, choice.FromEpisodeId, StringComparison.Ordinal));
        ChapterEpisode? to = chapter.Episodes.FirstOrDefault(episode => string.Equals(episode.EpisodeId, toEpisodeId, StringComparison.Ordinal));

        if (from is null || to is null || string.Equals(from.SceneId, to.SceneId, StringComparison.Ordinal))
        {
            return null;
        }

        int replacing = ReplacedEdgeIndex(chapter, choice);
        string label = LabelOf(choice);

        List<ChapterEdge> after = chapter.Edges
            .Where((_, index) => index != replacing)
            .Append(new ChapterEdge(choice.FromEpisodeId, toEpisodeId, label.Length > 0 ? label : null, ConditionLabel: null, LockedMessage: null))
            .ToList();

        List<ChapterEpisode> before = EntriesOf(chapter, chapter.Edges, to.SceneId);
        List<ChapterEpisode> entries = EntriesOf(chapter, after, to.SceneId);

        if (entries.Count <= Math.Max(before.Count, 1))
        {
            return null;
        }

        string others = string.Join(", ", entries
            .Where(episode => !string.Equals(episode.EpisodeId, toEpisodeId, StringComparison.Ordinal))
            .Select(episode => $"'{episode.ShownTitle}'"));

        return string.Format(Ui.Board_IsnTStartOfAnother, to.ShownTitle, Josa.은는(to.ShownTitle), to.SceneId, others, Josa.을를(to.ShownTitle));
    }

    private static List<ChapterEpisode> EntriesOf(ChapterDocument chapter, IReadOnlyList<ChapterEdge> edges, string sceneId) =>
        chapter.Episodes
            .Where(episode => string.Equals(episode.SceneId, sceneId, StringComparison.Ordinal) &&
                              ChapterSceneEntryCheck.IsEntry(chapter.Episodes, edges, episode))
            .ToList();

    /// <summary>그 칸이 지금 가는 에피소드의 보이는 이름.</summary>
    private string ArrivalName(GraphChoicePort choice) =>
        choice.ToNodeId is { } node
            ? ShownName(node, choice.ToEpisodeId ?? node)
            : choice.ToEpisodeId ?? string.Empty;

    // ── 끄는 동안 ────────────────────────────────────────────────────────────

    /// <summary>점에서 끌기를 시작했다 — 지금 자리(점 위 · 제 카드)의 판정을 바로 보인다.</summary>
    private void BeginChoiceDropFeedback(GraphChoicePort choice, string sourceNodeId, Point start)
    {
        UnhoverKnob();
        _shownDrop = null;
        ShowChoiceDrop(choice, sourceNodeId, start);
    }

    /// <summary>끄는 걸음마다 — 판정이 바뀌었을 때만 꼬리표 · 선 · 띠 · 테두리를 고치고, 꼬리표는 늘 포인터를 따라간다.</summary>
    private void ShowChoiceDrop(GraphChoicePort choice, string sourceNodeId, Point position)
    {
        ChoiceDrop drop = JudgeChoiceDrop(choice, sourceNodeId, NodeAt(position));

        if (drop != _shownDrop)
        {
            _shownDrop = drop;
            (IBrush brush, bool dashed) = drop switch
            {
                { Kind: ChoiceDropKind.Join or ChoiceDropKind.Move } => (DropJoinBrush, false),
                { Kind: ChoiceDropKind.Unjoin or ChoiceDropKind.Refuse } => (DropCutBrush, true),
                _ => (DropIdleBrush, true)
            };

            if (_connectingLine is not null)
            {
                _connectingLine.Stroke = brush;
                _connectingLine.StrokeDashArray = dashed ? new AvaloniaList<double> { 4, 3 } : null;
            }

            ShowBadge(drop.Badge, brush);
            FrameTarget(drop.TargetNodeId, brush);
            SetHint(drop.Hint.Length > 0 ? drop.Hint : null);
        }

        PlaceBadge(position);
    }

    /// <summary>끌기가 끝났다(놓았다 · 창을 떠났다) — 꼬리표 · 테두리를 거둔다. 띠는 부르는 쪽이 접는다.</summary>
    private void ClearChoiceDropFeedback()
    {
        _shownDrop = null;
        HideBadge();

        if (_dropFrame is not null)
        {
            GraphCanvas.Children.Remove(_dropFrame);
            _dropFrame = null;
        }
    }

    // ── 끌기 전 — 점에 포인터를 올렸다 ──────────────────────────────────────

    /// <summary>
    /// 점에 포인터가 올라왔다 — 끌 수 있는 점이면 커지고 꼬리표가 무엇을 할 수 있는지 말한다(풍선은 늦게 뜨고 잘 안 읽힌다).
    /// 끄는 중이면 아무것도 하지 않는다(그때는 끄는 판정이 말한다).
    /// </summary>
    private void HoverKnob(Control knob, GraphChoicePort choice, bool live)
    {
        if (_connectingFrom is not null)
        {
            return;
        }

        UnhoverKnob();
        _hoveredKnob = knob;
        knob.RenderTransformOrigin = RelativePoint.Center;
        knob.RenderTransform = new ScaleTransform(1.5, 1.5);

        // 회색 점도 끌 수 있다 — 문구 없이 먼저 잇고 나중에 적는다(2026-11-26). ⛔ 그 전에는 「문구를 적으면 끌어서 이을 수 있습니다」(회색 꼬리표)였다.
        string text = !live
            ? Ui.Board_DragNowWriteLater
            : choice.IsEmpty
                ? Ui.Board_DragOntoEpisodeCardIn2
                : Ui.Board_DragOntoAnotherEpisodeTo;

        ShowBadge(text, DropJoinBrush);

        if (knob.TranslatePoint(new Point(PortRadius * 2, PortRadius * 2), GraphCanvas) is { } at)
        {
            PlaceBadge(at);
        }
    }

    /// <summary>점에서 포인터가 내려왔다 — 끄는 중이 아니면 점과 꼬리표를 되돌린다.</summary>
    private void LeaveKnob(Control knob)
    {
        if (!ReferenceEquals(_hoveredKnob, knob) || _connectingFrom is not null)
        {
            return;
        }

        UnhoverKnob();
        HideBadge();
    }

    private void UnhoverKnob()
    {
        if (_hoveredKnob is null)
        {
            return;
        }

        _hoveredKnob.RenderTransform = null;
        _hoveredKnob = null;
    }

    // ── 꼬리표 · 테두리 ──────────────────────────────────────────────────────

    private void ShowBadge(string text, IBrush brush)
    {
        if (_dropBadge is null)
        {
            _dropBadgeText = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = Skin.OnAccent.Brush()
            };

            _dropBadge = new Border
            {
                Padding = new Thickness(6, 2),
                CornerRadius = new CornerRadius(4),
                Child = _dropBadgeText,
                IsHitTestVisible = false,
                ZIndex = 2000,
                RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative)
            };
        }

        if (!GraphCanvas.Children.Contains(_dropBadge))
        {
            GraphCanvas.Children.Add(_dropBadge);
        }

        _dropBadgeText!.Text = text;
        _dropBadge.Background = brush;
        _dropBadge.IsVisible = true;
    }

    /// <summary>꼬리표를 포인터 오른쪽 아래에 — 배율을 거슬러 늘 같은 크기로.</summary>
    private void PlaceBadge(Point position)
    {
        if (_dropBadge is null)
        {
            return;
        }

        double scale = 1 / Math.Max(_zoom, 0.01);
        _dropBadge.RenderTransform = new ScaleTransform(scale, scale);
        Canvas.SetLeft(_dropBadge, position.X + (14 * scale));
        Canvas.SetTop(_dropBadge, position.Y + (14 * scale));
    }

    private void HideBadge()
    {
        if (_dropBadge is not null)
        {
            GraphCanvas.Children.Remove(_dropBadge);
        }
    }

    /// <summary>놓을 카드를 감싼다 — 펼친 카드일 때만. 없으면 거둔다.</summary>
    private void FrameTarget(string? nodeId, IBrush brush)
    {
        NodeCard? card = nodeId is null
            ? null
            : _cards.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));

        if (card is null)
        {
            if (_dropFrame is not null)
            {
                GraphCanvas.Children.Remove(_dropFrame);
                _dropFrame = null;
            }

            return;
        }

        if (_dropFrame is null)
        {
            _dropFrame = new Rectangle
            {
                StrokeThickness = 3,
                RadiusX = 8,
                RadiusY = 8,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false,
                ZIndex = 1999
            };

            GraphCanvas.Children.Add(_dropFrame);
        }

        const double outset = 4;
        _dropFrame.Stroke = brush;
        _dropFrame.Width = card.Visual.Width + (outset * 2);
        _dropFrame.Height = CardHeightOf(card) + (outset * 2);
        Canvas.SetLeft(_dropFrame, Canvas.GetLeft(card.Visual) - outset);
        Canvas.SetTop(_dropFrame, Canvas.GetTop(card.Visual) - outset);
    }
}
