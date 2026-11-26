using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vn.App.Services;
using Vn.App.Theming;
using Vn.Authoring.Chapters;
using Vn.Authoring.Flow;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Text;
using Vn.App.Strings;
using Vn.Authoring.Strings;

namespace Vn.App.Views;

/// <summary>
/// 대사 노드 하나를 위에서 아래로 편집한다.
///
/// 카드의 들여쓰기와 색은 <see cref="ConditionFlowResolver"/>가 계산한 갈래에서만 나온다.
/// 화면이 조건 상태를 따로 들고 있지 않으므로, 조건 드롭다운을 한 번 바꾸면
/// 그 아래 줄들의 표시가 전부 알아서 따라온다.
///
/// <b>화자와 대사를 입력하면 대본이 바뀐다.</b> 이 노드는 본문을 소유하지 않는다.
/// 화면에 보이는 것은 대본과 이 노드의 조건 데이터를 합친 투영이다.
///
/// 글자 편집은 카드를 다시 만들지 않는다. 다시 만들면 편집 중이던 칸이 사라진다.
/// </summary>
public partial class DialogueNodeEditor : UserControl
{
    private static IBrush LineSelectionBrush => Skin.Selection.Brush(200);

    /// <summary>고른 줄의 면 — 하이어아키의 고른 줄과 같은 색이다(<see cref="Skin.RowSelected"/>).</summary>
    private static IBrush LineSelectionFill => Skin.RowSelected.Brush();

    private AuthoringSession? _session;
    private string? _nodeId;
    private bool _building;

    /// <summary>
    /// 줄 표를 몇 번째로 다시 세웠나 — 줄의 글 칸은 자기가 지어진 번째를 쥐고, 다시 세워져 낡은 칸이 되었으면 넣지 않는다(아래 Commit).
    /// </summary>
    private int _generation;
    private string? _selectedLineId;
    private readonly Dictionary<string, Border> _lineCards = new(StringComparer.Ordinal);

    /// <summary>줄마다의 화자 칸 — 줄을 더하면 새 줄의 이 칸으로 초점을 옮긴다(<see cref="FocusLineLater"/>).</summary>
    private readonly Dictionary<string, AutoCompleteBox> _speakerBoxes = new(StringComparer.Ordinal);

    /// <summary>줄마다의 대사 칸 — 줄의 빈 곳을 누르면 그 줄의 이 칸으로 초점을 옮긴다(<see cref="FocusLineText"/>).</summary>
    private readonly Dictionary<string, TextBox> _textBoxes = new(StringComparer.Ordinal);

    /// <summary>다시 세운 뒤 초점을 옮길 줄 — 줄을 더한 직후 셸이 표를 다시 세우므로 그 뒤에 새 칸을 찾는다.</summary>
    private string? _focusLineId;

    /// <summary>열려 있는 줄의 ＋ 차림과 그 ＋ · 그 안의 [아래에 줄 추가] — ＋에서 Tab이 그 단추로 간다(usability UX26).</summary>
    private Flyout? _lineMetaFlyout;
    private Button? _lineMetaAnchor;
    private Button? _lineMetaAddButton;

    /// <summary>
    /// 지금 보는 노드가 <b>챕터의 에피소드</b>인가 (<see cref="DialogueNode.MarkedEpisodeId"/>).
    ///
    /// 참이어도 대본(본문·화자·줄 구성)은 잠그지 않는다 — 대본의 원본은 프로젝트다.
    ///
    /// 뜻은 좁다 — <b>챕터(기획자 층)가 쥔 것</b>만이다. 둘뿐이고, 둘 다 대본이 아니다:
    /// ① 이름 칸 = 에피소드의 <b>보이는 제목</b> — EpisodeId는 [구성] 탭에서 정한다(<see cref="CommitName"/>)
    /// ② 스탯변화(챕터 간선의 것)
    /// (⛔ 셋째였던 갈래 출구 배선은 2026-11-19에 갈래 출구와 함께 걷혔다 · usability UX28.)
    /// </summary>
    private bool _chapterEpisode;

