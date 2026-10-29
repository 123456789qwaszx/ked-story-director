using System.Text;
using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Serialization;

/// <summary>
/// manifest·대본·StoryFile을 물리 파일로 읽고 쓴다.
/// StoryProject 자체는 경로 해석이나 파일 IO를 모른다.
///
/// 디스크 배치:
/// <code>
/// project.vnproject.json      목차와 파일을 넘나드는 관계
/// script/&lt;scriptId&gt;.vnscript.json   대본 산출물 (줄 정체성 + locale별 본문)
/// story/&lt;fileId&gt;.vnstory.json       노드
/// </code>
///
/// ⛔ <b>연출은 읽지 않는다</b> (2026-10-29 — 연출은 <c>ked-story-compositor</c>의 것이다). 연출을 담은 옛
/// 프로젝트를 열면 연출 데이터는 모델에 들어오지 않고, 그대로 저장하면 사라진다. 그래서 <b>열 때 원본을 한 번
/// 남긴다</b> — <see cref="PresentationBackupFolder"/>에 manifest · 대본 · StoryFile · 발행 결과 파일 · 게임 정의를
/// 같은 상대 경로로 복사한다. compositor는 그 폴더에서 연출을 가져간다.
/// </summary>
public static class ProjectStore
{
    public static ProjectLoadResult Load(string path)
    {
        string openedPath = Path.GetFullPath(path);
        string json = File.ReadAllText(openedPath, new UTF8Encoding(false));
        JsonObject root = JsonSupport.ParseObject(json, "VnTool 프로젝트");

        if (LooksLikeStandaloneStoryFile(root))
        {
            throw new InvalidDataException(
                "이 파일은 프로젝트가 소유하는 StoryFile입니다. " +
                $"개별 {StoryFileJson.FileExtension} 대신 {ProjectManifestJson.FileExtension} manifest를 여세요.");
        }

        if (LooksLikeStandaloneScript(root))
        {
            throw new InvalidDataException(
                "이 파일은 프로젝트가 소유하는 대본입니다. " +
                $"개별 {ScriptDocumentJson.FileExtension} 대신 {ProjectManifestJson.FileExtension} manifest를 여세요.");
        }

        ProjectManifest manifest = ProjectManifestJson.Read(json);
        StoryProject project = LoadManifest(openedPath, manifest, out bool storyCarriesPresentation);

        string? backup = manifest.CarriesPresentation || storyCarriesPresentation
            ? BackUpPresentationOriginal(openedPath, root, manifest)
            : null;

        return new ProjectLoadResult(project, openedPath, backup);
    }

    /// <summary>연출을 담은 옛 프로젝트의 원본을 남기는 폴더 이름(manifest 옆). 겹치면 뒤에 <c>-2</c>, <c>-3</c>…</summary>
    public const string PresentationBackupFolder = "presentation-original";

    /// <summary>옛 발행 결과 파일의 확장자 — 이 툴은 더 읽지 않고, 원본을 남길 때만 따라간다.</summary>
    private const string LegacyResultsExtension = ".vnresults.json";

