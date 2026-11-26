using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Vn.App.Services;
using Vn.App.Strings;
using Vn.App.Theming;
using Vn.Authoring.Chapters;
using Vn.Authoring.Text;
using Vn.Authoring.Strings;

namespace Vn.App.Views;

/// <summary>
/// <b>장면 색을 고르는 손잡이</b>(design/scene-colors.md 5.1 · 2026-11-27 소유자 — 「권고대로」) — 하이어아키 장면 줄의 우클릭 「장면 색」, 판의 장면 이름표 우클릭,
/// 인스펙터의 장면 색 줄이 같은 항목 · 같은 넣기를 쓴다. 항목은 「자동(지금: 그 색)」과 다섯 색이고, 지금 색에 ✓가 붙는다. 넣기는 되돌리기 한 칸이다 —
/// 여러 장면을 함께 칠해도 한 칸이다.
/// </summary>
internal static class SceneColorMenu
{
    /// <summary>메뉴 항목의 Tag 머리 — 뒤에 칸 번호(1 ~ 5) 또는 <c>auto</c>가 붙는다. 화면 시험이 찾는다.</summary>
    internal const string ItemTag = "scene-color:";

    /// <summary>그 칸의 이름(세이지 · 장미 · 민트 · 연두 · 라일락).</summary>
    internal static string NameOf(int color) => color switch
    {
        2 => Ui.SceneColor_2,
        3 => Ui.SceneColor_3,
        4 => Ui.SceneColor_4,
        5 => Ui.SceneColor_5,
        _ => Ui.SceneColor_1
    };

    /// <summary>그 칸의 점 — 메뉴 · 하이어아키 · 인스펙터가 같은 모양을 쓴다.</summary>
    internal static Ellipse Dot(int color, double size) => new()
    {
        Width = size,
        Height = size,
        Fill = Skin.SceneMarkOf(color).Brush(),
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>
    /// 메뉴 항목 — 「자동(지금: <paramref name="shown"/>)」과 다섯 색. <paramref name="chosen"/>이 null이면 자동에, 아니면 그 색에 ✓. 누르면 <paramref name="pick"/>
    /// (자동이면 null).
    /// </summary>
    internal static List<Control> Items(int shown, int? chosen, Action<int?> pick)
    {
        var items = new List<Control>();

        var auto = new MenuItem
        {
            Header = string.Format(Ui.SceneColor_AutoNow, NameOf(shown)) + (chosen is null ? "  ✓" : string.Empty),
            FontSize = Skin.Font(11),
            Tag = ItemTag + "auto"
        };
        auto.Click += (_, _) => pick(null);
        items.Add(auto);
        items.Add(new Separator());

        for (int color = 1; color <= SceneColors.Count; color++)
        {
            int slot = color;
            var item = new MenuItem
            {
                Header = NameOf(slot) + (chosen == slot ? "  ✓" : string.Empty),
                Icon = Dot(slot, 10),
                FontSize = Skin.Font(11),
                Tag = ItemTag + slot
            };
            item.Click += (_, _) => pick(slot);
            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// 장면들의 색을 넣는다 — 하나면 그 장면, 여럿이면 함께(되돌리기 한 칸). <paramref name="color"/>가 null이면 자동으로 돌린다. 상태줄이 한 일을 말한다.
    /// </summary>
    internal static void Apply(AuthoringSession session, IReadOnlyList<(string ChapterId, string SceneId, string Name)> scenes, int? color)
    {
        if (scenes.Count == 0)
        {
            return;
        }

        if (scenes.Count == 1)
        {
            session.Editor.SetSceneColor(scenes[0].ChapterId, scenes[0].SceneId, color);
        }
        else
        {
            session.Editor.Batch(() =>
            {
                foreach ((string ChapterId, string SceneId, string Name) scene in scenes)
                {
                    session.Editor.SetSceneColor(scene.ChapterId, scene.SceneId, color);
                }
            });
        }

        string? name = color is int slot ? NameOf(slot) : null;
        session.SetStatus((scenes.Count, name) switch
        {
            (1, null) => string.Format(Ui.SceneColor_BackToAuto, scenes[0].Name),
            (1, { } picked) => string.Format(Ui.SceneColor_Set, scenes[0].Name, picked, Josa.으로(picked)),
            (_, null) => Plural.Format(Ui.SceneColor_ManyBackToAuto, scenes.Count),
            (_, { } picked) => Plural.Format(Ui.SceneColor_SetMany, scenes.Count, picked, Josa.으로(picked))
        });
    }
}
