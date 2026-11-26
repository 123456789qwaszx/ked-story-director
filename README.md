# ked-story-director

**이야기의 뼈대와 대사를 쓰는 툴.** 비주얼 노벨 저작 도구 — C#/.NET 10 + Avalonia, 한국어 코드베이스. 화면은 한국어 · 영어 두 벌이다.
작가에게 보이는 이름은 **「스토리 디렉터」**(실행 파일 `StoryDirector`)이고, 솔루션 · 프로젝트 · 네임스페이스(`VnTool.sln` · `Vn.App`)는 안의 이름이다.

만들어 내는 것은 **원고(프로젝트 폴더)** 와 유니티 런타임(별도 저장소 `ked-presentation-runtime`)이 먹는 **`exported/{챕터}.progression.json`**,
그리고 작가가 대사만으로 굴러가는지 보는 **대사만 있는 Yarn**(`exported/yarn/` — 장면마다 한 파일, 메모는 주석)이다.
**연출(표정 · 이동 · 카메라 같은 줄 단위 명령 · 연출이 든 게임용 Yarn)은 이 툴에 없다.**

## 두 그래프

탭은 **[대본]**과 **[구성]** 둘이다. 탭 순서가 작업 순서이고 창은 [대본]에서 열린다 — 먼저 쓰고 잇고, 그다음 조건 · 스탯을 붙인다.
코드의 이름은 [대본] = `GraphEditorView`(옛 이름 「연출 그래프」), [구성] = `ChapterGraphView`(「챕터 그래프」)다.

두 탭 모두 왼쪽에 **하이어아키**(프로젝트 → 챕터 → 장면 → 에피소드), 오른쪽에 **탭 셋 — 인스펙터 · 화자 · 조건 및 스탯**이 서고,
두 기둥은 접힌다. [구성]의 하이어아키는 고르기 · 접기만 한다.

| | **[대본]** (대사 · 흐름) | **[구성]** (기획) |
|---|---|---|
| 만드는 것 | 챕터 · 장면 · 에피소드 세우기와 지우기 · 대사 카드 · 카드 안 글 · 선택지 잇기(선 긋기) · 분기 · 이름(챕터 · 장면 · 에피소드 · 화자 · 조건) · 작가용 글 서식 | 챕터 구조의 수치 — ID · 조건 식 · 스탯 · 선택지의 조건 / 스탯변화 · 화자의 캐릭터 키 |
| 값이 사는 곳 | 프로젝트 | 프로젝트 |
| 도구의 역할 | 편집한다 | 편집하고 검증하고 낸다 |

## 원본은 프로젝트 하나

모든 값의 원본은 **프로젝트**입니다(엑셀 워크북 연동은 없습니다).

- **대사 본문·화자** → [대본]의 **대사 카드**(카드마다 대본 하나). 카드 안의 글 칸에서 직접 씁니다.
- **에피소드 구조·간선·조건·스탯** → 챕터(`ChapterDocument`). 에피소드를 세우고 간선을 긋는 곳은 [대본]입니다 — 카드 아래 칸에
  문구를 적어 다른 카드로 끌면 선택지, 맨 위 「자동」 칸을 끌면 자동 길입니다. [구성]에서는 그 ID와 간선의 조건 · 스탯변화를 고칩니다.
  **창구가 둘이지 값이 사는 곳은 하나**입니다.
- 챕터 조건은 그 챕터의 대사 카드 조건 목록에 바로 뜹니다(카드는 조건 이름을 들고, 식은 [구성]에서 고친 그대로 따라갑니다).
- **진행 JSON**은 검증을 통과하면 저절로 나갑니다. 저장은 프로젝트를 씁니다.
- 툴이 만들지 않는 모양의 프로젝트(카드 없는 에피소드 · 장면 없는 에피소드 등)는 **열지 않습니다** — 고쳐 받지 않고 까닭을 알립니다.

**각 값의 주인은 한 곳뿐**이라는 것이 설계 전체를 관통하는 규칙입니다. 같은 사실이 두 곳에
있으면 어긋나고, 어긋남은 최종 출력에서야 드러납니다.

## 저장 구조

```text
MyStory/
├─ project.vnproject.json      매니페스트(형식 5) — 목차 · 챕터 · 화자 · 파일을 넘나드는 관계. 폴더에 하나뿐이어야 한다
├─ story/sf_xxxxxxxx.vnstory.json  판(챕터마다 하나)과 그 위의 카드·노드 — 카드는 제 대본(줄 신원 + 본문)을 든다
├─ runtime.target.json         (있으면) 복제할 폴더 — gameFolder(진행 JSON) · dialogueYarnFolder(대사 Yarn). 저장할 때 exported/를 거기로 복제한다([⚙ 설정]의 「내보내기」가 적는다)
└─ exported/                   툴의 작업 사본 — 고칠 때마다 저절로 나간다
   ├─ ch01.progression.json    런타임 수입물 — 검증을 통과하면 나간다
   └─ yarn/ch01_scene01.yarn   대사만 있는 Yarn — 장면마다 하나, 게임 폴더로는 보내지 않는다
```

