using System.Globalization;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Vn.App.Strings;
using Vn.App.Views;

namespace Vn.App.Theming;

/// <summary>
/// <b>작가의 「내 테마」</b>(2026-11-26 소유자 — 「작가 개인이 원한다면 편하게, 이미지나 색 정도는 자기만의 것으로 … AI한테 시켜서든 … 교체하거나 색을
/// 조정하기 쉽도록」 · PC마다 · 파일 고치고 다시 켜기). 설정 폴더의 <c>themes/{폴더}/</c> 하나가 테마 하나다:
/// <code>
/// themes/
///   읽어 주세요.txt          쓰는 법(처음 만들 때 생긴다)
///   내 테마/
///     theme.json             이름 · 밑바탕 테마 · 바꿀 값(주석을 달 수 있다)
///     decor.png …            그림 — theme.json에 파일 이름을 적는다
///     theme-problems.txt     읽지 못한 것(있을 때만 — 켤 때마다 다시 쓴다)
/// </code>
/// theme.json에 적을 수 있는 이름은 <see cref="ThemeKeys"/>의 것뿐이다. 값의 꼴(색 · 그림 · 수 …)은 이름이 정한다. 빈 값은 밑바탕 테마의 값 그대로다.
/// 틀린 값 · 없는 파일은 그 값만 건너뛰고 알린다 — 앱은 늘 켜진다.
/// </summary>
internal static class UserThemes
{
    /// <summary>테마 파일의 이름.</summary>
    public const string FileName = "theme.json";

    /// <summary>읽지 못한 것을 적는 파일의 이름.</summary>
    public const string ProblemsFileName = "theme-problems.txt";

    /// <summary>이 판이 읽는 테마 파일의 꼴 번호.</summary>
    public const int FormatVersion = 1;

    private static readonly JsonDocumentOptions Reading = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>테마 하나를 읽은 것 — 덮을 값 · 읽지 못한 것 · 작가가 적은 그대로의 값(복사할 때 옮긴다).</summary>
    internal sealed record Loaded(ResourceDictionary Resources, IReadOnlyList<string> Problems, IReadOnlyDictionary<string, string> Written);

    // ── 찾기 ─────────────────────────────────────────────

    /// <summary>폴더 아래의 「내 테마」 — theme.json이 든 폴더마다 하나, 폴더 이름 차례. 폴더가 없으면 없다.</summary>
    public static IReadOnlyList<ThemeInfo> Discover(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var found = new List<ThemeInfo>();

        foreach (string folder in Directory.GetDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(folder, FileName)))
            {
                continue;
            }

            string folderName = Path.GetFileName(folder);
            (string? name, string? baseId) = ReadHeader(Path.Combine(folder, FileName));
            ThemeInfo parent = ThemeCatalog.FindBuiltIn(baseId) ?? ThemeCatalog.Default;
            string shown = string.IsNullOrWhiteSpace(name) ? folderName : name.Trim();

