# ked-story-director

> **이야기의 뼈대와 대사를 쓰는 툴.** 2026-10-29에 VnTool(`VNStoryEditor_avalonia` `dev` @ `f2a7bdc`)에서 이력째 갈라졌다.
> 옛 대본 탭(엑셀 대본 편집기 — 지금의 [대본] 탭과 다른 것)과 엑셀 연동을 걷었고(D2), 연출 그래프를 위→아래로 세웠고(D3), 대사를 카드 안에서 직접 쓴다(D4).
> **연출(무대 프리뷰 · 연출 명령 · 게임용 Yarn · 발행)은 이 툴에 없다** — 2026-10-29에 걷었다. 그 일을 받았던 `ked-story-compositor`는
> 2026-11-22에 **버렸다**(소유자) — 이 툴은 그쪽이 프로젝트 파일을 어떻게 읽는지 고려하지 않는다. 걷은 계획과 단계는
> [`docs/plans/director-split.md`](docs/plans/director-split.md) · [`docs/plans/presentation-removal.md`](docs/plans/presentation-removal.md),
> 그 뒤의 레거시 정리는 [`docs/plans/legacy-cleanup.md`](docs/plans/legacy-cleanup.md), 지금 동작의 정본은 [`docs/design/two-graphs.md`](docs/design/two-graphs.md).
> 아래 "문서" 표에서 **이력**이라 적은 것은 VnTool 시절(엑셀 워크플로 · 무대)을 설명한다 — 머리에 무엇이 걷혔는지 적혀 있다.

비주얼 노벨 저작 도구. C#/.NET 10 + Avalonia, 한국어 코드베이스.
작가에게 보이는 이름은 **「스토리 디렉터」**(실행 파일 `StoryDirector`)다 — 솔루션 · 프로젝트 · 네임스페이스(`VnTool.sln` · `Vn.App`)는 안의 이름이라
그대로다(2026-11-19 · [`docs/work-orders/writer-app-orders.md`](docs/work-orders/writer-app-orders.md)).

만들어 내는 것은 **원고(프로젝트 폴더)** 와 유니티 런타임(별도 저장소 `ked-presentation-runtime`)이 먹는 **`exported/{챕터}.progression.json`** 입니다.
함께 **대사만 있는 Yarn**(`exported/yarn/` — 장면마다 한 파일, 메모는 주석, 연출 없음)이 나가 작가가 대사만으로 굴러가는지 봅니다(2026-11-17).
내보내는 것은 진행 JSON과 이 대사 Yarn 둘뿐입니다. 연출은 이 툴 밖의 일입니다.

## 두 그래프

화면의 탭 이름은 **[대본]**(연출 그래프)과 **[구성]**(챕터 그래프)이다(2026-11-05). 탭 순서가 작업 순서이고 창은 [대본]에서 열린다(2026-11-19 —
먼저 쓰고 잇고, 그다음 조건 · 스탯을 붙인다 · ⛔ 그 전에는 [구성]이 첫째였다).
문서와 코드는 아래의 옛 이름(챕터 그래프 · 연출 그래프, `ChapterGraphView` · `GraphEditorView`)을 그대로 쓴다.

두 탭 모두 왼쪽에 **하이어아키**(프로젝트 → 챕터 → 장면 → 에피소드), 오른쪽에 **탭 셋 — 인스펙터 · 화자 · 조건 및 스탯**이 서고,
두 기둥은 접힌다. [구성]의 하이어아키는 고르기 · 접기만 한다.

| | **① 챕터 그래프 [구성]** (기획) | **② 연출 그래프 [대본]** (대사 · 흐름) |
|---|---|---|
| 만드는 것 | 챕터 구조의 수치 — ID · 조건 식 · 스탯 · 선택지의 조건 / 스탯변화 · 화자의 캐릭터 키 | 챕터 · 장면 · 에피소드 세우기와 지우기 · 대사 카드 · 카드 안 글 · 선택지 잇기(선 긋기) · 분기 · 이름(챕터 · 장면 · 에피소드 · 화자 · 조건) |
| 값이 사는 곳 | 프로젝트 | 프로젝트 |
| 도구의 역할 | 편집하고 검증하고 낸다 | 편집한다 |

## 원본은 프로젝트 하나

모든 값의 원본은 **프로젝트**입니다. 엑셀 워크북을 들여오고 내던 연동과 옛 대본 탭(엑셀 대본 편집기)은 2026-10-29(D2)에
걷었습니다 — 나중에 다시 붙일 수 있지만 지금은 복잡하기만 하다는 판단입니다.

