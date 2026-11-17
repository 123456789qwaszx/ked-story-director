# 인계 — 2026-11-17 오후 (대사만 있는 Yarn · DesignerNote 걷기)

> 앞 세션 인계 [`2026-11-17-handoff.md`](2026-11-17-handoff.md)의 4절 1~3번을 이 세션이 닫았다. 지금 동작의 정본은
> [`docs/design/two-graphs.md`](../design/two-graphs.md) 7절 V, 단계별 근거는 [`docs/work-orders/yarn-output-orders.md`](../work-orders/yarn-output-orders.md)의 진행 기록.

## 1. 지금 상태

- **클라우드 세션**이었다(소유자 PC가 아니다). 브랜치 `claude/brave-tesla-x2ttj9` — `origin/dev`(`624d7a8`) 위에 커밋 6개(Y0 · Y1+Y2 · Y3 · Y4 · Y5),
  그 가지로 push했다. **`dev`에는 아직 안 들어갔다** — 합칠지는 소유자가 정한다.
- 빌드 경고 0, `Vn.Authoring.Tests` 696 · `Vn.App.Tests` 415 전부 통과(Linux 컨테이너, 헤드리스). **Windows에서 돌려 보지 않았고 화면을 눈으로 보지 않았다.**
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

## 3. 소유자가 정한 것 (다시 묻지 말 것)

- 대사만 있는 Yarn은 이 툴이 낸다. 놓는 곳은 `exported/yarn/{챕터}_{장면}.yarn`뿐 — 게임 폴더에 쓰지 않는다.
- 파일은 장면 단위, 장면 메모는 파일 맨 위, 에피소드 지시는 노드 앞 — 둘 다 `//` 주석.
- 진행 JSON에 `DesignerNote` 칸은 없다.
- Yarn 컴파일 확인은 시험 프로젝트에서만 — 앱은 컴파일러를 싣지 않는다.
- 선택지의 표시조건 · 해금조건은 [구성]에서만 고친다([대본]은 읽기만 — 지금대로).

## 4. 열린 일

1. **2단계 — 연출 합치기.** AI가 쓴 연출 파일(LineId별)을 대사 Yarn에 합쳐 완성 Yarn을 낸다. 먼저 정할 것: 명령 → Yarn 글 규칙을 compositor에서
   가져올지, AI가 Yarn 명령 글을 그대로 적을지. 그때 `scenes.json`(대사 · 메모가 Yarn과 겹친다)을 남길지도. 들어가기 전에 지시서부터.
2. 소유자가 실제 화면으로 볼 것 — 앞 인계의 7절 R · S · U, 그리고 이번의 보고 줄(나간 자리 안내의 「대사 Yarn → exported/yarn/ (N개)」, 막힌 장면 안내).
3. `claude/brave-tesla-x2ttj9`를 `dev`에 합칠지.

## 5. 작업 환경 (클라우드 컨테이너)

- .NET SDK — `builds.dotnet.microsoft.com`은 네트워크 정책에 막혀 있다. Ubuntu 24.04의 apt에 `dotnet-sdk-10.0`(10.0.115)이 있어 그것으로 빌드했다:
  `apt-get install -y dotnet-sdk-10.0`. NuGet(nuget.org)은 열려 있다.
- 빌드 · 시험: `dotnet build VnTool.sln -tl:off`, `dotnet test VnTool.sln --no-build`(Vn.App.Tests가 1분 40초쯤).
- 저장소가 얕은 클론으로 온다 — 걷힌 옛 코드(`d26fb601` · `86944596` · `d9d5daca`)를 보려면 `git fetch --unshallow origin dev`.
- 작업 관행은 앞 인계 5절 그대로(단계마다 커밋 · 문서는 한국어 · ⛔ 날짜). 이 저장소의 파일은 git 안에서 LF다.
