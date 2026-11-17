# 인계 — 2026-11-17 오후 (대사만 있는 Yarn · [대본] 손질 · [⚙ 설정]과 판 크기)

> 앞 세션 인계 [`2026-11-17-handoff.md`](2026-11-17-handoff.md)의 4절 1~3번을 이 세션이 닫았다. 지금 동작의 정본은
> [`docs/design/two-graphs.md`](../design/two-graphs.md) 7절 V · W, 단계별 근거는 [`yarn-output-orders.md`](../work-orders/yarn-output-orders.md) ·
> [`settings-orders.md`](../work-orders/settings-orders.md)의 진행 기록.

## 1. 지금 상태

- **클라우드 세션**이었다(소유자 PC가 아니다). 브랜치 `claude/brave-tesla-x2ttj9`에서 일하고, 소유자 말에 따라 **GitHub `dev`로 빨리감기**해 왔다
  (소유자가 PC에서 `dev`를 받아 화면으로 본다). 마지막 반영은 이 인계가 든 커밋이다.
- 빌드 경고 0, `Vn.Authoring.Tests` 705 · `Vn.App.Tests` 434 전부 통과(Linux 컨테이너, 헤드리스). **Windows에서 돌려 보지 않았다.** 화면은
  헤드리스 렌더(Skia)로만 봤다 — [구성] 간선 인스펙터 머리글, [⚙ 설정] 창 · 위 띠.
- ⚠ Y4가 `samples/RuntimeSamples/FestivalEve/exported/festival_eve.progression.json`에서 `DesignerNote` 줄 13개를 걷었다(`RuntimeSampleTests`가
  다시 낸 것과 견준다). 앞 인계가 말한 소유자의 커밋 안 한 `FestivalEve/` 변경에 이 파일이 들어 있으면 받을 때 겹친다 — 앱이 다음 내보내기에
  같은 모양으로 다시 쓰므로 소유자 쪽 파일을 버리고 받아도 잃는 것이 없다.

## 2. 이번 세션에 끝난 것

| 단계 | 무엇 |
|---|---|
| Y0 | 작업 지시서. 소유자 답 — 대사만 Yarn은 `exported/yarn/`에만(게임 폴더 ✕), 메모는 `.yarn` 주석, 선택지 조건은 지금대로([대본]은 읽기만) |
| Y0 답 | 처음부터 장면 단위 파일, `DesignerNote`는 진행 JSON에서 칸째 걷기, 컴파일 확인은 시험에서만 |
| Y1 · Y2 | `Chapters/ChapterYarnExporter` — 장면 = 파일, 에피소드 = 노드(타이틀 = `DialogueEntryId`, 같은 함수 `DialogueEntryIdOf`). 머리에 장면 이름 · 메모, 노드 앞에 에피소드 지시(주석). 작가 글 이스케이프(`YarnSyntax.EscapeLineText`). `YarnSpinner.Compiler` 3.2.1은 `Vn.Authoring.Tests`에만 |
| Y3 | `ChapterExportService.ExportAll`이 진행 JSON과 함께 `exported/yarn/`에 쓴다. 막힌 장면은 옛 파일을 두고 보고 맨 위에 적는다. 지운 장면 · 챕터의 파일은 걷는다 |
| Y4 | 진행 JSON의 `DesignerNote` 칸을 걷었다 — 골든 · 견본 산출물 둘 · `runtime-contract.md` |
| Y5 | 문서 — `two-graphs.md` 7절 V · `ARCHITECTURE.md` · `README.md` · `ORIENTATION.md` · `writer-guide.md` · `작가에게.txt` · 코드 주석 · 이 인계 |

바로잡은 것: 앞 인계의 「저장소에 남은 `Vn.Core`(Yarn 컴파일러)」는 틀렸다 — L1(`d9d5daca`)에 걷혀 없었다. 패키지를 시험 프로젝트에 다시 들였다.

그 뒤 소유자 보고로 고친 것(차례대로):

| 무엇 | 어디 |
|---|---|
| [대본] 카드 글 칸 위의 휠이 판 전체를 세로로 스크롤하던 것 — 캔버스가 버블에서 끝낸다 | `OnWritingBoxWheel` · two-graphs 3.4 |
| 다른 챕터 카드를 누른 뒤 **선택지 문구를 처음 열면 화면이 튀던 것** — 원인: 활성 판이 바뀐 것을 판을 다시 그릴 때만 보고 그 챕터로 옮겼다(GB-1). 이제 화면은 사람이 그 챕터로 가자고 할 때만(챕터 콤보 · 하이어아키 챕터 줄 · [＋ 챕터]) 옮기고, 프로젝트를 연 첫 화면은 [대본] 탭이 처음 보일 때 맞춘다. 판마다 기억하던 화면(GB-1)은 걷었다 | `GoToBoard` · `ProjectGeneration` · two-graphs 3.1 「화면」 |
| 글 칸 위의 휠 — **고른 카드**의 칸이면 칸이 스크롤, 고르지 않았으면 칸 위라도 확대 | `WheelBelongsToBox` |
| [구성] 간선 인스펙터 — 읽기 전용 문구 칸을 걷고 머리글을 「선택지 · {문구}」로(⛔ 7절 U) | `EdgeOptionText` |
| [⚙ 설정]과 [대본] 판 크기 — 프로젝트에 싣고, 줄일 때는 카드 전체를 같은 비율로(C0~C4) | 7절 W · `settings-orders.md` |

