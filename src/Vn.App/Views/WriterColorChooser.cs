using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Vn.App.Theming;
using Vn.Authoring.Model;
using Vn.App.Strings;

namespace Vn.App.Views;

/// <summary>
/// <b>색 차림</b> — 정해 둔 열 가지 · 「없음」 · <b>이 프로젝트에 쓴 색</b> · <b>직접 고르기(RGB)</b>(2026-11-23 소유자 — 「글자 색깔과 바탕색의 종류가 조금
/// 제한 적인게 아쉬운데, 사용한 색깔과 더불어서 RGB를 원하는 걸 선택할 수 있는 것도」 · writer-formatting WF-D14). [대본] 위쪽 서식 도구의 글자색 · 바탕색과
/// 「화자」 탭의 이름 색(WF-D15)이 같이 쓴다. 고르면 <see cref="Picked"/>(「없음」은 null) — 차림을 닫고 입히는 것은 쓰는 쪽이 한다.
///
/// 직접 고르기의 막대는 초점을 받지 않는다 — 초점이 막대에 있으면 글 칸 밖의 Delete가 고른 카드를 지운다. 16진수 칸은 글 칸이라 괜찮다.
/// </summary>
internal sealed class WriterColorChooser
{
    private readonly bool _fill;
    private readonly TextBlock[] _values = new TextBlock[3];
    private bool _syncing;

    /// <param name="fill">바탕색이면 참 — 정해 둔 열 가지의 옅은 색을 보인다.</param>
    /// <param name="noneTip">「없음」의 풍선 글.</param>
    /// <param name="noneLabel">「없음」 단추의 글 — 장면 색은 「자동」이다(2026-11-27). 없으면 「없음」.</param>
    public WriterColorChooser(bool fill, string noneTip, string? noneLabel = null)
    {
        _fill = fill;
        Last = WriterSwatch.All[fill ? 2 : 0].Of(fill);   // 바탕은 노랑, 글자는 빨강부터

        var palette = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 5 * 28 };

        foreach (WriterSwatch swatch in WriterSwatch.All)
        {
            palette.Children.Add(Swatch(swatch.Of(fill), swatch.Name));
        }

        var none = new Button
        {
            Content = noneLabel ?? Ui.Board_None,
            FontSize = Skin.Font(11),
            Margin = new Thickness(2),
            Focusable = false,
            [ToolTip.TipProperty] = noneTip
        };
        none.Click += (_, _) => Picked?.Invoke(null);
        palette.Children.Add(none);

