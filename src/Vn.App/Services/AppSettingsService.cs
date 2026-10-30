using System.Text;
using System.Text.Json;

namespace Vn.App.Services;

/// <summary>
/// 프로젝트 원본과 무관한 사용자별 편의 설정.
/// 읽기·쓰기에 실패해도 저작 작업 자체를 막지 않는다.
/// </summary>
internal static class AppSettingsService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 테스트 격리의 손잡이 (2026-08-26) — <b>피해가 양방향이었다.</b> 헤드리스
    /// MainWindow가 이 파일을 진짜 %APPDATA%에서 읽으면 개발자의 실제 최근 프로젝트를
    /// 열어 버리고(OnOpened — 그 프로젝트의 그날 상태에 따라 무관한 테스트들이 무작위로
    /// 죽고, 세션이 실제 원고 폴더에 감시자를 걸고 노드를 더한다), 반대로 테스트가 여는
    /// 임시 프로젝트가 <b>사용자의 진짜 최근 프로젝트를 덮어썼다</b> — 실제로 사용자의
    /// settings.json이 이미 지워진 임시 폴더를 가리키고 있었다.
    ///
    /// 돌려세우는 자리는 <c>Vn.App.Tests.TestProcessIsolation</c> 하나다(어셈블리가
    /// 실리는 순간 = 어떤 테스트보다 먼저). ⚠ 경로를 바꾸는 것만으로는 모자란다 —
    /// 복원 자체를 끄는 <see cref="MainWindow.RestoreRecentProjectOnOpen"/>이 짝이다.
    /// 임시 경로가 비어 있어 복원이 <b>조용히 실패하던 것</b>에 기대고 있었을 뿐이라,
    /// 그 폴더에 프로젝트가 남으면 같은 흔들림이 돌아온다.
    /// </summary>
    internal static string? SettingsPathOverride { get; set; }

    public static string SettingsPath => SettingsPathOverride ?? DefaultSettingsPath;

    private static readonly string DefaultSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VnTool",
        "settings.json");

    public static string? LoadRecentProject()
    {
        return LoadRecentProject(SettingsPath);
    }

    internal static string? LoadRecentProject(string settingsPath)
    {
        AppSettings settings = Load(settingsPath);
        string? path = settings.RecentProject;

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return File.Exists(path) ? path : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                NotSupportedException or
                PathTooLongException or
                IOException or
                UnauthorizedAccessException)
        {
            // 설정에 남은 경로가 형식부터 깨져 있어도 앱은 계속 시작해야 한다.
            return null;
        }
    }

    public static void SaveRecentProject(string projectPath)
    {
        AppSettings settings = Load(SettingsPath);
        settings.RecentProject = NormalizeProjectKey(projectPath);
        Save(settings);
    }

    /// <summary>
    /// <b>탐색기의 접힘</b> — 프로젝트별로 (<c>docs/plans/R6-explorer.md</c> §4).
    ///
    /// ⚠ <b>왜 프로젝트 파일이 아닌가.</b> 프로젝트 파일은 <b>팀의 것</b>이고 커밋된다 —
    /// 내가 접어 둔 것이 남의 화면을 접으면 안 된다. 재생 속도·음량이 이미 이 자리에 산다.
    ///
    /// ⚠ <b>편 것과 접은 것을 둘 다</b> 싣는다. 트리는 <i>"고른 에피소드가 든 마디는 저절로
    /// 펴진다"</i>를 기본값으로 쓰므로, 접은 것만 기억하면 사람이 <b>일부러 편</b> 마디가
    /// 안 돌아오고, 편 것만 기억하면 <b>접은 것이 안 남는다</b>.
    /// </summary>
    public static (IReadOnlyList<string> Expanded, IReadOnlyList<string> Collapsed) LoadExplorerState(
        string projectPath) => LoadExplorerState(SettingsPath, projectPath);

    internal static (IReadOnlyList<string> Expanded, IReadOnlyList<string> Collapsed) LoadExplorerState(
        string settingsPath, string projectPath)
    {
        AppSettings settings = Load(settingsPath);
        ExplorerState? state = FindExplorer(settings, NormalizeProjectKey(projectPath));

        return (state?.Expanded ?? [], state?.Collapsed ?? []);
    }

    public static void SaveExplorerState(
        string projectPath, IEnumerable<string> expanded, IEnumerable<string> collapsed) =>
        SaveExplorerState(SettingsPath, projectPath, expanded, collapsed);

    internal static void SaveExplorerState(
        string settingsPath,
        string projectPath,
        IEnumerable<string> expanded,
        IEnumerable<string> collapsed)
    {
        AppSettings settings = Load(settingsPath);
        settings.Explorers ??= [];

        string key = NormalizeProjectKey(projectPath);

        // 대소문자만 다른 옛 열쇠가 있으면 그것을 쓴다 — 윈도에서 같은 프로젝트가 둘로 갈리면
        // 접힘이 경로를 어떻게 적었느냐에 따라 달라 보인다.
        key = settings.Explorers.Keys.FirstOrDefault(existing =>
            string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)) ?? key;

        settings.Explorers[key] = new ExplorerState
        {
            Expanded = [.. expanded],
            Collapsed = [.. collapsed],
            FullChoices = settings.Explorers.TryGetValue(key, out ExplorerState? before) ? before.FullChoices : []
        };

        Prune(settings.Explorers);
        Save(settings, settingsPath);
    }

    /// <summary>
    /// 선택지 칸을 셋 다 세우는 카드들 (2026-10-30 — 인스펙터의 「선택지 3칸」) — 프로젝트별. 없는 카드는 빈 칸을 하나만 세운다.
    /// 하이어아키 접힘과 같은 까닭으로 프로젝트 파일이 아니라 여기다(내 화면 편의가 팀의 파일을 바꾸면 안 된다).
    /// </summary>
    public static IReadOnlyList<string> LoadFullChoices(string projectPath) =>
        FindExplorer(Load(SettingsPath), NormalizeProjectKey(projectPath))?.FullChoices ?? [];

    public static void SaveFullChoices(string projectPath, IEnumerable<string> nodeIds)
    {
        AppSettings settings = Load(SettingsPath);
        settings.Explorers ??= [];

        string key = NormalizeProjectKey(projectPath);
        key = settings.Explorers.Keys.FirstOrDefault(existing =>
            string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)) ?? key;

        ExplorerState state = settings.Explorers.TryGetValue(key, out ExplorerState? found) ? found : new ExplorerState();
        state.FullChoices = [.. nodeIds];
        settings.Explorers[key] = state;

        Prune(settings.Explorers);
        Save(settings);
    }

    /// <summary>하이어아키 기둥을 접어 두었는가 (2026-10-30 HC — 두 그래프가 함께 쓴다).</summary>
    public static bool LoadHierarchyFolded() => Load(SettingsPath).HierarchyFolded;

    public static void SaveHierarchyFolded(bool folded)
    {
        AppSettings settings = Load(SettingsPath);
        settings.HierarchyFolded = folded;
        Save(settings);
    }

    private static ExplorerState? FindExplorer(AppSettings settings, string key) =>
        settings.Explorers?.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// 사라진 프로젝트의 접힘을 버린다 — 설정 파일이 <b>자라기만</b> 하면 안 된다.
    ///
    /// ⚠ <b>못 읽는 경로는 남긴다.</b> 없는 것과 못 읽는 것은 다르다 — 네트워크 드라이브가
    /// 잠깐 안 붙었다고 그 프로젝트의 접힘을 버리면 돌아왔을 때 트리가 통째로 펴져 있다.
    /// </summary>
    private static void Prune(Dictionary<string, ExplorerState> explorers)
    {
        foreach (string key in explorers.Keys.ToList())
        {
            try
            {
                if (!File.Exists(key))
                {
                    explorers.Remove(key);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                    NotSupportedException or
                    PathTooLongException or
                    IOException or
                    UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// 설정 파일이 없거나, 잘린 JSON이거나, 형이 전혀 다른 내용이어도 기본값으로 돌아간다.
    /// 편의 설정 하나 때문에 앱이 시작하지 못하는 일은 없어야 한다.
    /// </summary>
    internal static AppSettings Load(string settingsPath)
    {
        try
        {
            if (!File.Exists(settingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(
                       File.ReadAllText(settingsPath),
                       ReadOptions)
                   ?? new AppSettings();
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                JsonException or
                NotSupportedException or
                ArgumentException)
        {
            return new AppSettings();
        }
    }

    private static void Save(AppSettings settings) => Save(settings, SettingsPath);

    private static void Save(AppSettings settings, string settingsPath)
    {
        try
        {
            string? directory = Path.GetDirectoryName(settingsPath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                settingsPath,
                JsonSerializer.Serialize(settings, WriteOptions),
                new UTF8Encoding(false));
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException or
                ArgumentException)
        {
            // 편의 설정을 기억하지 못해도 프로젝트와 원고는 계속 사용할 수 있다.
        }
    }

    private static string NormalizeProjectKey(string projectPath)
    {
        try
        {
            return Path.GetFullPath(projectPath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException)
        {
            return projectPath;
        }
    }

    internal sealed class AppSettings
    {
        public string? RecentProject { get; set; }

        /// <summary>프로젝트 경로 → 하이어아키 접힘 (R6 S-4 · 2026-10-30 HY4b에 되살아남). null이면 기억한 것이 없다.</summary>
        public Dictionary<string, ExplorerState>? Explorers { get; set; }

        /// <summary>하이어아키 기둥을 접어 두었는가 (2026-10-30 HC) — 프로젝트와 상관없는 화면 편의라 하나만 둔다.</summary>
        public bool HierarchyFolded { get; set; }
    }

    /// <summary>
    /// 한 프로젝트의 화면 기억 — 하이어아키 접힘(마디 <b>열쇠</b>만 싣는다 — 트리 구조는 그릴 때마다 프로젝트에서 다시 나오므로,
    /// 여기 남은 것이 실제 마디와 안 맞으면 그냥 안 쓰인다)과 선택지 칸을 셋 다 세우는 카드들.
    /// </summary>
    internal sealed class ExplorerState
    {
        public List<string> Expanded { get; set; } = [];

        public List<string> Collapsed { get; set; } = [];

        /// <summary>선택지 칸을 셋 다 세우는 카드(노드 Id) — 인스펙터의 「선택지 3칸」(2026-10-30).</summary>
        public List<string> FullChoices { get; set; } = [];
    }
}