## 3. 소유자가 정한 것 (다시 묻지 말 것)

- 대사만 있는 Yarn은 이 툴이 낸다. 놓는 곳은 `exported/yarn/{챕터}_{장면}.yarn`뿐 — 게임 폴더에 쓰지 않는다.
- 파일은 장면 단위, 장면 메모는 파일 맨 위, 에피소드 지시는 노드 앞 — 둘 다 `//` 주석.
- 진행 JSON에 `DesignerNote` 칸은 없다.
- Yarn 컴파일 확인은 시험 프로젝트에서만 — 앱은 컴파일러를 싣지 않는다.
- 선택지의 표시조건 · 해금조건은 [구성]에서만 고친다([대본]은 읽기만 — 지금대로).
- [대본] 화면은 [구성]에서 챕터를 골라도 옮기지 않는다. 판마다 기억한 화면은 쓰지 않는다 — 챕터를 고르면 그 챕터 전체가 들어오게 맞춘다.
- 글 칸의 휠은 그 카드를 골랐을 때만 칸의 것이다(고른 카드의 칸이 넘치지 않으면 아무 일도 없다).
- [구성] 간선 인스펙터의 머리글이 선택지 문구다(문구 칸 · 고치는 곳 안내 없음).
- 위 띠: 새 프로젝트 · 열기 · 저장은 **File** 메뉴, [실행 취소] · [다시 실행] 단추는 없다(Ctrl+Z · Ctrl+Y).
- 분기로 들어오는 카드는 **실제 연결**로 알아본다 — 도달할 수 있는 에피소드의 분기점이 들르면 도달 가능(알림), 분기점이 도달할 수 없으면 오류.
  「분기 추가」는 `도달불가 허용` 깃발을 달지 않는다. 카드를 종류(에피소드 / 분기 카드)로 나누지 않는다.
- 분기로만 들어오는 카드도 **선택지 칸을 열어 둔다**(그 선택지는 지금 런타임에서는 나오지 않는다 — two-graphs 7절 Y).
- 설정은 [⚙ 설정] 한 창에, 묶음마다 저장 자리를 적는다. 판 크기는 **프로젝트**에, 4,000 ~ 60,000 · 기본 12,000 × 8,000, 오른쪽 · 아래로
  늘어난다. 줄일 때는 판 밖으로 나가는 카드가 있을 때만 **카드 전체**를 같은 비율로(되돌리기 한 칸).

## 4. 열린 일

1. **2단계 — 연출 합치기.** AI가 쓴 연출 파일(LineId별)을 대사 Yarn에 합쳐 완성 Yarn을 낸다. 먼저 정할 것: 명령 → Yarn 글 규칙을 compositor에서
   가져올지, AI가 Yarn 명령 글을 그대로 적을지. 그때 `scenes.json`(대사 · 메모가 Yarn과 겹친다)을 남길지도. 들어가기 전에 지시서부터.
2. 소유자가 실제 화면으로 볼 것 — 앞 인계의 7절 R · S · U, 대사 Yarn 보고 줄, 휠 · 화면 튐 · [구성] 머리글, [⚙ 설정] 창과 판 크기.
3. compositor가 manifest의 새 칸 `board`를 무시하는지(이 저장소 밖) — 소유자가 확인.
4. 오늘 전에 「분기 추가」로 만든 카드에 남은 `도달불가 허용` 깃발 — 열 때 일괄로 지울지(화면에는 고치는 칸이 없다).
5. 설정 창 후보(정하지 않음) — [이 PC]: 자동 저장 간격(지금 10분 고정) · 시작할 때 마지막 프로젝트 열기 · 카드 글자 크기 · 새 카드 기본 크기,
   [이 프로젝트]: 대사 Yarn 내보내기 켜기/끄기.

## 5. 작업 환경 (클라우드 컨테이너)

- .NET SDK — `builds.dotnet.microsoft.com`은 네트워크 정책에 막혀 있다. Ubuntu 24.04의 apt에 `dotnet-sdk-10.0`(10.0.115)이 있어 그것으로 빌드했다:
  `apt-get install -y dotnet-sdk-10.0`. NuGet(nuget.org)은 열려 있다.
- 빌드 · 시험: `dotnet build VnTool.sln -tl:off`, `dotnet test VnTool.sln --no-build`(Vn.App.Tests가 1분 40초쯤).
- 저장소가 얕은 클론으로 온다 — 걷힌 옛 코드(`d26fb601` · `86944596` · `d9d5daca`)를 보려면 `git fetch --unshallow origin dev`.
- 작업 관행은 앞 인계 5절 그대로(단계마다 커밋 · 문서는 한국어 · ⛔ 날짜). 이 저장소의 파일은 git 안에서 LF다.