- **대사 본문·화자** → 연출 그래프의 **대사 카드**(카드마다 대본 하나). 카드 안의 글 칸에서 직접 씁니다(D4).
- **에피소드 구조·간선·조건·스탯** → 챕터(`ChapterDocument`). 에피소드를 세우고 간선을 긋는 곳은 [대본]입니다 — 카드 아래 칸에
  문구를 적어 다른 카드로 끌면 선택지, 맨 위 「자동」 칸을 끌면 자동 길입니다. [구성]에서는 그 ID와 간선의 조건 · 스탯변화를 고칩니다.
  **창구가 둘이지 값이 사는 곳은 하나**입니다.
- 챕터 조건은 판의 공급 노드(숨은 배관)로 저절로 건너가 대사 카드의 조건 목록에 뜹니다.
- **진행 JSON**은 검증을 통과하면 저절로 나갑니다. 저장은 프로젝트를 씁니다.
- 툴이 만들지 않는 모양의 프로젝트(카드 없는 에피소드 · 장면 없는 에피소드 등)는 **열지 않습니다** — 고쳐 받지 않고 까닭을 알립니다.

**각 값의 주인은 한 곳뿐**이라는 것이 설계 전체를 관통하는 규칙입니다. 같은 사실이 두 곳에
있으면 어긋나고, 어긋남은 최종 출력에서야 드러납니다.

## 저장 구조

```text
MyStory/
├─ project.vnproject.json      매니페스트(형식 4) — 목차 · 챕터 · 화자 · 파일을 넘나드는 관계. 폴더에 하나뿐이어야 한다
├─ game.definition.json        (옛 프로젝트에 있으면) 버린 compositor의 연출 커맨드 어휘 — 이 툴은 읽지도 쓰지도 않는다
├─ story/sf_xxxxxxxx.vnstory.json  판(챕터마다 하나)과 그 위의 카드·노드 — 카드는 제 대본(줄 신원 + 본문)을 든다
├─ runtime.target.json         (있으면) 복제할 폴더 — gameFolder(진행 JSON) · dialogueYarnFolder(대사 Yarn). 저장할 때 exported/를 거기로 복제한다([⚙ 설정]의 「내보내기」가 적는다)
└─ exported/                   툴의 작업 사본 — 고칠 때마다 저절로 나간다
   ├─ ch01.progression.json    런타임 수입물 — 검증을 통과하면 나간다
   └─ yarn/ch01_scene01.yarn   대사만 있는 Yarn — 장면마다 하나, 게임 폴더로는 보내지 않는다
```

**보낼 곳에는 저장(Ctrl+S)할 때만 보냅니다** — 진행 JSON의 보낼 곳이 게임 폴더(런타임의 `ChapterProgression`)이고, 대사 Yarn도 따로
정할 수 있습니다. 이 툴이 보낸 그대로인 지난 파일만 걷습니다.

프로젝트는 형식 4만 엽니다 — 그 앞의 형식 1~3(대본이 따로 서거나 연출을 담은 옛 프로젝트)은 열지 않고 알립니다.

## 실행

```powershell
dotnet run --project .\src\Vn.App\Vn.App.csproj
```

## 검증

```powershell
dotnet test .\VnTool.sln
```

테스트 프로젝트는 둘(`Vn.Authoring.Tests` · `Vn.App.Tests` — Avalonia 헤드리스)입니다. 요약 줄이 둘인지 세어 보세요 —
빌드에 실패한 프로젝트는 조용히 건너뜁니다. CI는 `windows-latest`가 정본이고, `macos-latest`가 한 번 더 돌려 맥 꾸러미를 굽습니다.

## 작가에게 전달할 꾸러미

