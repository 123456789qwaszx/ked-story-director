using System.Diagnostics;
using Vn.Authoring.Chapters;
using Vn.Authoring.Serialization;

namespace Vn.App.Services;

/// <summary>
/// <b>작업 폴더</b> — 프로젝트 폴더의 <c>exported/</c>(2026-11-18 소유자 — 위 띠 [📂 작업 폴더] · ⛔ 그 전 이름 「챕터진행」). 툴이 저절로 내는
/// 진행 JSON · 장면 파일 · 대사 Yarn의 자리다. 여기는 그 자리를 찾고, 파일을 고른 채 탐색기를 연다 — 설정 창의 형식 이름을 누르면 그 형식의 파일을
/// 보인다(「뭐가 뭔지 모르겠으니 … 클릭하면 작업 폴더가 열리면서 해당하는 파일이 선택되거나 열리도록」).
/// </summary>
internal static class WorkFolder
{
    /// <summary>그 프로젝트의 작업 폴더.</summary>
    public static string Of(string projectPath) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(projectPath))!, ChapterExportService.ExportFolderName);

    /// <summary>
    /// 그 형식에서 보일 것 — 진행 JSON · 장면 파일은 그 챕터의 파일(없으면 그 형식의 다른 파일, 이름 차례로 첫째), 대사 Yarn은 <c>yarn/</c> 폴더(장면마다
    /// 파일 하나라 폴더째 연다). 아직 나간 것이 없으면 null.
    /// </summary>
    public static string? TargetOf(string projectPath, ExportFormat format, string? chapterId)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        if (format == ExportFormat.DialogueYarn)
        {
            string yarn = ChapterYarnExporter.FolderFor(projectPath);
            return Directory.Exists(yarn) ? yarn : null;
        }

        string suffix = format == ExportFormat.Progression ? ChapterExportService.ExportFileSuffix : ChapterScenesExporter.FileSuffix;
        string folder = Of(projectPath);

        if (chapterId is not null && Path.Combine(folder, chapterId + suffix) is var mine && File.Exists(mine))
        {
            return mine;
        }

        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*" + suffix).Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            : null;
    }

    /// <summary>파일이면 그 파일을 고른 채 탐색기를 연다(Windows 탐색기 · macOS Finder — 그 밖에는 담은 폴더를 연다), 폴더면 그 폴더를 연다.</summary>
    public static void Reveal(string path)
    {
        if (!File.Exists(path))
        {
            Open(path);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
        }
        else if (OperatingSystem.IsMacOS())
        {
            var finder = new ProcessStartInfo("open") { UseShellExecute = false };
            finder.ArgumentList.Add("-R");
            finder.ArgumentList.Add(path);
            Process.Start(finder);
        }
        else
        {
            Open(Path.GetDirectoryName(path)!);
        }
    }

    /// <summary>폴더를 연다(그 OS의 파일 탐색기).</summary>
    public static void Open(string folder) =>
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
}