            found.Add(new ThemeInfo("my:" + folderName, parent.Folder, () => shown, parent.HasControls)
            {
                UserFolder = folder,
                Parent = parent
            });
        }

        return found;
    }

    /// <summary>이름 · 밑바탕만 읽는다 — 목록을 짓는 데는 값까지 읽지 않는다. 못 읽으면 둘 다 null(켤 때 <see cref="Load"/>가 까닭을 알린다).</summary>
    private static (string? Name, string? Base) ReadHeader(string file)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file), Reading);
            JsonElement root = document.RootElement;
            return (Text(root, "name"), Text(root, "base"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, null);
        }
    }

    private static string? Text(JsonElement root, string property) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // ── 읽기 ─────────────────────────────────────────────

    /// <summary>
    /// 「내 테마」의 값을 읽는다. <paramref name="existing"/>은 밑바탕 테마까지 깐 지금의 값 — 그림의 깔기(되풀이 · 늘이기 · 자리)를 거기서 이어 받는다.
    /// </summary>
    public static Loaded Load(ThemeInfo theme, Func<string, object?> existing)
    {
        var resources = new ResourceDictionary();
        var problems = new List<string>();
        var written = new Dictionary<string, string>(StringComparer.Ordinal);
        string folder = theme.UserFolder!;
        string file = Path.Combine(folder, FileName);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(File.ReadAllText(file), Reading);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            problems.Add(string.Format(Ui.Theme_ProblemBadFile, exception.Message));
            return new Loaded(resources, problems, written);
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                problems.Add(string.Format(Ui.Theme_ProblemBadFile, "{ … }"));
                return new Loaded(resources, problems, written);
            }

            if (Text(root, "base") is { } baseId && ThemeCatalog.FindBuiltIn(baseId) is null)
            {
                problems.Add(string.Format(Ui.Theme_ProblemUnknownBase, baseId, theme.BuiltIn.Name()));
            }

            foreach ((string name, JsonElement value) in Values(root))
            {
                if (ThemeKeys.Find(name) is not { } key)
                {
                    problems.Add(string.Format(Ui.Theme_ProblemUnknownName, name));
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Null ||
                    (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())))
                {
                    continue;   // 빈 값 — 밑바탕 테마의 값 그대로
                }

                string? problem = Convert(key, value, folder, existing(key.Name), out object? converted);

                if (problem is not null)
                {
                    problems.Add(problem);
                    continue;
                }

                resources[key.Name] = converted;
                written[key.Name] = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
            }
        }

        return new Loaded(resources, problems, written);
    }

    /// <summary>
    /// 값이 든 자리 — <c>"values": { … }</c>가 기본이고, 다른 묶음 이름(<c>"colors"</c> · <c>"images"</c> …) 아래 적어도 받는다. 맨 바깥에 이름을 바로 적어도
    /// 받는다(AI가 짜임을 바꿔 적어도 읽히게).
    /// </summary>
    private static IEnumerable<(string Name, JsonElement Value)> Values(JsonElement root)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Name is "format" or "name" or "base")
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object && ThemeKeys.Find(property.Name) is null)
            {
                foreach (JsonProperty inner in property.Value.EnumerateObject())
                {
                    yield return (inner.Name, inner.Value);
                }
            }
            else
            {
                yield return (property.Name, property.Value);
            }
        }
    }

    /// <summary>적힌 값 → 리소스 값. 못 읽으면 까닭(작가에게 보일 문장)을 돌려준다.</summary>
    private static string? Convert(ThemeKey key, JsonElement value, string folder, object? existing, out object? converted)
    {
        converted = null;
        string raw = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : value.GetRawText();
        string Bad() => string.Format(Ui.Theme_ProblemBadValue, key.Name, raw, Hint(key.Kind));

        try
        {
            switch (key.Kind)
            {
                case ThemeValueKind.Color:
                    if (!Color.TryParse(raw, out Color color))
                    {
                        return Bad();
                    }

                    converted = color;
                    return null;

                case ThemeValueKind.Brush:
                    if (!Color.TryParse(raw, out Color brushColor))
                    {
                        return Bad();
                    }

                    converted = new SolidColorBrush(brushColor);
                    return null;

                case ThemeValueKind.Paint:
                    if (value.ValueKind == JsonValueKind.String && Color.TryParse(raw, out Color paintColor))
                    {
                        converted = new SolidColorBrush(paintColor);
                        return null;
                    }

                    return Picture(key, value, folder, existing, stretchDefault: Stretch.UniformToFill, out converted);

                case ThemeValueKind.Picture:
                    if (value.ValueKind == JsonValueKind.String && string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        converted = null;
                        return null;
                    }

                    return Picture(key, value, folder, existing, stretchDefault: Stretch.Uniform, out converted);

                case ThemeValueKind.Number:
                    if (value.ValueKind == JsonValueKind.Number)
                    {
                        converted = value.GetDouble();
                        return null;
                    }

                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    {
                        return Bad();
                    }

                    converted = number;
                    return null;

                case ThemeValueKind.Thickness:
                    converted = value.ValueKind == JsonValueKind.Number ? new Thickness(value.GetDouble()) : Thickness.Parse(raw);
                    return null;

                case ThemeValueKind.Corner:
                    converted = value.ValueKind == JsonValueKind.Number ? new CornerRadius(value.GetDouble()) : CornerRadius.Parse(raw);
                    return null;

                case ThemeValueKind.Shadow:
                    converted = ThemeService.ParseShadow(raw);
                    return null;

                case ThemeValueKind.Font:
                    converted = new FontFamily(raw);
                    string first = raw.Split(',')[0].Trim();
                    return first.Contains("://", StringComparison.Ordinal) || InstalledFonts.Named(first) is not null
                        ? null
                        : string.Format(Ui.Theme_ProblemFontMissing, key.Name, first);   // 그래도 적용한다 — 없으면 기본 글꼴로 보인다

                default:
                    return Bad();
            }
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException)
        {
            converted = null;
            return Bad();
        }
    }

    /// <summary>
    /// 그림 파일 → 붓. 값은 파일 이름, 또는 <c>{ "file": "…", "tile": true }</c>. 되풀이(tile)를 적지 않으면 앱의 그림을 따른다 — 앱의 그림이 되풀이되는
    /// 무늬(위 띠 아래 물결)면 되풀이, 아니면 한 장. 한 장이면 늘이기 · 자리도 앱의 그림을 따른다.
    /// </summary>
    private static string? Picture(ThemeKey key, JsonElement value, string folder, object? existing, Stretch stretchDefault, out object? converted)
    {
        converted = null;
        string? name = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim(),
            JsonValueKind.Object when value.TryGetProperty("file", out JsonElement f) && f.ValueKind == JsonValueKind.String => f.GetString()?.Trim(),
            _ => null
        };

        if (string.IsNullOrEmpty(name))
        {
            return string.Format(Ui.Theme_ProblemBadValue, key.Name, value.GetRawText(), Hint(key.Kind));
        }

        var model = existing as TileBrush;
        bool tile = value.ValueKind == JsonValueKind.Object && value.TryGetProperty("tile", out JsonElement t) && t.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? t.GetBoolean()
            : model is { TileMode: not TileMode.None } || TilesByDefault.Contains(key.Name);

        string root = Path.GetFullPath(folder);
        string path = Path.GetFullPath(Path.Combine(root, name));

        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return string.Format(Ui.Theme_ProblemMissingFile, key.Name, name);
        }

        Bitmap bitmap;

        try
        {
            bitmap = new Bitmap(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return string.Format(Ui.Theme_ProblemBadPicture, key.Name, name);
        }

        var brush = new ImageBrush(bitmap)
        {
            Stretch = tile ? Stretch.Fill : model?.Stretch ?? stretchDefault,
            AlignmentX = model?.AlignmentX ?? AlignmentX.Center,
            AlignmentY = model?.AlignmentY ?? AlignmentY.Center
        };

        if (tile)
        {
            brush.TileMode = TileMode.Tile;
            brush.DestinationRect = new RelativeRect(0, 0, bitmap.Size.Width, bitmap.Size.Height, RelativeUnit.Absolute);
        }

        converted = brush;
        return null;
    }

    /// <summary>앱의 그림이 없어도(투명) 그림 파일을 주면 가로로 되풀이하는 자리 — 위 띠 아래 줄.</summary>
    private static readonly HashSet<string> TilesByDefault = new(StringComparer.Ordinal) { "TopBarEdgeBrush", "TopBarEdgeMask" };

    /// <summary>꼴마다 적는 법 — 테마 파일의 설명 끝 · 읽지 못한 까닭에 붙는다.</summary>
    internal static string Hint(ThemeValueKind kind) => kind switch
    {
        ThemeValueKind.Color or ThemeValueKind.Brush => Ui.Theme_KindColor,
        ThemeValueKind.Paint => Ui.Theme_KindPaint,
        ThemeValueKind.Picture => Ui.Theme_KindPicture,
        ThemeValueKind.Number => Ui.Theme_KindNumber,
        ThemeValueKind.Thickness => Ui.Theme_KindThickness,
        ThemeValueKind.Corner => Ui.Theme_KindCorner,
        ThemeValueKind.Shadow => Ui.Theme_KindShadow,
        ThemeValueKind.Font => Ui.Theme_KindFont,
        _ => string.Empty
    };

    // ── 알리기 ─────────────────────────────────────────────

    /// <summary>읽지 못한 것을 테마 폴더의 <see cref="ProblemsFileName"/>에 적는다 — 없으면 그 파일을 지운다. 못 쓰면 그냥 둔다(켜기를 막지 않는다).</summary>
    public static void WriteProblems(ThemeInfo theme, IReadOnlyList<string> problems)
    {
        if (theme.UserFolder is null)
        {
            return;
        }

        string path = Path.Combine(theme.UserFolder, ProblemsFileName);

        try
        {
            if (problems.Count == 0)
            {
                File.Delete(path);
                return;
            }

            File.WriteAllLines(path, [string.Format(Ui.Theme_ProblemsFileHeader, theme.Name()), string.Empty, .. problems.Select(line => "- " + line)]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 못 써도 앱은 켠다 — 상태줄이 알린다.
        }
    }

    // ── 만들기 ─────────────────────────────────────────────

    /// <summary>
    /// 지금 입힌 테마를 복사해 새 「내 테마」 폴더를 만든다 — 바꿀 수 있는 모든 이름을 지금 값 · 설명과 함께 적은 theme.json, 「내 테마」의 그림 파일,
    /// 처음이면 쓰는 법(<c>읽어 주세요.txt</c>). 새 폴더의 경로를 돌려준다.
    /// </summary>
    public static string Create(Application app)
    {
        ThemeInfo current = ThemeService.Current;
        string root = ThemeCatalog.UserThemesFolder;
        Directory.CreateDirectory(root);
        WriteReadme(root);

        string name = Ui.Theme_MyTheme;
        string folderName = name;
        for (int number = 2; Directory.Exists(Path.Combine(root, folderName)); number++)
        {
            folderName = $"{name} {number}";
        }

        string folder = Path.Combine(root, folderName);
        Directory.CreateDirectory(folder);

        IReadOnlyDictionary<string, string> written = ThemeService.UserWritten;

        // 「내 테마」를 복사하면 그 그림 파일도 함께 옮긴다.
        if (current.UserFolder is not null)
        {
            foreach (ThemeKey key in ThemeKeys.All.Where(key => key.Kind is ThemeValueKind.Picture or ThemeValueKind.Paint))
            {
                if (written.TryGetValue(key.Name, out string? file) && File.Exists(Path.Combine(current.UserFolder, file)))
                {
                    File.Copy(Path.Combine(current.UserFolder, file), Path.Combine(folder, file), overwrite: true);
                }
            }
        }

        File.WriteAllText(Path.Combine(folder, FileName), Compose(app, current, folderName, written), new UTF8Encoding(false));
        ThemeCatalog.Refresh();
        return folder;
    }

    /// <summary>쓰는 법을 테마 폴더 맨 위에 둔다 — 이미 있으면 그대로(작가가 고쳤을 수 있다).</summary>
    public static void WriteReadme(string root)
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, Ui.Theme_ReadmeFileName);

        if (!File.Exists(path))
        {
            File.WriteAllText(path, Ui.Theme_Readme.ReplaceLineEndings(), new UTF8Encoding(false));
        }
    }

    /// <summary>theme.json의 글 — 머리 주석, 이름 · 밑바탕, 묶음마다 이름 · 지금 값 · 설명.</summary>
    internal static string Compose(Application app, ThemeInfo current, string name, IReadOnlyDictionary<string, string> written)
    {
        var text = new StringBuilder();

        foreach (string line in Ui.Theme_FileHeader.ReplaceLineEndings("\n").Split('\n'))
        {
            text.Append("// ").AppendLine(line);
        }

        text.AppendLine("{");
        text.AppendLine($"  \"format\": {FormatVersion},");
        text.AppendLine($"  \"name\": {Quote(name)},");
        text.AppendLine($"  \"base\": {Quote(current.BuiltIn.Id)},   // {Ui.Theme_BaseHint}");
        text.AppendLine();
        text.AppendLine("  \"values\": {");

        var lines = new List<(ThemeKeyGroup Group, string Line, string Comment)>();

        foreach (ThemeKey key in ThemeKeys.All)
        {
            object? now = app.TryGetResource(key.Name, Avalonia.Styling.ThemeVariant.Light, out object? value) ? value : null;
            string? shown = written.TryGetValue(key.Name, out string? mine)
                ? (key.Kind is ThemeValueKind.Number ? mine : Quote(mine))
                : FormatValue(key, now);

            if (shown is null)
            {
                continue;   // 이 밑바탕 테마에는 없는 자리 — 적어도 아무 일도 하지 않는다
            }

            lines.Add((key.Group, $"    {Quote(key.Name)}: {shown}", $"{key.Description} [{Hint(key.Kind)}]"));
        }

        ThemeKeyGroup? group = null;

        for (int index = 0; index < lines.Count; index++)
        {
            (ThemeKeyGroup lineGroup, string line, string comment) = lines[index];

            if (lineGroup != group)
            {
                if (group is not null)
                {
                    text.AppendLine();
                }

                text.AppendLine($"    // ── {ThemeKeys.GroupName(lineGroup)} ──");
                group = lineGroup;
            }

            text.Append(line).Append(index < lines.Count - 1 ? "," : string.Empty).Append("   // ").AppendLine(comment);
        }

        text.AppendLine("  }");
        text.AppendLine("}");
        return text.ToString();
    }

    /// <summary>지금 값을 theme.json에 적는 꼴로. 이 자리에 값이 없으면 null(적지 않는다). 그림 · 글꼴은 앱에 든 것이라 빈 값으로 적는다.</summary>
    private static string? FormatValue(ThemeKey key, object? value) => (key.Kind, value) switch
    {
        (ThemeValueKind.Picture, not null) => "\"\"",
        (ThemeValueKind.Font, not null) => "\"\"",
        (ThemeValueKind.Color, Color color) => Quote(Hex(color)),
        (ThemeValueKind.Brush or ThemeValueKind.Paint, ISolidColorBrush brush) => Quote(Hex(brush.Color)),
        (ThemeValueKind.Paint, not null) => "\"\"",
        (ThemeValueKind.Number, double number) => number.ToString("0.###", CultureInfo.InvariantCulture),
        (ThemeValueKind.Thickness, Thickness t) => Quote(string.Join(",", new[] { t.Left, t.Top, t.Right, t.Bottom }.Select(Number))),
        (ThemeValueKind.Corner, CornerRadius c) => c.IsUniform
            ? Number(c.TopLeft)
            : Quote(string.Join(",", new[] { c.TopLeft, c.TopRight, c.BottomRight, c.BottomLeft }.Select(Number))),
        (ThemeValueKind.Shadow, BoxShadows shadows) => Quote(shadows.Count == 0 ? "none" : Shadow(shadows)),
        (ThemeValueKind.Shadow, string text) => Quote(text),
        _ => null
    };

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Hex(Color color) => color.A == 255
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string Shadow(BoxShadows shadows)
    {
        var parts = new List<string>();
        foreach (BoxShadow shadow in shadows)
        {
            parts.Add($"{(shadow.IsInset ? "inset " : string.Empty)}{Number(shadow.OffsetX)} {Number(shadow.OffsetY)} {Number(shadow.Blur)} {Number(shadow.Spread)} {Hex(shadow.Color)}");
        }

        return string.Join(", ", parts);
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text, new JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });
}
