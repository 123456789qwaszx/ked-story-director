using Avalonia.Platform;
using Avalonia.Threading;
using Vn.App.Services;
using Vn.App.Views;

namespace Vn.App.Tests;

/// <summary>
/// <b>앱의 아이콘</b> (2026-11-19 소유자 — 「아이콘 그림을 당신이 먼저 어울리는 것으로」 · <c>docs/work-orders/writer-app-orders.md</c> WA5).
///
/// 원본 <c>Assets/app-icon.png</c>에서 <c>tools/make-app-icons.py</c>가 구운 <c>app.ico</c>를 창과 실행 파일이 함께 쓴다.
/// </summary>
public sealed class AppIconTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "vn-app-icon", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void 주_창과_설정_창이_앱_아이콘을_쓴다() => HeadlessUi.Run(() =>
    {
        (MainWindow window, AuthoringSession _) = HierarchyFixture.Open(_directory);
        Assert.Same(App.WindowIcon, window.Icon);

        SettingsWindow settings = window.OpenSettings();
        Dispatcher.UIThread.RunJobs();
        Assert.Same(App.WindowIcon, settings.Icon);

        settings.Close();
        window.Close();
    });

    [Fact]
    public void 아이콘_파일은_작은_것부터_256까지_여덟_크기를_담는다()
    {
        // 작업 표시줄(16~48) · 탐색기의 큰 아이콘(256)이 제 크기를 골라 쓴다 — 한 크기뿐이면 늘이거나 줄여 뭉갠다.
        using Stream stream = AssetLoader.Open(new Uri("avares://StoryDirector/Assets/app.ico"));
        using var reader = new BinaryReader(stream);

        Assert.Equal((0, 1), (reader.ReadUInt16(), reader.ReadUInt16()));   // 예약 0 · 종류 1(아이콘)
        int count = reader.ReadUInt16();
        var sizes = new List<int>();

        for (int index = 0; index < count; index++)
        {
            int width = reader.ReadByte();
            sizes.Add(width == 0 ? 256 : width);   // 256은 0으로 적는다
            reader.ReadBytes(15);
        }

        Assert.Equal([16, 20, 24, 32, 40, 48, 64, 256], sizes);
    }
}
