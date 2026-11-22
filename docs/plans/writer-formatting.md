# 작가용 글 서식 — 설계와 단계

> 2026-11-22 소유자: *"시나리오 작가 입장에서는 한컴이나 엑셀 같은 문서 툴에서 흔히 하듯이, 자기가 현재 쓰는 텍스트를 직접 위쪽에서
> 크기나 색깔, bold, 기울기, 좌우 정렬, 글꼴을 지정하고 싶어 할 수 있겠다."* → *"이 툴에서 지정한 것을 Yarn에 반영시키진 않을 것입니다.
> 작가가 작업하기 편하려고 한 게 실제 게임에 섞여 버리니까. 작가가 보기 위한 서식으로 하겠습니다."* → *"옛판은 없으니 상관없습니다. 팀 공유입니다.
> compositor는 완전히 버린 것이니 신경 쓰지 마십시오."* → (구글 시트 도구 막대 그림) *"테두리, 셀 병합을 뺀 나머지 전부로 합시다."* →
> (AvaloniaEdit 시험판의 한글 입력을 보고) *"3번으로 한번 가능한 완성형으로 해주십시오."*
>
> **상태 — 만들었다(가지 `writer-formatting` · 소유자의 Windows 확인 대기).** `dev`에는 아직 넣지 않았다 — 소유자가 써 보고 정한다.
> ⛔ 이 문서의 첫 판(2026-11-22 `f4cc8c20`)에 있던 「줄 형광펜」(F1)은 소유자가 걷었다(WF-D8) — 만들지 않는다.

## 0. 시작하기 전에 (작업자에게)

- **먼저 읽을 것** — `ORIENTATION.md`(§9 빌드 · 테스트) → `docs/design/two-graphs.md` §7(카드 글 칸 · D4) → `ARCHITECTURE.md` §6(깨면 부서지는 규칙).
- **빌드 · 테스트** — Windows에서는 `dotnet test .\VnTool.sln`(요약 줄 2개). `TreatWarningsAsErrors` · `Nullable`이 켜져 있다. CI의 정본은 `windows-latest`.
  **클라우드 작업 공간(리눅스)에서 하는 법은 부록 A.**
- **파일 모양** — 소유자 PC의 작업 폴더는 CRLF다(저장소 안은 LF · Windows git의 autocrlf). 리눅스 쪽 git으로 보면 손대지 않은 파일도 `M`으로 보인다 —
  진짜 바뀐 것은 `git -c core.autocrlf=true status`로 본다. 고친 파일의 줄끝과 BOM 유무를 **그 파일이 쓰던 대로** 지킨다.
- **일하는 법** — 단계 하나 = 커밋 하나(또는 몇 개), 테스트 녹색을 보고 다음으로. 커밋 · 푸시는 소유자가 시킬 때. **소유자가 시키지 않은 기능을 더하지 않는다**
  (WF-D8 — 「다음 단계」를 제멋대로 읽어 형광펜을 만들었다가 모두 걷었다). 고친 동작은 문서(`writer-guide.md` · `작가에게.txt`)도 함께.

## 1. 정한 것 (소유자 · 2026-11-22)

