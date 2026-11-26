using Avalonia.Media;
using Vn.App.Theming;

namespace Vn.App.Views;

/// <summary>
/// 조건 갈래를 구분하는 색.
///
/// 색은 데이터가 아니다. <see cref="Vn.Authoring.Flow.ConditionBranch.PaletteIndex"/>에서
/// 계산해서 칠할 뿐이고, 반대로 색을 보고 조건을 되짚지 않는다.
/// 그래서 이 표를 통째로 바꿔도 저장된 원고는 한 글자도 달라지지 않는다.
///
/// <b>색만으로 정보를 전달하지 않는다.</b> 갈래에는 언제나 조건 이름이 함께 붙는다.
/// 색은 같은 갈래를 눈으로 빠르게 묶어 주는 보조 수단이다.
/// </summary>
internal static class BranchPalette
{
    /// <summary>갈래 색 여섯 — 테마가 정한다(<see cref="Skin.BranchLines"/> · 기본 테마는 파랑 · 보라 · 초록 · 주황 · 자홍 · 청록).</summary>
    private static IReadOnlyList<Color> Accents => Skin.BranchLines;

    /// <summary>갈래의 강조색. 왼쪽 테두리와 라벨에 쓴다.</summary>
    public static IBrush Accent(int paletteIndex)
    {
        return AccentColor(paletteIndex).Brush();
    }

    /// <summary>갈래 카드의 배경. 강조색을 아주 옅게 깐 것이다.</summary>
    public static IBrush Background(int paletteIndex)
    {
        Color color = AccentColor(paletteIndex);
        return color.Brush(28);
    }

    private static Color AccentColor(int paletteIndex)
    {
        if (paletteIndex < 0)
        {
            return Skin.Neutral;
        }

        return Accents[paletteIndex % Accents.Count];
    }
}
