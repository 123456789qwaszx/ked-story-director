using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Strings;
using Vn.App.Theming;
using Vn.Authoring.Chapters;
using Vn.Authoring.Model;
using Vn.Authoring.Text;
using Vn.Authoring.Strings;

namespace Vn.App.Views;

/// <summary>
/// <b>장면 색을 고르는 손잡이</b>(design/scene-colors.md 5.1 · 2026-11-27 소유자 — 「권고대로」) — 하이어아키 장면 줄의 우클릭 「장면 색」, 판의 장면 이름표 우클릭,
/// 인스펙터의 장면 색 줄이 같은 항목 · 같은 넣기를 쓴다. 항목은 「자동(지금: 그 색)」과 다섯 색이고, 지금 색에 ✓가 붙는다. 넣기는 되돌리기 한 칸이다 —
/// 여러 장면을 함께 칠해도 한 칸이다. 「직접 고르기 (RGB)…」는 색 차림을 연다(<see cref="Chooser"/> · 2026-11-27).
/// </summary>
internal static class SceneColorMenu
{
    /// <summary>메뉴 항목의 Tag 머리 — 뒤에 칸 번호(1 ~ 5) 또는 <c>auto</c>가 붙는다. 화면 시험이 찾는다.</summary>
    internal const string ItemTag = "scene-color:";

    /// <summary>그 칸의 이름(세이지 · 장미 · 민트 · 연두 · 라일락) — RGB로 고른 색이면 <c>#RRGGBB</c>.</summary>
    internal static string NameOf(int color) => color switch
    {
        _ when SceneColors.IsCustom(color) => SceneColors.HexOf(color),
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
    /// 메뉴 항목 — 「자동(지금: <paramref name="shown"/>)」과 다섯 색. <paramref name="chosen"/>이 null이면 자동에, 아니면 그 색에 ✓(RGB로 고른 색이면 그 색이 한 줄 더 선다).
    /// 누르면 <paramref name="pick"/>(자동이면 null). <paramref name="chooseRgb"/>를 주면 맨 끝에 「직접 고르기 (RGB)…」 — 색 차림을 연다(<see cref="ShowChooser"/>).
    /// </summary>
    internal static List<Control> Items(int shown, int? chosen, Action<int?> pick, Action? chooseRgb = null)
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

        // RGB로 고른 색이면 그 색도 한 줄 — 메뉴에서 지금 색이 보인다.
        if (chosen is int custom && SceneColors.IsCustom(custom))
        {
            var mine = new MenuItem
            {
                Header = NameOf(custom) + "  ✓",
                Icon = Dot(custom, 10),
                FontSize = Skin.Font(11),
                Tag = ItemTag + "custom"
            };
            mine.Click += (_, _) => pick(custom);
            items.Add(mine);
        }

        if (chooseRgb is not null)
        {
            items.Add(new Separator());
            var rgb = new MenuItem
            {
                Header = Ui.SceneColor_ChooseRgb,
                FontSize = Skin.Font(11),
                Tag = ItemTag + "rgb"
            };
            ToolTip.SetTip(rgb, Ui.SceneColor_ChooseRgbTip);

            // 메뉴가 닫힌 뒤에 차림을 연다 — 닫히는 메뉴가 막 연 차림까지 걷지 않게.
            rgb.Click += (_, _) => Dispatcher.UIThread.Post(chooseRgb);
            items.Add(rgb);
        }

        return items;
    }

    // ── 색 차림 (RGB · 2026-11-27) ─────────────────────────────────────────────

    /// <summary>이 실행에서 장면 색으로 고른 RGB — 최근 것이 앞(열 개까지). 색 차림의 「이 프로젝트에 쓴 색」 맨 앞에 선다.</summary>
    private static readonly List<WriterRgb> Recent = new();

    /// <summary>마지막으로 지은 장면 색 차림 — 화면 시험이 찾는다.</summary>
    internal static WriterColorChooser? LastChooser { get; private set; }

    /// <summary>그 장면 색의 RGB — RGB로 고른 색이면 그 색, 팔레트 칸이면 그 칸의 점 색(차림의 직접 고르기가 여기서 시작한다).</summary>
    internal static WriterRgb RgbOf(int color)
    {
        if (SceneColors.IsCustom(color))
        {
            int rgb = SceneColors.RgbOf(color);
            return new WriterRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }

        Avalonia.Media.Color mark = Skin.SceneMarkOf(color);
        return new WriterRgb(mark.R, mark.G, mark.B);
    }