| # | 결정 | 까닭 |
|---|---|---|
| WF-D1 | 서식은 **작가가 보기 위한 것**이다 — 게임에 나가지 않는다 | 작업용 표시가 게임 화면에 섞이면 안 된다 |
| WF-D2 | **대사 Yarn(`exported/yarn/`) · 진행 JSON(`exported/*.progression.json`)에 싣지 않는다** | 위와 같다. 내보낸 것은 서식 유무와 무관하게 바이트까지 같다(시험이 못 박는다) |
| WF-D3 | 게임에서 글자를 키우거나 색을 바꾸려면 **메모로 지시**하고 연출 과정에서 반영한다 | 게임의 글자 모양은 연출의 몫이다 |
| WF-D4 | 하이어아키의 순서는 자동 정렬 그대로 둔다(이 계획과 무관 — 같은 날 정했다) | — |
| WF-D5 | 서식은 **팀이 함께 본다** — 프로젝트 파일(대사 노드 JSON)에 저장한다 | 노드 JSON이면 되돌리기 · 「바뀐 것이 있는가」 비교가 저절로 따라온다 |
| WF-D6 | **옛 판 툴은 고려하지 않는다** — 프로젝트 형식 번호(4)는 그대로 | 새 칸은 비면 쓰지 않는 선택 칸이다 |
| WF-D7 | **ked-story-compositor는 버렸다** — 그쪽이 프로젝트 파일을 어떻게 읽는지 고려하지 않는다 | — |
| WF-D8 | **「줄 형광펜」은 없다** — 첫 판의 F1을 소유자가 걷었다 | 소유자가 청하지 않은 기능이었다 |
| WF-D9 | 도구 막대 = 구글 시트의 막대에서 **테두리 · 셀 병합을 뺀 전부** — 글꼴 · 크기 −/+ · 굵게 · 기울임 · 취소선 · 글자색 · 바탕색 · 좌우 정렬 | 소유자가 그림으로 보였다 |
| WF-D10 | 그리기는 **Avalonia TextBox 그대로, 글자를 놓는 겹만 바꾼다**(3.4) — 편집기 교체(AvaloniaEdit)는 버렸다 | 시험판(가지 `proto/writer-editor`)에서 한글 조합 중 글자가 안 보이고 Enter 뒤 첫 글자가 끊겼다. 한글 입력을 새로 만들지 않는 길을 골랐다 |
| WF-D11 | **정렬은 카드 단위**, **크기는 설정 글자 크기에 더하는 차(pt)**, **색은 정해 둔 열 가지** | 대본은 줄 목록이라 줄마다 정렬은 뜻이 적다. 글자 크기는 PC마다 다른 설정이라 차로 적어야 어느 PC에서나 같은 뜻이다 |
| WF-D12 | **한글은 기울지 않는다** — 받아들였다 | 한글 글꼴(맑은 고딕 등)에 기울임꼴이 없다(시험판에서 소유자가 확인 — 「한글은 안 기우는게 맞네요」) |

**하지 않는 것** — 게임용 서식 표기(Yarn 마크업), 줄 형광펜, 테두리, 셀 병합, 줄마다 다른 정렬, 화자 이름에 서식, 인스펙터 줄 표 · 하이어아키 ·
접은 장면 줄 목록에 서식 보이기.

## 2. 코드 자리

| 무엇 | 어디 |
|---|---|
| 모델 | `Vn.Authoring/Model/WriterMarks.cs` — `WriterColor`(열 가지) · `WriterAlign` · `WriterLook`(굵게 · 기울임 · 취소선 · 글자색 · 바탕색 · 글꼴 · 크기 차) · `WriterSpan` · `WriterRange` · `WriterStyleChange` · `WriterMarkMath`(순수 셈). `DialogueNode.WriterMarks`(LineId → 구간들) · `DialogueNode.WriterAlign` |
| 저장 · 읽기 | `Serialization/WriterMarksJson.cs` — 대사 노드 JSON의 `writerMarks` · `writerAlign` 칸(`StoryNodeJson`이 부른다) |
| 편집 | `Editing/ProjectEditor.WriterMarks.cs` — `ApplyWriterStyle` · `SetWriterAlign` · 글이 바뀔 때의 자리 옮기기(`RebaseWriterMarks` — `ApplyScriptSync` · `SetScriptLineText` 안에서) |
| 변경 알림 | `ProjectChangeKind.WriterMarks` + 그 카드 → `ProjectRefreshPlanner`의 `RefreshWriterMarks`(보통 · 되돌리기 두 갈래) → `MainWindow` → `GraphEditorView.RefreshWriterMarks`. `ChapterGraphView`는 이 알림에 다시 읽지 않는다(되돌리기는 읽는다) |
| 칸 한 줄의 대사 자리 | `ScenarioTextParser.DialogueBodyRange`(파서와 한 벌의 규칙) · 칸의 줄 → 대본 줄은 `ScenarioLineIdentity.RowLineIds`(글 넣기와 같은 짝) |
| 글 칸 | `Vn.App/Views/WriterTextBox.cs` — `WriterTextBox`(TextBox 그대로 · 틀은 Fluent TextBox 틀을 코드로 옮긴 것) · `WriterTextPresenter`(글자 배치) · `WriterPalette`(색 값) |
| 꾸밈 · 도구 막대 | `Vn.App/Views/GraphEditorView.Writer.cs` · 자리는 `GraphEditorView.axaml`의 `WriterToolbar`(위쪽 줄 [삭제] 오른쪽) |
| 시험 | `tests/Vn.Authoring.Tests/WriterMarksTests.cs` · `tests/Vn.App.Tests/WriterFormattingTests.cs` |

