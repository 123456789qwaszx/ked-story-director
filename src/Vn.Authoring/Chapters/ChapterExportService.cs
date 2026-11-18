using Vn.Authoring.Model;
using System.Text;

namespace Vn.Authoring.Chapters;

/// <summary>
/// 한 번의 내보내기 결과 — 못 나간 챕터들. 둘 다 비면 전부 나갔다는 뜻이다.
/// </summary>
/// <param name="Refused">검증 오류로 거부된 챕터. <b>파일은 손대지 않았다.</b></param>
/// <param name="Failed">쓰기 자체가 실패한 챕터(잠김·권한).</param>
/// <param name="Written">실제로 쓴(또는 같은 내용이라 둔) 파일 — 사람이 "어디로 나갔나"를 보는 자리.</param>
/// <param name="Delivery">
/// 저장 때의 보내기(<see cref="ExportDelivery"/>) 결과 — 저장 직후의 내보내기에만 있다. 그 밖의 다시 읽기는 <c>exported/</c>만 쓴다.
/// </param>
/// <param name="Removed">이번에 걷은 옛 진행 JSON의 챕터 — 프로젝트에 없는 챕터(지웠거나 이름을 바꿨다)의 것.</param>
/// <param name="RemoveFailed">걷어야 하는데 못 걷은 옛 진행 JSON의 챕터(잠김 · 권한).</param>
/// <param name="RefusalErrors">
/// 막힌 챕터들의 오류 — 검증기의 것과 진행 코어의 거부(<c>CoreRefusals</c> — 검증 캐시에는 없다)를 함께. 검증 보고는 고른 챕터 하나를 그리므로,
/// 결론(「아래 오류를 고치면」)의 그 오류가 아래에 서도록 이것을 더해 그린다(2026-11-18 소유자 — 「진행 JSON 미출력인데 보고할 것이 없다」).
/// </param>
public sealed record ChapterExportRun(
    IReadOnlyList<string> Refused,
    IReadOnlyList<string> Failed,
    IReadOnlyList<string>? DeploymentBlocked = null,
    IReadOnlyDictionary<string, string>? Checksums = null,
    IReadOnlyList<string>? Written = null,
    ExportDeliveryRun? Delivery = null,
    IReadOnlyList<string>? Removed = null,
    IReadOnlyList<string>? RemoveFailed = null,
    IReadOnlyList<string>? YarnWritten = null,
    IReadOnlyList<string>? YarnProblems = null,
    IReadOnlyList<ChapterDiagnostic>? RefusalErrors = null)
{
    public static ChapterExportRun Empty { get; } = new([], [], [], new Dictionary<string, string>());

    /// <summary>
    /// 나간 자리 — <b>늘 있다</b>(나간 것이 있을 때). 저절로 나가므로 어디로 갔는지 화면이
    /// 말해야 한다. 자동 + 보고.
    /// </summary>
    public string? WhereNotice
    {
        get
        {
            string? removed = Removed is { Count: > 0 } gone
                ? "지운 · 이름을 바꾼 챕터의 옛 진행 JSON을 걷었습니다: " +
                  string.Join(", ", gone.Select(id => id + ChapterExportService.ExportFileSuffix))
                : null;

            string? delivered = Delivery?.Where;

            if (Written is not { Count: > 0 } written)
            {
                return string.Join(" · ", new[] { removed, delivered }.Where(part => part is not null)) is { Length: > 0 } only ? only : null;
            }

            string folder = Path.GetFileName(Path.GetDirectoryName(written[0]) ?? string.Empty);
            string files = string.Join(", ", written.Select(Path.GetFileName));

            string yarn = YarnWritten is { Count: > 0 } scripts
                ? $" · 대사 Yarn → {folder}/{ChapterYarnExporter.FolderName}/ ({scripts.Count}개)"
                : string.Empty;

            return $"진행 JSON → {folder}/ ({files})" + yarn + (delivered is null ? string.Empty : $" · {delivered}") +
                   (removed is null ? string.Empty : $" · {removed}");
        }
    }

    public IReadOnlyList<string> Blocked => DeploymentBlocked ?? [];

    public bool AllExported => Refused.Count == 0 && Failed.Count == 0 && Blocked.Count == 0;

    /// <summary>
    /// 검증 보고 맨 위에 세울 결론 — <b>못 나갔거나 옛 파일을 못 걷었을 때만 있다.</b> 잘 나간 것은 말하지 않는다.
    /// </summary>
    public string? Notice => AllExported && RemoveFailed is not { Count: > 0 } &&
                             YarnProblems is not { Count: > 0 } && Delivery?.Problem is null
        ? null
        : string.Join("\n", new[]
        {
            Refused.Count == 0
                ? null
                : $"검증 오류로 진행 JSON이 나가지 않았습니다: {string.Join(", ", Refused)}" +
                  $" — 아래 오류를 고치면 저절로 나갑니다({ChapterExportService.ExportFolderName}/).",
            Failed.Count == 0
                ? null
                : $"진행 JSON을 쓰지 못했습니다: {string.Join(", ", Failed)}" +
                  $" — {ChapterExportService.ExportFolderName}/ 의 파일이 다른 프로그램에 " +
                  "잡혀 있는지 확인하세요.",
            Blocked.Count == 0
                ? null
                : $"출시 기준선 뒤 선택지 순번이 바뀌어 진행 JSON 갱신을 막았습니다: " +
                  $"{string.Join(", ", Blocked)} — 과거 선택 이력의 OptionIndex가 다른 선택을 " +
                  $"가리킬 수 있습니다. {ChapterReleaseBaseline.FolderName}/ 기준선과 순서를 확인하세요.",
            RemoveFailed is not { Count: > 0 } kept
                ? null
                : $"지운 · 이름을 바꾼 챕터의 옛 진행 JSON을 걷지 못했습니다: {string.Join(", ", kept)}" +
                  " — 파일이 다른 프로그램에 잡혀 있는지 확인하세요. 그냥 두면 게임이 없는 챕터를 계속 싣습니다.",
            YarnProblems is not { Count: > 0 } yarn
                ? null
                : $"대사 Yarn이 나가지 않은 장면이 있습니다 — 그 장면의 옛 파일은 그대로 둡니다:\n" +
                  string.Join("\n", yarn.Select(problem => "• " + problem)),
            Delivery?.Problem
        }.Where(part => part is not null));
}

