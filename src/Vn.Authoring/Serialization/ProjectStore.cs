using System.Text;
using System.Text.Json.Nodes;
using Vn.Authoring.Model;
using Vn.Authoring.Script;
using Vn.Authoring.Strings;

namespace Vn.Authoring.Serialization;

/// <summary>
/// manifest · StoryFile을 물리 파일로 읽고 쓴다.
/// StoryProject 자체는 경로 해석이나 파일 IO를 모른다.
///
/// 디스크 배치(형식 4):
/// <code>
/// project.vnproject.json      목차와 파일을 넘나드는 관계
/// story/&lt;fileId&gt;.vnstory.json       판 — 카드와 그 대본(줄 정체성 + locale별 본문)
/// </code>
///
/// 저장은 목차에서 빠진 판 파일(지운 챕터의 판)을 걷는다 — 툴이 지은 이름만, 모든 쓰기가 끝난 뒤에(<see cref="RemoveStaleStoryFiles"/>).
/// </summary>
public static class ProjectStore
{
    public static ProjectLoadResult Load(string path)
    {
        string openedPath = Path.GetFullPath(path);
        string json = File.ReadAllText(openedPath, new UTF8Encoding(false));
        JsonObject root = JsonSupport.ParseObject(json, Msg.File_Project);

        if (LooksLikeStandaloneStoryFile(root))
        {
            throw new InvalidDataException(
                string.Format(Msg.File_ThisFileIsStoryFileOwned, StoryFileJson.FileExtension, ProjectManifestJson.FileExtension));
        }

        ProjectManifest manifest = ProjectManifestJson.Read(json);

        return new ProjectLoadResult(LoadManifest(openedPath, manifest), openedPath);
    }

    public static void Save(string manifestPath, StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        JsonSupport.ValidateProject(project);

        string fullManifestPath = Path.GetFullPath(manifestPath);
        string rootDirectory = Path.GetDirectoryName(fullManifestPath)
            ?? throw new InvalidOperationException(Msg.File_CanTFindProjectManifest);
        Directory.CreateDirectory(rootDirectory);

        // 직렬화와 경로 검증은 디스크를 건드리기 전에 모두 끝낸다.
        // 한 파일의 데이터가 잘못됐다는 이유로 앞 파일만 새 내용으로 바뀌는 일을 줄인다.
        var writes = new List<PendingWrite>();

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

        // 목차에서 빠진 판 파일을 걷는다 — 쓰기가 모두 끝난 뒤라, 중간에 꺼져도 남는 것은 쓰지 않는 파일뿐이다.
        RemoveStaleStoryFiles(rootDirectory, fullManifestPath, writes.Select(write => write.Path));
    }

    /// <summary>판 파일이 사는 폴더 — 새 판의 기본 자리(<see cref="StoryFile"/>의 <c>story/{Id}.vnstory.json</c>)다.</summary>
    internal const string StoryFolderName = "story";

    /// <summary>
    /// <b>목차에서 빠진 판 파일을 지운다</b>(2026-11-18 소유자 — 챕터를 지운 뒤 저장해도 그 판 파일이 <c>story/</c>에 남아 쌓였다 ·
    /// <c>docs/work-orders/stale-story-files-orders.md</c>). 지우는 것은 툴이 지은 것만이다 — <c>story/</c> 바로 안, 이름이
    /// <see cref="IsToolMadeStoryFileName"/>인 것. 사람이 복사해 둔 파일 · 다른 이름 · 하위 폴더는 건드리지 않는다. 폴더에 다른 목차가 있으면
    /// (두 목차가 <c>story/</c>를 함께 쓰던 옛 배치) 아무것도 지우지 않는다. 지우지 못한 파일은 남긴다 — 저장은 이미 끝났고 다음 저장이 다시 본다.
    /// </summary>
    /// <returns>지운 파일 수.</returns>
    internal static int RemoveStaleStoryFiles(string rootDirectory, string manifestPath, IEnumerable<string> keptPaths)
    {
        string storyFolder = Path.Combine(rootDirectory, StoryFolderName);

        if (!Directory.Exists(storyFolder))
        {
            return 0;
        }

        bool otherManifest = Directory.GetFiles(rootDirectory, "*" + ProjectManifestJson.FileExtension, SearchOption.TopDirectoryOnly)
            .Any(path => !string.Equals(Path.GetFullPath(path), manifestPath, StringComparison.OrdinalIgnoreCase));

        if (otherManifest)
        {
            return 0;
        }

        // 이름이 대소문자만 다른 파일도 남긴다 — 윈도우에서는 같은 파일이다.
        var kept = new HashSet<string>(keptPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        int removed = 0;

        foreach (string file in Directory.GetFiles(storyFolder, "*" + StoryFileJson.FileExtension, SearchOption.TopDirectoryOnly))
        {
            if (!IsToolMadeStoryFileName(Path.GetFileName(file)) || kept.Contains(Path.GetFullPath(file)))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                removed++;
            }
            catch (IOException)
            {
                // 잠겨 있다 — 남긴다. 목차가 가리키지 않으니 읽히지는 않는다.
            }
            catch (UnauthorizedAccessException)
            {
                // 권한이 없다 — 남긴다.
            }
        }

        return removed;
    }