    /// <summary>
    /// manifest가 가리키는 파일 전부(대본 · StoryFile · 발행 결과)와 manifest · 게임 정의를 같은 상대 경로로 복사한다.
    ///
    /// ⚠ <b>같은 원본을 두 번 남기지 않는다</b> — 열기만 하고 저장하지 않으면 다음에 또 연출을 만난다. 이미 있는
    /// 폴더의 내용이 지금 원본과 바이트까지 같으면 그 폴더를 그대로 쓰고, 다르면(옛 툴에서 그 사이 고쳤다) 새 번호를 단다.
    /// </summary>
    private static string BackUpPresentationOriginal(string manifestPath, JsonObject root, ProjectManifest manifest)
    {
        string rootDirectory = Path.GetDirectoryName(manifestPath)!;
        var relativePaths = new List<string> { Path.GetFileName(manifestPath) };
        relativePaths.AddRange(manifest.Scripts.Select(script => script.RelativePath));
        relativePaths.AddRange(manifest.Files.Select(file => file.RelativePath));

        // 발행 결과 파일 — 다른 부속 파일처럼 프로젝트 안의 상대 경로일 때만 따라간다(손댄 manifest가 밖을
        // 가리키면 남기지 않는다. 그 경로로는 옛 툴도 열지 않았다).
        if ((string?)root["results"] is { Length: > 0 } results)
        {
            try
            {
                relativePaths.Add(NormalizeRelativePath(results, LegacyResultsExtension));
            }
            catch (InvalidDataException)
            {
            }
        }

        relativePaths.Add(Definition.GameDefinition.FileName);

        var sources = relativePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(relative => (Relative: relative, Source: Path.Combine(rootDirectory, relative)))
            .Where(item => File.Exists(item.Source))
            .ToList();

        for (int attempt = 1; ; attempt++)
        {
            string folder = Path.Combine(
                rootDirectory,
                attempt == 1 ? PresentationBackupFolder : $"{PresentationBackupFolder}-{attempt}");

            if (Directory.Exists(folder))
            {
                bool same = sources.All(item =>
                {
                    string copy = Path.Combine(folder, item.Relative);
                    return File.Exists(copy) && File.ReadAllBytes(copy).AsSpan()
                        .SequenceEqual(File.ReadAllBytes(item.Source));
                });

                if (same)
                {
                    return folder;
                }

                continue;
            }

            try
            {
                foreach ((string relative, string source) in sources)
                {
                    string target = Path.Combine(folder, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 남기지 못한 채 열면 저장 한 번에 연출이 사라진다 — 열지 않는다.
                throw new IOException(
                    $"연출을 담은 옛 프로젝트라 원본을 '{folder}'에 남기려 했지만 실패했습니다. " +
                    $"연출을 잃지 않으려고 열지 않습니다. ({exception.Message})",
                    exception);
            }

            return folder;
        }
    }

    public static void Save(string manifestPath, StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        JsonSupport.ValidateProject(project);

        string fullManifestPath = Path.GetFullPath(manifestPath);
        string rootDirectory = Path.GetDirectoryName(fullManifestPath)
            ?? throw new InvalidOperationException("프로젝트 manifest 디렉터리를 찾을 수 없습니다.");
        Directory.CreateDirectory(rootDirectory);

        // 직렬화와 경로 검증은 디스크를 건드리기 전에 모두 끝낸다.
        // 한 파일의 데이터가 잘못됐다는 이유로 앞 파일만 새 내용으로 바뀌는 일을 줄인다.
        var writes = new List<PendingWrite>();

        foreach (ScriptDocument script in project.EnumerateScripts())
        {
            writes.Add(new PendingWrite(
                Resolve(rootDirectory, DefaultScriptPath(script.Id), ScriptDocumentJson.FileExtension),
                ScriptDocumentJson.Write(script)));
        }

        foreach (StoryFile file in project.Files)
        {
            writes.Add(new PendingWrite(
                Resolve(rootDirectory, file.RelativePath, StoryFileJson.FileExtension),
                StoryFileJson.Write(file)));
        }

        string manifestText = ProjectManifestJson.Write(project);

        // 부속 파일을 먼저 안전하게 교체한 뒤 manifest를 마지막에 교체한다.
        // 새 manifest가 아직 쓰이지 않은 파일을 가리키는 상태를 만들지 않기 위해서다.
        foreach (PendingWrite write in writes)
        {
            JsonSupport.WriteAtomic(write.Path, write.Text);
        }

        JsonSupport.WriteAtomic(fullManifestPath, manifestText);
    }

    public static string DefaultRelativePath(string fileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        return $"story/{fileId}{StoryFileJson.FileExtension}";
    }

    public static string DefaultScriptPath(string scriptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptId);
        return $"script/{scriptId}{ScriptDocumentJson.FileExtension}";
    }

    public static string NormalizeRelativeStoryPath(string relativePath) =>
        NormalizeRelativePath(relativePath, StoryFileJson.FileExtension);

    /// <summary>
    /// 프로젝트 디렉터리 안의 상대 경로로 정규화한다.
    /// 프로젝트 밖을 가리킬 수 있는 경로는 읽지 않는다. manifest 하나로 임의의 파일을
    /// 덮어쓸 수 있게 되면 프로젝트를 여는 일 자체가 위험해진다.
    /// </summary>
    public static string NormalizeRelativePath(string relativePath, string expectedExtension)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("부속 파일 상대 경로가 비어 있습니다.");
        }