/// <summary>
/// 챕터를 <b>증명하고 내보내는</b> 자리. 검증 캐시와 내보내기가 한 객체인 이유는 규칙
/// 하나 때문이다 — <b>같은 증명을 두 번 돌리지 않는다</b>. 화면은 보고 패널을 세우려고
/// 어차피 한 번 증명하므로, 내보내기가 안에서 또 증명하면 챕터 하나당 200ms를 두 번 치른다.
///
/// <b>왜 화면 밖에 있나</b> — 이 정책이 화면 코드비하인드 안에 살면 <b>밖에서 보이지 않는다.</b>
/// 이를테면 동기화는 고른 챕터만 도는데 내보내기는 전 챕터를 돈다는 사실이 묻히면, 저작
/// 관문을 걸 때 "안 연 챕터가 전부 거부된다"는 것을 놓친다. 규칙이 안 보이는
/// 자리에 있으면 그 규칙을 지킬 수 없다.
///
/// 화면도 파일 대화상자도 모른다 — 프로젝트 경로 문자열 하나와 챕터 목록만 받는다.
/// </summary>
public sealed class ChapterExportService
{
    /// <summary>진행 JSON이 놓이는 폴더 이름. 프로젝트 파일 옆이다.</summary>
    public const string ExportFolderName = "exported";

    /// <summary>모델이 그대로면 다시 증명하지 않는다 — 챕터별 (지문, 결과) 한 벌.</summary>
    private readonly Dictionary<string, (string Fingerprint, ChapterValidationResult Result)>
        _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// 실제로 증명을 돌린 횟수. 테스트가 "일을 몇 번 했는가"를 보는 창이다 —
    /// 시간(ms)은 기계마다 다르지만 횟수는 규칙이고, 느려지는 회귀는 언제나 횟수가
    /// 먼저 는다. <b>인스턴스에 갇혀 있어</b> 테스트가 병렬로 돌아도 서로 섞이지 않는다.
    /// </summary>
    public int ValidationComputeCount { get; private set; }

