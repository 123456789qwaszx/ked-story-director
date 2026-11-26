# 테마 — 색 · 모양을 갈아입히는 구조와 새 테마 더하는 법

툴의 색 · 모서리 · 컨트롤 모양 · 글꼴은 **테마**가 정한다. 테마는 `src/Vn.App/Themes/` 아래 **폴더 하나**이고, [⚙ 설정] → 「기타」 →
「테마」에서 고른다. 고른 테마는 앱 설정에 들고 **다시 켤 때** 선다(화면 언어와 같다). 처음 테마는 「파스텔 하늘」이다.

| 테마 | 이름(앱 설정) | 폴더 | 무엇 |
|---|---|---|---|
| 파스텔 하늘 | `pastel-sky` | `Themes/PastelSky/` | 하늘색 바탕 · 흰 스티커 카드 · 분홍과 버터색 포인트 · 글꼴 Jua / Gowun Dodum |
| 기본 | `classic` | `Themes/Classic/` | 0.5.0까지의 모양 그대로. **모든 테마의 밑바탕**이다 |

## 1. 짜임

```
src/Vn.App/
  Theming/
    Skin.cs           색 · 모양의 이름표 — 화면 코드는 여기서 이름으로 꺼낸다(Skin.Selection.Brush())
    ThemeCatalog.cs   고를 수 있는 테마의 목록 · 처음 테마
    ThemeService.cs   테마를 앱에 입힌다(켤 때 한 번)
  Themes/
    Classic/Tokens.axaml        모든 이름의 값(기본)
    PastelSky/Tokens.axaml      바꾸는 이름의 값 + 틀의 꾸밈 + Fluent 컨트롤 색
    PastelSky/Controls.axaml    컨트롤의 모양 · 글꼴(스타일)
    PastelSky/Fonts/*.ttf       테마가 싣는 글꼴
```

- **테마는 기본 테마 위에 얹힌다.** `ThemeService`가 `Classic/Tokens.axaml`을 늘 깔고, 고른 테마의 `Tokens.axaml`을 그 위에 더한다(뒤에 더한 것이
  이긴다). 그래서 테마는 **바꾸고 싶은 이름만** 적으면 된다 — 적지 않은 이름은 기본 테마의 값이다.
- **값을 꺼내는 길은 둘, 이름은 하나.** XAML은 `{DynamicResource Selection}`, 코드는 `Skin.Selection`. 둘 다 같은 `Tokens.axaml`의 같은 키다.
- **색은 숫자로 적지 않는다.** 화면 코드에 `Color.Parse("#…")` · `Brushes.Red`를 새로 적지 않고 `Skin`의 이름을 쓴다. 맞는 이름이 없으면 이름을
  더한다(3절). 작가가 고른 글 색(`WriterPalette`)처럼 데이터인 색은 테마가 아니다.
- **값을 정적 필드에 담아 두지 않는다.** `static readonly IBrush X = Skin.….Brush()`는 테마가 서기 전에 굳는다 — 속성(`static IBrush X => …`)으로 쓴다.
- **컨트롤의 색**(단추 · 글 칸 · 고르기 칸 · 탭 · 풍선 · 메뉴)은 Fluent 테마가 쓰는 리소스 이름(`ButtonBackground` · `TextControlBorderBrush` …)을
  테마의 `Tokens.axaml`에서 덮는다. **모양 · 글꼴**처럼 이름만으로 못 바꾸는 것은 `Controls.axaml`의 스타일로 적는다. 코드가 지은 것은 클래스로
  고른다(카드 제목 = `TextBlock.card-title`).
- **테마만의 꾸밈**은 그 테마만 적는 이름으로 한다. 기본 테마에 없는 이름은 XAML에서 비어 있게 풀린다 — 위 띠의 바탕(`TopBarBackground`), 위 띠 아래
  꾸밈 줄(`TopBarEdgeHeight` · `TopBarEdgeBrush`), [대본] 판의 바탕(`BoardBackground`). 기본 테마에서는 바탕이 없고 줄의 높이가 0이다.

## 2. 새 테마 더하기