        UsedTitle = new TextBlock { Text = Ui.Format_ColorsUsedInThisProject, FontSize = Skin.Font(11), Margin = new Thickness(2, 6, 2, 1), Opacity = 0.7, IsVisible = false };
        Used = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 5 * 28, IsVisible = false };

        Preview = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = Skin.SwatchBorder.Brush(0x55)
        };
        Hex = new TextBox
        {
            Width = 86,
            FontSize = Skin.Font(12),
            [ToolTip.TipProperty] = Ui.Format_RRGGBBTypeItInHex
        };
        Apply = new Button { Content = Ui.Format_Apply, FontSize = Skin.Font(11), Focusable = false, [ToolTip.TipProperty] = Ui.Format_ApplyThisColor };
        Channels = new Slider[3];
        var rows = new StackPanel { Spacing = 0 };

        foreach ((int index, string label) in new[] { (0, "R"), (1, "G"), (2, "B") })
        {
            Channels[index] = new Slider
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
            _values[index] = new TextBlock { Width = 26, FontSize = Skin.Font(11), TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Channels[index].ValueChanged += (_, _) =>
            {
                if (!_syncing)
                {
                    Show(FromChannels(), keepHex: false);
                }
            };
            rows.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { new TextBlock { Text = label, Width = 10, FontSize = Skin.Font(11), VerticalAlignment = VerticalAlignment.Center }, Channels[index], _values[index] }
            });
        }

        // 16진수를 적으면 막대와 미리 보기가 따른다(글 바뀜 알림은 한 박자 뒤에 온다 — 입히기는 칸의 글을 바로 읽는다).
        Hex.TextChanged += (_, _) =>
        {
            if (!_syncing && WriterRgb.Parse(Hex.Text) is { } typed)
            {
                Show(typed, keepHex: true);
            }
        };
        Apply.Click += (_, _) => Picked?.Invoke(Chosen);
        Hex.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                Picked?.Invoke(Chosen);
            }
        };

        Content = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                palette,
                UsedTitle,
                Used,
                new TextBlock { Text = Ui.Format_ChooseRGB, FontSize = Skin.Font(11), Margin = new Thickness(2, 6, 2, 1), Opacity = 0.7 },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(2, 0), Children = { Preview, Hex, Apply } },
                rows
            }
        };

        Show(Last, keepHex: false);
    }

    /// <summary>골랐다 — 색, 「없음」이면 null.</summary>
    public event Action<WriterRgb?>? Picked;

    /// <summary>차림의 모습 — 플라이아웃 따위에 담는다.</summary>
    public Control Content { get; }

    /// <summary>이 프로젝트에 쓴 색(정해 둔 열 가지 밖) — <see cref="Prepare"/>가 다시 센다. 없으면 머리와 함께 숨는다.</summary>
    public WrapPanel Used { get; }

    public TextBlock UsedTitle { get; }

    /// <summary>직접 고르기 — <c>#RRGGBB</c> 칸.</summary>
    public TextBox Hex { get; }

    /// <summary>직접 고르기 — R · G · B 막대.</summary>
    public Slider[] Channels { get; }

    public Border Preview { get; }

    public Button Apply { get; }

    /// <summary>마지막에 고른 색 — 직접 고르기가 이 색에서 시작한다.</summary>
    public WriterRgb Last { get; set; }

    /// <summary>직접 고르기가 가리키는 색 — 16진수 칸이 읽히면 그것, 아니면 막대.</summary>
    private WriterRgb Chosen => WriterRgb.Parse(Hex.Text) ?? FromChannels();

    /// <summary>
    /// 차림을 열기 전에 — 이 프로젝트에 쓴 색을 다시 놓고(정해 둔 열 가지 밖 · 준 차례 · 열 개까지), 직접 고르기를 <paramref name="start"/>(없으면 마지막에 고른 색)에서
    /// 시작한다.
    /// </summary>
    public void Prepare(IEnumerable<WriterRgb> used, WriterRgb? start = null)
    {
        HashSet<WriterRgb> standard = WriterSwatch.All.Select(swatch => swatch.Of(_fill)).ToHashSet();
        Used.Children.Clear();

        foreach (WriterRgb rgb in used.Distinct().Where(rgb => !standard.Contains(rgb)).Take(10))
        {
            Used.Children.Add(Swatch(rgb, rgb.Hex));
        }

        UsedTitle.IsVisible = Used.IsVisible = Used.Children.Count > 0;
        Show(start ?? Last, keepHex: false);
    }

    private Button Swatch(WriterRgb rgb, string name)
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
        swatch.Click += (_, _) => Picked?.Invoke(rgb);
        return swatch;
    }

    private WriterRgb FromChannels() =>
        new((byte)Math.Round(Channels[0].Value), (byte)Math.Round(Channels[1].Value), (byte)Math.Round(Channels[2].Value));

    private void Show(WriterRgb rgb, bool keepHex)
    {
        _syncing = true;

        try
        {
            byte[] parts = [rgb.R, rgb.G, rgb.B];

            for (int index = 0; index < 3; index++)
            {
                Channels[index].Value = parts[index];
                _values[index].Text = parts[index].ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (!keepHex)
            {
                Hex.Text = rgb.Hex;
            }

            Preview.Background = WriterPalette.Brush(rgb);
        }
        finally
        {
            _syncing = false;
        }
    }
}
