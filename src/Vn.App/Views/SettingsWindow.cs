using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Vn.App.Services;
using Vn.Authoring.Chapters;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;
using Vn.Authoring.Text;

namespace Vn.App.Views;

/// <summary>
/// <b>[⚙ 설정] 창</b> (2026-11-17 · <c>docs/work-orders/settings-orders.md</c> C3) — 앞으로 필요할 설정을 한 창에 모은다.
///
/// 묶음은 넷이다 — ⛔ 묶음마다 어디에 저장되는가를 머리에 적던 것(C3)은 2026-11-18에 걷었다(소유자 — 「이런 설명은 필요없어」 ·
/// <c>docs/work-orders/settings-wording-orders.md</c> Z2). [이 프로젝트](「[대본] 판 크기」 — 프로젝트 파일)와
/// 「내보내기 (복제할 폴더 선택)」(형식마다 보낼 곳 — 프로젝트 폴더의 <c>runtime.target.json</c>, 2026-11-17 · <c>docs/work-orders/export-destinations-orders.md</c>
/// G2 · ⛔ 위 띠의 [🎮 게임 폴더…]를 여기로 옮겼다), [단축키](읽기만 — <see cref="Shortcuts"/> · K2), [대본 글 칸](이 PC의 앱 설정 — 카드 글 칸의 글꼴 ·
/// 글자 크기 · 줄 간격, <see cref="ScriptFont"/> · writer-comfort WC5), [기타](이 PC의 앱 설정 — [대본] 미니맵 감추기 · 2026-11-18 소유자 ·
/// <c>docs/work-orders/side-fold-settings-orders.md</c> K3, 설명 간소화 · 같은 날 brief-guides G1, 휠로 판 옮기기 · 2026-11-19 writer-app WA3).
///
/// [확인]이 반영하고(<see cref="Confirm"/> — 판 크기는 <see cref="Vn.Authoring.Editing.ProjectEditor.SetBoardSize"/> 한 번, 되돌리기 한
/// 칸 · 보낼 곳은 그 파일에 바로), [취소] · Esc는 버린다. 「대본 글 칸」과 「기타」는 [확인]을 기다리지 않고 고치는 즉시 앱 설정에 반영한다 — [취소]로
/// 돌아가지 않는다(2026-11-18 소유자 · <c>docs/work-orders/inspector-boxes-pan-orders.md</c> J3). 줄여서 카드가 움직이면 [확인] 전에 미리 말한다(<see cref="BoardResize.Plan"/> — 바꾸기와 같은 계산).
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly AuthoringSession _session;

    public SettingsWindow(AuthoringSession session)
    {
        _session = session;
        _destinations = session.Destinations;

        Title = "설정";
        Width = 560;

        // 크기 — 끌어서 바꾼다(2026-11-19 소유자 · usability UX1 · ⛔ 그 전에는 고정이었다). 처음 세로는 내용에 맞추되 화면을 넘지 않는다
        // (FitScreen이 MaxHeight를 준다) — 넘는 만큼은 묶음들이 스크롤한다. 사람이 끌어 바꾸면 Avalonia가 내용 맞춤을 끈다 — 그 뒤로는
        // 묶음을 펴도 창이 자라지 않고 스크롤한다.
        SizeToContent = SizeToContent.Height;
        CanResize = true;
        MinWidth = 420;
        MinHeight = 320;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BoardSize board = session.Project.Board;
        BoardWidthBox = SideBox(board.Width);
        BoardHeightBox = SideBox(board.Height);
        BoardNote = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, IsVisible = false };

        DefaultButton = new Button
        {
            Content = $"기본값으로 ({BoardSize.Default.Width:N0} × {BoardSize.Default.Height:N0})",
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0)
        };
        DefaultButton.Click += (_, _) =>
        {
            BoardWidthBox.Value = (decimal)BoardSize.Default.Width;
            BoardHeightBox.Value = (decimal)BoardSize.Default.Height;
        };

        // 「미니맵 감추기」 — 처음 꺼져 있다(지도가 보인다), 켜면 걷힌다(2026-11-18 소유자 · settings-wording Z1 — ⛔ 그 전에는 「[대본] 탭 미니맵
        // 보이기」가 처음 켜져 있었다). 앱 설정은 그대로 ShowMinimap이고 체크가 거꾸로 비춘다.
        HideMinimapCheck = new CheckBox
        {
            Content = "미니맵 감추기",
            FontSize = 12,
            IsChecked = !session.ShowMinimap
        };
        ToolTip.SetTip(HideMinimapCheck, "[대본] 판 오른쪽 아래의 전체 지도를 감춥니다. 지도만 걷히고 [전체 · 50 · 100 · 150] 배율 줄은 남습니다.");

        // 설명 간소화 — 오른쪽 기둥의 안내글을 걷고 「없습니다 — …」 줄은 상태만 남긴다(2026-11-18 소유자 · brief-guides G1). 누르는 즉시.
        BriefGuidesCheck = new CheckBox
        {
            Content = "설명 간소화",
            FontSize = 12,
            IsChecked = GuideMode.Brief
        };
        ToolTip.SetTip(BriefGuidesCheck,
            "켜면 인스펙터 · 화자 · 조건 및 스탯의 쓰는 법 안내글을 걷고, 「스탯이 없습니다 — 아래 칸에 키를 적고 [＋ 추가]로 만듭니다」 같은 줄은 앞부분만 남깁니다. " +
            "칸 이름 · 제목 · 경고는 그대로입니다.");
        BriefGuidesCheck.IsCheckedChanged += (_, _) => UiGuard.Run(_session, "설명 간소화", () =>
            GuideMode.Set(BriefGuidesCheck.IsChecked == true));

        // 누르는 즉시 판에 반영한다(J3 · ⛔ 그 전에는 [확인]을 기다렸다).
        HideMinimapCheck.IsCheckedChanged += (_, _) => UiGuard.Run(_session, "미니맵", () =>
            _session.SetShowMinimap(HideMinimapCheck.IsChecked != true));

        // 휠로 판 옮기기 — 두 판([구성] · [대본])의 휠이 하는 일을 가른다(2026-11-19 소유자 「트랙패드 수정」 · writer-app WA3). 맥만 처음부터 켜져 있다.
        // 누르는 즉시 — 다음 휠부터 따른다.
        WheelPansCheck = new CheckBox
        {
            Content = "휠로 판 옮기기",
            FontSize = 12,
            IsChecked = BoardWheel.Pans
        };
        ToolTip.SetTip(WheelPansCheck,
            "켜면 판 위의 휠(터치패드는 두 손가락 쓸기)이 판을 옮기고, 확대 · 축소는 Ctrl(맥은 ⌘)을 누른 채 휠입니다 — 맥 트랙패드는 두 손가락으로 집어도 됩니다. " +
            "끄면 휠이 확대 · 축소입니다. 가운데 단추 끌기는 어느 쪽이든 판을 옮깁니다. 터치패드를 쓰면 켜 두세요.");
        WheelPansCheck.IsCheckedChanged += (_, _) => UiGuard.Run(_session, "휠로 판 옮기기", () =>
            BoardWheel.Set(WheelPansCheck.IsChecked == true));

        ScriptFontCombo = new ComboBox { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = FontNames() };
        ScriptFontSizeBox = new NumericUpDown
        {
            Minimum = (decimal)ScriptFont.MinSize,
            Maximum = (decimal)ScriptFont.MaxSize,
            Increment = 1,
            FormatString = "0",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ScriptLineSpacingBox = new NumericUpDown
        {
            Minimum = ScriptFont.MinLineSpacing,
            Maximum = ScriptFont.MaxLineSpacing,
            Increment = 10,
            FormatString = "0",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ToolTip.SetTip(ScriptLineSpacingBox, "글자 크기에 대한 % — 100이면 줄이 붙고, 160이면 워드 프로세서의 「줄 간격 160%」와 같습니다.");
        ScriptPreview = new TextBox
        {
            Text = "윌로: 오늘따라 바람이 차네.\n라루: 그러게. 겉옷 챙겨 올 걸 그랬어.\n두 사람은 말없이 언덕을 오른다.",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 170,   // 세 줄이 다 보이게 글에 맞춰 자라되, 아주 큰 글자에서는 칸 안에서 흐른다(미리 보기가 묶음을 다 먹지 않게)
            Padding = new Thickness(4, 3)
        };
        ToolTip.SetTip(ScriptPreview, "미리 보기 — [대본] 판의 카드 글 칸이 이렇게 보입니다.");
        ScriptFontDefaultButton = new Button
        {
            Content = $"기본값으로 (기본 글꼴 · {ScriptFont.DefaultSize:0} · {ScriptFont.DefaultLineSpacing}%)",
            FontSize = 11
        };
        FillScriptFont();

        // 고치는 즉시 판의 카드 글 칸에 반영한다 — 「기타」의 체크와 같은 규칙([취소]로 돌아가지 않는다 · 되돌리기 없음 — 이 PC의 앱 설정이다).
        ScriptFontCombo.SelectionChanged += (_, _) => OnScriptFontEdited();
        ScriptFontSizeBox.ValueChanged += (_, _) => OnScriptFontEdited();
        ScriptLineSpacingBox.ValueChanged += (_, _) => OnScriptFontEdited();
        ScriptFontDefaultButton.Click += (_, _) => UiGuard.Run(_session, "대본 글 칸", () =>
        {
            ScriptFont.Reset();
            FillScriptFont();
        });

        ConfirmButton = new Button { Content = "확인", IsDefault = true, MinWidth = 72, HorizontalContentAlignment = HorizontalAlignment.Center };
        CancelButton = new Button { Content = "취소", IsCancel = true, MinWidth = 72, HorizontalContentAlignment = HorizontalAlignment.Center };
        ConfirmButton.Click += (_, _) => Confirm();
        CancelButton.Click += (_, _) => Close(false);

        BoardWidthBox.ValueChanged += (_, _) => OnSideChanged(BoardWidthBox);
        BoardHeightBox.ValueChanged += (_, _) => OnSideChanged(BoardHeightBox);

        // 「내보내기」는 접는다 — 자주 고칠 것이 아니다(2026-11-18 소유자 — 「내보내기의 내용은 접을 수 있도록」). 머리는 「내보내기 (복제할 폴더
        // 선택)」 한 글이다(같은 날 소유자 — 「조금 더 직관적」 · Z2, ⛔ 그 전에는 지금 보내는 형식을 말했다).
        ExportGroup = new Expander
        {
            Header = ExportHeader,
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        // 무엇이 「복제」인지만 말한다 — 저장 위치(runtime.target.json)는 걷었다(Z2).
                        Text = "작업 폴더(exported/)에는 늘 쓰고, 고른 폴더에는 저장(Ctrl+S)할 때 복제합니다.",
                        FontSize = 11,
                        Opacity = 0.6,
                        TextWrapping = TextWrapping.Wrap
                    },
                    ExportSection()
                }
            }
        };

        // 「단축키」 — 읽기만 하는 표다. 접어 둔다. 펼치면 길다 — 창 전체가 스크롤한다(UX1 · ⛔ 그 전에는 표가 제 안에서 380까지만 서고
        // 스크롤했다(side-fold K2) — 창이 스크롤하게 되면서 스크롤 안의 스크롤이 되어 걷었다).
        ShortcutGroup = new Expander
        {
            Header = new TextBlock { Text = "단축키", FontWeight = FontWeight.SemiBold, FontSize = 14 },
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = ShortcutSection()
        };

        // 묶음들은 스크롤하고 [취소] · [확인]은 아래에 늘 선다(UX1) — 창을 줄이거나 묶음을 펴도 단추가 화면 밖으로 나가지 않는다.
        // 오른쪽 여백 20 — 스크롤 막대가 묶음의 테두리를 덮지 않게.
        Body = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Margin = new Thickness(16, 16, 20, 8),
                Spacing = 12,
                Children =
                {
                    Group("이 프로젝트", BoardSection()),
                    ExportGroup,   // 접는 묶음은 제 테두리가 있다
                    ShortcutGroup,
                    Group("대본 글 칸", ScriptFontSection()),
                    Group("기타", new StackPanel { Spacing = 4, Children = { HideMinimapCheck, BriefGuidesCheck, WheelPansCheck } })
                }
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 8, 16, 16),
            Spacing = 8,
            Children = { CancelButton, ConfirmButton }
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        Content = new DockPanel { Children = { buttons, Body } };
    }

    /// <summary>
    /// 처음 세로가 화면을 넘지 않게 한다 — <paramref name="workingHeight"/>는 그 화면의 작업 영역 높이(작업 표시줄을 뺀 것 · 화면 단위가 아니라
    /// 앱의 단위). 열기 전에 부른다(가운데에 놓을 때 그 크기로 놓는다). 끌어서도 그보다 크게 하지 못한다 — 화면보다 큰 창은 쓸 데가 없다.
    /// </summary>
    internal void FitScreen(double workingHeight)
    {
        MaxHeight = Math.Max(MinHeight, workingHeight - ScreenMargin);
    }

    /// <summary>화면 높이에서 남기는 몫 — 창의 제목 줄 · 그림자.</summary>
    private const double ScreenMargin = 48;

    // ── 시험 손잡이 ───────────────────────────────────────────────────────────

    internal NumericUpDown BoardWidthBox { get; }

    internal NumericUpDown BoardHeightBox { get; }

    /// <summary>미리 알림 — 줄여서 카드가 움직이거나, 비켜 서느라 판이 적은 값보다 커질 때만 보인다.</summary>
    internal TextBlock BoardNote { get; }

    internal Button DefaultButton { get; }

    /// <summary>「내보내기」 묶음 — 접고 편다(처음에는 접혀 있다).</summary>
    internal Expander ExportGroup { get; }

    /// <summary>「내보내기」 머리의 글 — 무엇을 고르는 묶음인지 말한다(Z2).</summary>
    internal const string ExportTitle = "내보내기 (복제할 폴더 선택)";

    /// <summary>「내보내기」 머리 — <see cref="ExportTitle"/>.</summary>
    internal TextBlock ExportHeader { get; } = new() { Text = ExportTitle, FontWeight = FontWeight.SemiBold, FontSize = 14 };

    /// <summary>「단축키」 묶음 — 접고 편다(처음에는 접혀 있다). 읽기만 한다.</summary>
    internal Expander ShortcutGroup { get; }

    /// <summary>「대본 글 칸」의 글꼴 — 맨 위는 「기본」(테마의 글꼴), 그 아래 이 PC의 글꼴들.</summary>
    internal ComboBox ScriptFontCombo { get; }

    /// <summary>「대본 글 칸」의 글자 크기.</summary>
    internal NumericUpDown ScriptFontSizeBox { get; }

    /// <summary>「대본 글 칸」의 줄 간격 — 글자 크기에 대한 %.</summary>
    internal NumericUpDown ScriptLineSpacingBox { get; }

    /// <summary>「대본 글 칸」의 미리 보기 — 카드 글 칸과 같은 설정을 입는다.</summary>
    internal TextBox ScriptPreview { get; }

    internal Button ScriptFontDefaultButton { get; }

    /// <summary>글꼴 칸의 맨 위 — 테마의 글꼴을 그대로 쓴다.</summary>
    internal const string DefaultFontName = "기본";

    /// <summary>「기타」의 「설명 간소화」 — 누르는 즉시 반영한다(<see cref="GuideMode"/>).</summary>
    internal CheckBox BriefGuidesCheck { get; }

    /// <summary>「기타」의 「미니맵 감추기」 — 켜면 [대본] 판의 지도가 걷힌다. 누르는 즉시 반영한다.</summary>
    internal CheckBox HideMinimapCheck { get; }

    /// <summary>「기타」의 「휠로 판 옮기기」 — 켜면 두 판의 휠이 판을 옮긴다(<see cref="BoardWheel"/>). 누르는 즉시 반영한다.</summary>
    internal CheckBox WheelPansCheck { get; }

    /// <summary>묶음들 — [취소] · [확인]을 뺀 창의 몸통이 이 안에서 스크롤한다(UX1).</summary>
    internal ScrollViewer Body { get; }

    internal Button ConfirmButton { get; }

    internal Button CancelButton { get; }

    /// <summary>형식마다 보낼 곳의 경로 칸 — 절대 경로가 보인다. 비면 보내지 않는다.</summary>
    internal TextBox DestinationBox(ExportFormat format) => _destinationBoxes[format];

    /// <summary>형식마다 경로 칸 아래의 알림 — 없는 폴더일 때만 보인다.</summary>
    internal TextBlock MissingNote(ExportFormat format) => _missingNotes[format];

    /// <summary>
    /// 경로 칸의 글을 보낼 곳으로 — Enter · 초점 이탈 · [확인]. 비우면 보내지 않는다. 상대 경로를 적으면 프로젝트 폴더 기준이다. 지금과 같으면 아무것도 하지
    /// 않는다. 받지 않으면 그 까닭을 내고 칸은 지금 값으로 돌아간다.
    /// </summary>
    internal string? ApplyBox(ExportFormat format)
    {
        if (_session.ProjectPath is not { } projectPath)
        {
            return null;
        }

        string typed = (_destinationBoxes[format].Text ?? string.Empty).Trim().Trim('"');
        string? current = _destinations.FolderOf(format) is { } written ? ExportDestinations.ResolveFolder(projectPath, written) : null;
        string? wanted;

        try
        {
            wanted = typed.Length == 0 ? null : ExportDestinations.ResolveFolder(projectPath, typed);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            RefreshDestinations();
            return ShowRefusal($"경로를 읽지 못했습니다 — {typed}");
        }

        if (string.Equals(wanted, current, StringComparison.OrdinalIgnoreCase))
        {
            RefreshDestinations();   // 같은 곳을 다르게 적었다(상대 경로 · 따옴표) — 칸을 절대 경로로 맞춘다
            return null;
        }

        return SetDestination(format, wanted);
    }

    /// <summary>보낼 곳을 받지 않은 까닭 — 받지 않았을 때만 보인다.</summary>
    internal TextBlock ExportNote { get; } = new()
    {
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
        IsVisible = false,
        Foreground = new SolidColorBrush(Color.Parse("#B9770E"))
    };

    /// <summary>
    /// 그 형식의 보낼 곳을 정한다(폴더 고르기 창이 고른 절대 경로 · 비우면 끈다) — [확인] 전까지는 창 안에서만. 받지 않으면 그 까닭을 내고
    /// <see cref="ExportNote"/>에 보인다: <b>대사 Yarn은 게임 폴더(진행 JSON의 보낼 곳)와 같은 곳으로 보내지 않는다</b> — 게임의 Yarn 프로젝트가
    /// compositor의 연출 든 Yarn과 같은 노드 이름을 둘 읽어 컴파일이 깨진다.
    /// </summary>
    internal string? SetDestination(ExportFormat format, string? absoluteFolder)
    {
        if (_session.ProjectPath is not { } projectPath)
        {
            return "프로젝트를 먼저 저장해야 복제할 폴더를 정할 수 있습니다.";
        }

        ExportDestinations wanted = _destinations.With(
            format, absoluteFolder is null ? null : ExportDestinations.Relativize(projectPath, absoluteFolder));

        string? refusal = SameFolder(wanted, projectPath)
            ? "대사 Yarn은 게임 폴더(진행 JSON을 복제할 폴더)와 같은 곳으로 복제하지 않습니다 — 게임의 Yarn 프로젝트가 compositor의 연출 든 Yarn과 " +
              "같은 노드 이름을 둘 읽어 컴파일이 깨집니다."
            : null;

        if (refusal is null)
        {
            _destinations = wanted;
        }

        RefreshDestinations();   // 받지 않았으면 칸이 지금 값으로 돌아간다
        return ShowRefusal(refusal);
    }

    /// <summary>받지 않은 까닭을 보인다(없으면 걷는다) — 그 까닭을 돌려준다.</summary>
    private string? ShowRefusal(string? refusal)
    {
        ExportNote.Text = refusal ?? string.Empty;
        ExportNote.IsVisible = refusal is not null;
        return refusal;
    }

    /// <summary>[확인] — 판 크기를 바꾸고(같으면 아무것도 적지 않는다), 보낼 곳이 바뀌었으면 그 파일에 적고 창을 닫는다. 「기타」는 누를 때 이미 반영했다.</summary>
    internal void Confirm()
    {
        // 경로 칸에 적다 만 글도 반영한다 — 받지 않은 것이 있으면 창을 닫지 않고 까닭을 보인다.
        foreach (ExportFormat format in ExportDestinations.Formats)
        {
            if (ApplyBox(format) is not null)
            {
                ExportGroup.IsExpanded = true;
                return;
            }
        }

        (double width, double height) = WantedBoard();
        UiGuard.Run(_session, "설정", () =>
        {
            _session.Editor.SetBoardSize(width, height);

            if (_session.ProjectPath is { } projectPath && _destinations != _session.Destinations)
            {
                ExportDestinations.SaveBeside(projectPath, _destinations);
                _session.ReloadDestinations();
                _session.SetStatus(_destinations.IsEmpty
                    ? "복제할 폴더를 모두 껐습니다 — exported/에만 냅니다."
                    : "복제할 폴더를 바꿨습니다 — 다음 저장(Ctrl+S)부터 거기로 복제합니다.");
            }
        });
        Close(true);
    }

    // ── 「내보내기」 ───────────────────────────────────────────────────────────

    private ExportDestinations _destinations;

    private readonly Dictionary<ExportFormat, TextBox> _destinationBoxes = [];

    private readonly Dictionary<ExportFormat, TextBlock> _missingNotes = [];

    private readonly Dictionary<ExportFormat, HyperlinkButton> _formatLinks = [];

    /// <summary>형식 이름의 고리 — 누르면 작업 폴더에서 그 파일을 보인다.</summary>
    internal HyperlinkButton FormatLink(ExportFormat format) => _formatLinks[format];

    /// <summary>작업 폴더에서 보이기 — 시험이 바꿔 끼워 무엇을 보였는지 잰다(탐색기를 띄우지 않고).</summary>
    internal Action<string> Reveal { get; set; } = WorkFolder.Reveal;

    /// <summary>
    /// 그 형식의 파일을 작업 폴더에서 보인다 — 진행 JSON은 지금 챕터의 파일을 고른 채, 대사 Yarn은 <c>yarn/</c> 폴더를 연다
    /// (<see cref="WorkFolder.TargetOf"/>). 아직 나간 것이 없으면 그렇다고 말한다.
    /// </summary>
    internal void ShowInWorkFolder(ExportFormat format)
    {
        string? target = _session.ProjectPath is { } projectPath
            ? WorkFolder.TargetOf(projectPath, format, _session.ActiveFile?.Name)
            : null;

        if (target is null)
        {
            string label = ExportDestinations.LabelOf(format);
            ExportNote.Text = _session.ProjectPath is null
                ? "프로젝트를 먼저 저장해야 작업 폴더가 정해집니다."
                : $"아직 작업 폴더에 나간 {label}{Josa.이가(label)} 없습니다 — 검증을 통과한 챕터가 생기면 저절로 나갑니다.";
            ExportNote.IsVisible = true;
            return;
        }

        ExportNote.IsVisible = false;
        Reveal(target);
    }

    /// <summary>형식마다 무엇인가 — 형식 이름의 툴팁.</summary>
    private static string WhatIs(ExportFormat format) => format switch
    {
        ExportFormat.Progression =>
            $"챕터마다 하나 — 에피소드 · 선택지 · 조건이 든 게임 진행 파일({{챕터}}{ChapterExportService.ExportFileSuffix}). 게임(런타임)이 싣습니다. " +
            "복제할 폴더는 게임의 ChapterProgression 폴더입니다. 누르면 작업 폴더에서 지금 챕터의 파일을 보여 줍니다.",
        _ =>
            $"장면마다 하나 — 연출 없이 대사 · 조건 · 분기만 든 Yarn({ChapterYarnExporter.FolderName}/{{챕터}}_{{장면}}.yarn). 대사만으로 굴러가는지 볼 때 " +
            $"씁니다. 누르면 작업 폴더의 {ChapterYarnExporter.FolderName}/ 폴더를 엽니다."
    };

    private Control ExportSection()
    {
        var panel = new StackPanel { Spacing = 6 };

        if (_session.ProjectPath is null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "프로젝트를 먼저 저장해야 복제할 폴더를 정할 수 있습니다(경로는 프로젝트 폴더 기준으로 적힙니다).",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 4,
            IsEnabled = _session.ProjectPath is not null
        };

        int row = 0;

        foreach (ExportFormat format in ExportDestinations.Formats)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            // 형식 이름은 고리다 — 누르면 작업 폴더에서 그 형식의 파일을 보인다(2026-11-18 소유자 — 「뭐가 뭔지 모르겠으니 … 클릭하면 작업 폴더가
            // 열리면서 해당하는 파일이 선택되거나 열리도록」). 무엇인지는 툴팁이 말한다.
            var label = new HyperlinkButton
            {
                Content = ExportDestinations.LabelOf(format),
                FontSize = 12,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = $"export-show:{format}"
            };
            ToolTip.SetTip(label, WhatIs(format));
            label.Click += (_, _) => UiGuard.Run(_session, "작업 폴더에서 보기", () => ShowInWorkFolder(format));
            _formatLinks[format] = label;

            // 보낼 곳은 탐색기의 주소 칸처럼 — 경로가 보이고, 적거나 붙여 넣고, [찾아보기…]로 고른다(2026-11-18 소유자 — 「(보내지 않음)으로는 뭐가뭔지
            // 잘 이해가 안 가니, 다른 윈도우 탐색기처럼 경로를 연결하는 형식을」). 비우면 보내지 않는다 — 자리표시가 그렇게 말하고, 칸 안의 ✕가 비운다.
            // ⛔ 그 전에는 상대 경로 글(비면 「(보내지 않음)」) · [고르기…] · [✕]였다.
            var box = new TextBox
            {
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                PlaceholderText = format == ExportFormat.Progression
                    ? "복제하지 않음 — 게임의 ChapterProgression 폴더를 찾아보거나 경로를 붙여 넣으세요"
                    : "복제하지 않음 — 복제할 폴더를 찾아보거나 경로를 붙여 넣으세요",
                Tag = $"export-path:{format}"
            };
            box.Classes.Add("clearButton");
            box.LostFocus += (_, _) => ApplyBox(format);
            box.KeyDown += (_, args) =>
            {
                if (args.Key == Avalonia.Input.Key.Enter)
                {
                    args.Handled = true;
                    ApplyBox(format);
                }
            };

            var browse = new Button { Content = "찾아보기…", FontSize = 11, Tag = $"export-browse:{format}" };
            browse.Click += async (_, _) =>
            {
                if (await PickFolderAsync($"복제할 폴더 — {ExportDestinations.LabelOf(format)}", format) is { } folder)
                {
                    SetDestination(format, folder);
                }
            };

            // 없는 폴더는 칸 아래에 알린다(만들지 않는다).
            var missing = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                IsVisible = false,
                Foreground = new SolidColorBrush(Color.Parse("#B9770E"))
            };

            _destinationBoxes[format] = box;
            _missingNotes[format] = missing;
            Place(grid, label, row, 0);
            Place(grid, box, row, 1);
            Place(grid, browse, row, 2);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Place(grid, missing, row + 1, 1);
            Grid.SetColumnSpan(missing, 2);
            row += 2;
        }

        panel.Children.Add(grid);
        panel.Children.Add(new TextBlock
        {
            Text = "진행 JSON을 복제할 폴더는 런타임의 ChapterProgression 폴더(게임 폴더)이고 compositor도 이 칸을 읽어 Yarn을 씁니다. 대사 Yarn은 게임 " +
                   "폴더와 같은 곳으로 복제하지 않습니다(연출 든 Yarn과 노드 이름이 겹칩니다). 없는 폴더는 만들지 않습니다.",
            FontSize = 11,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(ExportNote);

        RefreshDestinations();
        return panel;
    }

    private void RefreshDestinations()
    {
        foreach ((ExportFormat format, TextBox box) in _destinationBoxes)
        {
            string? written = _destinations.FolderOf(format);
            string? full = written is not null && _session.ProjectPath is { } project ? ExportDestinations.ResolveFolder(project, written) : null;
            bool missing = full is not null && !Directory.Exists(full);

            box.Text = full ?? string.Empty;
            ToolTip.SetTip(box, full);   // 긴 경로는 칸에서 잘린다 — 다 보이게
            _missingNotes[format].Text = missing ? "⚠ 없는 폴더입니다 — 만들지 않습니다. 폴더를 만든 뒤 다시 찾아보세요." : string.Empty;
            _missingNotes[format].IsVisible = missing;
        }
    }

    /// <summary>대사 Yarn이 게임 폴더와 같은 곳인가 — 둘 다 정했을 때만.</summary>
    private static bool SameFolder(ExportDestinations destinations, string projectPath) =>
        destinations.Resolve(projectPath, ExportFormat.Progression) is { } game &&
        destinations.Resolve(projectPath, ExportFormat.DialogueYarn) is { } yarn &&
        string.Equals(
            game.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            yarn.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private async Task<string?> PickFolderAsync(string title, ExportFormat format)
    {
        if (!StorageProvider.CanPickFolder)
        {
            ExportNote.Text = "이 환경에서는 폴더 선택 창을 열 수 없습니다.";
            ExportNote.IsVisible = true;
            return null;
        }

        // 지금 보낼 곳(있으면)에서 연다 — 탐색기처럼 지금 자리에서 시작한다.
        string? current = _destinationBoxes[format].Text is { Length: > 0 } typed && Directory.Exists(typed) ? typed : null;
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                SuggestedStartLocation = current is null ? null : await StorageProvider.TryGetFolderFromPathAsync(current)
            });

        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }

    private static void Place(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    // ── 「[대본] 판 크기」 ─────────────────────────────────────────────────────

    private Control BoardSection()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 6
        };

        AddRow(grid, 0, "가로", BoardWidthBox);
        AddRow(grid, 1, "세로", BoardHeightBox);

        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "[대본] 판 크기", FontWeight = FontWeight.SemiBold, FontSize = 12 },
                new TextBlock
                {
                    Text = $"모든 챕터의 카드가 한 판에 섭니다. 대본이 많아지면 늘리세요. 한 변 {BoardSize.Minimum:N0} ~ {BoardSize.Maximum:N0}, " +
                           "왼쪽 위에서 오른쪽 · 아래로 늘어납니다. 줄여서 판 밖으로 나가는 카드가 있으면 카드 전체의 자리를 같은 비율로 줄여 판 안에 들입니다.",
                    FontSize = 11,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap
                },
                grid,
                DefaultButton,
                BoardNote
            }
        };
    }

    /// <summary>범위 밖의 값은 칸에서 바로 들인다 — 칸이 「100,000」을 보이는데 판은 60,000으로 서면 사람이 속는다.</summary>
    private void OnSideChanged(NumericUpDown box)
    {
        if (box.Value is { } value && (value < box.Minimum || value > box.Maximum))
        {
            box.Value = Math.Clamp(value, box.Minimum, box.Maximum);   // 다시 ValueChanged가 와서 알림을 고친다
            return;
        }

        RefreshBoardNote();
    }

    private void RefreshBoardNote()
    {
        (double width, double height) = WantedBoard();
        BoardResizePlan plan = BoardResize.Plan(_session.Project, width, height);
        var notes = new List<string>();

        if (plan.MovedCards > 0)
        {
            notes.Add($"⚠ 카드 {plan.MovedCards}장이 판 밖으로 나갑니다 — 카드 전체의 자리를 같은 비율로 줄여 안으로 모읍니다. 되돌리기(Ctrl+Z) 한 번이면 돌아옵니다.");
        }

        // 적은 값(범위 안으로 들인 것)보다 실제 크기가 크면 비켜 서느라 넓어진 것이다.
        BoardSize wanted = new BoardSize(width, height).Clamped();

        if (plan.Size.Width > wanted.Width || plan.Size.Height > wanted.Height)
        {
            notes.Add($"카드가 비켜 서느라 판이 {plan.Size.Width:N0} × {plan.Size.Height:N0}으로 섭니다.");
        }

        BoardNote.Text = string.Join("\n", notes);
        BoardNote.IsVisible = notes.Count > 0;
        if (plan.MovedCards > 0)
        {
            BoardNote.Foreground = new SolidColorBrush(Color.Parse("#B9770E"));
        }
        else
        {
            BoardNote.ClearValue(TextBlock.ForegroundProperty);   // null은 빈 붓이다 — 글이 사라진다
        }
    }

    /// <summary>두 칸의 값 — 적다 만 글자(아직 값으로 안 넘어온 것)도 읽는다. 못 읽으면 지금 판의 값.</summary>
    private (double Width, double Height) WantedBoard() =>
        (Side(BoardWidthBox, _session.Project.Board.Width), Side(BoardHeightBox, _session.Project.Board.Height));

    private static double Side(NumericUpDown box, double fallback) =>
        decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal typed)
            ? (double)typed
            : box.Value is { } value ? (double)value : fallback;

    /// <summary>「대본 글 칸」 — 글꼴 · 글자 크기 · 줄 간격과 미리 보기.</summary>
    private Control ScriptFontSection()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 6
        };

        AddRow(grid, 0, "글꼴", ScriptFontCombo);
        AddRow(grid, 1, "글자 크기", ScriptFontSizeBox);
        AddRow(grid, 2, "줄 간격 (%)", ScriptLineSpacingBox);

        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "[대본] 판의 카드에서 대본을 쓰는 칸의 글자입니다. 고치면 바로 바뀝니다 — 이 PC에만 적용되고 프로젝트 파일은 바뀌지 않습니다.",
                    FontSize = 11,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap
                },
                grid,
                ScriptPreview,
                ScriptFontDefaultButton
            }
        };
    }

    /// <summary>칸들을 지금 설정으로 채우고 미리 보기에 입힌다 — 채우는 동안의 바뀜은 넣기로 치지 않는다.</summary>
    private void FillScriptFont()
    {
        _fillingScript = true;

        try
        {
            ScriptFontCombo.SelectedItem = ScriptFont.Family.Length == 0 ? DefaultFontName : ScriptFont.Family;
            ScriptFontSizeBox.Value = (decimal)ScriptFont.Size;
            ScriptLineSpacingBox.Value = ScriptFont.LineSpacing;
            ScriptFont.Apply(ScriptPreview);
        }
        finally
        {
            _fillingScript = false;
        }
    }

    private bool _fillingScript;

    /// <summary>칸 하나를 고쳤다 — 셋을 함께 넣는다(범위 밖은 끝으로 당겨진다). 비운 숫자 칸은 기본값이다.</summary>
    private void OnScriptFontEdited()
    {
        if (_fillingScript)
        {
            return;
        }

        UiGuard.Run(_session, "대본 글 칸", () =>
        {
            string family = ScriptFontCombo.SelectedItem as string ?? DefaultFontName;
            ScriptFont.Set(
                family == DefaultFontName ? null : family,
                (double)(ScriptFontSizeBox.Value ?? (decimal)ScriptFont.DefaultSize),
                (int)(ScriptLineSpacingBox.Value ?? ScriptFont.DefaultLineSpacing));
            FillScriptFont();
        });
    }

    /// <summary>
    /// 글꼴 칸의 목록 — 「기본」과 이 PC의 글꼴(이름 차례). 기억한 글꼴이 이 PC에 없으면(지웠다 · 다른 PC의 설정) 그 이름도 목록에 둔다 —
    /// 칸이 빈 채로 서면 무엇을 쓰고 있는지 모른다(없는 글꼴은 테마의 글꼴로 그려진다).
    /// </summary>
    private static List<string> FontNames()
    {
        List<string> names = FontManager.Current.SystemFonts
            .Select(family => family.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), ignoreCase: true))
            .ToList();

        if (ScriptFont.Family.Length > 0 && !names.Contains(ScriptFont.Family, StringComparer.Ordinal))
        {
            names.Insert(0, ScriptFont.Family);
        }

        names.Insert(0, DefaultFontName);
        return names;
    }

    private static NumericUpDown SideBox(double value) => new()
    {
        Minimum = (decimal)BoardSize.Minimum,
        Maximum = (decimal)BoardSize.Maximum,
        Increment = 1000,
        Value = (decimal)value,
        FormatString = "N0",
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private static void AddRow(Grid grid, int row, string label, Control box)
    {
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        Grid.SetRow(text, row);
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 1);
        grid.Children.Add(text);
        grid.Children.Add(box);
    }

    /// <summary>「단축키」의 표 — 자리마다 제목과 「누르는 것 · 하는 일」 줄들(<see cref="Shortcuts.Table"/> 한 벌).</summary>
    private static Control ShortcutSection()
    {
        var places = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 8, 0) };

        foreach (Shortcuts.Place place in Shortcuts.Table)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*"), Tag = "shortcuts:" + place.Name };

            for (int index = 0; index < place.Entries.Count; index++)
            {
                Shortcuts.Entry entry = place.Entries[index];
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Place(grid, new TextBlock
                {
                    Text = PlatformKeys.Label(entry.Keys),   // 맥에서는 ⌘ · ⌫로 보인다
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 8, 2)
                }, index, 0);
                Place(grid, new TextBlock
                {
                    Text = entry.Does,
                    FontSize = 11,
                    Opacity = 0.8,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2)
                }, index, 1);
            }

            places.Children.Add(new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = place.Name, FontSize = 12, FontWeight = FontWeight.SemiBold },
                    grid
                }
            });
        }

        return places;
    }

    /// <summary>묶음 하나 — 머리(이름)와 그 아래 설정들. ⛔ 머리 아래 「어디에 저장되는가」 줄은 2026-11-18에 걷었다(Z2).</summary>
    private static Control Group(string title, Control body) => new Border
    {
        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(12, 10),
        Child = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14 },
                body
            }
        }
    };
}