    /// <summary>그 챕터의 진행 JSON이 놓일 자리.</summary>
    public static string ExportPathFor(string projectPath, string chapterId) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(projectPath))!,
        ExportFolderName,
        chapterId + ExportFileSuffix);

    /// <summary>진행 JSON 파일 이름 규칙 — 게임 폴더에서도 같다(런타임 예제도 이 이름을 쓴다).</summary>
    public const string ExportFileSuffix = ".progression.json";

    /// <summary>
    /// 챕터 하나의 검증 결과 — <b>모델이 그대로면 지난 결과를 그대로 준다</b>.
    ///
    /// 검증은 순수 함수다: 챕터 모델과 판(프로젝트)만 읽고 상태공간을 훑는다(챕터 하나에
    /// 수십~수백 ms). 화면은 갱신할 때마다 이것을 부르는데, 판을 한 번 다시 그리는 값이
    /// 6ms인 옆에서 매번 처음부터 돌리면 400ms를 태운다.
    ///
    /// <b>지문은 검증이 실제로 읽는 것 — 모델 — 에서 짓는다</b>. 산출물 파일의 해시로 지으면
    /// 되돌리기·조건 변경·출력 실패에 지문이 안 움직여 옛 판정을 되쓸 수 있다.
    /// </summary>
    /// <param name="project">
    /// 있으면 판(카드의 대본 · 분기 카드)까지 검증한다(<see cref="ChapterValidator"/>). 판의 카드가 <b>지문에 함께 들어가므로</b>,
    /// 카드를 고친 순간 캐시가 깨지고 다시 증명한다 — 그러지 않으면 방금 고친 카드를 모르는 옛 결론이 남아 "고쳤는데 계속 거부한다"가 된다.
    /// </param>
    public ChapterValidationResult ValidationFor(
        ChapterEntry entry,
        StoryProject? project = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string fingerprint = Fingerprint(entry.Model, BoardNames(project, entry.ChapterId));

        if (_cache.TryGetValue(entry.ChapterId, out var cached) &&
            string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return cached.Result;
        }

        ChapterValidationResult result = ChapterValidator.Validate(entry.Model, project);
        ValidationComputeCount++;
        _cache[entry.ChapterId] = (fingerprint, result);

        return result;
    }

    /// <summary>
    /// <b>모든</b> 챕터의 진행 JSON을 낸다. 작가의 Yarn은 라이브 출력이 저절로 쓰므로 챕터도
    /// 사람 손을 기다리지 않는다 — 누른 순간의 낡은 파일이 남지 않게, <b>고른 챕터만</b>이
    /// 아니라 전부 낸다.
    ///
    /// G8의 규칙: <b>검증을 통과해야만 나간다.</b> 거부되면 그 챕터의 파일은
    /// 손대지 않는다 — 낡은 파일이 남더라도 쓰레기로 덮는 것보다 낫다(런타임이 읽는 건
    /// 언제나 "한 번은 옳았던" 판이다).
    ///
    /// <b>이 함수는 전 챕터를 돈다.</b>
    /// </summary>
    /// <remarks>
    /// <c>exported/</c>에만 쓴다. 프로젝트 밖의 보낼 곳(게임 폴더 등)에는 <b>저장할 때만</b> <see cref="ExportDelivery"/>가 이 폴더를 비춘다(2026-11-17 ·
    /// ⛔ 그 전에는 여기서 바뀔 때마다 게임 폴더에도 진행 JSON을 썼다).
    /// </remarks>
    public ChapterExportRun ExportAll(
        IReadOnlyList<ChapterEntry> entries,
        string? projectPath,
        StoryProject? project = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (projectPath is null)
        {
            return ChapterExportRun.Empty;
        }

        var refused = new List<string>();
        var failed = new List<string>();
        var blocked = new List<string>();
        var checksums = new Dictionary<string, string>(StringComparer.Ordinal);
        var written = new List<string>();
        var yarnWritten = new List<string>();
        var yarnProblems = new List<string>();
        var refusalErrors = new List<ChapterDiagnostic>();

        // 대사 Yarn의 장부 — 이번에 다룬 챕터와, 그 챕터들이 지금 가진 파일 이름(막혀서 안 쓴 것도 — 옛 파일을 지키려고).
        var yarnChapters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var yarnOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (ChapterEntry entry in entries)
        {
            // 에피소드가 없는 챕터는 <b>낼 것이 없을 뿐</b>이다(2026-11-18 소유자 — 「빈 챕터를 띄운경우에는 보고할 것 없이 검증 오류만」) — 막힌 것이
            // 아니므로 결론을 세우지 않는다. 파일은 막혔을 때처럼 손대지 않는다. ⛔ 그 전에는 진행 코어가 「시작 에피소드가 비어 있다」로 거부해
            // 방금 만든 챕터마다 「진행 JSON 미출력」이 섰다.
            if (entry.Model.Episodes.Count == 0)
            {
                continue;
            }

            // 검증은 챕터별로 한 벌만 계산한다 — 보고 패널이 쓰는 것과 같은 결과다.
            // 프로젝트를 함께 넘긴다 — 에피소드가 재생할 대사 노드는 연출 그래프의 판에
            // 있어서, 챕터 모델만으로는 못 찾는다.
            ChapterExportResult result = ChapterProgressionExporter.ExportValidated(
                entry.Model,
                ValidationFor(entry, project),
                project);

            if (result.Refused)
            {
                refused.Add(entry.ChapterId);
                refusalErrors.AddRange(result.Validation.All.Where(item => item.Severity == ChapterDiagnosticSeverity.Error));
                continue;
            }

            string baselinePath = ChapterReleaseBaseline.PathFor(projectPath, entry.ChapterId);
            if (File.Exists(baselinePath))
            {
                try
                {
                    IReadOnlyList<string> moved = ChapterReleaseBaseline.FindOrderChanges(
                        File.ReadAllText(baselinePath), result.Json!);
                    if (moved.Count > 0)
                    {
                        blocked.Add($"{entry.ChapterId}({string.Join(", ", moved)})");
                        continue;
                    }
                }
                catch (IOException)
                {
                    failed.Add(entry.ChapterId);
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    failed.Add(entry.ChapterId);
                    continue;
                }
                catch (System.Text.Json.JsonException)
                {
                    failed.Add(entry.ChapterId);
                    continue;
                }
            }

            string exportPath = ExportPathFor(projectPath, entry.ChapterId);

            if (!TryWrite(exportPath, result.Json!))
            {
                failed.Add(entry.ChapterId);
                continue;
            }

            checksums[entry.ChapterId] = result.Checksum!;
            written.Add(exportPath);

            // 대사 Yarn — 장면 하나 = 파일 하나(docs/design/two-graphs.md 7절 V). 진행 JSON이 나갈 때 함께.
            ExportYarn(entry, projectPath, project, yarnChapters, yarnOwners, yarnWritten, yarnProblems);
        }

        // 프로젝트에 없는 챕터(지웠거나 이름을 바꿨다)의 옛 파일은 걷는다 — 두면 게임이 없는 챕터를 계속 싣는다.
        var removed = new List<string>();
        var removeFailed = new List<string>();
        RemoveStale(entries, projectPath, removed, removeFailed);
        RemoveStaleYarn(entries, projectPath, yarnChapters, yarnOwners, removeFailed);

        return new ChapterExportRun(
            refused, failed, blocked, checksums, written, Delivery: null, removed, removeFailed, yarnWritten, yarnProblems,
            refusalErrors);
    }

    /// <summary>
    /// 챕터 하나의 대사 Yarn을 <c>exported/yarn/</c>에 쓴다. <b>막는 문제를 안은 장면은 쓰지 않고</b> 그 장면의 옛 파일을 둔다 —
    /// 진행 JSON이 거부될 때와 같은 규칙(깨진 것으로 덮지 않는다). 다른 챕터가 이미 쓴 파일 이름이면 그것도 막는다
    /// (챕터 <c>a</c>의 장면 <c>b_c</c>와 챕터 <c>a_b</c>의 장면 <c>c</c>는 같은 이름이 된다).
    /// </summary>
    private static void ExportYarn(
        ChapterEntry entry,
        string projectPath,
        StoryProject? project,
        HashSet<string> chapters,
        Dictionary<string, string> owners,
        List<string> written,
        List<string> problems)
    {
        chapters.Add(entry.ChapterId);
        string folder = ChapterYarnExporter.FolderFor(projectPath);

        foreach (ChapterYarnFile file in ChapterYarnExporter.Export(entry.Model, project))
        {
            string where = $"{entry.ChapterId} · {file.SceneId}";

            if (owners.TryGetValue(file.FileName, out string? owner) &&
                !string.Equals(owner, entry.ChapterId, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{where}: 챕터 '{owner}'의 장면과 파일 이름이 같습니다({file.FileName}) — 장면 ID나 챕터 ID를 고쳐 주세요.");
                continue;
            }

            owners.TryAdd(file.FileName, entry.ChapterId);

            if (file.Problems.FirstOrDefault(problem => problem.IsBlocking) is { } blocking)
            {
                int more = file.Problems.Count(problem => problem.IsBlocking) - 1;
                problems.Add($"{where}: {blocking.Message}" + (more > 0 ? $" (외 {more}개)" : string.Empty));
                continue;
            }

            string path = Path.Combine(folder, file.FileName);

            if (TryWrite(path, file.Text))
            {
                written.Add(path);
            }
            else
            {
                problems.Add($"{where}: {YarnWriteFailedNote(file.FileName)}");
            }
        }
    }

    private static string YarnWriteFailedNote(string fileName) =>
        $"{ExportFolderName}/{ChapterYarnExporter.FolderName}/{fileName}을 쓰지 못했습니다 — 파일이 다른 프로그램에 잡혀 있는지 확인하세요.";

    /// <summary>
    /// <c>exported/yarn/</c>에서 지금 없는 장면 · 챕터의 파일을 걷는다. 파일의 주인은 <b>이름이 가장 길게 맞는 챕터</b>다
    /// (<c>ch01_</c>과 <c>ch01_extra_</c>가 함께 있으면 <c>ch01_extra_…</c>는 뒤의 것). 주인이 없으면(챕터를 지웠다) 걷고, 주인이 이번에
    /// 나갔는데 그 이름을 지금 짓지 않으면(장면을 지웠거나 개명했다) 걷는다. 주인이 거부 · 막힘으로 안 나갔으면 둔다.
    /// </summary>
    private static void RemoveStaleYarn(
        IReadOnlyList<ChapterEntry> entries,
        string projectPath,
        HashSet<string> exported,
        Dictionary<string, string> current,
        List<string> removeFailed)
    {
        string folder = ChapterYarnExporter.FolderFor(projectPath);

        if (!Directory.Exists(folder))
        {
            return;
        }

        (string ChapterId, string Prefix)[] live = entries
            .Select(entry => (entry.ChapterId, ChapterYarnExporter.FilePrefixOf(entry.ChapterId)))
            .OrderByDescending(item => item.Item2.Length)
            .ToArray();

        foreach (string path in Directory.EnumerateFiles(folder, "*" + ChapterYarnExporter.FileExtension).Order(StringComparer.Ordinal))
        {
            string fileName = Path.GetFileName(path);
            string? owner = live
                .Where(item => fileName.StartsWith(item.Prefix, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.ChapterId)
                .FirstOrDefault();

            bool stale = owner is null || (exported.Contains(owner) && !current.ContainsKey(fileName));

            if (!stale)
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                removeFailed.Add(fileName);
            }
            catch (UnauthorizedAccessException)
            {
                removeFailed.Add(fileName);
            }
        }
    }

    /// <summary>
    /// <c>exported/</c>에서 지금 프로젝트에 없는 챕터의 진행 JSON을 걷는다. <c>exported/</c>는 이 툴이 모델에서 짓는
    /// 산출물 자리라, 걷어도 잃는 것이 없다 — 챕터 제거 · 개명을 되돌리면 다음 내보내기가 같은 파일을 다시 쓴다.
    ///
    /// 보낼 곳(게임 폴더 등)의 같은 이름 파일은 여기서 걷지 않는다 — 저장할 때 보내기가 보낸 기록으로 걷는다(<see cref="ExportDelivery"/>).
    ///
    /// 이름은 대소문자를 가리지 않고 맞춘다 — 대소문자만 바꾼 개명에서, 그것을 가리지 않는 파일 시스템은 방금 쓴 새 파일을
    /// 옛 이름으로 들고 있다. 그것을 옛 파일로 보고 걷으면 새 파일이 사라진다.
    /// </summary>
    private static void RemoveStale(
        IReadOnlyList<ChapterEntry> entries,
        string projectPath,
        List<string> removed,
        List<string> removeFailed)
    {
        string folder = Path.GetDirectoryName(ExportPathFor(projectPath, "_"))!;

        if (!Directory.Exists(folder))
        {
            return;
        }

        var live = new HashSet<string>(entries.Select(entry => entry.ChapterId), StringComparer.OrdinalIgnoreCase);

        foreach (string path in Directory.EnumerateFiles(folder, "*" + ExportFileSuffix).Order(StringComparer.Ordinal))
        {
            string fileName = Path.GetFileName(path);
            string chapterId = fileName[..^ExportFileSuffix.Length];

            if (live.Contains(chapterId))
            {
                continue;
            }

            try
            {
                File.Delete(path);
                removed.Add(chapterId);
            }
            catch (IOException)
            {
                removeFailed.Add(chapterId);
            }
            catch (UnauthorizedAccessException)
            {
                removeFailed.Add(chapterId);
            }
        }
    }

    /// <summary>
    /// 같은 글이면 안 쓴다 — 다시 읽을 때마다 파일을 두드리면 클라우드 동기화가 계속
    /// 깨어나고, 바뀐 것이 없는데 시각만 새로 찍힌다.
    /// </summary>
    private static bool TryWrite(string path, string json)
    {
        try
        {
            byte[] bytes = ChapterExportBytes.Encode(json);
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                return true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 검증이 읽는 것 전부의 지문 — 챕터 모델의 내용과 판의 상태. 하나라도 다르면 다시 증명한다.
    ///
    /// 모델의 값 객체는 전부 레코드라 <c>ToString</c>이 내용 그대로다(진단까지). 파일을 읽지
    /// 않으므로 디스크가 바뀌어도(출력·잠김·손상) 지문은 그대로이고, 모델이 바뀌면(편집·되돌리기·
    /// 가져오기) 반드시 움직인다 — 캐시가 <b>검증과 같은 것</b>을 보는 것이 규칙이다.
    /// </summary>
    private static string Fingerprint(ChapterGraphModel chapter, IReadOnlyList<string>? boardNames)
    {
        var builder = new StringBuilder();

        builder.Append("판|").Append(boardNames is null ? "없음" : string.Join(",", boardNames)).Append('\n');
        builder.Append("챕터|").Append(chapter.ChapterId).Append('\n');

        void Append(string label, IEnumerable<object> items)
        {
            builder.Append(label).Append('|');

            foreach (object item in items)
            {
                builder.Append(item).Append('\u001f');
            }

            builder.Append('\n');
        }

        Append("에피소드", chapter.Episodes);

        // 레코드의 글은 목록을 「List`1」로만 적는다 — 스탯변화는 내용을 따로 싣는다. 안 그러면 스탯변화만 고친 간선이 같은
        // 지문을 내어 옛 증명(도착 스탯 · 도달성)을 되쓴다.
        Append("간선", chapter.Edges.Select(edge => (object)$"{edge}|{string.Join(",", edge.StatChanges)}"));
        Append("조건", chapter.Conditions);
        Append("스탯", chapter.Stats);
        Append("픽스처", chapter.Fixtures);
        Append("선택지", chapter.ChoiceOptions);
        Append("진단", chapter.Diagnostics);

        return builder.ToString();
    }

    /// <summary>
    /// 지문에 실을 <b>판의 상태</b> — 그 챕터 판의 대사노드(이름·표식·줄이 있는지). 판이 없으면
    /// null이고, 그것도 상태의 일부다(안 연 챕터 ↔ 연 챕터가 지문으로 갈린다).
    ///
    /// 그 챕터의 판만 본다 — 대사 노드를 찾는 규칙(<see cref="ChapterBoard"/>)이
    /// 그 판만 보므로, 다른 판이 서는 것은 이 챕터의 판정을 바꾸지 않는다. 줄이 있는지까지 싣는
    /// 이유: 빈 노드가 오류라 줄이 생기고 없어지는 것도 판정을 바꾼다.
    /// </summary>
    private static IReadOnlyList<string>? BoardNames(StoryProject? project, string chapterId)
    {
        if (project is null || EpisodeNaming.BoardOf(project, chapterId) is not { } board)
        {
            return null;
        }

        return board.Nodes
            .OfType<DialogueNode>()
            .Select(node =>
                $"{node.Name}{node.MarkedEpisodeId}" +
                (node.Script.ActiveLines.Any() ? "1" : "0"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}
