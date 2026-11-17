using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vn.App.Services;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;

namespace Vn.App.Views;

/// <summary>
/// <b>[⚙ 설정] 창</b> (2026-11-17 · <c>docs/work-orders/settings-orders.md</c> C3) — 앞으로 필요할 설정을 한 창에 모은다.
///
/// 묶음마다 <b>어디에 저장되는가</b>를 머리에 적는다 — 프로젝트에 싣는 설정은 되돌리기가 되고 「저장되지 않은 변경」을 켜지만, PC별 설정은
/// 그렇지 않다. 섞으면 「이건 왜 Ctrl+Z가 안 되지?」가 생긴다. 지금은 [이 프로젝트] 묶음 하나(「[대본] 판 크기」)이고, PC별 설정이
/// 생기면 [이 PC] 묶음을 더한다.
///
/// [확인]이 반영하고(<see cref="Confirm"/> — 판 크기는 <see cref="Vn.Authoring.Editing.ProjectEditor.SetBoardSize"/> 한 번, 되돌리기 한
/// 칸), [취소] · Esc는 버린다. 줄여서 카드가 움직이면 [확인] 전에 미리 말한다(<see cref="BoardResize.Plan"/> — 바꾸기와 같은 계산).
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly AuthoringSession _session;

    public SettingsWindow(AuthoringSession session)
    {
        _session = session;

        Title = "설정";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
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

        ConfirmButton = new Button { Content = "확인", IsDefault = true, MinWidth = 72, HorizontalContentAlignment = HorizontalAlignment.Center };
        CancelButton = new Button { Content = "취소", IsCancel = true, MinWidth = 72, HorizontalContentAlignment = HorizontalAlignment.Center };
        ConfirmButton.Click += (_, _) => Confirm();
        CancelButton.Click += (_, _) => Close(false);

        BoardWidthBox.ValueChanged += (_, _) => OnSideChanged(BoardWidthBox);
        BoardHeightBox.ValueChanged += (_, _) => OnSideChanged(BoardHeightBox);

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                Group(
                    "이 프로젝트",
                    "project.vnproject.json에 저장됩니다 — 바꾸면 되돌리기(Ctrl+Z)가 되고, File → 저장(Ctrl+S)해야 파일에 남습니다.",
                    BoardSection()),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { CancelButton, ConfirmButton }
                }
            }
        };
    }

    // ── 시험 손잡이 ───────────────────────────────────────────────────────────

    internal NumericUpDown BoardWidthBox { get; }

    internal NumericUpDown BoardHeightBox { get; }

    /// <summary>미리 알림 — 줄여서 카드가 움직이거나, 비켜 서느라 판이 적은 값보다 커질 때만 보인다.</summary>
    internal TextBlock BoardNote { get; }

    internal Button DefaultButton { get; }

    internal Button ConfirmButton { get; }

    internal Button CancelButton { get; }

    /// <summary>[확인] — 판 크기를 바꾸고(같으면 아무것도 적지 않는다) 창을 닫는다.</summary>
    internal void Confirm()
    {
        (double width, double height) = WantedBoard();
        UiGuard.Run(_session, "설정", () => _session.Editor.SetBoardSize(width, height));
        Close(true);
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
        BoardNote.Foreground = plan.MovedCards > 0 ? new SolidColorBrush(Color.Parse("#B9770E")) : null;
    }

    /// <summary>두 칸의 값 — 적다 만 글자(아직 값으로 안 넘어온 것)도 읽는다. 못 읽으면 지금 판의 값.</summary>
    private (double Width, double Height) WantedBoard() =>
        (Side(BoardWidthBox, _session.Project.Board.Width), Side(BoardHeightBox, _session.Project.Board.Height));

    private static double Side(NumericUpDown box, double fallback) =>
        decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal typed)
            ? (double)typed
            : box.Value is { } value ? (double)value : fallback;

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

    /// <summary>묶음 하나 — 머리(이름 · 어디에 저장되는가)와 그 아래 설정들.</summary>
    private static Control Group(string title, string where, Control body) => new Border
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
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, FontSize = 14 },
                        new TextBlock { Text = where, FontSize = 11, Opacity = 0.6, TextWrapping = TextWrapping.Wrap }
                    }
                },
                body
            }
        }
    };
}