## 3. 설계

### 3.1 저장 — 서식은 글 밖, 대사 노드 JSON에

대사 글(`LocalizedLine.Text`)은 지금처럼 순수한 글자다. 서식은 **카드마다, 줄(LineId)마다** 그 줄 **기본 언어 대사 글의 자리**로 붙는다(화자는 밖).

```json
"writerMarks": {
  "ln_ab12": [ { "start": 3, "length": 4, "bold": true, "color": "red", "fill": "yellow", "font": "맑은 고딕", "grow": 2 } ]
},
"writerAlign": "center"
```

- 비었으면 칸을 쓰지 않는다 — **서식 없는 프로젝트의 파일은 바이트까지 그대로다.** 줄은 LineId 차례, 구간은 자리 차례(결정적 출력). 모양은 켜진 것만 적는다.
- 구간 목록은 늘 정규형 — 겹치지 않고 자리 차례, 모양 없는 구간이 없고, 이웃한 같은 모양은 합친다.
- **읽기는 너그럽다** — 모르는 색 · 숫자가 아닌 자리 · 글 밖 · 겹침은 버리거나 잘라 정규형으로 읽는다. 서식 때문에 프로젝트가 안 열리는 일이 없다.
- 은퇴한 줄의 서식도 본문처럼 남는다 — 되살아나면 돌아온다. 다른 언어의 글이 바뀌어도 서식은 그대로다.
- 글 안의 표기(`[b]…[/b]`)가 아닌 까닭 — 표기를 섞으면 내보내기 · 글자 수 세기 · 화자 이름 바꾸기 · 줄 맞추기가 모두 그 표기를 걸러야 하고, 하나라도 놓치면
  게임에 샌다(WF-D2). 밖에 두면 새는 길이 없다.

### 3.2 편집

- `ApplyWriterStyle(nodeId, ranges, change)` — 여러 줄이어도 **되돌리기 한 칸**, 바뀐 것이 없으면 적지 않는다. 켜고 끄는 것(굵게 · 기울임 · 취소선)은 문서 도구와
  같은 판정: 고른 글자가 모두 그 모양이면 끄고, 하나라도 아니면 모두 켠다. 크기는 글자마다 차를 ±1(−6 ~ +36에서 멈춘다).
- `SetWriterAlign(nodeId, align)` — 카드의 정렬.
- 알림 `ProjectChangeKind.WriterMarks` + 그 카드 — 판은 그 카드의 꾸밈만 다시 입힌다(카드 · 철도를 다시 짓지 않는다), 곁기둥 · [구성]은 그대로다.

### 3.3 글이 바뀌면 서식이 같은 글자를 따라간다 (`WriterMarkMath.Rebase`)

대사 글이 바뀌는 두 자리(글 칸의 넣기 → `ApplyScriptSync`, 인스펙터 줄 표 → `SetScriptLineText`)가 **같은 변경 안에서** 옮긴다(되돌리기 한 칸이 글과 서식을 함께).
두 글의 글자를 앞에서부터 짝짓고(가장 긴 공통 부분열) 짝이 선 글자는 제 모양을 가져간다. 새로 들어온 글자는:

- 그 자리에서 지운 글자가 없으면(넣기) **바로 앞 글자의 모양** — 서식 끝에 이어 친 글자 · 서식 안에 친 글자는 그 모양이고, 서식 바로 앞에 친 글자는 아니다(워드 ·
  한글과 같다). 글 맨 앞에 친 글자는 모양이 없다.