    /// <summary>
    /// 툴이 지은 판 파일 이름인가 — <c>sf_</c> + <see cref="Identifier"/>의 글자 8자 + <c>.vnstory.json</c>(<see cref="Identifier.File"/>의 모양).
    /// </summary>
    internal static bool IsToolMadeStoryFileName(string fileName)
    {
        const string prefix = "sf_";
        const int idLength = 8;

        if (!fileName.StartsWith(prefix, StringComparison.Ordinal) ||
            !fileName.EndsWith(StoryFileJson.FileExtension, StringComparison.Ordinal) ||
            fileName.Length != prefix.Length + idLength + StoryFileJson.FileExtension.Length)
        {
            return false;
        }

        return fileName.AsSpan(prefix.Length, idLength).ToString().All(Identifier.IsIdCharacter);
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
            throw new InvalidDataException(Msg.File_AttachedFileSRelativePath);
        }

        string normalized = relativePath.Replace('\\', '/').Trim();

        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException(string.Format(Msg.File_AttachedFilePathMustBe, relativePath));
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
        {
            throw new InvalidDataException(string.Format(Msg.File_AttachedFilePathCouldPoint, relativePath));
        }

        normalized = string.Join('/', parts);

        if (!normalized.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                string.Format(Msg.File_AttachedFilePathMustEnd, relativePath, expectedExtension));
        }

        return normalized;
    }

    private static StoryProject LoadManifest(
        string manifestPath,
        ProjectManifest manifest)
    {
        string rootDirectory = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidDataException(Msg.File_CanTFindProjectManifest);
        var project = new StoryProject
        {
            Title = manifest.Title,
            StartNodeId = manifest.StartNodeId,
            Speakers = manifest.Speakers.ToList(),
            Board = manifest.Board
        };
        foreach (ProjectStoryFileReference reference in manifest.Files)
        {
            string storyPath = Resolve(rootDirectory, reference.RelativePath, StoryFileJson.FileExtension);

            if (!File.Exists(storyPath))
            {
                throw new FileNotFoundException(
                    string.Format(Msg.File_CanTFindStoryFileProject, reference.RelativePath),
                    storyPath);
            }

            StoryFile file = StoryFileJson.Read(File.ReadAllText(storyPath, new UTF8Encoding(false)));

            if (!string.Equals(file.Id, reference.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    string.Format(Msg.File_StoryFileIdInManifestDiffers, reference.Id, file.Id));
            }

            if (!string.Equals(file.Name, reference.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    string.Format(Msg.File_StoryFileNameInManifestDiffers, reference.Name, file.Name));
            }

            file.RelativePath = reference.RelativePath;
            project.Files.Add(file);
        }

        // 복제하지 않는다 — 리더가 부를 때마다 새 객체를 만들고 매니페스트는 재사용되지 않는다.
        project.Chapters.AddRange(manifest.Chapters);

        JsonSupport.ValidateProject(project);
        return project;
    }

    private static bool LooksLikeStandaloneStoryFile(JsonObject root)
    {
        // manifest에는 fileId가 없다.
        return root["fileId"] is not null &&
            root["nodes"] is JsonArray;
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
            throw new InvalidDataException(string.Format(Msg.File_AttachedFilePathPointsOutside, relativePath));
        }

        return combined;
    }
}

public sealed record ProjectLoadResult(
    StoryProject Project,
    string ManifestPath);