        string normalized = relativePath.Replace('\\', '/').Trim();

        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"부속 파일 경로 '{relativePath}'는 상대 경로여야 합니다.");
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
        {
            throw new InvalidDataException($"부속 파일 경로 '{relativePath}'가 프로젝트 밖을 가리킬 수 있습니다.");
        }

        normalized = string.Join('/', parts);

        if (!normalized.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"부속 파일 경로 '{relativePath}'는 {expectedExtension}으로 끝나야 합니다.");
        }

        return normalized;
    }

    private static StoryProject LoadManifest(
        string manifestPath,
        ProjectManifest manifest,
        out bool storyCarriesPresentation)
    {
        string rootDirectory = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidDataException("프로젝트 manifest 디렉터리를 찾을 수 없습니다.");
        var project = new StoryProject
        {
            FormatVersion = StoryProject.CurrentFormatVersion,
            Title = manifest.Title,
            StartNodeId = manifest.StartNodeId
        };
        storyCarriesPresentation = false;
        var scripts = new List<ScriptDocument>();

        foreach (ScriptFileReference reference in manifest.Scripts)
        {
            string scriptPath = Resolve(rootDirectory, reference.RelativePath, ScriptDocumentJson.FileExtension);

            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException(
                    $"프로젝트가 참조하는 대본을 찾을 수 없습니다: {reference.RelativePath}",
                    scriptPath);
            }

            ScriptDocument script = ScriptDocumentJson.Read(
                File.ReadAllText(scriptPath, new UTF8Encoding(false)));

            if (!string.Equals(script.Id, reference.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"manifest의 대본 Id '{reference.Id}'와 파일의 Id '{script.Id}'가 다릅니다.");
            }

            scripts.Add(script);
        }

        // 카드마다 제 대본을 꽂는다(S2) — 어느 카드도 안 가리키는 대본은 받아 가는 카드가 없으니 여기서 버려진다(S1).
        var table = new ScriptTable(scripts);

        foreach (ProjectStoryFileReference reference in manifest.Files)
        {
            string storyPath = Resolve(rootDirectory, reference.RelativePath, StoryFileJson.FileExtension);

            if (!File.Exists(storyPath))
            {
                throw new FileNotFoundException(
                    $"프로젝트가 참조하는 StoryFile을 찾을 수 없습니다: {reference.RelativePath}",
                    storyPath);
            }

            StoryFile file = StoryFileJson.Read(
                File.ReadAllText(storyPath, new UTF8Encoding(false)),
                out bool carried,
                table);
            storyCarriesPresentation |= carried;

            if (!string.Equals(file.Id, reference.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"manifest의 StoryFile Id '{reference.Id}'와 파일의 Id '{file.Id}'가 다릅니다.");
            }

            if (!string.Equals(file.Name, reference.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"manifest의 StoryFile 이름 '{reference.Name}'과 파일의 이름 '{file.Name}'이 다릅니다.");
            }

            file.RelativePath = reference.RelativePath;
            project.Files.Add(file);
        }

        project.Links.AddRange(manifest.Links.Select(link => link.Clone()));

        // ⚠ 복제하지 않는다 — 리더가 부를 때마다 새 객체를 만들고 매니페스트는 재사용되지
        //    않는다(WriterSpeaker는 가변이라 복제한다).
        project.Chapters.AddRange(manifest.Chapters);
        project.WriterSpeakers.AddRange(manifest.WriterSpeakers.Select(speaker => speaker.Clone()));

        JsonSupport.ValidateProject(project);
        return project;
    }

    private static bool LooksLikeStandaloneStoryFile(JsonObject root)
    {
        return (int?)root["formatVersion"] == StoryFileJson.CurrentFormatVersion &&
            root["fileId"] is not null &&
            root["nodes"] is JsonArray;
    }

    private static bool LooksLikeStandaloneScript(JsonObject root)
    {
        return (int?)root["formatVersion"] == ScriptDocumentJson.CurrentFormatVersion &&
            root["scriptId"] is not null &&
            root["lines"] is JsonArray;
    }

    private sealed record PendingWrite(string Path, string Text);

    private static string Resolve(string rootDirectory, string relativePath, string expectedExtension)
    {
        string normalized = NormalizeRelativePath(relativePath, expectedExtension);
        string fullRoot = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string combined = Path.GetFullPath(Path.Combine(
            fullRoot,
            normalized.Replace('/', Path.DirectorySeparatorChar)));

        if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"부속 파일 경로 '{relativePath}'가 프로젝트 디렉터리 밖을 가리킵니다.");
        }

        return combined;
    }
}

/// <param name="PresentationBackupDirectory">
/// 연출을 담은 옛 프로젝트였으면 원본을 남긴 폴더(<see cref="ProjectStore.PresentationBackupFolder"/>), 아니면 null.
/// </param>
public sealed record ProjectLoadResult(
    StoryProject Project,
    string ManifestPath,
    string? PresentationBackupDirectory = null);
