using System.Security.Cryptography;
using System.Text;

namespace Vn.Authoring.Tests;

/// <summary>
/// <b>코어 사본을 여기서 고치지 않았다</b> — 지문 (T2 · R6 §9).
///
/// ⛔ <c>src/Ked.Progression</c>은 <b>런타임의 사본</b>이다. 내보내기 관문이 이 사본으로
/// 판정하고 게임은 저쪽 원본으로 판정하므로, 둘이 갈리면 <b>툴이 통과시킨 것을 게임이
/// 거부한다</b> — 그것도 작가가 다 쓴 다음에.
///
/// 재는 것은 <b>코드</b>지 글이 아니다. 주석은 이 저장소 쪽이 더 두껍고 그건 의도다
/// (실제로 2026-09-16 대조에서 <c>ChapterDefinition.cs</c>의 차이는 주석·빈 줄뿐이었다).
/// 글까지 같아야 한다고 하면 설명을 못 붙인다.
///
/// <b>지문</b>(<see cref="ManifestName"/>) — 이 저장소 안에서만 돈다. 사본을 여기서 고치면 잡힌다. 사본을 고치려면 반드시 지문을
/// 갱신해야 하고, 지문을 갱신하는 것은 <b>옮겨 왔다는 선언</b>이다. 런타임(<c>ked-presentation-runtime</c> — 정본)이 바뀐 것은 옮겨 올 때
/// 사람이 견준다(<c>src/Ked.Progression/UPSTREAM.md</c>의 「재반입 검산」).
///
/// ⛔ 옆 폴더 런타임과 파일을 맞대던 <b>맞대조</b>는 2026-11-18에 걷혔다(소유자 — 이 저장소만 시나리오 작가에게 툴로 건네는 경우가 많다,
/// core-sync V2).
/// </summary>
public sealed class CoreCopyInStepTests
{
    private const string ManifestName = "runtime-sync.txt";
    private const string CopyFolder = "src/Ked.Progression";

    /// <summary>
    /// ⚠ 이쪽에만 있는 것 — <b>저작 전용</b>이라 런타임에 갈 이유가 없다. 도달성 증명은
    /// 쓰기 전에 길이 막혔는지 보는 일이고, 게임은 그냥 그 길을 걷는다.
    /// </summary>
    private static readonly string[] EditorOnly = ["Reachability/"];

    [Fact]
    public void 사본을_여기서_고치면_지문이_어긋난다()
    {
        string manifest = Path.Combine(RepoRoot, CopyFolder, ManifestName);
        string actual = Fingerprint();

        Assert.True(File.Exists(manifest),
            $"지문이 없습니다. 아래 내용을 '{CopyFolder}/{ManifestName}'에 넣으세요:\n\n{actual}");

        string expected = File.ReadAllText(manifest).ReplaceLineEndings("\n");

        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        // 고쳐야 할 것이 사본인지 지문인지는 사람이 정한다 — 옮겨 온 것이면 지문을 갱신하고,
        // 여기서 고친 것이면 되돌린 뒤 저쪽부터 고친다.
        File.WriteAllText(manifest + ".actual", actual);

        Assert.Fail(
            $"코어 사본이 지문과 다릅니다. 이 폴더는 런타임의 사본이라 여기서 고치는 자리가 " +
            $"아닙니다 — 저쪽을 고치고 옮겨 온 것이라면 '{ManifestName}.actual'을 확인한 뒤 " +
            $"'{ManifestName}'으로 바꾸세요.\n\n지금 지문:\n{actual}");
    }

    // ── 기반 ────────────────────────────────────────────────────────────────

    /// <summary>파일마다 한 줄 — `상대경로 공백 코드해시`. 정렬해 두어 diff가 읽힌다.</summary>
    private static string Fingerprint()
    {
        var lines = new List<string>();

        foreach (string relative in SharedFiles())
        {
            byte[] hash = SHA256.HashData(
                Encoding.UTF8.GetBytes(Code(File.ReadAllText(Path.Combine(RepoRoot, CopyFolder, relative)))));

            lines.Add($"{relative} {Convert.ToHexString(hash)[..32].ToLowerInvariant()}");
        }

        return string.Join("\n", lines) + "\n";
    }

    /// <summary>런타임과 같이 쓰는 파일들 — 이쪽 전용 폴더와 빌드 부산물은 뺀다.</summary>
    private static List<string> SharedFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, CopyFolder), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Path.Combine(RepoRoot, CopyFolder), path).Replace('\\', '/'))
            .Where(relative =>
                !relative.StartsWith("obj/", StringComparison.Ordinal) &&
                !relative.StartsWith("bin/", StringComparison.Ordinal) &&
                !EditorOnly.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 주석과 공백을 걷어 낸 코드. <b>글이 아니라 규칙을 재기 위한 것</b>이다.
    ///
    /// ⚠ 문자열 안의 <c>//</c>를 가리지 않는다(지금 이 폴더에는 하나도 없다). 양쪽에 같은
    /// 규칙을 쓰므로 그런 것이 생겨도 <b>엉뚱한 실패</b>가 나지는 않는다 — 차이가 정확히
    /// 그 자리일 때만 못 보고 지나칠 뿐이다.
    /// </summary>
    internal static string Code(string source)
    {
        var builder = new StringBuilder(source.Length);

        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    while (index < source.Length && source[index] != '\n')
                    {
                        index++;
                    }

                    builder.Append(' ');
                    continue;
                }

                if (source[index + 1] == '*')
                {
                    index += 2;

                    while (index + 1 < source.Length &&
                           !(source[index] == '*' && source[index + 1] == '/'))
                    {
                        index++;
                    }

                    index++;
                    builder.Append(' ');
                    continue;
                }
            }

            builder.Append(source[index]);
        }

        return Squeeze(builder.ToString());
    }

    /// <summary>
    /// 공백을 <b>토큰을 갈라 놓는 자리에만</b> 남긴다.
    ///
    /// ⛔ 한 칸으로 줄이기만 해서는 모자랐다(2026-09-16에 잡혔다): 저쪽이 인자 목록을
    /// 여러 줄로 편 것만으로 <c>Resolve(ChapterDefinition</c> ↔ <c>Resolve( ChapterDefinition</c>이
    /// 되어 <b>서식 고침이 코드 변경으로 읽혔다</b>. 그러면 이 테스트는 곧 아무도 안 믿는다.
    ///
    /// ⚠ 문자열 리터럴 안의 앞뒤 공백도 함께 사라진다 — 양쪽에 같은 규칙이라 엉뚱한 실패는
    /// 안 나고, 차이가 정확히 그 자리일 때만 못 보고 지나간다.
    /// </summary>
    private static string Squeeze(string source)
    {
        var builder = new StringBuilder(source.Length);
        bool pending = false;

        foreach (char letter in source)
        {
            if (char.IsWhiteSpace(letter))
            {
                pending = builder.Length > 0;
                continue;
            }

            if (pending && IsWord(builder[^1]) && IsWord(letter))
            {
                builder.Append(' ');
            }

            pending = false;
            builder.Append(letter);
        }

        return builder.ToString();
    }

    /// <summary>붙이면 한 낱말이 되어 버리는 글자인가 — 그 사이에만 칸이 필요하다.</summary>
    private static bool IsWord(char letter) => char.IsLetterOrDigit(letter) || letter == '_';

    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
