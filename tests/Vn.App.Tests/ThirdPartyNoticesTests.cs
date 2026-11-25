using System.Text.Json;

namespace Vn.App.Tests;

/// <summary>
/// <b>제3자 소프트웨어 고지</b>(저장소 맨 위의 <c>THIRD-PARTY-NOTICES.txt</c> — 두 꾸러미에 함께 담는다 · 2026-11-23 소유자 「THIRD-PARTY-NOTICES.txt
/// 넣어주십시오」 · writer-release 8절)가 지금 앱이 싣는 패키지의 판을 말한다. 패키지 판을 올리면 이 시험이 깨져 고지문도
/// 다시 옮기라고 알린다 — 그 판의 패키지 안 LICENSE · THIRD-PARTY-NOTICES가 바뀌었을 수 있다.
/// </summary>
public sealed class ThirdPartyNoticesTests
{
    /// <summary>꾸러미에 실려 나가는 패키지(Release 배포에서 확인 — 개발 도구 · 시험 · 빌드 때만 쓰는 것은 빠진다).</summary>
    private static readonly string[] Shipped =
    [
        "Avalonia", "Avalonia.Fonts.Inter", "Avalonia.Angle.Windows.Natives", "SkiaSharp", "HarfBuzzSharp", "MicroCom.Runtime", "Tmds.DBus.Protocol"
    ];

    [Fact]
    public void 고지문은_앱이_싣는_패키지의_지금_판을_말한다()
    {
        string notices = File.ReadAllText(Path.Combine(RepoRoot, "THIRD-PARTY-NOTICES.txt"));
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "src", "Vn.App", "obj", "project.assets.json")));
        List<string> libraries = assets.RootElement.GetProperty("libraries").EnumerateObject().Select(library => library.Name).ToList();

        foreach (string id in Shipped)
        {
            string version = libraries.Single(name => name.StartsWith(id + "/", StringComparison.OrdinalIgnoreCase)).Split('/')[1];
            Assert.True(notices.Contains($"{id} {version}", StringComparison.Ordinal),
                $"고지문에 「{id} {version}」이 없습니다 — 패키지 판이 바뀌었으면 그 패키지의 LICENSE · THIRD-PARTY-NOTICES를 다시 옮기세요.");
        }

        // 글꼴(Inter)은 OFL 원문과 저작권 고지가 함께 있어야 한다.
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", notices, StringComparison.Ordinal);
        Assert.Contains("Copyright 2020 The Inter Project Authors", notices, StringComparison.Ordinal);
    }

    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