- 지운 글자가 있으면(바꿔 쓰기) 지운 글자들이 **모두 같은 모양**일 때만 그 모양.

쓰다 멈추기 전에 두 곳을 고쳐도 고친 곳마다 따로 셈한다. 아주 긴 줄(두 글 길이의 곱이 40만을 넘는다)은 앞 · 뒤 공통 부분만 보는 셈으로 갈음한다.
**글 칸은 쓰는 동안에도 같은 셈으로 그린다**(모델의 글 → 칸의 글) — 넣은 뒤의 모델과 넣기 전의 그림이 같다.

### 3.4 화면

**글 칸** — Avalonia `TextBox` 그대로다(`WriterTextBox`). 입력 · 커서 · 고른 범위 · 한글 조합 · 되돌리기는 손대지 않는다. 글자를 놓는 겹
(`TextPresenter`)의 `CreateTextLayout`만 바꿔, 칸 좌표의 구간마다 모양(`GenericTextRunProperties` — 굵기 · 기울임 · 취소선 · 글자색 · 바탕색 · 글꼴 · 크기)을
글자 배치에 넘긴다. Avalonia가 조합 중인 글자에 밑줄을 긋는 바로 그 길이라, 커서 · 고르기 · 누른 자리 · 분기 점 높이 · 「글에 맞춤」이 서식 입은 배치를 그대로
쓴다. 서식이 없는 칸은 바탕의 배치 그대로다.

- 틀은 Fluent 테마의 TextBox 틀(Avalonia 12.1.1 `Themes.Fluent/Controls/TextBox.xaml`)을 코드로 옮긴 것이다 — 부분 이름(`PART_BorderElement` · `PART_ScrollViewer` ·
  `PART_TextPresenter` …)을 지켜 테마의 모양이 그대로 붙는다. **Avalonia를 올리면 이 틀과 테마 틀을 견줘 본다.**
- 크기를 키운 글자가 있는 칸은 줄 높이(설정의 줄 간격 — 고정)를 가장 큰 글자의 비율로 칸 전체에서 키운다(줄마다 다른 높이는 TextBox 배치가 받지 않는다).
- ⚠ **한글 굵게** — 주 글꼴(Inter)에 한글이 없어 한글은 대체 글꼴(맑은 고딕 · Noto Sans CJK)로 그려지는데, Avalonia는 바로 앞 구간이 쓴 대체 글꼴이 그 글자를
  그릴 수 있으면 그것을 다시 쓴다. 그대로 두면 보통 글 다음의 굵은 한글이 보통 굵기로, 굵은 글 다음의 보통 한글이 굵게 나온다. 한글은 앞 구간과 **문화권이
  다르면** 물려받지 않으므로 글꼴 모양(굵기 · 기울임 · 글꼴)마다 다른 문화권을 준다(`WriterTextPresenter.CultureOf`). **Avalonia를 올리면 이 동작을 다시 본다**
  (`TextCharacters.CreateShapeableRun`).

**칸 좌표 ↔ 대본** — 칸의 줄을 처음 보인 글과의 줄 짝(`RowLineIds`)으로 대본 줄에 잇고, 줄 안의 대사 글 자리는 `DialogueBodyRange`로 잰다. 화자 · 조건 줄 ·
빈 줄 · 아직 대본 줄이 아닌 새 줄에는 서식이 붙지 않는다.

**도구 막대** — [대본] 위쪽 줄의 [삭제] 오른쪽: 글꼴(「기본 글꼴」 + 이 PC의 글꼴 · 서식에 든 글꼴이 이 PC에 없으면 그 이름도 더해 보인다) · − 크기 + ·
**B** · *I* · S · 가(글자색 ▾) · 펜(바탕색 ▾) · 정렬 셋.

- 입힐 곳 = **마지막으로 초점을 쥔 카드 글 칸과 그 고른 범위**. 판의 빈 곳 · 카드 머리를 누르거나 다른 것을 고르면 놓고 도구가 꺼진다.
- 단추는 초점을 받지 않는다 — 칸이 초점과 고른 범위를 쥔 채 입힌다. 초점을 가져가는 것(글꼴 고르기 · 색 차림)은 고른 범위를 붙들어 두었다가 닫힌 뒤 칸에
  초점과 범위를 되돌린다.
