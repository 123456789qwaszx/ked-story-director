using System.Security.Cryptography;

namespace Vn.Authoring.Serialization;

/// <summary>
/// <b>프로젝트 파일의 디스크 지문</b> (2026-11-19 소유자 · writer-release WR1) — 목차(<c>*.vnproject.json</c>)와
/// <c>story/</c> 바로 안의 판 파일(<c>*.vnstory.json</c>)의 내용 해시. 열 때와 저장한 뒤에 찍어 두고, 그 사이 다른 곳(Git으로 받기 · 다른 PC의
/// 동기화 · 손으로 고침)이 파일을 바꿨는지 견준다 — 바뀌었으면 저장이 그것을 말없이 덮지 않는다(<c>AuthoringSession</c>).
///
/// 툴이 저장할 때 쓰는 파일만 본다 — <c>exported/</c>(저장마다 다시 낸다) · <c>runtime.target.json</c>(이 PC의 보낼 곳)은 보지 않는다.
/// 내용으로 견준다 — 날짜만 바뀐 파일(같은 내용으로 다시 받기)은 바뀐 것이 아니다.
/// </summary>
public sealed class ProjectDiskStamp
{
    private readonly IReadOnlyDictionary<string, string> _hashes;

    private ProjectDiskStamp(IReadOnlyDictionary<string, string> hashes) => _hashes = hashes;

    /// <summary>그 목차와 판 파일들을 지금 읽어 찍는다. 없는 파일은 없는 대로 찍힌다(지워진 것도 바뀐 것이다).</summary>
    public static ProjectDiskStamp Read(string manifestPath)
    {
        string manifest = Path.GetFullPath(manifestPath);
        string root = Path.GetDirectoryName(manifest) ?? string.Empty;
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Add(hashes, root, manifest);

        string story = Path.Combine(root, ProjectStore.StoryFolderName);

        if (Directory.Exists(story))
        {
            foreach (string file in Directory.GetFiles(story, "*" + StoryFileJson.FileExtension, SearchOption.TopDirectoryOnly))
            {
                Add(hashes, root, file);
            }
        }

        return new ProjectDiskStamp(hashes);
    }

    /// <summary>두 지문이 같은가 — 같은 파일들이 같은 내용이다.</summary>
    public bool SameAs(ProjectDiskStamp other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return _hashes.Count == other._hashes.Count &&
               _hashes.All(pair => other._hashes.TryGetValue(pair.Key, out string? hash) && string.Equals(hash, pair.Value, StringComparison.Ordinal));
    }

    private static void Add(Dictionary<string, string> hashes, string root, string file)
    {
        string key = Path.GetRelativePath(root, file);

        try
        {
            hashes[key] = File.Exists(file) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) : "(없음)";
        }
        catch (IOException)
        {
            hashes[key] = "(읽지 못함)";   // 다른 프로그램이 쓰는 중이다 — 바뀐 것으로 본다(다음에 다시 견준다)
        }
        catch (UnauthorizedAccessException)
        {
            hashes[key] = "(읽지 못함)";
        }
    }
}
