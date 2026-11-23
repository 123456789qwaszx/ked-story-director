param(
    # 판 번호 — 창 제목에 선다(앱이 읽는다 · writer-release WR2). 릴리스(.github/workflows/release.yml)가 정한다. 정하지 않으면 「개발판」.
    #   .\publish-windows.ps1 -Version 0.1.0
    [string]$Version = "0.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root

# 비개발자(시나리오 작가)에게 그대로 건네는 꾸러미를 만든다.
#
# 받는 사람에게 없는 것을 전제하지 않는다 — .NET 런타임도, 개발 도구도, 명령줄도.
# 그래서 self-contained · 단일 exe이고, 압축을 풀면 바로 실행된다.
#
# ⚠ 실행 파일 하나로 묶는 것은 csproj가 아니라 여기서 정한다. 개발 중 F5는 그 묶기를
#   치를 이유가 없고(느리다), 묶는 것은 배포의 성질이지 프로젝트의 성질이 아니다.

try {
    # 꾸러미 · 실행 파일의 이름은 작가에게 보이는 이름이다(스토리 디렉터 — 2026-11-19 · docs/work-orders/writer-app-orders.md WA4).
    #   실행 파일 이름은 Vn.App.csproj의 AssemblyName이 정한다 — 여기와 CI(test.yml)가 그 이름을 찾는다.
    $publishDirectory = Join-Path $root "artifacts\StoryDirector-win-x64"
    $zipPath = Join-Path $root "artifacts\StoryDirector-win-x64.zip"

    Remove-Item $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

    dotnet publish .\src\Vn.App\Vn.App.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -p:Version=$Version `
        -o $publishDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "StoryDirector publish failed with exit code $LASTEXITCODE."
    }

    # 안내문을 함께 담는다 — `docs\작가에게.txt`를 「먼저 읽어주세요.txt」로(2026-11-19 소유자 · writer-release WR3). 견본 프로젝트는
    #   담지 않는다 — 실제 프로젝트는 Git(GitHub Desktop)으로 받는다(안내문에 그 절이 있다).
    #   ⛔ 2026-08-26부터는 실행 파일만 담았다(그때의 안내문 · 견본이 며칠 사이 굴러간 옛 규격을 말해서).
    Copy-Item (Join-Path $root "docs\작가에게.txt") (Join-Path $publishDirectory "먼저 읽어주세요.txt")

    # 제3자 소프트웨어 고지 — 함께 묶이는 오픈소스(Avalonia · SkiaSharp · HarfBuzz · ANGLE · .NET 런타임)와 글꼴(Inter)의 저작권 고지 · 라이선스 원문
    #   (2026-11-23 소유자 — 「THIRD-PARTY-NOTICES.txt 넣어주십시오」). MIT · BSD · OFL이 배포할 때 함께 담으라고 한다.
    Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.txt") (Join-Path $publishDirectory "THIRD-PARTY-NOTICES.txt")

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath

    $exe = Join-Path $publishDirectory "StoryDirector.exe"
    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)

    Write-Host ""
    Write-Host "꾸러미: $zipPath"
    Write-Host "실행 파일: $exe ($sizeMb MB)"
    Write-Host "판 번호: $Version"
    Write-Host "담긴 것: StoryDirector.exe · 먼저 읽어주세요.txt · THIRD-PARTY-NOTICES.txt"
    Write-Host "받는 사람은 압축을 풀고 StoryDirector.exe를 누르면 됩니다."
}
finally {
    Pop-Location
}
