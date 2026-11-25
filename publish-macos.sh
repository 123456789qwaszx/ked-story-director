#!/usr/bin/env bash
# 맥을 쓰는 작가에게 건네는 꾸러미를 만든다 — StoryDirector.app을 zip 하나로.
#
#   ./publish-macos.sh                Apple 칩(M1~) 맥용 — 기본
#   ./publish-macos.sh x64            인텔 맥용
#   ./publish-macos.sh arm64 0.1.0    판 번호를 정해 굽는다(릴리스 — .github/workflows/release.yml). 정하지 않으면 「개발판」
#
# publish-windows.ps1의 맥판이다(2026-11-19 · writer-app WA6). 받는 사람에게 없는 것을 전제하지 않는다 —
# .NET을 함께 담으므로(self-contained) 받는 맥에 설치할 것이 없다.
#
# ⚠ 맥에서 돌린다(CI의 macOS 작업이 돌린다 — .github/workflows/test.yml). 다른 OS에서도 폴더는 만들어지지만 맥의 서명 도구(codesign)를
#   거치지 못해, Apple 칩 맥이 실행을 막을 수 있다.
#
# ⛔ 서명 · 공증은 하지 않는다(「가볍게 최소로」의 밖 — Apple 개발자 계정이 든다). 이 Mac에서만 통하는 임시 서명(ad-hoc)만 한다.
#    그래서 받는 사람은 처음 한 번 「시스템 설정 → 개인정보 보호 및 보안 → 그래도 열기」를 지나야 한다(docs/작가에게.txt).
#    서명 · 공증을 붙일 때는 아래 codesign 줄을 Developer ID로 바꾸고 notarytool을 잇는다.
set -euo pipefail

cd "$(dirname "$0")"

arch="${1:-arm64}"
# 판 번호 — 창 제목(앱이 읽는다 · -p:Version)과 Finder의 「정보 가져오기」(Info.plist)에 같은 번호가 선다(writer-release WR2).
version="${2:-0.0.0}"

case "$arch" in
    arm64 | x64) ;;
    *)
        echo "쓰는 법: ./publish-macos.sh [arm64|x64] [판 번호]" >&2
        exit 2
        ;;
esac

rid="osx-$arch"
out="artifacts/StoryDirector-$rid"
app="$out/StoryDirector.app"
zip="artifacts/StoryDirector-$rid.zip"

rm -rf "$out" "$zip"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

# 맥의 앱은 폴더(.app) 하나가 곧 앱이다 — 실행 파일 하나로 묶지 않고(Windows 꾸러미와 다르다) 그 안에 그대로 편다.
# ⭐ ReadyToRun으로 미리 기계어로 굽는다(2026-11-25 소유자 보고 — 새 프로젝트에서 글을 쓰다 처음 몇 번 1~2초씩 멈췄다). 까닭은
#   publish-windows.ps1의 같은 줄에 적었다 — 굽지 않으면 글을 넣을 때마다 도는 검증 · 내보내기 코드가 처음 몇 번은 그 자리에서 번역(JIT)된다.
dotnet publish src/Vn.App/Vn.App.csproj \
    -c Release \
    -r "$rid" \
    --self-contained true \
    -p:DebugType=none \
    -p:PublishReadyToRun=true \
    -p:Version="$version" \
    -o "$app/Contents/MacOS"

# 독 · Finder의 그림 — tools/make-app-icons.py가 원본(Assets/app-icon.png)에서 굽는다.
cp src/Vn.App/Assets/app.icns "$app/Contents/Resources/app.icns"

