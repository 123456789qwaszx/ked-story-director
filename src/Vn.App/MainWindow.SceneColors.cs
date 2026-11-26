using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Vn.App.Services;
using Vn.App.Strings;
using Vn.App.Theming;
using Vn.App.Views;
using Vn.Authoring.Chapters;

namespace Vn.App;

/// <summary>
/// <b>장면 요약의 장면 색 줄</b>(design/scene-colors.md 5.1 · 2026-11-27 소유자 — 「권고대로」) — 하이어아키에서 장면을 고르면 인스펙터의 장면 이름 칸 아래에
/// [자동] · 다섯 색 단추가 선다. 우클릭 「장면 색」은 모르면 못 찾지만 여기는 눈에 보인다. 지금 색은 파란 고리다. 누르면 넣고(되돌리기 한 칸) 고리를
/// 옮긴다 — 인스펙터는 다시 세우지 않는다(<c>NodeMetadata</c>). 끝의 「직접 고르기 (RGB)…」는 색 차림을 연다(<see cref="SceneColorMenu.Chooser"/> · 2026-11-27) —
/// RGB로 고른 색은 다섯 색 뒤에 그 색의 단추 하나로 선다.
/// </summary>
public partial class MainWindow
{
    /// <summary>장면 색 줄 — 화면 시험이 찾는다.</summary>
    internal const string SceneColorRowTag = "scene-color-row";

    private void SceneColorRow(string chapterId, ChapterScene scene, ChapterGraphModel model)
    {
        int shown = SceneColors.ColorOf(SceneColors.Of(ChapterSceneGrouping.Of(model), model.SceneNotes), scene.SceneId);
        int? chosen = ChapterSceneNotes.ColorOf(model.SceneNotes, scene.SceneId);
        string sceneId = scene.SceneId;
        string name = scene.DisplayName;

        Heading(Ui.SceneColor_Menu);

        var row = new WrapPanel { Orientation = Orientation.Horizontal, Tag = SceneColorRowTag };
        var buttons = new List<(int? Color, Button Button)>();

        void Ring(int? picked)
        {
            foreach ((int? color, Button button) in buttons)
            {
                button.BorderBrush = color == picked ? Skin.Selection.Brush() : Brushes.Transparent;
            }
        }

        Button Make(int? color)
        {
            Control content = color is int slot
                ? SceneColorMenu.Dot(slot, 14)
                : new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children =
                    {
                        SceneColorMenu.Dot(shown, 8),
                        new TextBlock { Text = Ui.SceneColor_Auto, FontSize = Skin.Font(10), VerticalAlignment = VerticalAlignment.Center }
                    }
                };

            var button = new Button
            {
                Content = content,
                Padding = new Thickness(color is null ? 8 : 5, 4),
                Margin = new Thickness(0, 0, 4, 4),
                MinHeight = 0,
                BorderThickness = new Thickness(2),
                Background = Brushes.Transparent,
                Tag = SceneColorMenu.ItemTag + (color switch
                {
                    null => "auto",
                    int customColor when SceneColors.IsCustom(customColor) => "custom",
                    int number => number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                })
            };
            ToolTip.SetTip(button, color is int tipped ? SceneColorMenu.NameOf(tipped) : string.Format(Ui.SceneColor_AutoNow, SceneColorMenu.NameOf(shown)));
            button.Click += (_, _) => UiGuard.Run(_session, Ui.SceneColor_Menu, () =>
            {
                SceneColorMenu.Apply(_session, [(chapterId, sceneId, name)], color);
                Ring(color);
            });

            buttons.Add((color, button));
            return button;
        }

        row.Children.Add(Make(null));

        for (int color = 1; color <= SceneColors.Count; color++)
        {
            row.Children.Add(Make(color));
        }

        // 「직접 고르기 (RGB)…」 — 색 차림(정해 둔 색 · 이 프로젝트에 쓴 색 · RGB). 직접 고르기는 그 장면의 지금 색에서 시작한다.
        var rgbButton = new Button
        {
            Content = new TextBlock { Text = Ui.SceneColor_ChooseRgb, FontSize = Skin.Font(10), VerticalAlignment = VerticalAlignment.Center },
            Padding = new Thickness(8, 4),
            Margin = new Thickness(0, 0, 4, 4),
            MinHeight = 0,
            BorderThickness = new Thickness(2),
            Background = Brushes.Transparent,
            Tag = SceneColorMenu.ItemTag + "rgb"
        };
        ToolTip.SetTip(rgbButton, Ui.SceneColor_ChooseRgbTip);
        row.Children.Add(rgbButton);

        // RGB로 고른 색의 단추 — 하나, 지금 고른 RGB(다섯 색 뒤 · 「직접 고르기」 앞). 다른 RGB를 고르면 갈아 끼운다.
        Button? custom = null;

        void ShowCustom(int? color)
        {
            if (custom is not null)
            {
                row.Children.Remove(custom);
                buttons.RemoveAll(item => ReferenceEquals(item.Button, custom));
                custom = null;
            }

            if (color is int rgb && SceneColors.IsCustom(rgb))
            {
                custom = Make(rgb);
                row.Children.Insert(row.Children.IndexOf(rgbButton), custom);
            }
        }

        rgbButton.Flyout = SceneColorMenu.Chooser(
            _session,
            () => SceneColorMenu.ColorNow(_session, chapterId, sceneId),
            color => UiGuard.Run(_session, Ui.SceneColor_Menu, () =>
            {
                SceneColorMenu.Apply(_session, [(chapterId, sceneId, name)], color);
                ShowCustom(color);
                Ring(color);
            }));

        ShowCustom(chosen);
        Ring(chosen);
        _sheet.Children.Add(row);
    }
}
