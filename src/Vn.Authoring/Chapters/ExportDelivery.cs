using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Vn.Authoring.Serialization;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 한 번의 보내기 결과.
/// </summary>
/// <param name="Sent">보낸 형식과 그 자리 — 「진행 JSON → ../game」.</param>
/// <param name="Missing">정했지만 그 폴더가 없어 못 보낸 형식과 자리. 폴더는 만들지 않는다.</param>
/// <param name="Failed">쓰거나 걷지 못한 파일(잠김 · 권한).</param>
/// <param name="Removed">이번에 걷은 지난 파일 — 이 툴이 보낸 그대로였다.</param>
public sealed record ExportDeliveryRun(
    IReadOnlyList<string> Sent,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Failed,
    IReadOnlyList<string> Removed)
{
    public static ExportDeliveryRun Empty { get; } = new([], [], [], []);

    /// <summary>나간 자리 — 「보낸 곳 → 진행 JSON ../game · 대사 Yarn …」. 보낸 것이 없으면 null.</summary>
    public string? Where => Sent.Count == 0 && Removed.Count == 0
        ? null
        : "보낸 곳 → " + string.Join(" · ", Sent) +
          (Removed.Count == 0 ? string.Empty : $" · 지난 파일을 걷었습니다: {string.Join(", ", Removed)}");

    /// <summary>못 보낸 것 — 없으면 null.</summary>
    public string? Problem => Missing.Count == 0 && Failed.Count == 0
        ? null
        : string.Join("\n", new[]
        {
            Missing.Count == 0
                ? null
                : $"보낼 곳이 없어 보내지 않았습니다: {string.Join(" · ", Missing)} — [⚙ 설정]의 「내보내기」에서 폴더를 확인하세요(폴더는 만들지 않습니다).",
            Failed.Count == 0
                ? null
                : $"보낼 곳에 쓰거나 걷지 못했습니다: {string.Join(", ", Failed)} — 파일이 다른 프로그램에 잡혀 있는지 확인하세요."
        }.Where(part => part is not null));
}

/// <summary>
/// <b>보내기</b> — 저장할 때 <c>exported/</c>를 형식마다 보낼 곳(<see cref="ExportDestinations"/>)으로 비춘다(2026-11-17 ·
/// <c>docs/work-orders/export-destinations-orders.md</c>). <c>exported/</c>는 바뀔 때마다 나가는 툴의 작업 사본이고, 보낼 곳(게임 폴더 등
/// 프로젝트 밖)에는 <b>저장할 때만</b> 쓴다 — 저장 안 된 중간 상태가 런타임 저장소에 나가 유니티가 다시 읽거나 git에 잡히지 않게.
///
/// <b>보낸 기록</b>(<c>exported/.delivered.json</c>)이 이 툴이 보낸 파일과 그 바이트의 지문을 든다. 이번에 안 보낸 파일은 <b>기록과 바이트가
/// 같을 때만</b> 걷는다 — 보낼 곳은 다른 프로젝트 · 견본과 나눠 쓰는 폴더라, 사람이 고쳤거나 남이 둔 파일은 건드리지 않는다. 보낼 곳을 바꾸거나
/// 끈 형식은 옛 폴더를 건드리지 않는다(기록만 놓는다).
/// </summary>
public static class ExportDelivery
{
    /// <summary>보낸 기록 — <c>exported/</c> 안. 자리는 프로젝트 폴더 기준 상대 경로로 적는다(PC 사이에서 diff가 조용하게).</summary>
    public const string RecordFileName = ".delivered.json";

    public static ExportDeliveryRun Deliver(string projectPath, ExportDestinations destinations)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentNullException.ThrowIfNull(destinations);

        string exported = Path.GetDirectoryName(ChapterExportService.ExportPathFor(projectPath, "_"))!;

        if (!Directory.Exists(exported))
        {
            return ExportDeliveryRun.Empty;
        }

        string projectFolder = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        string recordPath = Path.Combine(exported, RecordFileName);
        List<Delivered> before = ReadRecord(recordPath, projectFolder);
        var after = new List<Delivered>();
        var sent = new List<string>();
        var missing = new List<string>();
        var failed = new List<string>();
        var removed = new List<string>();

