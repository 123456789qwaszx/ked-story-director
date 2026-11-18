using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Vn.App.Services;
using Vn.Authoring.Graph;
using Vn.Authoring.Model;
using Vn.Authoring.Serialization;

namespace Vn.App.Views;

/// <summary>
/// <b>[⚙ 설정] 창</b> (2026-11-17 · <c>docs/work-orders/settings-orders.md</c> C3) — 앞으로 필요할 설정을 한 창에 모은다.
///
/// 묶음마다 <b>어디에 저장되는가</b>를 머리에 적는다 — 프로젝트에 싣는 설정은 되돌리기가 되고 「저장되지 않은 변경」을 켜지만, PC별 설정은
/// 그렇지 않다. 섞으면 「이건 왜 Ctrl+Z가 안 되지?」가 생긴다. 묶음은 둘이다 — [이 프로젝트](「[대본] 판 크기」 — 프로젝트 파일)와
/// [내보내기](형식마다 보낼 곳 — 프로젝트 폴더의 <c>runtime.target.json</c>, 2026-11-17 · <c>docs/work-orders/export-destinations-orders.md</c>
/// G2 · ⛔ 위 띠의 [🎮 게임 폴더…]를 여기로 옮겼다). PC별 설정이 생기면 [이 PC] 묶음을 더한다.
///
/// [확인]이 반영하고(<see cref="Confirm"/> — 판 크기는 <see cref="Vn.Authoring.Editing.ProjectEditor.SetBoardSize"/> 한 번, 되돌리기 한
/// 칸 · 보낼 곳은 그 파일에 바로), [취소] · Esc는 버린다. 줄여서 카드가 움직이면 [확인] 전에 미리 말한다(<see cref="BoardResize.Plan"/> — 바꾸기와 같은 계산).
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

        // 「내보내기」는 접는다 — 자주 고칠 것이 아니다(2026-11-18 소유자 — 「내보내기의 내용은 접을 수 있도록」). 머리가 지금 보내는 형식을 말한다.
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
                        Text = $"{ExportDestinations.FileName}(프로젝트 폴더)에 저장됩니다 — [확인]을 누르면 바로 파일에 남고 되돌리기는 없습니다. 작업 폴더" +
                               "(exported/)에는 늘 쓰고, 보낼 곳에는 File → 저장(Ctrl+S)할 때 보냅니다.",
                        FontSize = 11,
                        Opacity = 0.6,
                        TextWrapping = TextWrapping.Wrap
                    },
                    ExportSection()
                }
            }
        };

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
                Framed(ExportGroup),
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

    /// <summary>「내보내기」 묶음 — 접고 편다(처음에는 접혀 있다).</summary>
    internal Expander ExportGroup { get; }

    /// <summary>「내보내기」 머리 — 접혀 있어도 지금 보내는 형식이 보인다.</summary>
    internal TextBlock ExportHeader { get; } = new() { FontWeight = FontWeight.SemiBold, FontSize = 14 };

    internal Button ConfirmButton { get; }

    internal Button CancelButton { get; }

    /// <summary>형식마다 보낼 곳 줄의 글(적힌 경로 · 「보내지 않음」 · 없는 폴더 표시).</summary>
    internal TextBlock DestinationText(ExportFormat format) => _destinationTexts[format];

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
            return "프로젝트를 먼저 저장해야 보낼 곳을 정할 수 있습니다.";
        }

        ExportDestinations wanted = _destinations.With(
            format, absoluteFolder is null ? null : ExportDestinations.Relativize(projectPath, absoluteFolder));

        string? refusal = SameFolder(wanted, projectPath)
            ? "대사 Yarn은 게임 폴더(진행 JSON의 보낼 곳)와 같은 곳으로 보내지 않습니다 — 게임의 Yarn 프로젝트가 compositor의 연출 든 Yarn과 " +
              "같은 노드 이름을 둘 읽어 컴파일이 깨집니다."
            : null;

        if (refusal is null)
        {
            _destinations = wanted;
            RefreshDestinations();
        }

        ExportNote.Text = refusal ?? string.Empty;
        ExportNote.IsVisible = refusal is not null;
        return refusal;
    }

    /// <summary>[확인] — 판 크기를 바꾸고(같으면 아무것도 적지 않는다), 보낼 곳이 바뀌었으면 그 파일에 적고 창을 닫는다.</summary>
    internal void Confirm()
    {
        (double width, double height) = WantedBoard();
        UiGuard.Run(_session, "설정", () =>
        {
            _session.Editor.SetBoardSize(width, height);

            if (_session.ProjectPath is { } projectPath && _destinations != _session.Destinations)
            {
                ExportDestinations.SaveBeside(projectPath, _destinations);
                _session.ReloadDestinations();
                _session.SetStatus(_destinations.IsEmpty
                    ? "보낼 곳을 모두 껐습니다 — exported/에만 냅니다."
                    : "보낼 곳을 바꿨습니다 — 다음 저장(Ctrl+S)부터 거기로 보냅니다.");
            }
        });
        Close(true);
    }

    // ── 「내보내기」 ───────────────────────────────────────────────────────────

    private ExportDestinations _destinations;

    private readonly Dictionary<ExportFormat, TextBlock> _destinationTexts = [];

    private Control ExportSection()
    {
        var panel = new StackPanel { Spacing = 6 };

        if (_session.ProjectPath is null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "프로젝트를 먼저 저장해야 보낼 곳을 정할 수 있습니다(경로는 프로젝트 폴더 기준으로 적힙니다).",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 6,
            IsEnabled = _session.ProjectPath is not null
        };

        int row = 0;

        foreach (ExportFormat format in ExportDestinations.Formats)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var label = new TextBlock
            {
                Text = format == ExportFormat.Progression ? "진행 JSON → 게임 폴더" : ExportDestinations.LabelOf(format),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            var text = new TextBlock
            {
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var pick = new Button { Content = "고르기…", FontSize = 11, Tag = $"export-pick:{format}" };
            var clear = new Button { Content = "✕", FontSize = 11, Tag = $"export-clear:{format}" };
            ToolTip.SetTip(clear, "보내지 않는다");

            pick.Click += async (_, _) =>
            {
                if (await PickFolderAsync($"보낼 곳 — {ExportDestinations.LabelOf(format)}") is { } folder)
                {
                    SetDestination(format, folder);
                }
            };
            clear.Click += (_, _) => SetDestination(format, null);

            _destinationTexts[format] = text;
            Place(grid, label, row, 0);
            Place(grid, text, row, 1);
            Place(grid, pick, row, 2);
            Place(grid, clear, row, 3);
            row++;
        }

        panel.Children.Add(grid);
        panel.Children.Add(new TextBlock
        {
            Text = "진행 JSON의 보낼 곳은 런타임의 ChapterProgression 폴더(게임 폴더)이고 compositor도 이 칸을 읽어 Yarn을 씁니다. 대사 Yarn은 게임 " +
                   "폴더와 같은 곳으로 보내지 않습니다(연출 든 Yarn과 노드 이름이 겹칩니다). 없는 폴더는 만들지 않습니다.",
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
        List<string> sending = ExportDestinations.Formats
            .Where(format => _destinations.FolderOf(format) is not null)
            .Select(ExportDestinations.LabelOf)
            .ToList();
        ExportHeader.Text = sending.Count == 0
            ? "내보내기 — 작업 폴더에만 냅니다"
            : $"내보내기 — {string.Join(" · ", sending)}을(를) 보낼 곳에도 냅니다";

        foreach ((ExportFormat format, TextBlock text) in _destinationTexts)
        {
            string? written = _destinations.FolderOf(format);
            bool missing = written is not null && _session.ProjectPath is { } path && _destinations.ResolveExisting(path, format) is null;

            text.Text = written is null ? "(보내지 않음)" : missing ? $"⚠ 없는 폴더 — {written}" : written;
            text.Opacity = written is null ? 0.55 : 1;
            // null을 넣으면 물려받은 글자색이 아니라 빈 붓이 된다(글이 사라진다) — 값을 걷는다.
            if (missing)
            {
                text.Foreground = new SolidColorBrush(Color.Parse("#B9770E"));
            }
            else
            {
                text.ClearValue(TextBlock.ForegroundProperty);
            }
            ToolTip.SetTip(text, written is null || _session.ProjectPath is not { } project
                ? null
                : ExportDestinations.ResolveFolder(project, written));
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

    private async Task<string?> PickFolderAsync(string title)
    {
        if (!StorageProvider.CanPickFolder)
        {
            ExportNote.Text = "이 환경에서는 폴더 선택 창을 열 수 없습니다.";
            ExportNote.IsVisible = true;
            return null;
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });

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

    /// <summary>묶음의 테두리 — 펼친 묶음과 접는 묶음이 같은 테두리에 선다.</summary>
    private static Border Framed(Control child) => new()
    {
        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(12, 10),
        Child = child
    };

    /// <summary>묶음 하나 — 머리(이름 · 어디에 저장되는가)와 그 아래 설정들.</summary>
    private static Control Group(string title, string where, Control body) => Framed(
        new StackPanel
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
        });
}