Windows — `artifacts\StoryDirector-win-x64.zip`(실행 파일 `StoryDirector.exe` 하나):

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-windows.ps1
```

맥 — `artifacts/StoryDirector-osx-arm64.zip`(`StoryDirector.app`). 맥에서 돌립니다 — 맥이 없으면 CI의 `macos` 작업이 구워 올려 둔 것을
Actions의 실행 화면 아래 Artifacts에서 받습니다. 인텔 맥은 인자 `x64`:

```bash
./publish-macos.sh
```

둘 다 .NET 런타임을 함께 담으므로 받는 PC에 개발 환경이 필요 없습니다. 서명은 없습니다 — Windows는 파란 경고 창을, 맥은 「그래도 열기」를
처음 한 번 지납니다([`docs/작가에게.txt`](docs/작가에게.txt)). 맥의 서명 · 공증은 하지 않았습니다(Apple 개발자 계정이 듭니다).

아이콘은 원본 `src/Vn.App/Assets/app-icon.png`(1024) 하나에서 굽습니다 — 그림을 바꾸려면 원본을 갈고 다시 굽습니다(Pillow가 있어야 합니다):

```bash
python3 tools/make-app-icons.py
```

## 문서

**처음 보는 사람은 [`ORIENTATION.md`](ORIENTATION.md)(지도), 작업을 이어받는 사람은
[`docs/design/two-graphs.md`](docs/design/two-graphs.md)(지금 동작 — 7절 끝이 가장 새 결정)와 그 절이 가리키는 [`docs/work-orders/`](docs/work-orders/)의 지시서에서 시작합니다.**

| 문서 | 무엇이 있나 |
|---|---|
| [`ORIENTATION.md`](ORIENTATION.md) | **처음 보는 사람의 지도.** 프로젝트 · 폴더 · 데이터 흐름 · 지뢰 · 읽는 순서 |
| [`docs/design/two-graphs.md`](docs/design/two-graphs.md) | **지금 동작의 정본.** 두 그래프의 기능 · 사용자 동작 · 다시 쓸 때 지킬 것 · 7절의 결정 기록(D2부터 — 끝이 가장 새 것) |
| [`docs/plans/legacy-cleanup.md`](docs/plans/legacy-cleanup.md) | 레거시 정리 L0~L8 — 걷은 것 · 남긴 것과 그 까닭 · 걷다가 찾은 결함 |
| [`docs/plans/writer-formatting.md`](docs/plans/writer-formatting.md) | **만듦(가지 `writer-formatting` · 확인 대기).** 작가용 글 서식 — 게임에 나가지 않는 글꼴 · 크기 · 굵게 · 기울임 · 취소선 · 색 · 정렬의 설계 · 완료 조건 · 클라우드 작업 공간에서 빌드하는 법(부록 A) |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | 코드 구조 — "이 기능은 어느 파일" |
| [`docs/external-overview.md`](docs/external-overview.md) | **이력**(2026-09-17 기준). 외부 작업자용 책임 · 데이터 흐름 · 런타임 영향 |
| [`docs/plans/PLAN.md`](docs/plans/PLAN.md) | **이력.** 런타임 정렬 R0~R5 순서 · 결정 지점 · 완료 기준 |
| [`docs/handoff/current-state.md`](docs/handoff/current-state.md) | **이력**(2026-08까지). 계층 규칙과 결정의 까닭 |
| [`docs/run-log.md`](docs/run-log.md) | **이력**(VnTool 시절 결정의 정본). 한 일 · 근거 · 되돌리는 법 |
| [`docs/handoff/architecture-decisions.md`](docs/handoff/architecture-decisions.md) | 확정된 설계와 **그 이유** |
| [`docs/runtime-contract.md`](docs/runtime-contract.md) | 유니티 런타임과의 계약 (Gate D) — 이 툴의 몫은 2부(진행 JSON). 1부(연출 · 게임용 Yarn)는 이 툴 밖이다(맡던 compositor는 2026-11-22에 버렸다) |
| [`docs/progression-handoff.md`](docs/progression-handoff.md) | 진행 코어 교환 이력·현행 상대·표본 JSON 안내 |
| [`tests/fixtures/chapter-samples/`](tests/fixtures/chapter-samples/) | 견본 챕터 둘 — `ch05`(복도) · `ch01`(Scene·Auto·조건·스탯·EventKey를 담은 계약 표본). 2026-10-29까지 `docs/*.xlsx`였다 |
| [`docs/work-orders/chapter-graph-orders.md`](docs/work-orders/chapter-graph-orders.md) | 챕터 계층 규격 원본 |
| [`docs/chapter-layer-guide.md`](docs/chapter-layer-guide.md) | 기획자용 사용 안내 — **이력**(엑셀 부분은 걷혔다) |
| [`docs/writer-guide.md`](docs/writer-guide.md) | 작가용 사용 안내 — **이력**(엑셀 · 연출 부분은 걷혔다) |
| [`docs/handoff/io-reference.md`](docs/handoff/io-reference.md) | 입출력의 형식 — **이력**(워크북 입출력은 걷혔다) |
| [`docs/runtime-ui-tooling-principles.md`](docs/runtime-ui-tooling-principles.md) | 도구 설계 원칙 (`원칙 §…` 인용의 출처) |