        foreach (ExportFormat format in ExportDestinations.Formats)
        {
            if (destinations.FolderOf(format) is not { } written)
            {
                continue;   // 끈 형식 — 옛 기록은 놓는다(옛 폴더를 건드리지 않는다)
            }

            // 대사 Yarn은 게임 폴더(진행 JSON의 보낼 곳)로 보내지 않는다 — 게임의 Yarn 프로젝트가 compositor의 연출 든 Yarn과 같은
            //   노드 이름을 둘 읽어 컴파일이 깨진다. 설정 창이 받지 않지만 손으로 고친 파일도 여기서 막는다.
            if (format == ExportFormat.DialogueYarn &&
                destinations.Resolve(projectPath, ExportFormat.Progression) is { } game &&
                destinations.Resolve(projectPath, format) is { } yarnFolder &&
                SameFolder(game, yarnFolder))
            {
                failed.Add($"{ExportDestinations.LabelOf(format)} — 게임 폴더와 같은 곳으로는 보내지 않습니다(연출 든 Yarn과 노드 이름이 겹칩니다)");
                after.AddRange(before.Where(item => item.Format == format));
                continue;
            }

            if (destinations.ResolveExisting(projectPath, format) is not { } folder)
            {
                missing.Add($"{ExportDestinations.LabelOf(format)} {written}");
                after.AddRange(before.Where(item => item.Format == format));   // 폴더가 다시 보이면 그때 걷는다
                continue;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string source in SourcesOf(exported, format))
            {
                string name = Path.GetFileName(source);
                names.Add(name);

                try
                {
                    byte[] bytes = File.ReadAllBytes(source);
                    string target = Path.Combine(folder, name);

                    if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(bytes))
                    {
                        File.WriteAllBytes(target, bytes);
                    }

                    after.Add(new Delivered(format, folder, name, Hash(bytes)));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    failed.Add(name);
                }
            }

            // 이번에 안 보낸 것 — 같은 폴더에 이 툴이 보낸 그대로 남아 있으면 걷는다.
            foreach (Delivered stale in before.Where(item =>
                         item.Format == format && SameFolder(item.Folder, folder) && !names.Contains(item.Name)))
            {
                string target = Path.Combine(folder, stale.Name);

                try
                {
                    if (File.Exists(target) && Hash(File.ReadAllBytes(target)) == stale.Hash)
                    {
                        File.Delete(target);
                        removed.Add(stale.Name);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    failed.Add(stale.Name);
                    after.Add(stale);   // 다음에 다시 걷는다
                }
            }

            sent.Add($"{ExportDestinations.LabelOf(format)} {written}");
        }

        WriteRecord(recordPath, projectFolder, after);

        return new ExportDeliveryRun(sent, missing, failed, removed);
    }

    /// <summary>그 형식이 <c>exported/</c>에서 보낼 파일들 — 진행 JSON은 바로 아래, 대사 Yarn은 <c>yarn/</c> 아래.</summary>
    private static IEnumerable<string> SourcesOf(string exported, ExportFormat format)
    {
        (string folder, string pattern) = format switch
        {
            ExportFormat.Progression => (exported, "*" + ChapterExportService.ExportFileSuffix),
            _ => (Path.Combine(exported, ChapterYarnExporter.FolderName), "*" + ChapterYarnExporter.FileExtension)
        };

        return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, pattern).Order(StringComparer.Ordinal) : [];
    }

    private sealed record Delivered(ExportFormat Format, string Folder, string Name, string Hash);

    private static bool SameFolder(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>기록 — 읽지 못하면 빈 기록이다(그러면 걷지 않을 뿐이다 — 지우는 일은 늘 아는 것만 한다).</summary>
    private static List<Delivered> ReadRecord(string path, string projectFolder)
    {
        try
        {
            if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root)
            {
                return [];
            }

            return [.. (root["files"] as JsonArray ?? []).OfType<JsonObject>()
                .Select(item => (
                    Format: Enum.TryParse((string?)item["format"], ignoreCase: true, out ExportFormat format) ? format : (ExportFormat?)null,
                    Folder: (string?)item["folder"],
                    Name: (string?)item["name"],
                    Hash: (string?)item["sha256"]))
                .Where(item => item.Format is not null && item.Folder is not null && item.Name is not null && item.Hash is not null)
                .Select(item => new Delivered(
                    item.Format!.Value, ExportDestinations.ResolveFolder(Path.Combine(projectFolder, "_"), item.Folder!), item.Name!, item.Hash!))];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
                                              or InvalidOperationException or FormatException)
        {
            return [];
        }
    }

    private static void WriteRecord(string path, string projectFolder, List<Delivered> items)
    {
        try
        {
            if (items.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            var files = new JsonArray();

            foreach (Delivered item in items.OrderBy(item => item.Format).ThenBy(item => item.Name, StringComparer.Ordinal))
            {
                files.Add(new JsonObject
                {
                    ["format"] = item.Format.ToString(),
                    ["folder"] = ExportDestinations.Relativize(Path.Combine(projectFolder, "_"), item.Folder),
                    ["name"] = item.Name,
                    ["sha256"] = item.Hash
                });
            }

            JsonSupport.WriteAtomic(path, JsonSupport.ToDeterministicText(new JsonObject
            {
                ["_안내"] = "이 툴이 저장할 때 보낼 곳으로 보낸 파일 — 이번에 안 보낸 파일은 바이트가 이 지문과 같을 때만 걷는다.",
                ["files"] = files
            }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 기록을 못 남기면 다음에 걷지 못할 뿐이다 — 보낸 것은 이미 나갔다.
        }
    }
}