# 앱 이름의 언어판 — 메뉴 막대 · 독 · Finder에 서는 이름(2026-11-25 · english-ui E7). 맥이 제 언어(시스템 설정의 언어)로 고른다 — 한국어 맥이면
#   ko.lproj, 그 밖은 en.lproj(아래 Info.plist의 CFBundleDevelopmentRegion). 앱 안의 화면 언어 설정과는 따로다 — 그것은 창 안의 글만 바꾼다.
#   ⛔ 그 전에는 Info.plist 한 벌(「스토리 디렉터」)이라 영어 맥에서도 메뉴 막대가 한국어였다.
mkdir -p "$app/Contents/Resources/ko.lproj" "$app/Contents/Resources/en.lproj"
printf 'CFBundleName = "스토리 디렉터";\nCFBundleDisplayName = "스토리 디렉터";\n' > "$app/Contents/Resources/ko.lproj/InfoPlist.strings"
printf 'CFBundleName = "Story Director";\nCFBundleDisplayName = "Story Director";\n' > "$app/Contents/Resources/en.lproj/InfoPlist.strings"

# 꾸러미의 신원. 이름(CFBundleName)은 메뉴 막대와 독에 선다. 판 번호는 위의 번호다 — 「어느 판을 쓰세요?」에 창 제목과 Finder의 「정보 가져오기」가
# 같은 번호로 답한다. ⛔ 2026-11-19까지는 구운 날(2026.11.19)이었고 창에는 번호가 없었다.

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>LSHasLocalizedDisplayName</key>
    <true/>
    <key>CFBundleName</key>
    <string>스토리 디렉터</string>
    <key>CFBundleDisplayName</key>
    <string>스토리 디렉터</string>
    <key>CFBundleIdentifier</key>
    <string>com.ked.story-director</string>
    <key>CFBundleExecutable</key>
    <string>StoryDirector</string>
    <key>CFBundleIconFile</key>
    <string>app.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleShortVersionString</key>
    <string>$version</string>
    <key>CFBundleVersion</key>
    <string>$version</string>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

chmod +x "$app/Contents/MacOS/StoryDirector"

# 안내문 — 앱 곁에(2026-11-19 · writer-release WR3 · Windows 꾸러미와 같은 글).
cp "docs/작가에게.txt" "$out/먼저 읽어주세요.txt"
cp "docs/to-writers.en.txt" "$out/Read me first.txt"   # 영어판(2026-11-25 · english-ui E6)

# 제3자 소프트웨어 고지 — 함께 묶이는 오픈소스와 글꼴(Inter)의 저작권 고지 · 라이선스 원문(2026-11-23 소유자 · Windows 꾸러미와 같은 파일).
cp "THIRD-PARTY-NOTICES.txt" "$out/THIRD-PARTY-NOTICES.txt"

# 임시 서명(ad-hoc) — Apple 칩 맥은 서명이 아예 없는 코드를 실행하지 않는다. 신원을 증명하는 서명이 아니다(위 ⛔).
if command -v codesign > /dev/null; then
    codesign --force --deep --sign - "$app"
else
    echo "⚠ codesign이 없습니다(맥이 아님) — 서명 없이 묶습니다. 건넬 꾸러미는 맥에서 구우세요." >&2
fi

# zip — 실행 권한을 지켜야 한다(권한이 빠지면 앱이 켜지지 않는다). 맥은 ditto가 꾸러미를 제대로 싼다. 폴더째가 아니라 그 안(앱 · 안내문 · 고지문)을 싼다.
if command -v ditto > /dev/null; then
    ditto -c -k "$out" "$zip"
else
    (cd "$out" && zip -qry "../$(basename "$zip")" StoryDirector.app "먼저 읽어주세요.txt" "Read me first.txt" "THIRD-PARTY-NOTICES.txt")
fi

echo
echo "꾸러미: $zip"
echo "판 번호: $version"
echo "앱: $app ($(du -sh "$app" | cut -f1)) · 먼저 읽어주세요.txt · Read me first.txt · THIRD-PARTY-NOTICES.txt"
echo "받는 사람은 압축을 풀고 StoryDirector.app을 「응용 프로그램」 폴더로 옮긴 뒤 엽니다."
echo "처음 한 번은 「시스템 설정 → 개인정보 보호 및 보안 → 그래도 열기」를 지나야 합니다(서명 · 공증이 없습니다)."
