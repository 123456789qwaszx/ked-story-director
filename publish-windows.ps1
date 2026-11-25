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
    # 꾸러미 · 실행 파일의 이름은 작가에게 보이는 이름이다(스토리 디렉터 — 2026-11-19 · writer-app WA4).
    #   실행 파일 이름은 Vn.App.csproj의 AssemblyName이 정한다 — 여기와 CI(test.yml)가 그 이름을 찾는다.
    $publishDirectory = Join-Path $root "artifacts\StoryDirector-win-x64"
    $zipPath = Join-Path $root "artifacts\StoryDirector-win-x64.zip"

    Remove-Item $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

    # ⭐ ReadyToRun으로 미리 기계어로 굽는다(2026-11-25 소유자 보고 — 「처음 프로젝트를 생성하고 글을 쓸 때 자꾸 멈추는 현상 … 1~2초 정도이며
    #   3번정도 반복된 뒤에는 멈추는 일이 없었다」). 굽지 않으면 코드는 처음 불릴 때 그 자리에서 번역(JIT)된다. 카드 글 칸은 쓰다 멈출 때마다
    #   (0.4초) 글을 넣고 검증 · 도달성 증명 · 진행 JSON · 대사 Yarn 내보내기까지 화면 스레드에서 도는데, 처음 몇 번은 그 길을 처음 타는
    #   코드가 많아 번역에 1~2초를 썼다 — 한 번 번역된 코드는 다시 번역하지 않아 몇 번 뒤에는 멈추지 않았다.
    #   ⛔ 2026-08-17의 배포 설정(Properties/PublishProfiles/win-x64.pubxml)은 ReadyToRun을 켰지만, 2026-11-19에 이 스크립트로 꾸러미를 굽기
    #   시작하면서 그 설정이 빠졌다(v0.1.0 · v0.2.0은 굽지 않은 판이다). 꾸러미가 커진다(전에 잰 것: 53MB → 78MB).
    dotnet publish .\src\Vn.App\Vn.App.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishReadyToRun=true `
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
    # 영어판도 곁에 — 화면 언어가 PC 언어를 따르므로 영어 화면의 작가도 읽을 안내가 있어야 한다(2026-11-25 · english-ui E6).
    Copy-Item (Join-Path $root "docs\to-writers.en.txt") (Join-Path $publishDirectory "Read me first.txt")

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
    Write-Host "담긴 것: StoryDirector.exe · 먼저 읽어주세요.txt · Read me first.txt · THIRD-PARTY-NOTICES.txt"
    Write-Host "받는 사람은 압축을 풀고 StoryDirector.exe를 누르면 됩니다."
}
finally {
    Pop-Location
}