    public DialogueNodeEditor()
    {
        InitializeComponent();

        NameBox.LostFocus += (_, _) => CommitName();

        // 줄의 칸(화자 · 대사)에 초점이 들어오면 그 줄이 「쓰고 있는 줄」이다 — 그 줄을 고르고(왼쪽 띠 · [줄 추가]의 자리) 셸에 알린다(판의 카드 왼쪽에 표시).
        // 초점이 줄 표를 떠나면 걷는다. 다른 줄의 칸으로 옮기면 들어온 쪽이 먼저 알린다.
        LineHost.GotFocus += (_, args) => UiGuard.Run(_session, Ui.Dialogue_Dialogue, () =>
        {
            if (LineOf(args.Source as Visual) is { } lineId)
            {
                SelectLine(lineId);
                SetEditingLine(lineId);
            }
        });
        LineHost.LostFocus += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!LineHost.IsKeyboardFocusWithin)
            {
                SetEditingLine(null);
            }
        });

        // 하이어아키에서 여러 줄을 골랐으면 그 에피소드 카드 모두를 한 번에 켜고 끈다(2026-11-23 소유자 — 「선택 된것들 모두 일괄적으로 키고 꺼지도록」).
        // 섞여 있으면(일부만 켜짐) 체크가 반쯤이고, 누르면 모두 켠다.
        HideEmptyChoicesBox.IsCheckedChanged += (_, _) => UiGuard.Run(_session, Ui.Main_ChoiceSlots, () =>
        {
            if (_building || _nodeId is null || _session is null)
            {
                return;
            }

            bool hide = _hideMixed || HideEmptyChoicesBox.IsChecked == true;
            _session.SetHideEmptyChoices(HideTargets(), hide);
            RefreshHideEmpty();
        });

        // Enter = 바꾸기, Esc = 그만두기 — 하이어아키의 이름 칸과 같은 손짓.
        NameBox.KeyDown += (_, args) => UiGuard.Run(_session, Ui.Board_Rename, () =>
        {
            if (args.Key == Avalonia.Input.Key.Enter)
            {
                args.Handled = true;
                CommitName();
            }
            else if (args.Key == Avalonia.Input.Key.Escape && _session?.Project.FindDialogue(_nodeId) is { } shown)
            {
                args.Handled = true;
                NameBox.Text = EpisodeNaming.ShownNameOf(_session.Project, shown);
            }
        });
        AddLineButton.Click += (_, _) => AddLine();

        // Ctrl+Tab = 앞 칸으로(Shift+Tab과 같다) — 줄 표를 쓰다가 손을 떼지 않고 이전 줄로 돌아간다(2026-11-19 소유자 · usability UX26).
        // 칸보다 먼저 받는다(터널) — 글 칸 · 화자 칸이 Tab을 제 것으로 쓰기 전에.
        AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Tab && args.KeyModifiers == Avalonia.Input.KeyModifiers.Control &&
                TopLevel.GetTopLevel(this)?.FocusManager is { } focus)
            {
                args.Handled = true;
                focus.TryMoveFocus(Avalonia.Input.NavigationDirection.Previous);
            }
        }, RoutingStrategies.Tunnel);

        // 목록 아래 빈 공간 우클릭 = 맨 아래 줄 추가.카드 위 우클릭은 카드가
        // 먼저 받아 Handled로 막고, 텍스트 칸은 기본 편집 메뉴의 자리다.
        LineScroll.PointerPressed += (_, args) =>
        {
            if (args.GetCurrentPoint(LineScroll).Properties.IsRightButtonPressed &&
                (args.Source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is null)
            {
                ShowEmptyAreaFlyout();
            }
        };
    }

    /// <summary>빈 공간의 줄 메뉴 —여기서의 추가는 언제나 맨 아래, 삭제는 대상이 없어 잠긴다.</summary>
    private void ShowEmptyAreaFlyout()
    {
        if (_session?.Project.FindDialogue(_nodeId) is null)
        {
            return;
        }

        var panel = new StackPanel { Spacing = 2, MinWidth = 150 };
        var flyout = new Flyout { Content = panel };

        var add = new Button
        {
            Content = Ui.Dialogue_AddLineAtEnd,
            FontSize = Skin.Font(11),
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent
        };
        add.Click += (_, _) =>
        {
            flyout.Hide();
            UiGuard.Run(_session, Ui.Dialogue_AddLine, AddLineAtEnd);
        };
        panel.Children.Add(add);

        panel.Children.Add(MenuSeparator());

        panel.Children.Add(new Button
        {
            Content = Ui.Board_Delete,
            FontSize = Skin.Font(11),
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            IsEnabled = false // 빈 공간에는 지울 줄이 없다 — 줄 카드에서 우클릭
        });

        flyout.ShowAt(LineScroll, showAtPointer: true);
    }

    private void AddLineAtEnd()
    {
        if (_session is null || _session.Project.FindDialogue(_nodeId) is not { } node)
        {
            return;
        }

        ScriptLine line = _session.Editor.InsertScriptLine(node.Id); // 인덱스 없음 = 맨 끝
        SelectLine(line.Id);
        FocusLineLater(line.Id);
    }

    internal void Attach(AuthoringSession session)
    {
        _session = session;
        session.HiddenChoicesChanged += _ => UiGuard.Run(_session, Ui.Main_ChoiceSlots, RefreshHideEmpty);

        // 카드 글 칸에서 지운 줄(칸을 나오면 지워진다)이 바뀌면 그 줄을 취소선으로 다시 보인다.
        session.PendingLineRemovalsChanged += nodeId =>
        {
            if (string.Equals(nodeId, _nodeId, StringComparison.Ordinal))
            {
                UiGuard.Run(_session, Ui.Dialogue_PendingRemoval, Rebuild);
            }
        };
    }

    /// <summary>
    /// 한꺼번에 바꿀 카드들 — 셸이 준다(하이어아키에서 고른 줄들에 든 카드 · <see cref="GraphEditorView.PickedCards"/>). 없거나 이 카드가 들지 않았으면 이 카드만이다.
    /// </summary>
    internal Func<IReadOnlyList<DialogueNode>>? BatchCards { get; set; }

    /// <summary>「빈 선택지 숨기기」를 바꿀 카드 Id — 고른 카드들(에피소드 카드만 — 선택지 칸은 그 카드에만 선다)에 이 카드가 들었으면 그 모두, 아니면 이 카드.</summary>
    private IReadOnlyList<string> HideTargets()
    {
        List<string> batch = BatchCards?.Invoke()
            .Where(card => card.MarkedEpisodeId is not null)
            .Select(card => card.Id)
            .ToList() ?? [];

        return _nodeId is not null && batch.Count > 1 && batch.Contains(_nodeId, StringComparer.Ordinal) ? batch : _nodeId is null ? [] : [_nodeId];
    }

    /// <summary>체크가 반쯤인가 — 고른 카드들의 「빈 선택지 숨기기」가 섞였다.</summary>
    private bool _hideMixed;

    /// <summary>
    /// 「빈 선택지 숨기기」 체크를 고른 카드들에 맞춘다 — 모두 켜졌으면 켬, 모두 꺼졌으면 끔, 섞였으면 반쯤(누르면 모두 켠다). 셸이 하이어아키의 고른 줄이 바뀔 때도
    /// 부른다.
    /// </summary>
    internal void RefreshHideEmpty()
    {
        if (_session is null || _nodeId is null)
        {
            return;
        }

        IReadOnlyList<string> targets = HideTargets();
        int hidden = targets.Count(id => _session.HiddenChoiceNodeIds.Contains(id));
        bool wasBuilding = _building;
        _building = true;

        try
        {
            _hideMixed = hidden > 0 && hidden < targets.Count;
            HideEmptyChoicesBox.IsChecked = _hideMixed ? null : hidden > 0;
            ToolTip.SetTip(HideEmptyChoicesBox, targets.Count > 1
                ? Plural.Format(Ui.Dialogue_ForAllSelectedEpisodeCards, targets.Count) +
                  (_hideMixed ? Ui.Dialogue_OnlySomeAreOnNow : string.Empty)
                : HideEmptyTip);
        }
        finally
        {
            _building = wasBuilding;
        }
    }

    private static string HideEmptyTip =>
        Ui.Dialogue_WhenOnOnlyChoicesConnected;

    /// <summary>
    /// 인스펙터에서 지금 쓰고 있는 줄이 바뀌었다 — (노드, 줄). 줄의 칸(화자 · 대사)에 초점이 있는 동안 그 줄이고, 초점이 줄 표를 떠나면 (null, null)이다.
    /// 셸이 [대본] 판의 그 카드 왼쪽에 표시한다(2026-11-26 소유자 — 「인스펙터에서 대사를 수정할 때 … 노드 왼쪽으로 현재 수정 중인 줄을 화살표로든 혹은
    /// 파란색 막대로든 표시」 · <see cref="GraphEditorView.ShowEditingLine"/>).
    /// </summary>
    internal event Action<string?, string?>? EditingLineChanged;

    private string? _editingLineId;

    /// <summary>지금 쓰고 있는 줄 — 시험의 손잡이.</summary>
    internal string? EditingLineId => _editingLineId;

    private void SetEditingLine(string? lineId)
    {
        if (string.Equals(_editingLineId, lineId, StringComparison.Ordinal))
        {
            return;
        }

        _editingLineId = lineId;
        EditingLineChanged?.Invoke(lineId is null ? null : _nodeId, lineId);
    }

    /// <summary>그 칸이 든 줄 — 줄을 감싼 띠(<see cref="WrapForLineSelection"/>)를 거슬러 찾는다. 줄 밖(조건 · 고아 카드)이면 null.</summary>
    private string? LineOf(Visual? source)
    {
        foreach (Visual at in source?.GetSelfAndVisualAncestors() ?? [])
        {
            if (ReferenceEquals(at, LineHost))
            {
                return null;
            }

            if (at is Border wrapper)
            {
                foreach ((string lineId, Border card) in _lineCards)
                {
                    if (ReferenceEquals(card, wrapper))
                    {
                        return lineId;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>어떤 노드를 편집할지 정하고 화면을 만든다.</summary>
    internal void Show(string? nodeId)
    {
        if (!string.Equals(_nodeId, nodeId, StringComparison.Ordinal))
        {
            _selectedLineId = null;
            SetEditingLine(null);
        }

        _nodeId = nodeId;
        Rebuild();
    }

    internal string? NodeId => _nodeId;

    internal void Rebuild()
    {
        if (_session is null || _session.Project.FindDialogue(_nodeId) is not { } node)
        {
            LineHost.Children.Clear();
            return;
        }

        _building = true;
        _generation++;

        try
        {
            _chapterEpisode = node.MarkedEpisodeId is not null;

            // 에피소드 카드의 이름 칸은 보이는 제목이다(비면 EpisodeId가 보인다). EpisodeId는 [구성] 탭에서 정한다.
            NameBox.Text = EpisodeNaming.ShownNameOf(_session.Project, node);
            NameBox.PlaceholderText = _chapterEpisode ? Ui.Board_TitleEmptyShowsEpisodeID : Ui.Board_TitleEmptyShowsBranchCard;
            ToolTip.SetTip(NameBox, _chapterEpisode
                ? string.Format(Ui.Dialogue_TitleShownInGameEdit, EpisodeNaming.EpisodeIdOf(node))
                : string.Format(Ui.Dialogue_BranchCardSTitleEdit, node.Name));

            // 선택지 칸은 챕터 판의 에피소드 카드에만 선다 — 체크도 그 카드에만.
            HideEmptyChoicesBox.IsVisible = _chapterEpisode;
            RefreshHideEmpty();

            FillBranchCondition(node);

            DialogueScript script = DialogueScriptResolver.Resolve(_session.Project, node);
            DialogueFlow flow = ConditionFlowResolver.Resolve(
                node,
                script,
                _session.Project);

            LineHost.Children.Clear();
            _lineCards.Clear();
            _speakerBoxes.Clear();
            _textBoxes.Clear();

            // 분기로 다녀오는 카드의 첫머리 조건 묶음도 그 분기의 색이다 — 판의 분기 선 · 카드 표와 같은 번호로 칠한다.
            _entryGate = BranchEntryCondition.EntryPalette(_session.Project, node.Id) is { } gatePalette &&
                         node.Script.ActiveLines.FirstOrDefault() is { } gateLine
                ? (gateLine.Id, gatePalette)
                : null;

            // 분기 카드의 색은 연출 그래프 카드의 그 분기 포트 · 선과 같다 — 같은 포트 번호(PaletteIndex)로 칠한다.
            Dictionary<string, int> detourPalette = NodeConnections.PortsOf(node, _session.Project)
                .Where(port => port.Kind == ExitPortKind.Detour && port.ExitKey is not null)
                .ToDictionary(port => port.ExitKey!, port => port.PaletteIndex, StringComparer.Ordinal);

            if (_selectedLineId is null ||
                flow.Lines.All(item => !string.Equals(item.Line.LineId, _selectedLineId, StringComparison.Ordinal)))
            {
                _selectedLineId = flow.Lines.FirstOrDefault()?.Line.LineId;
            }

            // 선택지는 대사 안에 서지 않는다 — 챕터 간선에서만 선다.

            IReadOnlySet<string> pendingRemovals = _session.PendingLineRemovalsOf(node.Id);

            foreach (ResolvedLine line in flow.Lines)
            {
                // 「조건 분기」 카드는 <b>줄 사이에</b> 낀다 —
                // 그 줄에 붙은 것이 아니라 그 줄 <b>앞에서</b> 갈라진다는 표식이다. 여럿이면 다녀오는 차례로 쌓인다.
                IReadOnlyList<Control> markers = BuildBranchMarkerCards(node, line.Line.LineId, detourPalette);

                Control card = WrapForLineSelection(
                    pendingRemovals.Contains(line.Line.LineId) ? BuildPendingRemovalCard(line) : BuildCard(node, script, line),
                    line.Line.LineId);

                LineHost.Children.AddRange(markers);
                LineHost.Children.Add(card);
            }

            if (flow.Lines.Count == 0)
            {
                LineHost.Children.Add(new TextBlock
                {
                    Text = Ui.Dialogue_PressAddLineToStart,
                    Opacity = Skin.Dim(0.6),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            AddOrphanCards(node, script);
            ShowProblems(flow);
        }
        finally
        {
            _building = false;
        }
    }

    /// <summary>클릭한 줄을 왼쪽 강조 띠로 감싼다 — 고른 줄이 [줄 추가]의 자리다.</summary>
    private Control WrapForLineSelection(Control card, string lineId)
    {
        var wrapper = new Border
        {
            // Transparent는 보이지 않지만 히트테스트는 된다 — 배경 없는 일반 대사 카드의
            // 빈 부분을 클릭해도 선택이 되게 한다(글자 위만 반응하지 않도록).
            // 고른 줄은 윈도우의 목록처럼 옅은 파랑 면에 왼쪽 강조색 띠다(2026-11-26 소유자 — 고른 것의 시각 언어를 윈도우 문법으로).
            Background = string.Equals(lineId, _selectedLineId, StringComparison.Ordinal) ? LineSelectionFill : Brushes.Transparent,
            BorderThickness = new Thickness(3, 0, 0, 0),
            BorderBrush = string.Equals(lineId, _selectedLineId, StringComparison.Ordinal)
                ? LineSelectionBrush
                : Brushes.Transparent,
            Padding = new Thickness(4, 0, 0, 0),
            Child = card
        };

        // 줄의 표(테마가 고른 줄 · 마우스를 올린 줄에만 「화자」 자리 글과 [+]를 보이는 손잡이 — Themes/*/Controls.axaml · 2026-11-26 디자인 점검 6번).
        wrapper.Classes.Add(LineRowClass);
        wrapper.Classes.Set(PickedLineClass, string.Equals(lineId, _selectedLineId, StringComparison.Ordinal));

        wrapper.AddHandler(
            PointerPressedEvent,
            (_, args) =>
            {
                SelectLine(lineId);

                // 줄의 빈 곳(번호 · 칸 사이)을 누르면 그 줄의 대사 칸으로 초점이 간다 — 안 그러면 앞서 쓰던 다른 줄의 칸이 초점을 쥔 채 커서가 깜빡여, 고른
                // 줄과 쓰는 줄이 갈렸다(2026-11-27 소유자 — 「다른 줄을 클릭했는데도, 여전히 화자칸이 열린채 깜빡이는」). 칸 · 단추를 누르면 그것이 초점을 갖는다.
                if (args.GetCurrentPoint(wrapper).Properties.IsLeftButtonPressed &&
                    !wrapper.IsKeyboardFocusWithin &&
                    !PressedOnFocusable(args.Source as Visual, wrapper))
                {
                    FocusLineText(lineId);
                }

                // 우클릭 = 줄 추가/삭제 바로가기 — ＋ 플라이아웃을 거치지 않는 짧은 길.
                // 텍스트 입력 칸 위에서는 비켜선다 —잘라내기/복사/붙여넣기 기본
                // 메뉴가 그 자리의 주인이다.
                if (args.GetCurrentPoint(wrapper).Properties.IsRightButtonPressed &&
                    (args.Source as Visual)?.FindAncestorOfType<TextBox>(includeSelf: true) is null)
                {
                    ShowLineContextFlyout(wrapper, lineId);
                    args.Handled = true; // 아래 빈 공간 메뉴가 겹쳐 뜨지 않게
                }
            },
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _lineCards[lineId] = wrapper;

        return wrapper;
    }

    private static Control MenuSeparator() => new Border
    {
        Height = 1,
        Margin = new Thickness(4, 2),
        Background = Skin.Hairline.Brush(60)
    };

    /// <summary>줄 카드 우클릭 메뉴 —추가·삭제는 ＋ 플라이아웃과 같은 편집 경로 하나를 쓴다.</summary>
    private void ShowLineContextFlyout(Control anchor, string lineId)
    {
        if (_session?.Project.FindDialogue(_nodeId) is not { } node)
        {
            return;
        }

        string nodeId = node.Id;

        var panel = new StackPanel { Spacing = 2, MinWidth = 140 };

        Button Item(string label, string tip)
        {
            var button = new Button
            {
                Content = label,
                FontSize = Skin.Font(11),
                Padding = new Thickness(10, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent
            };
            ToolTip.SetTip(button, tip);
            panel.Children.Add(button);
            return button;
        }

        var flyout = new Flyout { Content = panel };

        Button add = Item(Ui.Dialogue_AddLineBelow, Ui.Dialogue_InsertsNewLineRightBelow);
        add.Click += (_, _) =>
        {
            flyout.Hide();
            UiGuard.Run(_session, Ui.Dialogue_AddLine, AddLine); // 선택된 줄 아래에 — 위의 SelectLine이 이미 골랐다
        };

        // 추가와 삭제 사이에 구분선 —성격이 반대인 두 동작이 붙어 있으면 헷갈린다.
        panel.Children.Add(MenuSeparator());

        Button remove = Item(Ui.Board_Delete, Ui.Dialogue_RemovesThisLineFromScript);
        remove.Click += (_, _) =>
        {
            flyout.Hide();
            UiGuard.Run(_session, Ui.Dialogue_DeleteLine, () => _session!.Editor.RetireScriptLine(nodeId, lineId));
        };

        // 분기 조건이 걸린 첫 줄은 지우지 않는다(줄 칸의 [✕ 삭제]와 같은 까닭).
        if (GuardsEntry(node) && string.Equals(node.Script.ActiveLines.FirstOrDefault()?.Id, lineId, StringComparison.Ordinal))
        {
            remove.IsEnabled = false;
            ToolTip.SetTip(remove, EntryGuardTip);
            ToolTip.SetShowOnDisabled(remove, true);
        }

        // 마우스 지점에서 연다 —카드 기준으로 뜨면 클릭한 곳과 멀어 헷갈린다.
        flyout.ShowAt(anchor, showAtPointer: true);
    }

    /// <summary>고른 줄(파란 띠) — 시험의 손잡이.</summary>
    internal string? SelectedLineId => _selectedLineId;

    /// <summary>
    /// [대본] 판의 카드 글 칸에서 커서가 선 줄을 고른 줄로 삼는다(2026-11-26 소유자 — 「그래프 뷰에서 특정 줄을 수정하는데, 그것과 별개로 우측 인스펙터에서는
    /// 다른 줄을 파랗게 강조하니 되게 헷갈립니다 … 동기화」 · <see cref="GraphEditorView.CaretLineChanged"/>). 파란 띠가 그 줄로 옮기고, 그 줄이 보이게
    /// 줄 표를 내린다. 초점은 가져가지 않는다. 다른 노드를 보고 있거나 인스펙터의 줄 칸에서 쓰는 중이면 하지 않는다 — 사람이 쓰는 칸이 먼저다.
    /// 이미 고른 줄이어도 보이게는 한다 — 카드 글 칸에 다시 들어왔을 때 그 줄이 줄 표 밖에 있을 수 있다.
    /// </summary>
    internal void FollowCaretLine(string nodeId, string lineId)
    {
        if (!string.Equals(_nodeId, nodeId, StringComparison.Ordinal) || LineHost.IsKeyboardFocusWithin)
        {
            return;
        }

        SelectLine(lineId);
        BringLineIntoViewLater(lineId);
    }

    /// <summary>줄 표를 그 줄이 보이게 내릴 줄 — 배치가 끝난 뒤에 내린다(<see cref="BringLineIntoViewLater"/>).</summary>
    private string? _scrollLineId;

    /// <summary>
    /// 그 줄이 보이게 줄 표를 내린다 — <b>배치가 끝난 뒤에</b>(2026-11-26 소유자 — 「처음 에피소드 노드 내부의 특정 줄을 처음 클릭했을 때 … 강조자체는 제대로
    /// 되는데 그 줄로 바로 이동하는게 안 되는 경우」). 카드 글 칸을 처음 누르면 그 카드가 골라지며 줄 표를 새로 세우는데, 막 세운 줄은 아직 자리가 없어 곧바로
    /// 내리면 아무 일도 없었다. 그 사이에 줄 표를 또 세웠으면 그때의 줄을 찾는다. 여러 번 부르면 마지막 줄이다.
    /// </summary>
    private void BringLineIntoViewLater(string lineId)
    {
        _scrollLineId = lineId;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_scrollLineId is not { } pending)
            {
                return;
            }

            _scrollLineId = null;

            if (_lineCards.TryGetValue(pending, out Border? card) && card.IsAttachedToVisualTree())
            {
                card.BringIntoView();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>누른 자리가 초점을 받는 것(대사 · 화자 칸 · 단추) 안인가 — 줄(<paramref name="row"/>) 안에서만 본다.</summary>
    private static bool PressedOnFocusable(Visual? source, Visual row)
    {
        foreach (Visual at in source?.GetSelfAndVisualAncestors() ?? [])
        {
            if (ReferenceEquals(at, row))
            {
                return false;
            }

            if (at is Avalonia.Input.InputElement { Focusable: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>그 줄의 대사 칸으로 초점을 옮기고 커서를 글 끝에 둔다.</summary>
    private void FocusLineText(string lineId)
    {
        if (_textBoxes.TryGetValue(lineId, out TextBox? box) && box.IsAttachedToVisualTree())
        {
            box.Focus(Avalonia.Input.NavigationMethod.Pointer);
            box.CaretIndex = box.Text?.Length ?? 0;
        }
    }

    private void SelectLine(string lineId)
    {
        if (string.Equals(_selectedLineId, lineId, StringComparison.Ordinal))
        {
            return;
        }

        _selectedLineId = lineId;

        foreach ((string cardLineId, Border card) in _lineCards)
        {
            bool picked = string.Equals(cardLineId, lineId, StringComparison.Ordinal);
            card.BorderBrush = picked ? LineSelectionBrush : Brushes.Transparent;
            card.Background = picked ? LineSelectionFill : Brushes.Transparent;
            card.Classes.Set(PickedLineClass, picked);
        }
    }

    /// <summary>줄 표의 한 줄(고름 띠)의 클래스 — 테마의 손잡이다.</summary>
    internal const string LineRowClass = "line-row";

    /// <summary>고른 줄의 클래스 — 테마의 손잡이다.</summary>
    internal const string PickedLineClass = "picked";

    /// <summary>줄마다의 화자 칸 · [+]의 클래스 — 테마가 고른 줄 · 마우스를 올린 줄에만 보인다(「파스텔 하늘」).</summary>
    internal const string LineSpeakerClass = "line-speaker";

    /// <inheritdoc cref="LineSpeakerClass"/>
    internal const string LineAddClass = "line-add";

    // ── 대본 ────────────────────────────────────────────────────────────────

    private void AddLine()
    {
        if (_session is null || _session.Project.FindDialogue(_nodeId) is not { } node)
        {
            return;
        }

        // 선택한 줄이 있으면 그 바로 아래에 끼워 넣는다. 없으면 맨 끝, 비어 있으면 첫 줄.
        int? at = null;

        if (_selectedLineId is not null)
        {
            int index = node.Script.Lines.FindIndex(line =>
                string.Equals(line.Id, _selectedLineId, StringComparison.Ordinal));

            if (index >= 0)
            {
                at = index + 1;
            }
        }

        ScriptLine line = _session.Editor.InsertScriptLine(node.Id, at);

        // 새 줄이 새 선택이다 — 프리뷰 기준도 함께 옮긴다.
        SelectLine(line.Id);
        FocusLineLater(line.Id);
    }

    /// <summary>
    /// 그 줄의 화자 칸으로 초점을 옮긴다 — 줄을 더하면 바로 그 줄을 쓴다(usability UX26 · ⛔ 「줄 추가를 누르면 바로 타이핑할 수 있습니다」라고
    /// 안내했지만 초점이 가지 않았다). 셸이 표를 다시 세운 뒤에(그 사이에 또 세우면 마지막 표의) 칸을 찾는다. 셸 없이 선 편집기면 스스로 세운다.
    /// </summary>
    private void FocusLineLater(string lineId)
    {
        if (!_speakerBoxes.ContainsKey(lineId))
        {
            Rebuild();
        }

        _focusLineId = lineId;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_focusLineId is { } pending && _speakerBoxes.TryGetValue(pending, out AutoCompleteBox? box))
            {
                _focusLineId = null;
                box.Focus(Avalonia.Input.NavigationMethod.Tab);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    // ── 카드 ────────────────────────────────────────────────────────────────
    //
    // 고밀도 배치: 평범한 대사는 딱 한 줄(34~42px)이다. 조건·선택·Set은 메인 행 위의
    // 태그 레일, 출구는 아래의 출구 레일로만 나타난다 — 태그가 없으면 레일도 없다.
    // 태그는 표시가 아니라 편집 진입점이다: 누르면 기존 편집 컨트롤이 Flyout으로 열린다.

    /// <summary>
    /// 모든 카드가 공유하는 열 구성 — Index | 화자 | 대사 | ＋. 태그 · 출구 레일은 화자 열(30)에서 시작한다.
    /// 목록 위의 열 머리(axaml의 <c>ColumnHeader</c>)도 앞 세 열이 이것과 같다 — 넷째 열은 [줄 추가] 자리다.
    /// LineId는 화면에 열로 내지 않는다 — 줄 신원은 데이터에만 산다.
    /// </summary>
    private const string RowColumns = "30,96,*,28";

    private Control BuildCard(DialogueNode node, DialogueScript script, ResolvedLine resolved)
    {
        ConditionBranch? branch = resolved.Branch;
        int palette = branch is null ? -1 : PaletteOf(branch);

        var body = new StackPanel { Spacing = 1 };

        if (BuildTagRail(node, resolved) is { } rail)
        {
            body.Children.Add(rail);
        }

        body.Children.Add(BuildMainRow(node, script, resolved));

        return new Border
        {
            // 조건 안이면 오른쪽으로 한 단계 들어가고 그 조건 팔레트의 색을 쓴다.
            Margin = new Thickness(resolved.Depth * 20, 0, 0, 0),
            Padding = new Thickness(6, 3),
            CornerRadius = new CornerRadius(4),
            // 줄마다 상자를 두르거나(기본 테마), 줄 아래 구분선만 둔다(Skin.LineRowBoxed · themes) — 조건 안의 줄은 어느 쪽이든 왼쪽 색 막대가 선다.
            BorderThickness = Skin.LineRowBoxed > 0
                ? (branch is null ? new Thickness(1) : new Thickness(3, 1, 1, 1))
                : new Thickness(branch is null ? 0 : 3, 0, 0, 1),
            BorderBrush = branch is null
                ? Skin.Hairline.Brush(60)
                : BranchPalette.Accent(palette),
            Background = branch is null ? null : BranchPalette.Background(palette),
            Child = body
        };
    }

    /// <summary>
    /// <b>지울 줄</b>(2026-11-26 소유자 — 「쓴 줄을 지운 다음에 우측 인스펙터에 반영이 느리다」) — 카드 글 칸에서 지웠고 칸을 나오면(또는 Ctrl+S) 지워질
    /// 줄. 그때까지는 대본에 남아 있으므로 자리는 지키되 취소선으로 보이고 고칠 칸을 세우지 않는다. 쓰는 동안 줄을 바로 지우지 않는 까닭은 다 지우고
    /// 새로 쓰는 도중에 줄이 은퇴하면 그 줄의 신원(LineId)과 거기 붙은 분기 · 번역 · 연출이 떨어져 나가기 때문이다.
    /// </summary>
    private static Control BuildPendingRemovalCard(ResolvedLine resolved)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,Auto") };

        var index = new TextBlock
        {
            Text = (resolved.Index + 1).ToString(),   // 사람이 세는 번호 — 1부터(2026-11-26 — 디자인 점검 6번 · 「n번째 줄」 알림과 같다 · ⛔ 0부터)
            FontSize = Skin.Font(10),
            Opacity = Skin.Dim(0.45),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(index);

        string speaker = resolved.Line.Speaker;
        var said = new TextBlock
        {
            Text = speaker.Length > 0 ? $"{speaker}: {resolved.Line.Text}" : resolved.Line.Text,
            TextDecorations = TextDecorations.Strikethrough,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = Skin.Font(12),
            Opacity = Skin.Dim(0.55),
            MinHeight = 26,
            Padding = new Thickness(6, 5),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(said, 1);
        row.Children.Add(said);

        var chip = new TextBlock
        {
            Text = Ui.Dialogue_PendingRemoval,
            FontSize = Skin.Font(10),
            Foreground = PendingRemovalBrush,
            Margin = new Thickness(6, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(chip, 2);
        row.Children.Add(chip);

        var card = new Border
        {
            Margin = new Thickness(resolved.Depth * 20, 0, 0, 0),
            Padding = new Thickness(6, 3),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = PendingRemovalBrush,
            Tag = PendingRemovalTag,
            Child = row
        };
        ToolTip.SetTip(card, Ui.Dialogue_PendingRemovalTip);

        return card;
    }

    /// <summary>지울 줄 카드의 표 — 시험이 찾는다.</summary>
    internal const string PendingRemovalTag = "pending-removal";

    private static IBrush PendingRemovalBrush => Skin.Danger.Brush(170);

    /// <summary>
    /// Index | 화자 | 대사 | ＋ 의 메인 행. 긴 대사는 칸 안에서 아래로 줄을 바꾸며 행이 그만큼 높아진다 — 번호 · 화자 · ＋는 첫 줄에 맞춰 위에 선다
    /// (2026-11-26 소유자 — 「줄의 뒷부분이 화면 바깥으로 넘어가면 아예 안 보이는데 … 아래쪽으로 줄바꿈하면서 보여주는 게 맞겠습니다」 · ⛔ 그 전에는 한 줄로
    /// 고정하고 칸 안에서 가로로 흘렀다).
    /// </summary>
    private Control BuildMainRow(DialogueNode node, DialogueScript script, ResolvedLine resolved)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(RowColumns) };

        var index = new TextBlock
        {
            Text = (resolved.Index + 1).ToString(),   // 사람이 세는 번호 — 1부터(2026-11-26 — 디자인 점검 6번 · 「n번째 줄」 알림과 같다 · ⛔ 0부터)
            FontSize = Skin.Font(10),
            Opacity = Skin.Dim(0.45),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 0, 0)
        };
        Grid.SetColumn(index, 0);
        row.Children.Add(index);

        // 긴 대사는 칸 안에서 아래로 줄을 바꾼다 — 칸(과 행)이 그만큼 높아진다. Enter는 줄을 넣지 않는다(한 줄 = 대사 하나).
        var text = new TextBox
        {
            Text = resolved.Line.Text,
            PlaceholderText = Ui.Dialogue_Dialogue,
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap,
            FontSize = Skin.Font(12),
            MinHeight = 26,
            Padding = new Thickness(6, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };

        AutoCompleteBox? speaker = null;
        string nodeId = _nodeId ?? string.Empty;
        int generation = _generation;

        // 글자가 바뀔 때마다 대본에 넣되, 그것이 카드 목록을 다시 만들지는 않는다.
        // 편집기가 내용 변경과 구조 변경을 구분해서 알리기 때문에 가능하다.
        //
        // ⚠ 칸의 「글이 바뀌었다」는 한 박자 늦게 온다 — 줄 표를 세우며 채운 글도 세우기가 끝난 뒤에 「바뀜」으로 온다. 그래서 <b>낡은 칸</b>(그 뒤에
        //   표를 다시 세웠다)과 <b>지어질 때의 글 그대로인 칸</b>은 넣지 않는다. ⛔ 2026-11-18까지는 넣었다 — 글이 짧은 사이에 두 번 바뀌면(되돌리고
        //   곧 다시 실행) 앞선 칸이 옛 글을 도로 넣고, 표가 다시 서고, 그 칸이 또 옛 글을 넣어 두 값 사이를 끝없이 오갔다(board-speed PF1).
        void Commit()
        {
            if (_building || nodeId.Length == 0 || generation != _generation)
            {
                return;
            }

            string nextSpeaker = speaker?.Text ?? resolved.Line.Speaker;
            string nextText = text.Text ?? string.Empty;

            if (string.Equals(nextSpeaker, resolved.Line.Speaker, StringComparison.Ordinal) &&
                string.Equals(nextText, resolved.Line.Text, StringComparison.Ordinal))
            {
                return;   // 지어질 때의 글 그대로다 — 세우며 채운 글의 늦은 「바뀜」
            }

            _session!.Editor.SetScriptLineText(
                nodeId, resolved.Line.LineId, nextSpeaker, nextText, script.Locale);
        }

        // 글자가 바뀔 때마다 낸다 — 글자는 프로젝트로 간다.
        void Wire(Control field)
        {
            if (field is TextBox box)
            {
                box.TextChanged += (_, _) => Commit();
            }
            else if (field is AutoCompleteBox auto)
            {
                // 후보 사이를 화살표로 오가는 동안은 칸에 그 이름이 미리 보일 뿐 넣지 않는다 — 고르면(Enter · 누르기 · 목록이 닫힐 때) 넣는다
                // (2026-11-26 소유자 — 화살표로 고르고 Enter로 넣기 · 오간 이름마다 되돌리기 칸이 쌓이지 않게).
                auto.TextChanged += (_, _) =>
                {
                    if (auto.IsDropDownOpen && auto.SelectedItem is string previewing &&
                        string.Equals(auto.Text, previewing, StringComparison.Ordinal))
                    {
                        return;
                    }

                    Commit();
                };
                auto.DropDownClosed += (_, _) => Commit();
            }
        }

        {
            // 화자는 프로젝트의 화자 목록의 드롭다운 + 자유 입력 겸용이다.
            speaker = new AutoCompleteBox
            {
                Text = resolved.Line.Speaker,
                PlaceholderText = Ui.Dialogue_Speaker,
                FontSize = Skin.Font(11),
                MinHeight = 26,
                ItemsSource = SpeakerNames(),
                Classes = { LineSpeakerClass }
            };
            Wire(speaker);
            _speakerBoxes[resolved.Line.LineId] = speaker;

            // 후보는 조합 중인 첫 글자 · 자음부터 거른다(writer-comfort WC3 — ⛔ 칸의 글만 보아 한글은 둘째 글자부터 움직였다). 목록은 칠 때마다
            // 지금 화자 목록에서 — 방금 등록한 화자도 카드를 다시 짓지 않고 나온다.
            SpeakerSuggest.Attach(speaker, SpeakerNames);

            // ⛔ 옆의 ▾(등록 화자 전체 목록 — 누르면 이름 목록이 떴다)는 2026-11-26에 걷었다(소유자 — 「인스펙터에서 등록된 화자 목록 드롭다운은 필요가
            //    없을 것 같습니다」). 등록 화자는 칸에 적기 시작하면 후보로 뜬다(SpeakerSuggest).
            speaker.Margin = new Thickness(4, 0, 0, 0);
            speaker.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(speaker, 1);
            row.Children.Add(speaker);
        }

        Wire(text);
        _textBoxes[resolved.Line.LineId] = text;
        Grid.SetColumn(text, 2);
        row.Children.Add(text);

        Button plus = BuildPlusButton(node, resolved);
        plus.VerticalAlignment = VerticalAlignment.Top;
        plus.Margin = new Thickness(plus.Margin.Left, 3, plus.Margin.Right, 0);
        Grid.SetColumn(plus, 3);
        row.Children.Add(plus);

        return row;
    }

    /// <summary>
    /// 화자 후보 — <b>프로젝트의 화자 목록</b>(<c>StoryProject.Speakers</c>) 하나가 원천이다. [대본] 탭 오른쪽의 「화자」 탭에서 더하고,
    /// 모든 챕터가 같은 목록을 본다.
    ///
    /// 여기 없는 이름도 화자 칸에 직접 적으면 그만이다 — 이 목록은 편의일 뿐이다.
    /// </summary>
    private List<string> SpeakerNames() =>
        _session?.Project.Speakers
            .Select(item => item.Name)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

    // ── 태그 레일 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 조건 전환·Set이 있는 줄에만 만드는 메타데이터 레일. 태그가 없으면 null —
    /// 그 줄은 추가 높이를 전혀 갖지 않는다.
    /// </summary>
    private Control? BuildTagRail(DialogueNode node, ResolvedLine resolved)
    {
        var rail = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(30, 0, 0, 1)
        };

        if (resolved.Line.Transition is not null)
        {
            rail.Children.Add(BuildTransitionTag(node, resolved));
        }

        return rail.Children.Count > 0 ? rail : null;
    }

    private static Border TagChip(string text, IBrush background)
    {
        return new Border
        {
            Child = new TextBlock
            {
                Text = text,
                FontSize = Skin.Font(9),
                FontWeight = FontWeight.Bold,
                Foreground = Skin.OnAccent.Brush()
            },
            Background = background,
            Padding = new Thickness(6, 1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private string ConditionLabelOf(DialogueNode node, string conditionId)
    {
        AvailableConditionCatalog available = AvailableConditionResolver.Resolve(
            _session!.Project, node.Id);
        AvailableCondition? condition = available.Find(conditionId);
        AvailableCondition? known = condition ?? AvailableConditionResolver.FindKnown(
            _session.Project, conditionId);

        return condition is not null
            ? AvailableConditionResolver.LayeredLabel(condition)
            : known is not null
                ? AvailableConditionResolver.UnavailableLabel(known, conditionId)
                : Ui.Dialogue_UnknownCondition;
    }

    /// <summary>
    /// 이 줄의 전환 태그 — 보이기만 한다. 줄의 조건 전환은 분기 카드의 조건(분기 패널)과 대본 글의 <c>&lt;&lt;if&gt;&gt;</c>가 정한다(2026-11-18 소유자 ·
    /// old-shapes O6 — ⛔ 그 전에는 누르면 줄 조건 드롭다운이 열렸고, 옛 「X 설정」 노드의 조건만 고를 수 있었다).
    /// </summary>
    private Control BuildTransitionTag(DialogueNode node, ResolvedLine resolved)
    {
        IBrush neutral = Skin.Neutral.Brush(160);

        (string label, IBrush background) = resolved.Line.Transition!.Kind switch
        {
            ConditionTransitionKind.BeginIf or ConditionTransitionKind.BeginElseIf =>
                (ConditionLabelOf(node, resolved.Branch?.ConditionId ?? string.Empty),
                    resolved.Branch is { } branch ? BranchPalette.Accent(PaletteOf(branch)) : (IBrush)neutral),
            _ => (Ui.Dialogue_EndOfCondition, neutral)
        };

        Border tag = TagChip(label, background);
        ToolTip.SetTip(tag, string.Format(Ui.Dialogue_ThisLineSConditionSwitch, BranchConditionNote.Where));

        return tag;
    }

    // ── 「조건 분기」 카드 ──────────────────────────────────────────

    /// <summary>
    /// 그 줄 <b>앞</b>에 낀 「조건 분기」 카드들 — 분기마다 한 장, 다녀오는 차례로(한 줄에 여럿). 표식이 없으면 빈 목록이다.
    ///
    /// <b>내용이 없다.</b> 연출 그래프는 "어디서 갈라지는가"만 짚는다 —
    /// 구체적인 조건식도 선택지 문구도 여기서 정하지 않는다. 대본에는 <c>&lt;&lt;detour {노드}&gt;&gt;</c>
    /// 한 줄로 나가고, <c>&lt;&lt;if&gt;&gt;</c>가 없다.
    ///
    /// [✕ 분기 지우기]는 분기점과 선만 걷는다 — 다녀가던 카드는 늘 남는다(2026-11-17 —
    /// <see cref="Vn.Authoring.Editing.ProjectEditor.RemoveBranchMarker"/>). 같은 단추가 그 줄의
    /// ＋ 메뉴에도 서고, 카드를 <b>우클릭</b>해도 같다(연출 그래프에서 분기 선을
    /// 우클릭하는 것과 같은 손놀림이다).
    ///
    /// 색은 연출 그래프 카드의 그 분기 포트 · 선의 색이다 — 분기끼리 색으로 구분된다.
    /// </summary>
    /// <param name="detourPalette">분기 열쇠(<see cref="ExitPort.DetourKey"/>) → 그 포트의 색 번호.</param>
    private IReadOnlyList<Control> BuildBranchMarkerCards(
        DialogueNode node, string lineId, IReadOnlyDictionary<string, int> detourPalette)
    {
        // 분기점 전부를 차례대로 — 연결만 끊은 분기점은 제자리에 선다.
        List<string?> slots = node.FindExtension(lineId)?.DetourSlots() ?? [];
        int lastConnected = slots.FindLastIndex(slot => slot is not null);
        var cards = new List<Control>();

        for (int order = 0; order < slots.Count; order++)
        {
            cards.Add(slots[order] is { } targetId
                ? BuildBranchMarkerCard(
                    node,
                    lineId,
                    targetId,
                    order,
                    slots.Count,
                    lastConnected: order == lastConnected,
                    detourPalette.GetValueOrDefault(ExitPort.DetourKey(lineId, targetId), -1))
                : BuildOpenDetourCard(
                    node,
                    lineId,
                    order,
                    slots.Count,
                    detourPalette.GetValueOrDefault(ExitPort.DetourKey(lineId, ExitPort.OpenSlot(order)), -1)));
        }

        return cards;
    }

    /// <summary>
    /// 이어지지 않은 분기점 하나 — 다녀갈 곳과의 연결만 끊은 분기다. 다녀가지 않고 지나간다. [대본] 판에서 그 점을 끌어
    /// 카드에 이으면 다시 분기가 된다. [✕ 분기점 지우기] · 우클릭이 분기점째 걷는다.
    /// </summary>
    /// <param name="order">그 줄의 분기점 전체 가운데 몇째 자리인가 — 그 분기점의 신원이다(<see cref="ExitPort.OpenSlot"/>).</param>
    private Control BuildOpenDetourCard(DialogueNode node, string lineId, int order, int count, int palette)
    {
        var head = new DockPanel { LastChildFill = true };
        head.Children.Add(new TextBlock
        {
            Text = (count > 1 ? string.Format(Ui.Dialogue_BranchPoint, order + 1, count) : Ui.Dialogue_BranchPoint2) + Ui.Dialogue_NotConnected,
            FontSize = Skin.Font(11),
            FontWeight = FontWeight.Bold,
            Foreground = BranchPalette.Accent(palette),
            VerticalAlignment = VerticalAlignment.Center
        });

        Button remove = SmallButton(Ui.Dialogue_DeleteBranchPoint, () => RemoveOpenDetour(node, lineId, order));
        ToolTip.SetTip(remove, Ui.Dialogue_DeletesUnconnectedBranchPointUndo);
        DockPanel.SetDock(remove, Dock.Right);
        head.Children.Insert(0, remove);

        var card = new Border
        {
            Tag = BranchMarkerTag(lineId, ExitPort.OpenSlot(order)),
            Margin = new Thickness(0, 2, 0, 2),
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1.5),
            BorderBrush = BranchPalette.Accent(palette),
            Background = Brushes.Transparent,
            Child = new StackPanel
            {
                Children =
                {
                    head,
                    new TextBlock
                    {
                        Text = Ui.Dialogue_ThereSNowhereToVisit,
                        FontSize = Skin.Font(10),
                        Opacity = Skin.Dim(0.7),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0)
                    }
                }
            }
        };
        ToolTip.SetTip(card, Ui.Dialogue_RightClickToDeleteThis);

        card.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(card).Properties.IsRightButtonPressed)
            {
                return;
            }

            args.Handled = true;
            RemoveOpenDetour(node, lineId, order);
        };

        return card;
    }

    /// <summary>이어지지 않은 분기점 하나를 지운다 — 분기점 카드의 [✕] · 우클릭 · ＋ 메뉴가 같은 길이다.</summary>
    private void RemoveOpenDetour(DialogueNode node, string lineId, int position) =>
        UiGuard.Run(_session!, Ui.Dialogue_DeleteBranchPoint2, () =>
        {
            _session!.Editor.RemoveOpenDetour(node.Id, lineId, position);
            _session.SetStatus(Ui.Dialogue_DeletedUnconnectedBranchPoint);
        });

    private Control BuildBranchMarkerCard(
        DialogueNode node, string lineId, string targetId, int order, int count, bool lastConnected, int palette)
    {
        // 분기 대상은 늘 있다 — 저장 검증이 지킨다(old-shapes O8).
        string name = EpisodeNaming.ShownNameOf(_session!.Project, _session.Project.FindNode(targetId)!);

        // 두 줄이다 — 위는 무엇으로 갈라지는가(와 지우기), 아래는 지금 조건과 조건을 고르는 칸. 한 줄에 다 세우면 인스펙터 폭을
        // 넘어 단추가 밖으로 밀린다.
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        var head = new DockPanel { LastChildFill = true };
        var detail = new StackPanel { Spacing = 3, Margin = new Thickness(0, 2, 0, 0) };

        row.Children.Add(new TextBlock
        {
            // 여럿이면 차례를 붙인다 — 위에서부터 다녀온다.
            Text = count > 1 ? string.Format(Ui.Dialogue_ConditionalBranch, order + 1, count) : Ui.Dialogue_ConditionalBranch2,
            FontSize = Skin.Font(11),
            FontWeight = FontWeight.Bold,
            Foreground = BranchPalette.Accent(palette),
            VerticalAlignment = VerticalAlignment.Center
        });

        row.Children.Add(new TextBlock
        {
            Text = $"→ {name}",
            FontSize = Skin.Font(11),
            Opacity = Skin.Dim(0.8),
            VerticalAlignment = VerticalAlignment.Center
        });

        // 지금 걸린 조건 — 글로만 보인다. 고르는 곳은 그 분기 카드 위쪽이다(2026-11-17 소유자 — ⛔ 같은 날 아침에는 여기 칸이 있었다).
        BranchEntryCondition.State state = BranchEntryCondition.Read(_session!.Project, targetId);

        detail.Children.Add(new TextBlock
        {
            Text = BranchConditionNote.Sentence(state, BranchConditionNote.IsUndecided(_session.Project, targetId, state.Label)) + " " + (lastConnected
                ? Ui.Dialogue_VisitsThenContinuesFromThis
                : Ui.Dialogue_VisitsThenGoesToNext),
            FontSize = Skin.Font(10),
            Opacity = Skin.Dim(0.7),
            TextWrapping = TextWrapping.Wrap
        });

        Button remove = RemoveBranchButton(node, lineId, targetId);
        DockPanel.SetDock(remove, Dock.Right);
        head.Children.Add(remove);
        head.Children.Add(row);

        var card = new Border
        {
            Tag = BranchMarkerTag(lineId, targetId),
            Margin = new Thickness(0, 2, 0, 2),
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1.5),
            BorderBrush = BranchPalette.Accent(palette),
            Background = BranchPalette.Background(palette),
            Child = new StackPanel { Children = { head, detail } }
        };
        ToolTip.SetTip(card, Ui.Dialogue_RightClickToDeleteThis2);

        card.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(card).Properties.IsRightButtonPressed)
            {
                return;
            }

            args.Handled = true;
            RemoveBranch(node, lineId, targetId);
        };

        return card;
    }

    /// <summary>분기 카드 위쪽의 조건 고르는 칸이 지는 표.</summary>
    internal const string BranchConditionTag = "branch-card-condition";

    /// <summary>
    /// <b>분기 카드의 조건</b> — 대본 위쪽에 한 칸(2026-11-17 소유자: 분기는 무조건 들른 뒤 그 카드에서 <c>&lt;&lt;if&gt;&gt;</c>로 보므로,
    /// 조건은 그 카드에서 고르는 것이 직관적이다). 조건은 분기마다가 아니라 이 카드의 첫머리 하나다(<see cref="BranchEntryCondition"/>) —
    /// 어느 분기점에서 들러도 같다. 고르는 칸은 여기 한 곳이다 — ⛔ 부르는 카드의 분기 상자와 [대본] 분기 선 인스펙터의 칸은 같은 날 걷었다.
    /// 챕터 판의 분기 카드에만 선다(조건은 챕터의 것이다).
    /// </summary>
    private void FillBranchCondition(DialogueNode node)
    {
        BranchConditionHost.Children.Clear();
        BranchConditionHost.IsVisible =
            _session is not null && node.IsBranchCard;

        if (_session is null || !BranchConditionHost.IsVisible)
        {
            return;
        }

        BranchEntryCondition.State state = BranchEntryCondition.Read(_session.Project, node.Id);

        // 고르면 모델이 바뀌어 편집기가 다시 선다 — 설명 글도 따라온다.
        ComboBox pick = BranchConditionPicker.Create(_session, node.Id, BranchConditionTag);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        row.Children.Add(new TextBlock
        {
            Text = Ui.Dialogue_Condition,
            FontSize = Skin.Font(12),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(pick, 1);
        row.Children.Add(pick);

        BranchConditionHost.Children.Add(row);
        BranchConditionHost.Children.Add(new TextBlock
        {
            Text = BranchConditionNote.Sentence(state, BranchConditionNote.IsUndecided(_session.Project, node.Id, state.Label)) +
                   Ui.Dialogue_BranchIsAlwaysVisitedAnd,
            FontSize = Skin.Font(10),
            Opacity = Skin.Dim(0.7),
            TextWrapping = TextWrapping.Wrap
        });
    }


    /// <summary>그 줄의 「조건 분기」 카드 하나가 지는 표(줄과 다녀갈 카드) — 화면 테스트가 이것으로 찾는다.</summary>
    internal static string BranchMarkerTag(string lineId, string targetId) =>
        $"branch:{Vn.Authoring.Flow.ExitPort.DetourKey(lineId, targetId)}";

    /// <summary>
    /// [✕ 분기 지우기] — 분기 카드와 그 줄의 ＋ 메뉴가 같은 단추를 쓴다. 분기만 지운다 — 카드는 남는다.
    /// </summary>
    private Button RemoveBranchButton(DialogueNode node, string lineId, string targetId, string? label = null)
    {
        Button remove = SmallButton(label ?? Ui.Dialogue_DeleteBranch, () => RemoveBranch(node, lineId, targetId));
        ToolTip.SetTip(
            remove,
            Ui.Dialogue_DeletesOnlyThisBranchBranch);
        return remove;
    }

    /// <summary>그 분기 하나를 지운다 — [✕ 분기 지우기] · 분기 카드 우클릭 · ＋ 메뉴가 같은 길이다.</summary>
    private void RemoveBranch(DialogueNode node, string lineId, string targetId) =>
        UiGuard.Run(_session!, Ui.Dialogue_DeleteBranch2, () =>
        {
            if (_session!.Editor.RemoveBranchMarker(node.Id, lineId, targetId) is { } removal)
            {
                _session.SetStatus(removal.Describe());
            }
        });

    // ── ＋ (메타데이터 추가) ─────────────────────────────────────────────────

    /// <summary>
    /// 행 오른쪽의 ＋ — <b>이 줄</b>의 차림이다: 이 줄 앞의 분기, 줄 옮기기 · 지우기, 이 줄 아래에 줄 추가. 차림이 열린 채 ＋에서 Tab을 누르면
    /// [아래에 줄 추가]로 간다 — Enter면 새 줄이 서고 그 줄의 화자 칸에서 바로 쓴다(usability UX26). 항목은 전부 기존 데이터 구조와 기존 편집
    /// 컨트롤을 그대로 쓴다.
    /// </summary>
    private Button BuildPlusButton(DialogueNode node, ResolvedLine resolved)
    {
        var plus = new Button
        {
            Content = "＋",
            FontSize = Skin.Font(11),
            Padding = new Thickness(5, 1),
            MinHeight = 0,
            Margin = new Thickness(2, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { LineAddClass }
        };
        ToolTip.SetTip(plus, Ui.Dialogue_AddBranchBeforeThisLine);
        plus.Click += (_, _) => OpenLineMetaFlyout(plus, node, resolved);

        // 초점이 ＋에 남아 있으면(차림이 초점을 가져가지 않았다) Tab이 차림 밖 다음 줄로 간다 — 열린 차림의 [아래에 줄 추가]로 데려간다.
        plus.KeyDown += (_, args) =>
        {
            if (args.Key == Avalonia.Input.Key.Tab && args.KeyModifiers == Avalonia.Input.KeyModifiers.None &&
                ReferenceEquals(_lineMetaAnchor, plus) && _lineMetaFlyout is { IsOpen: true } && _lineMetaAddButton is { } add)
            {
                args.Handled = true;
                add.Focus(Avalonia.Input.NavigationMethod.Tab);
            }
        };
        return plus;
    }

    private void OpenLineMetaFlyout(
        Button anchor,
        DialogueNode node,
        ResolvedLine resolved)
    {
        var panel = new StackPanel { Spacing = 4, MinWidth = 250 };

        void Section(string title)
        {
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.6),
                Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 6, 0, 0)
            });
        }

        // 「분기 추가」 — <b>에피소드 카드에서</b> 선다. 분기 카드는 분기를 내지 않는다(usability UX27 · ProjectEditor.CanBranchFrom) —
        //    그 전의 파일에 남은 분기는 아래 목록에서 지운다.
        //
        // 조건을 걸지 않는다. 이 줄 <b>앞</b>에 아무것도 없는 카드가 끼고, 연출 그래프의
        //    카드에 포트가 하나 뚫리며, 다녀올 에피소드가 함께 서서 이미 이어진 상태가 된다.
        //    조건이 성립하든 말든 다녀오고, 다녀온 곳이 제 첫머리에서 보고 아니면 곧바로
        //    돌아온다 — "어디서 갈라지는가"만 짚는 것이 연출 그래프의 몫이기 때문이다.
        Section(Ui.Dialogue_Branch);

        // 이미 분기가 있어도 「＋ 분기 추가」가 선다 — 한 줄에 분기가 여럿일 수 있다.
        //    새 분기는 그 뒤에 서고 차례로 다녀온다. 있는 분기는 하나씩 지운다.
        IReadOnlyList<string> existing = resolved.Line.LineId is { Length: > 0 } markerLineId
            ? node.FindExtension(markerLineId)?.DetourTargetNodeIds ?? []
            : [];
        List<int> openPoints = resolved.Line.LineId is { Length: > 0 } openLineId
            ? [.. node.FindExtension(openLineId)?.OpenDetourPositions ?? []]
            : [];

        if (existing.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = existing.Count == 1
                    ? Ui.Dialogue_ThereSOneBranchBefore
                    : string.Format(Ui.Dialogue_ThereAreBranchesBeforeThis, existing.Count),
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.6),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 250
            });

            foreach (string target in existing)
            {
                string name = EpisodeNaming.ShownNameOf(_session!.Project, _session.Project.FindNode(target)!);
                panel.Children.Add(RemoveBranchButton(node, resolved.Line.LineId, target, string.Format(Ui.Dialogue_DeleteBranch3, name)));
            }
        }

        // 연결만 끊은 분기점 — 하나씩 지운다.
        foreach (int position in openPoints)
        {
            Button removeOpen = SmallButton(
                string.Format(Ui.Dialogue_DeleteUnconnectedBranchPointNo, position + 1), () => RemoveOpenDetour(node, resolved.Line.LineId, position));
            ToolTip.SetTip(removeOpen, Ui.Dialogue_DeletesUnconnectedBranchPointUndo);
            panel.Children.Add(removeOpen);
        }

        if (!_session!.Editor.CanBranchFrom(node.Id))
        {
            panel.Children.Add(new TextBlock
            {
                Text = Ui.Dialogue_BranchCardsDonTBranch,
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.65),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 250
            });
        }
        else
        {
            panel.Children.Add(BuildAddBranchButton(node, resolved));

            // 여기서 막는 것은 챕터의 것이다 — 선택지와 스탯변화는 간선.
            panel.Children.Add(new TextBlock
            {
                Text = Ui.Dialogue_ChapterEpisodeChoicesAndStat,
                FontSize = Skin.Font(10),
                Opacity = Skin.Dim(0.65),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 250
            });
        }

        // ⛔ 갈래 출구(이 갈래 끝에서 점프)는 2026-11-19에 차림에서(usability UX26), 이어 통째로(UX28) 걷혔다.

        // ▲▼✕ 줄 이동 · 삭제의 진입점 — 행에는 단추를 두지 않고 여기서 한다.
        // 줄 구성의 주인은 프로젝트라 모든 노드에서 선다.
        Section(Ui.Dialogue_Line);
        string nodeId = _nodeId ?? string.Empty;
        string lineId = resolved.Line.LineId;
        var lineRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        Button up = SmallButton(Ui.Dialogue_Up, () =>
            _session!.Editor.MoveScriptLine(nodeId, lineId, -1));
        Button down = SmallButton(Ui.Dialogue_Down, () =>
            _session!.Editor.MoveScriptLine(nodeId, lineId, 1));
        Button remove = SmallButton(Ui.Dialogue_Delete, () =>
            _session!.Editor.RetireScriptLine(nodeId, lineId));
        ToolTip.SetTip(remove, Ui.Dialogue_RemovesThisLineFromScript);
        lineRow.Children.Add(up);
        lineRow.Children.Add(down);
        lineRow.Children.Add(remove);
        panel.Children.Add(lineRow);

        // [아래에 줄 추가] — 차림 맨 아래. ＋에서 Tab이면 여기로 오고, 누르면 이 줄 아래에 새 줄이 서고 그 줄의 화자 칸으로 간다(usability UX26).
        var addBelow = new Button
        {
            Content = Ui.Dialogue_AddLineBelow2,
            FontSize = Skin.Font(11),
            Padding = new Thickness(8, 3),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Tag = LineMetaAddTag
        };
        ToolTip.SetTip(addBelow, Ui.Dialogue_AddsNewLineRightBelow);
        addBelow.Click += (_, _) => UiGuard.Run(_session, Ui.Dialogue_AddLine, () =>
        {
            _lineMetaFlyout?.Hide();
            SelectLine(lineId);
            AddLine();
        });
        panel.Children.Add(addBelow);

        // 분기 조건이 걸린 첫 줄은 지우거나 옮기지 않는다 — 첫 줄의 조건이 함께 사라지거나 따라 내려가 조건 구조가 깨진다.
        // 둘째 줄을 위로 올리는 것도 같다(조건 위로 올라선다). 글은 카드 글 칸에서 고친다 — 거기서는 조건 줄이 지켜진다.
        int row = node.Script.ActiveLines.ToList().FindIndex(line => string.Equals(line.Id, lineId, StringComparison.Ordinal));

        if (GuardsEntry(node) && row is 0 or 1)
        {
            foreach (Button guarded in row == 0 ? [down, remove] : new[] { up })
            {
                guarded.IsEnabled = false;
                ToolTip.SetTip(guarded, EntryGuardTip);
                ToolTip.SetShowOnDisabled(guarded, true);
            }
        }

        // 차림을 열면 초점이 그 안의 첫 단추로 든다 — 연 뒤 <b>첫 Tab</b>은 [아래에 줄 추가]로 간다(그다음부터는 보통의 Tab 차례).
        bool firstTab = true;
        panel.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (firstTab && args.Key == Avalonia.Input.Key.Tab && args.KeyModifiers == Avalonia.Input.KeyModifiers.None)
            {
                firstTab = false;
                args.Handled = true;
                addBelow.Focus(Avalonia.Input.NavigationMethod.Tab);
            }
        }, RoutingStrategies.Tunnel);

        LineMetaPanel = panel;
        _lineMetaAnchor = anchor;
        _lineMetaAddButton = addBelow;
        _lineMetaFlyout = new Flyout { Content = panel };
        _lineMetaFlyout.ShowAt(anchor);
    }

    /// <summary>＋ 차림의 「＋ 분기 추가」 — 에피소드 카드에만 선다(분기 카드는 분기를 내지 않는다 · UX27).</summary>
    private Button BuildAddBranchButton(DialogueNode node, ResolvedLine resolved)
    {
        var addBranch = new Button
        {
            Content = Ui.Dialogue_AddBranch,
            FontSize = Skin.Font(10),
            Padding = new Thickness(7, 2)
        };
        ToolTip.SetTip(
            addBranch,
            Ui.Dialogue_SplitsBeforeThisLineBranch +
            BranchConditionNote.Where + Ui.Dialogue_IfThereSAlreadyBranch);

        addBranch.Click += (_, _) =>
        {
            if (_building)
            {
                return;
            }

            // 새 분기에는 조건이 없다 — 만든 순간에 그 사실과 정하는 자리를 말한다.
            UiGuard.Run(_session!, Ui.Dialogue_AddBranch2, () =>
            {
                DialogueNode created = _session!.Editor.AddBranchMarker(node.Id, resolved.Line.LineId);
                _session.SetStatus(string.Format(Ui.Dialogue_AddedBranchForNowIt, created.Name, Josa.을를(created.Name), BranchConditionNote.Where));
            });
        };

        return addBranch;
    }

    /// <summary>줄 ＋ 차림의 [아래에 줄 추가] 단추의 표 — 시험이 찾는다.</summary>
    internal const string LineMetaAddTag = "line-meta-add";

    /// <summary>마지막으로 연 줄의 ＋ 차림 — 화면 시험이 그 안의 단추를 누르는 손잡이다(차림은 따로 뜨는 창이라 판에서 안 찾인다).</summary>
    internal StackPanel? LineMetaPanel { get; private set; }

    /// <summary>
    /// 대본에서 사라진 줄에 남은 조건 데이터. 자동으로 지우지 않으므로 눈에 보여야 한다.
    /// </summary>
    private void AddOrphanCards(DialogueNode node, DialogueScript script)
    {
        IReadOnlyList<OrphanLineExtension> orphans = script.Orphans
            .Where(orphan => !orphan.Extension.IsEmpty)
            .ToArray();

        if (orphans.Count == 0)
        {
            return;
        }

        LineHost.Children.Add(new TextBlock
        {
            Text = Ui.Dialogue_ConditionLeftOnLineRemoved,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 10, 0, 0)
        });

        foreach (OrphanLineExtension orphan in orphans)
        {
            var content = new StackPanel { Spacing = 3 };

            content.Children.Add(new TextBlock
            {
                Text = orphan.LastKnownText is { } text
                    ? string.Format(Ui.Dialogue_LastContent, text.Speaker, text.Text)
                    : Ui.Dialogue_ThisLineIsnTIn,
                FontSize = Skin.Font(11),
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(SmallButton(
                Ui.Dialogue_DeleteCondition,
                () => _session!.Editor.SetLineTransition(node.Id, orphan.Extension.LineId, null)));

            LineHost.Children.Add(new Border
            {
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(6),
                Background = Skin.Danger.Brush(20),
                Child = content
            });
        }
    }

    // ── 그 밖 ───────────────────────────────────────────────────────────────

    private void ShowProblems(DialogueFlow flow)
    {
        if (flow.Problems.Count == 0)
        {
            ProblemText.IsVisible = false;
            return;
        }

        ProblemText.IsVisible = true;
        ProblemText.Text = string.Join(
            Environment.NewLine,
            flow.Problems.Select(problem => "• " + problem.Message).Distinct());
    }

    private void CommitName()
    {
        if (_building || _session is null || _nodeId is null)
        {
            return;
        }

        // 이름 칸은 보이는 제목이다 — 카드 머리의 제목 칸과 같은 길(CardTitles · ID는 그대로).
        if (CardTitles.Rename(_session, _nodeId, NameBox.Text))
        {
            // 제목은 카드의 곁 자료라 인스펙터를 다시 세우지 않는다(usability UX15) — 비웠으면 칸이 ID를 보이게 여기서 맞춘다.
            RefreshName(force: true);
        }
    }

    /// <summary>
    /// 이름 칸을 지금 제목으로 — 카드 머리에서 제목을 고쳤을 때 셸이 부른다(제목만 바뀌면 인스펙터를 다시 세우지 않는다 · usability UX15). 이 칸에서 쓰는
    /// 중이면 건드리지 않는다.
    /// </summary>
    internal void RefreshName(bool force = false)
    {
        if (_session?.Project.FindDialogue(_nodeId) is { } node && (force || !NameBox.IsFocused))
        {
            NameBox.Text = EpisodeNaming.ShownNameOf(_session.Project, node);
        }
    }

    /// <summary>분기로 다녀오는 카드의 첫머리 조건 — 그 갈래를 여는 줄(첫 줄)과 그 분기의 색 번호. 아니면 null.</summary>
    private (string OpenLineId, int Palette)? _entryGate;

    /// <summary>
    /// 조건 갈래를 칠할 색 번호 — 분기 카드의 첫머리 조건이면 그 분기의 색(판의 분기 선 · 카드 표와 같다), 아니면 카드 안
    /// 갈래 차례의 색이다.
    /// </summary>
    private int PaletteOf(ConditionBranch branch) =>
        _entryGate is { } gate && string.Equals(branch.OpenLineId, gate.OpenLineId, StringComparison.Ordinal)
            ? gate.Palette
            : branch.PaletteIndex;

    /// <summary>분기 조건이 걸린 첫 줄의 막힌 손짓이 하는 말.</summary>
    private static string EntryGuardTip =>
        Ui.Dialogue_ThisIsFirstLineUnder;

    /// <summary>첫머리 조건을 지켜야 하는 분기 카드인가(<see cref="BranchEntryCondition.IsGuarded"/>).</summary>
    private bool GuardsEntry(DialogueNode node) =>
        _session is { } session && BranchEntryCondition.IsGuarded(session.Project, node.Id);

    private static Button SmallButton(string glyph, Action action)
    {
        var button = new Button
        {
            Content = glyph,
            FontSize = Skin.Font(10),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(2, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        button.Click += (_, _) => action();
        return button;
    }
}