1. `Themes/{새 폴더}/Tokens.axaml`을 만든다. `Classic/Tokens.axaml`에서 바꾸고 싶은 줄만 옮겨 값을 고친다(전부 옮겨도 된다).
   컨트롤 색을 바꾸려면 `PastelSky/Tokens.axaml` 아래쪽의 Fluent 이름들을 본보기로 삼는다.
2. (바라면) `Controls.axaml`에 컨트롤의 모양 · 글꼴을 적는다. 글꼴 파일은 `Fonts/`에 두면 함께 실린다(`Vn.App.csproj`가 `Themes/**/Fonts/*.ttf`를
   싣는다) — **라이선스를 `THIRD-PARTY-NOTICES.txt`에 옮긴다.**
3. `Theming/ThemeCatalog.cs`의 `All`에 한 줄을 더한다 — `new("이름", "폴더", () => Ui.Theme_이름, HasControls: true/false)`.
4. 문구 표 `Strings/Ui.resx` · `Ui.en.resx`에 테마 이름(`Theme_이름`)을 한 줄씩 더한다.

설정 창의 테마 칸과 시험은 목록을 돌므로 따로 고칠 것이 없다.

## 3. 이름 더하기 (새 화면 요소의 색)

1. `Theming/Skin.cs`에 속성 한 줄 — `public static Color 이름 => C();`(모양의 수는 `N()`).
2. `Themes/Classic/Tokens.axaml`에 같은 키 한 줄 — `<Color x:Key="이름">#…</Color>`.
3. 다른 테마는 바꾸고 싶을 때만 같은 키를 적는다.

이름은 **맡은 일**로 짓는다(`Selection` · `Danger` · `EpisodeBorder`) — 색 이름(`Blue`)으로 짓지 않는다. 투명도만 다른 것은 이름을 늘리지 않고 쓰는
곳에서 준다(`Skin.Selection.Brush(42)`).

## 4. 확인

- `ThemeTests` — 이름표의 이름이 모두 기본 테마에 제 꼴로 있는지, 테마가 덮는 값의 꼴, 목록의 테마마다 폴더 · 이름, 테마마다 입히면 모든 이름이
  풀리고 주 창 · 두 판 · 설정 창이 서는지, 걷으면 기본 모양으로 돌아오는지, 앱 설정 · 설정 창의 테마 칸.
- **화면 시험 전체는 기본 테마로 돈다**(`TestProcessIsolation` — 색 · 치수를 재는 시험의 기준이 기본 테마다). 다른 테마로 전체를 돌려 보려면
  환경 변수 `VN_TEST_THEME`에 테마 이름을 준다 — 색 · 치수를 재는 시험은 그때 어긋나는 것이 정상이고, **터지는 시험**(예외)이 없는지를 본다.
- 그림으로 보기 — `UiTextFitProbe`(docs/ui-text.md 1절)에 `VN_TEST_THEME`을 함께 주면 그 테마로 주요 화면을 그린다:
  `$env:VN_UI_PROBE="ko"; $env:VN_TEST_THEME="pastel-sky"; dotnet test tests/Vn.App.Tests --filter "FullyQualifiedName~UiTextFitProbe"`.

## 5. 알아 둘 것

- **켜 있는 동안은 바꾸지 않는다.** 판의 카드 · 선은 코드가 지을 때 색을 꺼내 쓰므로, 켜 있는 동안 바꾸면 이미 지은 것과 새로 짓는 것의 색이
  갈린다. `ThemeService.Apply`는 다시 입힐 수 있게 지어져 있다(시험이 쓴다) — 켠 채 바꾸기를 넣으려면 두 판을 다시 짓는 길이 함께 있어야 한다.
- **Avalonia를 올리면** 파스텔 하늘이 덮는 Fluent 이름들이 그대로인지 본다(이름이 바뀌면 조용히 기본 색으로 돌아간다 — 그림으로 확인).
- 「파스텔 하늘」의 제목 글꼴 Jua에는 한글이 2,350자쯤만 있다 — 없는 글자는 본문 글꼴(Gowun Dodum · 11,172자 모두)로 그려진다.