- **넣지 않은 글이 있으면 먼저 넣는다**(살아 있는 넣기 — 줄 신원이 서야 자리를 센다). 넣기와 서식은 되돌리기 두 칸이다.
- 고른 글자가 없으면 입히지 않고 상태줄이 말한다(정렬은 커서만 있어도 된다). 단추는 고른 글자의 지금 모양을 보인다(모두 굵으면 B가 눌려 있다 · 크기가 하나면 그 숫자).

## 4. 단계

| | 무엇 | 상태 |
|---|---|---|
| F0 | 조사 — AvaloniaEdit 12.0.0 시험판(가지 `proto/writer-editor`): 빌드 · 서식 그리기 · 줄 간격은 됐으나 한글 조합 중 글자가 안 보이고(입력기 짝이 조합을 받지 않는다) Enter 뒤 첫 글자가 끊겼다. 편집기 160개 짓기 0.85초(TextBox 0.26초) | 끝 — WF-D10 |
| F1 | 모델 · 저장 · 편집 · 자리 옮기기 · 글 칸 그리기 · 도구 막대 · 문서(이 문서 2 · 3절) | 가지 `writer-formatting` — Authoring 759 · App 전체 녹색(클라우드 헤드리스), **소유자의 Windows 확인 대기**(한글 조합 · 굵은 한글 · 글꼴 목록) |

완료 조건(F1) — 시험이 못 박은 것: 내보낸 대사 Yarn · 진행 JSON이 서식을 잔뜩 입혀도 바이트까지 같다 · 서식 없는 프로젝트의 저장에 칸이 생기지 않는다 · 이상한
서식이 든 파일도 열린다 · 토글 판정 · 여러 줄 한 칸 · 되돌리기 · 다시 실행 · 글 칸 · 줄 표에서 고쳐도 같은 글자를 따라간다(넣기 전의 그림도) · 화자 이름을
바꿔도 그대로 · 판을 다시 짓지 않는다(철도 · 칸 객체 그대로) · [구성]을 다시 읽지 않는다 · 입힌 뒤에도 칸이 초점과 고른 범위를 쥔다 · 넣지 않은 글을 먼저
넣는다 · 화자 부분은 빠진다 · 기존 글 칸 시험(카드 쓰기 · 살아 있는 넣기 · 맞춤 · 분기 점 · 글꼴 설정 등)이 고치지 않고 지난다.
Windows에서 손으로 볼 것: 한글 조합(조합 중 글자 · 후보 창 · Enter 뒤 첫 글자)이 서식 있는 칸 · 없는 칸 모두 고치기 전과 같다, 굵은 한글이 굵게 보인다.

## 5. 늘 지킬 것 (어느 단계든)

1. **내보낸 대사 Yarn · 진행 JSON은 서식과 무관하다.** 내보내기 코드가 `WriterMarks`를 읽는 순간 이 계획은 틀린 것이다.
2. **대사 글에 표기를 섞지 않는다.** `LocalizedLine.Text`는 순수한 글자다.
3. **서식 때문에 프로젝트가 안 열리는 일이 없다.**
4. **서식 없는 프로젝트의 저장 파일은 지금과 같다.**
5. **판 속도를 잃지 않는다** — board-speed · usability UX7 ~ UX21로 줄여 둔 「판을 통째로 다시 짓지 않는다」를 서식이 깨지 않는다.
6. **한글 입력을 잃지 않는다.**

## 6. 소유자가 정할 것

| | 무엇 | 언제 |
|---|---|---|
| ① | 가지 `writer-formatting`을 `dev`에 넣을지 | Windows에서 써 본 뒤 |
| ② | 인스펙터 줄 표 · 접은 장면의 줄 목록에도 서식을 보일지 | 써 본 뒤 |

## 부록 A. 클라우드 작업 공간(리눅스)에서 빌드 · 테스트 (2026-11-22에 해 본 길)