    /// <summary>RGB를 장면 색 값으로(<see cref="SceneColors.Custom"/>).</summary>
    internal static int ColorOf(WriterRgb rgb) => SceneColors.Custom((rgb.R << 16) | (rgb.G << 8) | rgb.B);

    /// <summary>이 프로젝트의 장면들이 RGB로 고른 색(챕터 · 적힌 차례).</summary>
    internal static IEnumerable<WriterRgb> UsedInProject(StoryProject project) =>
        project.Chapters
            .SelectMany(chapter => chapter.SceneNotes)
            .Select(note => note.Color)
            .OfType<int>()
            .Where(SceneColors.IsCustom)
            .Select(RgbOf)
            .Distinct();

    /// <summary>그 장면의 지금 색 — 고른 색이 먼저, 그 밖은 자동(프로젝트의 지금 값으로 센다).</summary>
    internal static int ColorNow(AuthoringSession session, string chapterId, string sceneId)
    {
        if (session.Editor.FindChapter(chapterId) is not { } chapter)
        {
            return 1;
        }

        return SceneColors.ColorOf(SceneColors.Of(ChapterSceneGrouping.Of(chapter.ToGraphModel(chapterId)), chapter.SceneNotes), sceneId);
    }

    /// <summary>
    /// 장면 색의 <b>색 차림</b>(2026-11-27 소유자 — 「장면색을 원한다면, RGB 표에서 선택도 할 수 있게 해줄래? 그게 최근 사용한 항목에 반영이 되도록 해주고, 또 특정
    /// 장면색을 선택한 상태에서 열면 그 색이 선택된 상태로 RGB가 열리도록」) — [대본] 서식 도구의 글자색 · 「화자」 탭의 이름 색과 같은 차림(<see cref="WriterColorChooser"/> —
    /// 정해 둔 열 가지 · 이 프로젝트에 쓴 색 · 직접 고르기 RGB)이다. 「이 프로젝트에 쓴 색」의 맨 앞은 장면 색으로 최근에 고른 RGB, 그다음 이 프로젝트의 장면들이
    /// 쓰는 RGB, 그다음 서식 · 화자의 색이다. 열 때마다 직접 고르기가 그 장면의 지금 색(<paramref name="current"/>)에서 시작한다. 「자동」은 고른 색을 걷는다.
    /// 고른 RGB의 판 면 · 점은 테마가 짓는다(<see cref="Skin.CustomSceneTint"/> · <see cref="Skin.CustomSceneMark"/>).
    /// </summary>
    internal static Flyout Chooser(AuthoringSession session, Func<int> current, Action<int?> pick)
    {
        var chooser = new WriterColorChooser(fill: false, Ui.SceneColor_ChooserAutoTip, Ui.SceneColor_Auto);
        var flyout = new Flyout { Content = chooser.Content, Placement = PlacementMode.BottomEdgeAlignedLeft };

        flyout.Opening += (_, _) =>
        {
            LastChooser = chooser;
            chooser.Prepare(Recent.Concat(UsedInProject(session.Project)).Concat(GraphEditorView.ProjectColors(session.Project, fill: false)), RgbOf(current()));
        };
        chooser.Picked += rgb =>
        {
            flyout.Hide();
            pick(rgb is { } picked ? ColorOf(picked) : null);
        };

        LastChooser = chooser;
        return flyout;
    }

    /// <summary>색 차림을 그 자리(<paramref name="anchor"/>)에 연다 — 메뉴의 「직접 고르기 (RGB)…」.</summary>
    internal static void ShowChooser(Control anchor, AuthoringSession session, Func<int> current, Action<int?> pick) =>
        Chooser(session, current, pick).ShowAt(anchor);

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

        // RGB로 고른 색은 최근 고른 색의 맨 앞으로 — 색 차림의 「이 프로젝트에 쓴 색」 맨 앞에 선다.
        if (color is int custom && SceneColors.IsCustom(custom))
        {
            WriterRgb rgb = RgbOf(custom);
            Recent.Remove(rgb);
            Recent.Insert(0, rgb);

            if (Recent.Count > 10)
            {
                Recent.RemoveAt(Recent.Count - 1);
            }
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
