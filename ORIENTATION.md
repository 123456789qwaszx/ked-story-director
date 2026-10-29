# ked-story-director — 처음 보는 사람을 위한 지도

> 2026-10-29 다시 씀(레거시 정리 L7 · [`docs/plans/legacy-cleanup.md`](docs/plans/legacy-cleanup.md)). 기준은 `dev`의 L8 뒤다.
> 이 문서는 정본을 **대체하지 않는다.** 지금 동작의 정본은 [`docs/design/two-graphs.md`](docs/design/two-graphs.md)이고,
> 이 문서는 "어떤 순서로 읽고 무엇부터 건드릴지"를 정리한 안내판이다.
>
> ⛔ 2026-09-15에 쓴 첫 판(VNStoryEditor_avalonia 시절 — 엑셀 원본 · `Vn.Core`/`Vn.Cli` · 게임용 Yarn 번들 · 연출 층)은
> 갈아 썼다. 그 시절의 까닭은 [`docs/run-log.md`](docs/run-log.md)와 [`docs/handoff/current-state.md`](docs/handoff/current-state.md)(이력)에 있다.

---

## 1. 한 문장으로

**이야기의 뼈대(챕터 그래프)와 대사(연출 그래프)를 쓰고, 검증해서 유니티 런타임이 먹는 `progression.json`을 내는
Avalonia 데스크톱 도구.** 원고(프로젝트 파일)는 연출 도구 [`ked-story-compositor`](https://github.com/123456789qwaszx/ked-story-compositor)가
읽기만 해서 제 연출과 합쳐 게임용 Yarn을 낸다.

## 2. 왜 이런 모양인가

> **각 값의 주인은 한 곳뿐이다.**
> 같은 사실이 두 곳에 있으면 어긋나고, 어긋남은 최종 출력에서야 드러난다.

원본은 **프로젝트 하나**다(2026-10-29 D2에 엑셀 연동을 걷었다). 창구는 둘이어도 값이 사는 곳은 하나다 — 간선은 챕터
그래프에서도 연출 그래프의 선택지 칸에서도 긋지만 둘 다 `ChapterDocument.Edges`를 고친다.

| 층 | 화면 | 값이 사는 곳 |
|---|---|---|
| 기획 — 에피소드 · 간선 · 조건 · 스탯 | ① 챕터 그래프 | `StoryProject.Chapters`(`ChapterDocument`) |
| 대사 — 카드 · 글 · 분기 · 조건 갈래 | ② 연출 그래프 | `StoryProject.Files`(판 = 챕터 1:1) · `StoryProject.Scripts`(대본) |

## 3. 세 저장소 관계

```
  ked-story-director   ← 이 레포. 원고 · 검증 · progression.json
        ├─ exported/{챕터}.progression.json ──────────→ 유니티 런타임
        └─ 프로젝트 파일(원고, 읽기 전용) ─→ ked-story-compositor ─→ 게임용 Yarn 번들
```

다른 저장소는 **읽어서 맞추되 고치지 않는다.** compositor는 이 툴의 파일을 읽기만 하므로, 그쪽이 읽는 칸
(`ChoiceExits` · 링크 데이터 · `SourceRevision` 등)은 여기서 쓰지 않아도 지우지 않는다.

## 4. 프로젝트 지도

| 프로젝트 | 파일 · 줄 | 역할 | 경계 |
|---|---:|---|---|
| **`Vn.Authoring`** | 79 · 17k | 저작 도메인 · 검증 · 도달성 증명 · 내보내기 | **제품 핵심.** 화면을 모른다. 화면 없이 테스트한다 |
| **`Vn.App`** | 15 · 11k | Avalonia 화면과 OS 연결 | 도메인 규칙을 소유하지 않는다 |
| **`Ked.Progression`** | 29 · 2.5k | 런타임 진행 코어의 **사본** — 낸 JSON을 소비자 규칙으로 다시 실어 보는 오라클 | **고치지 않는다.** `CoreCopyInStepTests`의 지문이 지킨다 |
| `Vn.Authoring.Tests` · `Vn.App.Tests` | 120 · 22k | xUnit · Avalonia 헤드리스 | |

참조 방향: `Ked.Progression → Vn.Authoring → Vn.App`.

### `Vn.Authoring` 폴더의 뜻

| 폴더 | 무엇이 사는가 |
|---|---|
| `Chapters/` | 챕터 층 — `ChapterDocument`(저장 모델) · `ChapterGraphModel`(펼친 모델) · 검사(`ChapterStructureCheck` 등) · 검증(`ChapterValidator`) · 도달성 증명(`ChapterReachabilityProver`) · 내보내기(`ChapterProgressionExporter` · `ChapterExportService`) · 판 공급(`ChapterBoardSupply`) |
| `Model/` | 프로젝트 · 판(`StoryFile`) · 노드(`DialogueNode` · `SetNode`) · 줄 논리(`DialogueLineExtension`) |
| `Script/` | 대본 — **화자 · 대사의 유일한 원본.** 줄의 신원(`LineId`)과 본문을 나눈다. 카드 글 칸의 글을 신원을 지키며 잇는 `ScriptSynchronizer` |
| `Editing/` | **모델을 바꾸는 유일한 통로**(`ProjectEditor`). 되돌리기 · 변경 알림을 함께 진다 |
| `Flow/` | 계산 — 조건 갈래 해석(`ConditionFlowResolver`) · 출구 포트(`NodeConnections`) · 쓸 수 있는 조건(`AvailableConditionResolver`) |
| `Graph/` | 화면에 *무엇을* 그릴지(`GraphProjectionBuilder`) · 선의 쪽(`EdgeSide`) · 선 모양 |
| `Rendering/` | 대사 노드를 평평한 글로 펴기 — 카드 글 칸(`WorkingDialoguePreview.ScenarioText`) |
| `Results/` | 대사 노드를 펼친 불변 결과(`DialoguePublisher.Draft`) — 합성기 · 검증이 읽는다(발행은 compositor로 갔다) |
| `Definition/` | `game.definition.json` — 스탯 후보 · 화자 |
| `Serialization/` | 저장 형식 |

## 5. 데이터가 흐르는 길

```
1. 프로젝트           ChapterDocument (매니페스트의 chapters[])
        ↓                → ToGraphModel(): 조건식을 다시 풀고 모델 검사를 돌린다
2. 펼친 모델          ChapterGraphModel (+ Diagnostics)
        ↓
3. 검증               ChapterValidator + ChapterReachabilityProver (상태공간 완전 탐색)
        ↓
4. 내보내기           ChapterProgressionExporter → JSON
        ↓
5. 재검증(오라클)     Ked.Progression.ProgressionLoader에 실제로 실어 본다
        ↓                → 거부되면 파일을 내지 않고 에피소드 · 간선을 짚어 진단한다
6. exported/{챕터}.progression.json  (검증을 통과하면 저절로 — ChapterExportService.ExportAll)
```

**5번이 이 레포의 성격을 가장 잘 보여 준다.** 검증 규칙을 다시 쓰지 않고 소비자의 실제 코드에 통과시킨다 —
"에디터에서는 통과했는데 게임은 거부"를 구조적으로 막는다.

대사 쪽: 카드 글 칸의 글 → `ProjectEditor.ApplyScenarioText` → `ScenarioTextParser` → `ScriptSynchronizer.Plan`(diff로
`LineId`를 지킨다) → `ApplyScriptSync`.

## 6. 기능을 바꾸려면 어디를 여는가

`ARCHITECTURE.md` §5.1에 긴 빠른-조회 표가 있다. 자주 쓰는 것만:

| 하고 싶은 일 | 여는 곳 |
|---|---|
| 모델을 바꾸는 편집 명령 | `Editing/ProjectEditor*.cs` — **여기에만** |
| 챕터 · 에피소드 · 간선 · 조건 · 스탯 편집 | `Editing/ProjectEditor.Chapters.cs` |
| 화자 · 대사 바꾸기 | `Editing/ProjectEditor.Scripts.cs`(`SetScriptLineText` · `ApplyScenarioText`) |
| if/elseif/endif · 선택 블록의 뜻 | `Flow/ConditionFlowResolver.cs` |
| 그래프에 무엇이 나타나는지 | `Graph/GraphProjectionBuilder.cs` — 화면이 아니라 여기 |
| 챕터 값의 검사 하나 더하기 | `Chapters/ChapterStructureCheck.cs`(값의 흠) · `ChapterValidator.cs`(판과 맞대는 검사) |
| 진행 JSON의 모양 | `Chapters/ChapterProgressionExporter.cs` — 런타임 계약 [`docs/runtime-contract.md`](docs/runtime-contract.md) |
| 챕터 그래프 화면 | `Vn.App/Views/ChapterGraphView.axaml{,.cs}` |
| 연출 그래프 화면 | `Vn.App/Views/GraphEditorView.axaml.cs` · 대사 편집기 `DialogueNodeEditor.axaml.cs` |
| 도구 모음 · 창 | `Vn.App/MainWindow.axaml{,.cs}` |

## 7. 밟으면 조용히 터지는 지뢰

- **개명은 참조를 끌고 간다.** 화자 · 조건 · 스탯 이름은 *이름이 곧 신원*이라 전용 개명 경로(`RenameSpeaker` ·
  `RenameChapterStat` · `ChapterRenamer`)를 쓴다. 스탯 · 조건은 **챕터마다 따로**다 — 고치는 일은 그 챕터의 판만 건드린다.
- **결정적 출력이 계약이다.** 같은 입력의 재출력은 공백 · 순서 · 인코딩까지 바이트가 같아야 한다(서버가 SHA-256으로 챕터
  버전을 식별). 컬렉션 순서를 바꾸는 리팩터링에 주의.
- **간선 순서 = 화면의 이은 줄 차례 = 런타임 `OptionIndex`.** 출시된 선택지 사이에 간선을 끼우면 과거 플레이 이력의 뜻이 바뀐다.
- **진행 스탯을 Yarn 변수로 읽지 않는다.** `stat("키")` 함수로만 읽는다(`YarnSyntax.StatRead`) — 변수면 대사 중에 쓸 수 있게 되어
  도달성 증명 밖의 뒷길이 생긴다.
- **`LineId`는 영구 불변이다.** 화면에는 안 보이지만(E3) compositor가 연출을 거는 열쇠다. 글을 다시 넣을 때 확신이 없으면
  도구가 잇지 않고 멈춘다.
- **솔루션 단위 `dotnet test`는 빌드 실패한 프로젝트를 조용히 건너뛴다.** 요약 줄이 **2개**(`Vn.Authoring.Tests` · `Vn.App.Tests`)인지
  세고 나서 "전부 통과"라고 말할 것.
- **성능 문제는 대부분 "같은 일을 여러 번"이다.** 고친 법은 최적화가 아니라 중복 제거(검증 캐시 · 변경 병합)였다.

## 8. 지금 어디까지 왔나

- 2026-10-29: VnTool에서 갈라져(D0) 엑셀 연동을 걷고(D2) 연출 그래프를 위→아래로 세우고(D3) 카드 안에서 쓰게 했다(D4).
  연출 층은 compositor로 갔다. 그 뒤 화면 다듬기(E · G · H)와 레거시 정리(L0~L8)를 마쳤다 — 걷은 것 · 남긴 것 ·
  찾은 결함은 [`docs/plans/legacy-cleanup.md`](docs/plans/legacy-cleanup.md).
- 다음 일은 정해져 있지 않다. 화면의 동작과 "다시 쓸 때 지킬 것"은 [`docs/design/two-graphs.md`](docs/design/two-graphs.md).

## 9. 빌드하고 돌리는 법

```powershell
dotnet run  --project .\src\Vn.App\Vn.App.csproj     # 앱 실행
dotnet test .\VnTool.sln                              # 전체 검증 (요약 줄 2개 확인!)
powershell -ExecutionPolicy Bypass -File .\publish-windows.ps1   # 작가 배포용 portable
```

- .NET **10**, Avalonia **12**, 중앙 패키지 관리(`Directory.Packages.props`)
- `TreatWarningsAsErrors=true`, `Nullable=enable` — 경고 하나가 빌드를 깬다
- **CI는 `windows-latest`가 정본이다**(`.github/workflows/test.yml` — 빌드 · 테스트 · portable 실행 확인)

## 10. 읽는 순서 (추천)

1. `README.md` — 5분. 무엇을 만드는지
2. [`docs/design/two-graphs.md`](docs/design/two-graphs.md) — **지금 동작의 정본.** 두 그래프의 기능 · 사용자 동작 · 다시 쓸 때 지킬 것
3. `ARCHITECTURE.md` §3(프로젝트 구성) → §5.1(빠른 조회) — 코드 지도
4. 손댈 자리가 정해지면 `ARCHITECTURE.md` §6(깨면 부서지는 규칙)
5. "왜 이렇게 됐지?" — 2026-10-29 뒤의 단계와 결정은 [`docs/plans/`](docs/plans/)(`director-split` · `presentation-removal` ·
   `excel-boundary` · `legacy-cleanup`)와 `two-graphs.md`의 절에, 그 전(VnTool 시절)은 [`docs/run-log.md`](docs/run-log.md)에 있다.
   코드의 ⛔ 주석도 결정의 기록이다 — 무엇이 언제 왜 걷혔는지 그 자리에 남긴다

## 11. 규모 감각

| | 파일 | 줄 |
|---|---:|---:|
| `src/` | 123 | 31k |
| `tests/` | 120 | 22k |

문서 · 테스트 밀도가 높은 레포이고, 그게 이 프로젝트의 작업 방식이다 — 일은 계획 문서의 단계 표 한 줄(커밋 · CI 번호)로
끝나고, 걷은 자리에는 ⛔ 주석이 남는다.