이 저장소의 일은 소유자 PC(Windows)의 폴더를 연결한 대화 세션에서 하는 일이 많다. 그 세션의 셸 두 가지 — **소유자 PC 쪽 셸**(`device_bash` · 연결한 폴더가
`$HOME/mnt/<이름>`)은 디스크가 거의 없어(100MB 남짓) 빌드할 수 없고, **클라우드 작업 공간**(`Bash` · Ubuntu 24.04 · 메모리 7GB)은 nuget.org · dot.net에 닿지
않는다(archive.ubuntu.com은 닿는다). 그래서 SDK는 우분투 꾸러미로, 패키지는 소유자 PC의 NuGet 캐시에서 들여온다.

1. **SDK** — 클라우드에서 `apt-get install -y dotnet-sdk-10.0 netstandard-targeting-pack-2.1-8.0`(`Ked.Progression`이 `netstandard2.1`이다).
2. **패키지** — 소유자에게 `C:\Users\river\.nuget\packages`를 세션에 연결해 달라고 한다. PC 쪽 셸에서 패키지마다 가장 새 판의 `.nupkg`만 골라(`*.runtime.win-x64` ·
   `crossgen2` · `host.win-x64` · `windowsdesktop` · 쓰지 않는 `npoi` · `documentformat*` · `bouncycastle`은 빼도 된다 — 약 400MB) tar로 묶고, **40MB 조각으로 잘라**
   (`split -b 40M`) 저장소 폴더 안 임시 폴더(예: `.claude-build/`)에 둔다 — 200MB짜리 하나는 올리기가 시간 초과로 실패했다. 조각을 `device_stage_files`로 올려
   클라우드에서 이어 붙여 풀고(`cat part-* | tar -xif -` — 두 tar를 이어 붙였으면 `-i`), `.nupkg`를 한 폴더에 모아 **평평한 로컬 원본**으로 쓴다. 가장 새 판만
   고르면 `Newtonsoft.Json 13.0.1`이 빠진다(테스트 호스트가 그 판을 찾는다 — 경고가 오류다) — 따로 올린다.
3. **저장소** — PC 쪽 셸에서 `tar -cf .claude-build/src.tar --exclude=bin --exclude=obj VnTool.sln Directory.Build.props Directory.Packages.props src tests samples docs`
   (`docs`의 진행 견본 · `samples/RuntimeSamples`를 읽는 시험이 있다)를 올려 클라우드의 빌드 폴더에 푼다. **소유자 폴더 안에서 빌드하지 않는다** — `obj`의
   `project.assets.json`이 리눅스 경로로 덮여 소유자의 Windows 빌드가 다시 복원해야 한다.
4. **설정** — 빌드 폴더에 `nuget.config`(`<clear />` + 로컬 원본 하나), `NUGET_PACKAGES`를 클라우드 안의 빈 폴더로. `dotnet restore` → `dotnet build`.
5. **테스트** — `Vn.Authoring.Tests`는 한 번에. **`Vn.App.Tests`는 클래스 묶음 넷으로 나눠** 돌린다(`--list-tests`로 클래스를 모아 `--filter`) — 한 번에 돌리면
   메모리를 넘어 테스트 호스트가 죽는다(exit 137 · 「Test host process crashed」 · 그 전까지의 「Passed!」 줄은 일부일 뿐이다 — 셈이 `--list-tests`와 맞는지 본다).
6. **고칠 때** — 고치는 것은 소유자 폴더의 파일(PC 쪽 셸 · 줄끝 · BOM을 지켜)이고, 빌드 폴더에는 src.tar를 다시 올려 덮어 푼다(bin · obj는 그대로라 빌드가 빠르다).
7. **치우기** — 임시 폴더를 지우려면 지우기 허락(`device_request_delete_permission`)이 필요하다. 허락이 없으면 PC 쪽 셸의 git도 잠금 파일을 지우지 못해
   `git commit`이 `.lock`을 남긴다 — 커밋도 그 허락 뒤에 한다.

2026-11-22 결과: 빌드 경고 0, `Vn.Authoring.Tests` 732 · `Vn.App.Tests` 621(179 + 155 + 133 + 154) 모두 녹색.
