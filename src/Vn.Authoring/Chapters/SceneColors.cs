namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>장면 색</b>(design/scene-colors.md · 2026-11-26 소유자 — 「장면끼리 구분을 위해서 장면 바탕을 조금씩 다르게 하거나 커스텀 할 수 있게」 · 「권고대로」).
/// 장면마다 팔레트의 한 칸(1 ~ <see cref="Count"/>)이고, 칸마다의 색은 테마가 정한다(<c>SceneTint1</c> ~ · <c>SceneMark1</c> ~). 런타임은 읽지 않는다.
///
/// 작가가 고른 칸(<see cref="ChapterSceneNote.Color"/>)이 먼저다. 고르지 않은 장면은 <b>자동</b>이다 — 장면ID에서 첫 후보 칸을 뽑고(같은 ID면 늘 같은 칸),
/// 이야기 차례로 바로 앞 두 장면의 칸과 같으면 팔레트 차례의 다음 칸으로 간다. 그래서 저장하지 않아도 장면을 더하거나 카드를 옮겨도 이미 있는 장면의 색이
/// 거의 그대로이고(차례의 이웃이 바뀐 장면만 바뀔 수 있다), 이야기 차례로 이웃한 장면은 늘 다르다. ⛔ 차례대로 돌리는 길은 앞에 장면 하나를 끼우면 뒤의
/// 장면 색이 모두 밀려 걷었다. 장면ID를 바꾸면 첫 후보가 바뀌므로 바꿀 때 그때의 색을 묶는다(<c>ProjectEditor.RenameScene</c>).
///
/// 팔레트 차례(1 세이지 · 2 장미 · 3 민트 · 4 연두 · 5 라일락)는 이웃한 차례끼리 색조가 멀게 놓았다 — 「다음 칸」으로 비켜도 가까운 색이 붙지 않는다.
/// </summary>
public static class SceneColors
{
    /// <summary>팔레트의 칸 수 — 칸은 1부터 센다.</summary>
    public const int Count = 5;

    /// <summary>
    /// <b>RGB로 고른 색</b>의 표(2026-11-27 소유자 — 「장면색을 원한다면, RGB 표에서 선택도 할 수 있게」) — 칸 번호와 같은 자리(<c>int</c>)에 <c>0x1RRGGBB</c>로 담는다.
    /// 팔레트 칸(1 ~ 5)과 겹치지 않는다. 판의 면 · 점의 색은 테마가 이 RGB에서 짓는다(<c>Skin.SceneTintOf</c> · <c>SceneMarkOf</c>) — 파일에는 <c>rgb</c>로 적는다.
    /// </summary>
    public const int CustomFlag = 0x1000000;

    /// <summary>RGB로 고른 색인가.</summary>
    public static bool IsCustom(int color) => (color & ~0xFFFFFF) == CustomFlag;

    /// <summary>RGB(<c>0xRRGGBB</c>)를 색 값으로.</summary>
    public static int Custom(int rgb) => CustomFlag | (rgb & 0xFFFFFF);

    /// <summary>RGB로 고른 색의 <c>0xRRGGBB</c>.</summary>
    public static int RgbOf(int color) => color & 0xFFFFFF;

    /// <summary>RGB로 고른 색의 <c>#RRGGBB</c>.</summary>
    public static string HexOf(int color) => "#" + RgbOf(color).ToString("X6", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary><c>#RRGGBB</c>(앞의 #은 없어도 된다 · 대소문자 상관없다)를 RGB로 고른 색 값으로.</summary>
    public static bool TryParseHex(string? text, out int color)
    {
        color = 0;
        string hex = (text ?? string.Empty).Trim().TrimStart('#');

        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int rgb))
        {
            return false;
        }

        color = Custom(rgb);
        return true;
    }

    /// <summary>팔레트 칸인가(1 ~ <see cref="Count"/>).</summary>
    public static bool IsSlot(int color) => color is >= 1 and <= Count;

    /// <summary>장면 색으로 쓸 수 있는 값인가 — 팔레트 칸(1 ~ <see cref="Count"/>)이나 RGB로 고른 색.</summary>
    public static bool IsValid(int color) => IsSlot(color) || IsCustom(color);

    /// <summary>
    /// 장면마다의 칸 — <paramref name="scenes"/>는 이야기 차례(<see cref="ChapterSceneGrouping.Of"/>)다. 고른 칸이 먼저, 그 밖은 자동.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Of(IReadOnlyList<ChapterScene> scenes, IEnumerable<ChapterSceneNote> notes)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        ArgumentNullException.ThrowIfNull(notes);

        var chosen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (ChapterSceneNote note in notes)
        {
            if (note.Color is int color && IsValid(color))
            {
                chosen.TryAdd(note.SceneId, color);
            }
        }

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var recent = new List<int>(2);

        foreach (ChapterScene scene in scenes)
        {
            int color = chosen.TryGetValue(scene.SceneId, out int picked) ? picked : Auto(scene.SceneId, recent);
            result[scene.SceneId] = color;
            recent.Add(color);

            if (recent.Count > 2)
            {
                recent.RemoveAt(0);
            }
        }

        return result;
    }

    /// <summary>그 장면의 칸 — 묶음에 없는 장면(곧 그려지지 않을 장면)은 자동의 첫 후보.</summary>
    public static int ColorOf(IReadOnlyDictionary<string, int> colors, string sceneId) =>
        colors.TryGetValue(sceneId, out int color) ? color : First(sceneId);

    /// <summary>자동 — 첫 후보 칸, 피할 칸이면 팔레트 차례의 다음 칸.</summary>
    private static int Auto(string sceneId, IReadOnlyList<int> avoid)
    {
        int first = First(sceneId) - 1;

        for (int step = 0; step < Count; step++)
        {
            int color = ((first + step) % Count) + 1;

            if (!avoid.Contains(color))
            {
                return color;
            }
        }

        return first + 1;
    }

    /// <summary>장면ID의 첫 후보 칸 — 실행마다 같은 값(FNV-1a · <c>string.GetHashCode</c>는 실행마다 다르다).</summary>
    private static int First(string sceneId)
    {
        uint hash = 2166136261;

        foreach (char letter in sceneId)
        {
            hash ^= letter;
            hash *= 16777619;
        }

        return (int)(hash % Count) + 1;
    }
}