**보낼 곳에는 저장(Ctrl+S)할 때만 보냅니다** — 진행 JSON의 보낼 곳이 게임 폴더(런타임의 `ChapterProgression`)이고, 대사 Yarn도 따로
정할 수 있습니다. 이 툴이 보낸 그대로인 지난 파일만 걷습니다.

프로젝트는 형식 5만 엽니다 — 그 앞의 형식 1~4(대본이 따로 서거나 연출을 담은 옛 프로젝트 · 0.3.x까지의 프로젝트)는 열지 않고 알립니다.

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

Windows — `artifacts\StoryDirector-win-x64.zip`(실행 파일 `StoryDirector.exe` · 「먼저 읽어주세요.txt」 · `THIRD-PARTY-NOTICES.txt`):

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-windows.ps1
```

맥 — `artifacts/StoryDirector-osx-arm64.zip`(`StoryDirector.app` · 안내문 · 고지문). 맥에서 돌립니다 — 맥이 없으면 CI의 `macos` 작업이 구워 올려 둔 것을
Actions의 실행 화면 아래 Artifacts에서 받습니다. 인텔 맥은 인자 `x64`:

```bash
./publish-macos.sh
```

둘 다 .NET 런타임을 함께 담으므로 받는 PC에 개발 환경이 필요 없습니다. 서명은 없습니다 — Windows는 파란 경고 창을, 맥은 「그래도 열기」를
처음 한 번 지납니다([`docs/작가에게.txt`](docs/작가에게.txt)). 맥의 서명 · 공증은 하지 않았습니다(Apple 개발자 계정이 듭니다).
판 번호는 `-Version 0.5.0`(맥은 둘째 인자)으로 정하고, GitHub 릴리스로 굽는 길은 [`docs/handover.md`](docs/handover.md) 3절입니다.

`THIRD-PARTY-NOTICES.txt`(저장소 맨 위)는 함께 담기는 오픈소스와 글꼴(Inter)의 저작권 고지 · 라이선스 원문입니다. 패키지 판을 올리면 다시
옮깁니다 — 시험(`ThirdPartyNoticesTests`)이 알립니다([`docs/handover.md`](docs/handover.md) 7절).

아이콘은 원본 `src/Vn.App/Assets/app-icon.png`(1024) 하나에서 굽습니다 — 그림을 바꾸려면 원본을 갈고 다시 굽습니다(Pillow가 있어야 합니다):

```bash
python3 tools/make-app-icons.py
```

## 문서

| 문서 | 무엇이 있나 |
|---|---|
| [`ORIENTATION.md`](ORIENTATION.md) | **처음 보는 사람의 지도.** 프로젝트 · 폴더 · 데이터 흐름 · 지뢰 · 읽는 순서 |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | 코드 구조 — 저장 형식 · 계층 · "이 기능은 어느 파일" |
| [`docs/design/two-graphs.md`](docs/design/two-graphs.md) | **지금 동작의 정본.** [대본] · [구성] 두 탭이 맡는 것 · 사용자 동작 · 다시 쓸 때 지킬 것 |
| [`docs/writer-guide.md`](docs/writer-guide.md) · [`.en`](docs/writer-guide.en.md) | 작가 설명서 — [대본] 탭 |
| [`docs/chapter-layer-guide.md`](docs/chapter-layer-guide.md) · [`.en`](docs/chapter-layer-guide.en.md) | 기획자 · 개발자 안내 — [구성] 탭(조건 식 · 스탯 · 검증 보고 · 진행 JSON) |
| [`docs/작가에게.txt`](docs/작가에게.txt) · [`docs/to-writers.en.txt`](docs/to-writers.en.txt) | 꾸러미의 「먼저 읽어주세요.txt」 · 「Read me first.txt」 — 처음 켜기 · 프로젝트 주고받기 |
| [`docs/handover.md`](docs/handover.md) | 소유자가 할 일 — 작가에게 앱 · 프로젝트 건네기 · 판 내기(릴리스) · 판마다 섞어 쓰지 않기 · 제3자 고지 |
| [`docs/ui-text.md`](docs/ui-text.md) | 화면 문구 — 두 언어 표 고치는 법 · 영어 용어집 |
| [`docs/runtime-contract.md`](docs/runtime-contract.md) | 유니티 런타임과의 계약 — 진행 JSON의 모양과 규칙 |
| [`docs/work-orders/`](docs/work-orders/) | 지금 진행 중인 일의 지시서(끝나면 지운다 — 지난 기록은 git 기록에 있다) |
| [`src/Ked.Progression/UPSTREAM.md`](src/Ked.Progression/UPSTREAM.md) | 진행 코어 사본 — 정본(런타임 저장소)에서 다시 들이는 법 |
| [`tests/fixtures/`](tests/fixtures/) · [`samples/RuntimeSamples/`](samples/RuntimeSamples/) | 시험 고정물(`festival-eve` 프로젝트 · 견본 챕터) · 런타임 견본 프로젝트 |
