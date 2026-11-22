# ked-story-director 구조

이 문서는 **코드가 어디 있는지**를 답한다 — 각 조각이 무슨 일을 하는지, 어떤 기능을 손보려면 어느 파일을 여는지,
그리고 깨면 다른 곳이 조용히 부서지는 규칙. 제품 사용 흐름은 [README.md](README.md), 화면의 동작과 "다시 쓸 때 지킬 것"은
[`docs/design/two-graphs.md`](docs/design/two-graphs.md)가 정본이다. 처음이면 [ORIENTATION.md](ORIENTATION.md)부터.

> ⛔ **2026-10-29(레거시 정리 L7)에 다시 썼다.** 그 전 판(VnTool 시절 — 엑셀 워크북 리더/라이터 · 연출 층 · 발행과 결과 저장소 ·
> 런타임 합성 · Yarn 이미터 · `Vn.Core`)은 걷힌 코드 115곳을 가리키고 있었다. 그 시절의 설계 까닭은 지우지 않았다 —
> `git show 82fc34d:ARCHITECTURE.md`로 본다. 무엇을 왜 걷었는지는 [`docs/plans/`](docs/plans/)의 `director-split` ·
> `presentation-removal` · `excel-boundary` · `legacy-cleanup`.

---

## 1. 한눈에

| | |
|---|---|
| **읽는 것** | 프로젝트 하나(`*.vnproject.json` · `story/`). `game.definition.json`은 읽지도 쓰지도 않는다(2026-11-17 · `docs/work-orders/definition-split-orders.md`) |
| **쓰는 것** | 같은 프로젝트(원고), 그리고 `exported/{챕터}.progression.json`(유니티 런타임 수입물 — 검증을 통과하면 저절로)과 함께 나가는 **대사만 있는 Yarn**(`exported/yarn/` — 2026-11-17). ⛔ 장면 파일(`{챕터}.scenes.json`)은 2026-11-18에 걷혔다 — 대사 Yarn과 겹쳤고 읽는 곳이 없었다 |
| **읽어 가는 쪽** | [`ked-story-compositor`](https://github.com/123456789qwaszx/ked-story-compositor)가 원고를 **읽기만** 해서 제 연출과 합쳐 게임용 Yarn을 낸다. 대사 Yarn은 게임 폴더로 보내지 않으므로 그것을 덮지 않는다 |

두 화면이 같은 프로젝트를 고친다.

| | ① 챕터 그래프 (기획) | ② 연출 그래프 (대사 · 흐름) |
|---|---|---|
| 값 | `StoryProject.Chapters` — 에피소드 · 간선 · 조건 · 스탯 · 선택지 사전 · 픽스처 | `StoryProject.Files`(판 = 챕터 1:1) — 카드마다 제 대본(`DialogueNode.Script`) |
| 화면 | `Vn.App/Views/ChapterGraphView.axaml{,.cs}` | `Vn.App/Views/GraphEditorView.axaml.cs` · `DialogueNodeEditor.axaml.cs` |

**각 값의 주인은 한 곳뿐이다.** 창구가 둘이어도 값은 한 곳에 산다 — 간선을 긋고 끊는 곳은 연출 그래프의 선택지 칸이고
(`GraphEditorView` → `AddEdge` · `RemoveEdge` · `SetEdgeRoute`), 챕터 그래프는 그 간선의 조건 · 스탯변화를 고친다(`UpdateEdge`).
둘 다 `ChapterDocument.Edges`를 고친다. 에피소드도 연출 그래프에서 세우고 지운다 — 챕터 그래프는 ID · 조건 식 · 스탯을 고친다.

## 2. 저장 구조와 형식 버전

```text
MyStory/
├─ project.vnproject.json      매니페스트(폴더 안에 하나 — 새 프로젝트는 이 이름, `ProjectFolder`) — 판 목록 · 챕터(chapters[]) · 화자(speakers — 없으면 없다) · [대본] 판 크기(board — 기본이면 없다)
├─ game.definition.json        (있으면) compositor의 연출 어휘. 이 툴은 읽지도 쓰지도 않는다
├─ story/*.vnstory.json        판 — 노드(대사 · 설정). 대사 카드는 제 대본(`script` 칸 — 줄 신원(LineId) · 개정 번호 ·
│                              locale별 화자 · 글)과 그 위의 줄 논리 · 출구 · 선 모양을 든다
│                              목차에서 빠진 판(지운 챕터의 판)은 저장할 때 지운다 — 툴이 지은 이름 `sf_` + ID 8자만, 폴더에 다른 목차가 없을 때만
│                              (`ProjectStore.RemoveStaleStoryFiles` · 2026-11-18)
└─ exported/{챕터}.progression.json  진행 JSON — 런타임 계약
   exported/yarn/{챕터}_{장면}.yarn  대사만 있는 Yarn — 장면 = 파일, 에피소드 = 노드(타이틀 = DialogueEntryId), 장면 메모 · 에피소드
                                     지시는 // 주석. 진행 JSON이 나갈 때 함께 나가고 게임 폴더로는 보내지 않는다
runtime.target.json                  (있으면) 보낼 곳 — 형식마다 exported/ 밖의 폴더(gameFolder = 진행 JSON · compositor도 읽는다,
                                     dialogueYarnFolder = 대사 Yarn — 칸은 이 둘, 모르는 칸은 지킨다). 저장할 때만 exported/를 거기로 비춘다(ExportDelivery · 보낸 기록 exported/.delivered.json)
```

챕터는 장면 항목(`sceneNotes[]` — 장면마다 이름(`name`)과 자유 글(`text`), 열쇠는 `SceneId`)을 든다. [대본] 탭은 이 이름 · 챕터 표시
이름 · 에피소드 제목만 다루고 ID는 [구성] 탭에서 바꾼다(2026-11-16 · [`docs/work-orders/display-names-orders.md`](docs/work-orders/display-names-orders.md)). 장면이 아니라 곁 자료다 — 장면의 구성원은
늘 `Episode.SceneId`에서 계산하고, 에피소드는 늘 장면에 속한다(빈 장면ID는 읽지도 세우지도 않는다 · 2026-11-18). 에피소드마다의 지시는 에피소드의 `memo`다(2026-11-16 · [`docs/work-orders/scene-notes-orders.md`](docs/work-orders/scene-notes-orders.md)).

| 형식 | 상수 | 파일 |
|---|---|---|
| 프로젝트 | `StoryProject.CurrentFormatVersion` = 4 (이 형식만 연다 — §8) | `*.vnproject.json` |
| 판 | `StoryFileJson.CurrentFormatVersion` = 2 | `*.vnstory.json` |
| 되돌리기 스냅샷 | `ProjectSnapshotCodec.CurrentSnapshotVersion` = 3 | (메모리) |

⛔ 형식 3까지는 대본이 `script/*.vnscript.json`에 따로 섰고(`ScriptDocumentJson` 형식 1) 카드는 그 Id를 가리켰다
(2026-10-29 · [`docs/plans/script-merge.md`](docs/plans/script-merge.md)).

진행 JSON의 모양은 런타임 계약이다 — [`docs/runtime-contract.md`](docs/runtime-contract.md).

## 3. 프로젝트 구성

| 프로젝트 | 역할 | 경계 |
|---|---|---|
| `src/Vn.Authoring` | 저작 도메인 · 검증 · 도달성 증명 · 내보내기 | 화면을 모른다. 화면 없이 테스트한다 |
| `src/Vn.App` | Avalonia 화면과 OS 연결 | 도메인 규칙을 소유하지 않는다 |
| `src/Ked.Progression` | 런타임 진행 코어의 **사본** — 낸 JSON을 소비자 규칙으로 다시 실어 보는 오라클 | **고치지 않는다.** `CoreCopyInStepTests`의 지문이 지킨다 |
| `tests/Vn.Authoring.Tests` · `tests/Vn.App.Tests` | xUnit · Avalonia 헤드리스 | `tests/fixtures/chapter-samples/` 견본 챕터 둘 |

참조 방향: `Ked.Progression → Vn.Authoring → Vn.App`.

### 3.1 `Vn.Authoring/Script` — 화자와 대사는 여기에만 있다

| 파일 | 하는 일 |
|---|---|
| `ScriptDocument.cs` | 대본 하나 — 줄(`ScriptLine`: `Id` · `Revision` · `IsRetired`)과 locale별 본문(`ScriptLocale.Entries` → `LocalizedLine(화자, 글)`). `SourceRevision` · `SourceContentHash`는 반영할 때마다 오른다(결과 문서의 개정 번호 — compositor도 읽는다) |
| `ScenarioTextParser.cs` | 카드 글 칸의 글(시나리오 표기 — 화자: 대사, 조건 줄, `#line:` 신원)을 읽는다 |
| `ScriptSynchronizer.cs` | 새 글과 지금 대본을 맞대 **LineId를 잇는 계획**을 세운다(§4.4). 애매하면 멈춘다 |
| `ParsedScript.cs` | 동기화 입력(줄 목록 + 글 해시) |

### 3.2 `Vn.Authoring/Model` — 무엇이 있는가

| 파일 | 하는 일 |
|---|---|
| `StoryProject.cs` · `Speaker.cs` | 프로젝트 — 판(카드와 그 대본) · 챕터 · **화자 목록**(이름 · 캐릭터 키 — 모든 챕터가 쓴다, 2026-11-17 · ⛔ 그 전에는 정의 파일 speakers) · [대본] 판 크기 |
| `StoryFile.cs` | 판 하나 — 노드 목록(순서 = 파일 순서, 그래프 좌표와 별개 — §6-7) |
| `StoryNode.cs` | `DialogueNode`(제 대본 `Script`를 들고, 줄 논리(조건 전환 · 분기점) · 에피소드 표식(`MarkedEpisodeId`) · 분기 카드의 제목(`Title`)을 든다. ⛔ 기본 출구는 2026-11-17에, 갈래 출구(`BranchExits`)는 2026-11-19에 걷혔다) · `SetNode`(조건 공급자 — 카드가 없다) · `ConditionDefinition` |
| `DialogueLineExtension.cs` | 줄에 얹는 대사 논리 — 그 줄 **앞**의 조건 전환(`Transitions`)과 분기 표식(`DetourTargetNodeIds` — 한 줄에 여럿, 연결만 끊은 분기점은 `OpenDetourPositions`). 화자 · 글은 없다. ⛔ 대사 안 선택 전환은 2026-11-04에 걷혔다 — 선택지는 간선에서만 |
| `EdgeShape.cs` | 사람이 고친 선 모양(꼴 · 굽이 — 자동 자리에서 옮긴 양) |
| `Identifier.cs` | 식별자 형식 |

### 3.3 `Vn.Authoring/Chapters` — 챕터 층

| 파일 | 하는 일 |
|---|---|
| `ChapterDocument.cs` | **저장 모델**(매니페스트의 `chapters[]`). `ToGraphModel()`이 조건식을 다시 풀고 모델 검사를 돌려 펼친다 |
| `ChapterGraphModel.cs` | **펼친 모델** — `ChapterEpisode` · `ChapterEdge`(`Where` = 사람이 읽는 자리) · `ChapterCondition` · `ChapterStat` · `ChapterChoiceOption` · `ChapterFixture` · `Diagnostics` · `EpisodeHasError` |
| `ChapterDiagnostic.cs` | 진단 하나 — 심각도 · 코드 · 자리(`Sheet`) · 대상 에피소드(`EpisodeId`) · 메시지 |
| `ChapterStructureCheck.cs` · `ChapterAutoEdgeCheck.cs` · `ChapterSceneEntryCheck.cs` | **모델 검사** — 값의 흠(빈 · 겹친 Id, 끝점, 관문 라벨, 문구, 스탯 어휘, 자동 길 규칙, 장면 진입점) |
| `ChapterValidator.cs` | 모델 검사 + **판과 맞대는 검사**(재생할 줄이 있나) + 도달성 증명을 한 벌로 |
| `ChapterReachabilityProver.cs` | (에피소드, 스탯 벡터) 상태공간을 끝까지 걸어 "닿을 수 있는가"를 증명. 도착 스탯 폭도 걷는 김에 적는다 |
| `ChapterProgressionExporter.cs` | 진행 JSON. 낸 것을 `Ked.Progression.ProgressionLoader`에 다시 실어 보고 거부 사유를 에피소드 · 간선으로 옮긴다(`CoreRefusals`) |
| `ChapterExportService.cs` | 검증 캐시(지문 = 챕터 모델 + 판의 노드 이름) · 전 챕터 자동 내보내기(`exported/`만) · 거부 장부 |
| `ExportDelivery.cs` | 저장할 때 `exported/`를 형식마다 보낼 곳으로 비춘다 — 보낸 기록으로 이 툴이 보낸 그대로인 지난 파일만 걷는다 |
| `ChapterExportBytes.cs` · `ChapterReleaseBaseline.cs` | 바이트 계약(서버가 SHA-256으로 버전을 식별) · 출시 기준선과 선택지 배열 비교 |
| `ChapterBoard.cs` · `EpisodeNaming.cs` · `ChapterSpine.cs` | 에피소드 ↔ 판의 카드를 잇는 규약(그 챕터의 판에서만 찾는다) · 착지 카드 판정 |
| `ChapterBoardSupply.cs` · `ConditionYarnTranslator.cs` | 챕터 조건을 판의 공급 노드(`챕터 {Id} 조건`)로 번역해 나른다 — 대사 카드의 조건 목록에 뜨는 길 |
| `BranchEntryCondition.cs` · `ChapterBranchMarkers.cs` | 분기 에피소드의 첫머리 조건 · 분기 표식 모으기 |
| `ConditionExpressionParser.cs` · `StatDeltaParser.cs` | 조건식 · 스탯변화 문법(스탯키 갈아 끼우기도 여기) |
| `ChapterRenamer.cs` · `EpisodeRenamer.cs` · `ChapterDeleter.cs` · `EpisodeDeleter.cs` | 이름이 신원인 것들의 개명 · 삭제 — 참조를 함께 옮긴다 |
| `ChapterBranchPlanner.cs` · `ChapterSceneGrouping.cs` · `ChapterFixtureWalker.cs` · `ChapterGateJudge.cs` | 챕터 그래프 배치 · 장면 묶기 · 픽스처 경로 · 관문 판정 |

### 3.4 `Vn.Authoring/Flow` — 무엇을 계산하는가

| 파일 | 하는 일 |
|---|---|
| `ConditionFlowResolver.cs` | **조건 모델 그 자체.** 줄의 전환만 보고 "지금 어느 갈래 안인가"를 앞에서부터 계산한다(§4.1) |
| `DialogueFlow.cs` | 그 결과 — 갈래 · 줄 · 문제(`FlowProblemKind`) |
| `DialogueScriptResolver.cs` | 대본 줄 + 줄 논리를 합친 읽기 전용 투영(고아 줄 포함) |
| `BranchAnchor.cs` | 조건 갈래의 신원을 짓는 유일한 자리 |
| `NodeConnections.cs` | 실행 출구 포트(`PortsOf`)와 간선 문구 |
| `AvailableConditionResolver.cs` · `ConnectedSetNodeResolver.cs` | 쓸 수 있는 조건 — 그 판의 챕터 조건(공급 노드의 사본) |

### 3.5 `Vn.Authoring/Graph` — 그래프에 무엇을 그릴지

| 파일 | 하는 일 |
|---|---|
| `GraphProjectionBuilder.cs` | 프로젝트 → `GraphProjection`. **화면이 아니라 여기가** 무엇이 보이는지 정한다 — 설정노드는 그리지 않는다(`IsHiddenPlumbing`), 접힌 판은 장면으로 묶는다 |
| `EdgeSide.cs` | 선이 나가는 쪽 — 도착 카드가 출발 카드보다 왼쪽이면 왼쪽(H1) |
| `OrthogonalEdgeRouter.cs` · `EdgeShapeRouter.cs` | 자동 직각 경로 · 사람이 고친 모양(직각 · 곡선 · 직선)대로 긋기 |
| `NodePlacement.cs` | 새 카드가 설 자리(위→아래, D3) |

### 3.6 `Vn.Authoring/Editing` — 어떻게 바꾸는가

`ProjectEditor`가 **모델을 바꾸는 유일한 통로**다. 모든 편집은 `Mutate(kind, action)`을 지나 되돌리기 스냅샷
(`ProjectSnapshotCodec`)을 쌓고 `Changed`를 한 번 울린다. 편집 묶음(`Batch`)은 알림을 끝에 한 번 낸다.

| 파일 | 맡는 것 |
|---|---|
| `ProjectEditor.cs` | 판 · 노드 · 조건 · 출구(`SetExitTarget`) · 줄 전환 · 선 모양 · 되돌리기 |
| `ProjectEditor.Scripts.cs` | 대본 줄(`InsertScriptLine` · `RetireScriptLine` · `MoveScriptLine` · `SetScriptLineText`) · 카드 글 반영(`ApplyScenarioText` — 화자 가르기는 프로젝트의 화자 목록을 본다) |
| `ProjectEditor.Speakers.cs` | 화자 목록(`AddSpeaker` · `RemoveSpeaker` · `SetSpeakerCharacterId`) · 화자 개명(`RenameSpeaker` — 목록과 이미 쓴 줄을 한 칸에) |
| `ProjectEditor.Chapters.cs` | 챕터 · 에피소드(장면을 꼭 받는다 — 카드와 한 칸에 서고 함께 지워진다) · 간선(`AddEdge(before:)` — 줄 자리에 끼운다) · 조건 · 스탯(개명 · 삭제 — 그 챕터의 공급 노드만) |
| `ProjectEditor.WriterMarks.cs` | 작가용 글 서식(`ApplyWriterStyle` · `SetWriterAlign`) — 대사 글 밖(`DialogueNode.WriterMarks`)에 서고 글이 바뀌면 같은 글자를 따라간다(`ApplyScriptSync` · `SetScriptLineText` 안에서). **내보내기가 읽지 않는다**(`docs/plans/writer-formatting.md`) |
| `ProjectChangedEventArgs.cs` | 변경의 종류(`ProjectChangeKind`) — 화면이 무엇을 다시 그릴지(`Vn.App/Services/ProjectRefreshPlanner`)의 근거 |

### 3.7 `Vn.Authoring/Rendering` · `Results` — 평평한 글로 펴기

| 파일 | 하는 일 |
|---|---|
| `Results/DialoguePublisher.cs` | 대사 노드를 펼친 불변 초안(`Draft`) — 줄마다 그 순간의 화자 · 글 · 얼린 조건식 · 출구. 문제(`PublishProblem`)를 함께 낸다 |
| `Rendering/ResultDocumentComposer.cs` | 그 초안을 의미 조각(`RenderedSegment`)으로 — 프리셋 둘(`RuntimeFull` · `ScenarioOnly`, `DocumentOutputOptions`) |
| `Rendering/WorkingDialoguePreview.cs` · `DocumentPreviewFormatter.cs` | **카드 글 칸의 글**(`ScenarioText`) — 넣을 때 `ScenarioTextParser`가 같은 문법으로 되읽는다 |
| `Rendering/YarnSyntax.cs` · `YarnNaming.cs` | 조건 줄 표기 · 스탯 읽기(`stat("키")`) · 노드 이름 규칙 · 대사 Yarn의 들여쓰기 · `<<detour>>` · 작가 글 이스케이프(`EscapeLineText`) |

이름의 「발행」 · 「결과」는 남은 이름이다 — 발행(고정본 · 결과 저장소)은 compositor로 갔다.

### 3.8 `Vn.Authoring/Serialization`

| 파일 | 하는 일 |
|---|---|
| `ProjectStore.cs` | 파일 배치 · 원자적 저장 · 목차에서 빠진 판 파일 걷기 |
| `ProjectFolder.cs` | 프로젝트는 폴더다 — 폴더 안의 유일한 `*.vnproject.json`을 찾고, 새 프로젝트는 `project.vnproject.json`으로 깐다 |
| `ExportDestinations.cs` | 보낼 곳(`runtime.target.json`) — 형식마다 폴더, 모르는 칸은 지킨다 |
| `ProjectManifestJson.cs` · `ChapterDocumentJson.cs` | 매니페스트 · 챕터 |
| `StoryFileJson.cs` · `StoryNodeJson.cs` · `EdgeShapeJson.cs` · `DetourJson.cs` | 판 · **노드 직렬화는 여기 하나뿐** · 선 모양 · 분기 표식(`detour` — 하나면 글, 여럿이면 배열) |
| `ScriptDocumentJson.cs` | 대본 |
| `ProjectSnapshotCodec.cs` · `JsonSupport.cs` | 되돌리기 · 저장 여부 비교 · 공통 |

### 3.9 `Vn.App` — 화면

| 파일 | 하는 일 |
|---|---|
셸 `MainWindow`는 맡은 일별 partial이다(`MainWindow.*.cs`). 두 탭 모두 왼쪽 기둥은 하이어아키(`StoryHierarchy`), 오른쪽 기둥은
탭 셋(인스펙터 · 화자 · 조건 및 스탯 — `SideTabs`)이고 두 기둥은 접힌다(`ColumnDock`).

| 파일 | 하는 일 |
|---|---|
| `Program.cs` · `App.axaml{,.cs}` | 시작 — 시작 예외를 로그로 남기고, 주 창을 못 만들면 까닭을 보이는 창을 띄운다 |
| `MainWindow.axaml{,.cs}` | 셸 — 위 띠(File 메뉴 · [📂 작업 폴더] · [⚙ 설정]) · 탭 둘([구성] · [대본]) · 상태줄 · 창 제목, 새로 만들기 · 열기(폴더) · 저장 · 10분 자동 저장 · 창 키(Ctrl+Z/Y · Delete) · 챕터 세우기(`CreateChapterNow`) · 챕터 지우기 확인 |
| `MainWindow.Hierarchy.cs` | 두 탭의 하이어아키를 한 원천에서 채우고 선택을 함께 움직인다 · [대본] 인스펙터(카드 → 대사 편집기, 프로젝트 · 챕터 · 장면 줄 → 요약)와 탭 고르기 · 「조건 및 스탯」 탭 맨 위의 챕터 이름 칸 |
| `MainWindow.HierarchyEdit.cs` | 하이어아키에서 고치기 — 우클릭 차림표(챕터 · 장면 · 에피소드 추가, 에피소드 지우기) · 두 번 눌러 이름 바꾸기 · 끌어 다른 장면으로 · [대본] [삭제]의 챕터 · 장면 지우기([구성] 하이어아키는 읽기 전용) |
| `MainWindow.Speakers.cs` | [대본] 「화자」 탭 — 화자 이름 더하기 · 고치기 · 지우기(캐릭터 키는 [구성] 「화자」 탭) |
| `MainWindow.Conditions.cs` | [대본] 「조건 및 스탯」 탭의 조건 이름 — 더하기 · 고치기 · 지우기(식은 [구성]) |
| `MainWindow.SceneNotes.cs` | [대본] 장면 메모 공간 — 장면 메모 · 에피소드 지시 카드, 챕터의 주인 없는 메모 |
| `MainWindow.LineInspector.cs` | [대본] 인스펙터의 선 설명 — 선택지 문구(자동 길 ↔ 선택지)는 여기서, 조건 · 스탯변화는 [구성]으로 데려간다 |
| `Views/ChapterGraphView.axaml{,.cs}` | [구성] — 챕터 그래프 판 · 툴바(챕터 콤보 · [자동 정렬] · [화면 맞춤] · 「빈 선택지 숨기기」 · 「ID로 보기」) · 오른쪽 탭 셋(인스펙터의 ID 칸 · 간선 조건/스탯변화, 「화자」의 캐릭터 키, 「조건 및 스탯」의 식 · 스탯) · 검증 보고 |
| `Views/GraphEditorView.axaml{,.cs}` | [대본] — 연출 그래프 판(카드 · 글 칸 · 포트 · 선택지 줄 · 선 고치기 · 판 우클릭 · 챕터 · 장면 이름표 · 미니맵) · 툴바(챕터 콤보 · [＋ 에피소드] · [삭제]) · 챕터가 없을 때의 [＋ 첫 챕터 만들기] |
| `Views/DialogueNodeEditor.axaml{,.cs}` | [대본] 인스펙터의 대사 편집기(줄 표 · 조건 · 분기) |
| `Views/StoryHierarchy.axaml{,.cs}` | 하이어아키 한 그루(프로젝트 → 챕터 → 장면 → 에피소드 · 분기 카드 줄) — 고르기 · 접기 · ↑↓←→. 고치는 일은 셸에 알리기만 한다 |
| `Views/SideTab.cs` · `ColumnDock.cs` | 오른쪽 기둥의 탭 셋(차례 · 이름) · 기둥 접기(하이어아키 · 오른쪽 기둥) |
| `Views/SettingsWindow.cs` · `Shortcuts.cs` | [⚙ 설정] 창(이 프로젝트 · 내보내기 · 단축키 · 기타) · 실제로 듣는 단축키 표 한 벌 |
| `Views/PlatformKeys.cs` · `BoardWheel.cs` | OS마다 갈리는 키 한 벌(맥은 ⌘를 Ctrl처럼 · ⌘⇧Z · ⌫) · 두 판 위의 휠이 하는 일 한 벌(배율은 신호의 크기에 비례 · 설정 「휠로 판 옮기기」 · 트랙패드 집기) |
| `Services/AppIdentity.cs` · `Assets/` | 보이는 이름(「스토리 디렉터」)과 설정 폴더 이름(`VnTool` — 바꾸지 않는다) · 아이콘(원본 `app-icon.png` → `tools/make-app-icons.py` → `app.ico` · `app.icns`) |
| `Views/EpisodeAdder.cs` | 에피소드 하나 더하기 — 장면에 길 없이(하이어아키 「에피소드 추가」 · 「장면 추가」) |
| `Views/BranchConditionPicker.cs` · `BranchConditionNote.cs` | [대본] 분기 카드의 조건 칸 · 분기 조건을 말하는 글(두 탭이 같은 말) |
| `Views/StatChangeEditor.cs` · `ConfirmFlyout.cs` · `BranchPalette.cs` | 스탯변화 칸 · 확인 칸 · 갈래 색 |
| `Views/RubberBand.cs` · `EscToRevert.cs` · `GuideLine.cs` | 범위 선택(두 판) · 글 칸의 Esc = 넣기 전 값 · 안내글 한 줄과 설명 간소화 스위치(`GuideMode`) |
| `Services/AuthoringSession.cs` | 열린 프로젝트 · 경로 · 저장 여부 · 선택 · 화자 창구 · 보낼 곳 · 화면 편의 상태 · 상태줄 |
| `Services/ProjectRefreshPlanner.cs` | 변경 종류 → 그래프 · 곁기둥 중 무엇을 다시 만들지 |
| `Services/WorkFolder.cs` | 작업 폴더(`exported/`)를 찾고, 그 형식의 파일을 고른 채 탐색기를 연다 |
| `Services/AppSettingsService.cs` · `StartupLog.cs` · `UiGuard.cs` | 최근 프로젝트 · 화면 편의(기둥 접기 · 「빈 선택지 숨기기」 · 「ID로 보기」 · 미니맵 · 설명 간소화(`Views/GuideLine.cs`) · 휠로 판 옮기기(`Views/BoardWheel.cs`) — PC별) · 시작 오류 로그 · 이벤트 핸들러의 예외를 붙잡아 알린다(크래시 금지) |

---

## 4. 핵심 설계 판단

### 4.0 소유권

| 질문 | 답 |
|---|---|
| 화자와 대사의 원본은 | `ScriptDocument`의 `ScriptLocale.Entries`. 바꾸는 코드는 `ProjectEditor.Scripts.cs`에만 있다 |
| LineId는 글을 고친 뒤 어떻게 유지되나 | `ScriptSynchronizer`가 잇는다. 애매하면 멈춘다(§4.4) |
| 에피소드 구조 · 간선 · 조건 · 스탯의 원본은 | `ChapterDocument`. 두 화면이 같은 `ProjectEditor.Chapters` 명령을 부른다 |
| 챕터 조건이 대사 카드에 뜨는 길은 | 판의 공급 노드(`ChapterBoardSupply`) — 조건 편집 명령 안과 판이 서는 순간에 번역해 나른다 |
| 런타임이 무엇을 받나 | 검증과 코어 재적재를 통과한 진행 JSON만(§4.5) |
| 이전 프로젝트는 | 형식 4 이전(1~3)은 열지 않는다(§8) |

### 4.1 조건은 반복 저장하지 않고 전환만 저장한다

줄마다 "지금 어떤 조건 안인지"를 적으면 같은 사실이 여러 줄에 흩어진다. 한 줄만 고쳤을 때 서로 어긋나고, 그 어긋남을 발견할
방법이 없다.

```
Line 0                                  Line 0
Line 1  BeginIf(호감)          if 호감 { Line 1
Line 2                                    Line 2 }
Line 3  BeginElseIf(신뢰)   elseif 신뢰 { Line 3
Line 4                                    Line 4 }
Line 5  EndIf                           Line 5
```

- `BeginIf` 줄은 자기가 연 갈래 **안**에 있고, `EndIf` 줄은 이미 **바깥**이다. `BeginElseIf`는 같은 체인의 형제다.
- 한 줄 앞에 전환이 여럿일 수 있다(`Transitions` — 겹쳐 닫기 · 연달아 열기). 대사 없는 블록이 대본 끝이면 노드의
  `TrailingTransitions`가 닫는다.
- 조건 안 조건(중첩)은 문제로 알린다. ⛔ 대사 안 선택 블록(조건 안 선택지 W54 · 결합 종료 W55 포함)은 2026-11-04에 걷혔다 — 선택지는 챕터 간선(에피소드 끝)에서만 선다. 옛 파일의 `beginChoice` 등은 모르는 전환 종류라 깨진 파일로 거절한다(2026-11-17 · §8 — ⛔ 그 전에는 알아보고 버리며 상태줄에 알렸다).

### 4.2 실행 출구는 분기점 하나다

카드에서 나가는 실행 선은 **분기점**(줄 앞에서 분기 카드로 `<<detour>>` — `DialogueLineExtension.DetourTargetNodeIds`)뿐이다. 분기점은
에피소드 카드에만 서고 분기 카드에만 잇는다(`ProjectEditor.CanBranchFrom` · `CanBranchTo`).

⛔ **조건 갈래 출구**(갈래를 여는 줄에 매달아 갈래 끝에서 들르던 카드 — `DialogueNode.BranchExits` · 판 파일 전환의 `exit` · 꼬리 전환의
`anchor`)는 2026-11-19에 걷혔다(`docs/work-orders/usability-orders.md` UX28). 대상을 가리지 않아 아무도 부르지 않는 분기 카드에 이으면 내보낸
Yarn이 없는 노드를 불러 게임이 멈췄고, 에피소드에 이으면 그 에피소드가 진행 기록 밖에서 재생됐다. 옛 칸은 모르는 칸으로 지나가 다음
저장에서 빠진다. 갈래의 신원(`Flow.BranchAnchor`)은 흐름 해석이 갈래를 가르는 데 그대로 쓴다.

### 4.3 그래프 포트는 저장하지 않고 계산한다

포트와 간선은 조건 전환에서 나온다. 대사 편집기에서 갈래를 더하면 그래프에 포트가 늘고, 그래프에서 포트를 이으면 편집기의
출구 표시가 바뀐다 — **두 화면 사이에 동기화 코드가 한 줄도 없다.** 모든 출구 변경은 `ProjectEditor.SetExitTarget`을 지난다.

### 4.4 글을 다시 넣을 때 확신할 수 없으면 멈춘다

`ScriptSynchronizer`의 절차다.

```
0. 입력이 신원(#line:)을 밝혔으면 그것이 닻이다
1. 양쪽에서 정확히 한 번씩만 나타나는 문장 → 닻
2. 닻 중 순서가 유지되는 최장 부분열 → Unchanged, 나머지 닻 → Moved
3. 순서가 유지된 닻 사이 구간마다:
     같은 문장끼리 앞에서부터 짝짓기        → Unchanged
     양쪽에 하나씩만 남음                   → Modified (LineId 유지, Revision +1)
     한쪽이 빔                              → Inserted / Deleted
     양쪽에 서로 다른 문장이 둘 이상 남음   → Ambiguous
```

**`Ambiguous`가 하나라도 있으면 계획 전체를 적용하지 않는다.** 도구가 대신 고르는 순간 compositor가 그 LineId에 건 연출이 다른
대사에 붙는다. 은퇴한 줄은 후보에서 빠진다 — 사라졌던 문장이 다시 나타나면 새 Id다. 바꿀 것이 없으면 손대지 않는다
(`ChangesNothing` — 되돌리기 스냅샷 · 화면 재구성을 아낀다).

### 4.5 런타임이 거부할 것은 내보내지 않는다

검증 규칙을 직접 다시 쓰지 않고 **소비자의 실제 코드**에 통과시킨다. 내보내기는 ① 모델 검사 + 판과 맞대는 검사 + 도달성 증명을
통과해야 하고, ② 낸 JSON을 `Ked.Progression.ProgressionLoader`(런타임 사본)에 다시 실어 거부되지 않아야 파일을 쓴다. 거부되면
낡은 파일을 덮지 않고 그 사유를 에피소드 · 간선으로 옮겨 검증 보고에 세운다. "에디터에서는 통과했는데 게임은 거부"를 구조로 막는다.

### 4.6 챕터 = 판 1:1, 범위는 챕터다

EpisodeId는 챕터 안에서만 유일하다. 그래서 에피소드의 카드 · 공급 노드 · 스탯 · 조건은 **그 챕터의 판에서만** 찾는다
(`EpisodeNaming.BoardOf` · `ChapterBoard.For` · `SupplyNodesOf`). 프로젝트 전체를 훑으면 다른 챕터의 같은 이름을 집는다 — 이 저장소가
여러 번 다친 자리다(C1 · L4b).

---

## 5. 기능을 바꾸려면 어디를 여는가

### 5.1 빠른 조회

**대본과 LineId**

| 하고 싶은 일 | 여는 곳 |
|---|---|
| 화자 · 대사를 바꾸는 명령 | `Editing/ProjectEditor.Scripts.cs`의 `SetScriptLineText` — **여기 하나뿐** |
| 카드 글 칸의 문법 | `Script/ScenarioTextParser.cs` ↔ `Rendering/DocumentPreviewFormatter.cs`(낼 때와 읽을 때가 같은 문법이어야 한다) |
| 글을 다시 넣을 때의 매칭 | `Script/ScriptSynchronizer.cs` — **애매하면 멈춘다는 규칙을 깨지 말 것** |
| 대본에 항목 추가 | `Script/ScriptDocument.cs` → `Serialization/ScriptDocumentJson.cs` |
| 화자 개명 | `ProjectEditor.RenameSpeaker` — locale마다 옛 이름과 글자가 같은 것만, `Revision`은 안 올린다 |
| 대본과 노드를 합치는 규칙 | `Flow/DialogueScriptResolver.cs` |

**조건과 갈래**

| 하고 싶은 일 | 여는 곳 |
|---|---|
| if/elseif/endif의 뜻 | `Flow/ConditionFlowResolver.cs` ← **이 파일이 조건 모델 그 자체다** |
| 전환 종류 추가 | `Model/DialogueLineExtension.cs`의 `ConditionTransitionKind` → 위 해석자 |
| 쓸 수 있는 조건 | `Flow/AvailableConditionResolver.cs` |
| 갈래 신원 | `Flow/BranchAnchor.cs` |
| 갈래 색 | 자리는 `Flow/DialogueFlow.cs`의 `PaletteIndex`, 색은 `App/Views/BranchPalette.cs` |
| 잘못된 구조를 알리는 방식 | `Flow/DialogueFlow.cs`의 `FlowProblemKind` |

**출구 · 연결 · 그래프**

| 하고 싶은 일 | 여는 곳 |
|---|---|
| 출구를 어디에 저장하나 | 줄 확장의 분기점(`Model/DialogueLineExtension.cs`) |
| 포트를 만드는 규칙 | `Flow/NodeConnections.cs`의 `PortsOf` |
| 연결 · 해제 | `ProjectEditor.SetExitTarget` |
| 그래프에 무엇이 나타나는지 | `Graph/GraphProjectionBuilder.cs`(`IsHiddenPlumbing`) |
| 선의 쪽 · 경로 · 사람이 고친 모양 | `Graph/EdgeSide.cs` · `OrthogonalEdgeRouter.cs` · `EdgeShapeRouter.cs` |
| 포트를 끌어 잇기 · 선택지 줄 · 선 고치기 | `App/Views/GraphEditorView.axaml.cs` |
| 새 카드가 설 자리 | `Graph/NodePlacement.cs` |

**챕터**

| 하고 싶은 일 | 여는 곳 |
|---|---|
| 챕터 · 에피소드 · 간선 · 조건 · 스탯 편집 명령 | `Editing/ProjectEditor.Chapters.cs` |
| 값의 검사 하나 더하기 | `Chapters/ChapterStructureCheck.cs`(값의 흠 — 툴이 만든 값도 거기서 본다). 판과 맞대야 하면 `ChapterValidator.cs` |
| 도달성 | `Chapters/ChapterReachabilityProver.cs` |
| 런타임에 나가는 필드 | `Chapters/ChapterProgressionExporter.cs` + [`docs/runtime-contract.md`](docs/runtime-contract.md) |
| 진단이 짚는 자리 · 문장 | `Chapters/ChapterDiagnostic.cs`(에피소드는 `EpisodeId`, 간선은 메시지의 `ChapterEdge.Where`) |
| 챕터 조건 → 대사 카드 | `Chapters/ChapterBoardSupply.cs`(사본은 **이름**으로 짝짓는다, 식 미정은 `true`) · `ConditionYarnTranslator.cs`, 분기에 거는 칸 `App/Views/BranchConditionPicker.cs` |
| 이름 바꾸기 · 지우기 | `ChapterRenamer` · `EpisodeRenamer` · `ChapterDeleter` · `EpisodeDeleter` · `ProjectEditor.RenameChapterStat` · `RenameChapterCondition`(간선이 따라간다, 공급 사본은 Id 보존) |
| 화자 목록(이름 · 캐릭터 키) | 규칙 `App/Services/AuthoringSession.cs`의 `AddSpeakerName` · `RenameSpeakerName` · `RemoveSpeakerName` · `SetSpeakerCharacterId`(쓰면 `SpeakersChanged`), 화면 이름 `App/MainWindow.Speakers.cs`([대본] 「화자」 탭) · 키 `ChapterGraphView` 「화자」 탭([구성]) — 두 탭의 오른쪽 기둥은 탭 셋 `SideTabs`(`Views/SideTab.cs` · 2026-11-18) |
| 조건 이름 · 식 미정 | 이름 화면 `App/MainWindow.Conditions.cs`([대본] 「조건 및 스탯」 탭 — 지금 챕터 = 활성 판), 식 · [＋ 조건](이름 + 식)은 `ChapterGraphView` 「조건 및 스탯」 탭의 조건 표([구성]). 빈 식 = 항상 통과 `ChapterDocument.Reparse`, 진단 `ConditionExpressionBlank`(경고) |
| 장면 메모 · 에피소드 지시 | 규칙(주인 · 차례 · 주인 없는 메모 경고) `Chapters/ChapterSceneNotes.cs`, 편집 `ProjectEditor.SetSceneNote` · `UpdateEpisodeMemo` · `RenameScene`(메모를 데려간다), 화면 `App/MainWindow.SceneNotes.cs` |
| 보이는 이름 · 새 ID | 챕터 `ChapterDocument.ShownName` · 장면 `ChapterSceneGrouping`(`ChapterScene.DisplayName`) · 에피소드 `ShownTitle`, 새 ID와 처음 이름 `Chapters/NewNames.cs` |
| 분기 카드(집 · 검증) | `Chapters/ChapterBranchCards.cs` — 분기 카드(`DialogueNode.IsBranchCard`)는 에피소드가 아니라 증명(`ChapterReachabilityProver`, 간선만 — 런타임의 오라클) 밖이다. `HomesOf` = 분기 카드의 집(먼저 부르는 에피소드 — 장면 · Yarn 파일 · [구성] 칩이 같은 규칙), `Verify` = 분기점이 에피소드에(오류) · 아무도 안 부름 · 도달 불가에서만 불림(경고) · 줄 없음(오류) · Yarn 노드 이름 겹침(오류). 부르는 곳 `ChapterValidator.Validate`(프로젝트를 줄 때) |
| [대본] 판 크기 · [⚙ 설정] | 값 `Model/BoardSize.cs`(범위 · 카드 차지 자리), 줄이기 `Graph/BoardResize.cs`(전체 비율 · 비키기 · 꺾임), 명령 `ProjectEditor.SetBoardSize`, 저장 `ProjectManifestJson.WriteBoard/ReadBoard`(스냅숏도 같은 것), 판 `GraphEditorView.ApplyBoardSize`, 창 `App/Views/SettingsWindow.cs`(묶음: 이 프로젝트 · 내보내기 · 단축키 — 표 `Views/Shortcuts.cs` · 기타 — 미니맵 감추기 · 설명 간소화 · 휠로 판 옮기기) |
| 작가용 글 서식 | 모델 · 셈 `Model/WriterMarks.cs`, 저장 `Serialization/WriterMarksJson.cs`(대사 노드의 `writerMarks` · `writerAlign`), 명령 `ProjectEditor.WriterMarks.cs`, 알림 `ProjectChangeKind.WriterMarks`, 글 칸 `App/Views/WriterTextBox.cs`(TextBox 그대로 — 글자 배치만), 도구 막대 `App/Views/GraphEditorView.Writer.cs`. 내보내기에 나가지 않는다 — `docs/plans/writer-formatting.md` |
| 대사 Yarn(`exported/yarn/`) | 글 짓기 `Chapters/ChapterYarnExporter.cs`(타이틀은 `ChapterProgressionExporter.DialogueEntryIdOf`), 쓰기 · 낡은 파일 걷기 `ChapterExportService.ExportAll`, 컴파일 확인은 시험 `ChapterYarnExportTests`(앱은 컴파일러를 싣지 않는다) |

**편집 · 저장 · 화면**

| 하고 싶은 일 | 여는 곳 |
|---|---|
| 새 편집 명령 | `Editing/ProjectEditor*.cs` — **모델을 바꾸는 코드는 전부 여기에만** |
| 그 변경이 어느 화면을 다시 만들지 | `Editing/ProjectChangedEventArgs.cs`의 `ProjectChangeKind` → `App/Services/ProjectRefreshPlanner.cs` |
| 노드에 저장 칸 추가 | `Serialization/StoryNodeJson.cs` — 노드 직렬화는 여기 하나뿐 |
| 프로젝트 · 챕터 저장 칸 | `Serialization/ProjectManifestJson.cs` · `ChapterDocumentJson.cs` |
| 파일 배치 · 열지 말지 | `Serialization/ProjectStore.cs` · `JsonSupport.ValidateProject` |
| 형식 버전 올리기 | `Model/StoryProject.cs`의 `CurrentFormatVersion` + 위 읽기 경로들 |
| 화자 | `Model/StoryProject.cs`(`Speakers`) · 명령은 `Editing/ProjectEditor.Speakers.cs` |
| 도구 모음 · 창 | `App/MainWindow.axaml{,.cs}` |
| 열린 프로젝트 · 저장 여부 · 선택 | `App/Services/AuthoringSession.cs` |

### 5.2 여러 파일에 걸치는 작업의 순서

**줄에 대사 논리 칸 하나 더하기** — `Model/DialogueLineExtension.cs`(칸) → `Serialization/StoryNodeJson.cs`(읽기 · 쓰기, 빈 값은 안 쓴다)
→ `Flow/ConditionFlowResolver.cs` 또는 `DialogueScriptResolver.cs`(계산이 보게) → `Results/DialoguePublisher.cs`(얼려 싣기) →
`Rendering/ResultDocumentComposer.cs`(글로 펴기) → 편집 명령(`ProjectEditor`) → 화면. 은퇴 줄 정리(`PruneLineExtensions`)가 빈 확장을
버리므로 "비었다"의 정의에 새 칸을 넣는다.

**챕터에 칸 하나 더하기** — `Chapters/ChapterGraphModel.cs`(레코드) → `Serialization/ChapterDocumentJson.cs` → 검사가 필요하면
`ChapterStructureCheck.cs` → 런타임에 나가면 `ChapterProgressionExporter.cs` + 계약 문서 + 골든(`ProgressionSampleGoldenTests`) →
편집 명령(`ProjectEditor.Chapters.cs`) → `ChapterGraphView`.

---

## 6. 깨면 다른 곳이 조용히 부서지는 규칙

0. **화자와 대사를 쓰는 코드는 `ProjectEditor.Scripts.cs`에만 둔다**(화자 개명만은 목록과 함께 `ProjectEditor.Speakers.cs`). `DialogueNode`,
   결과, 화면 어디에도 편집 가능한 본문 복사본을 만들지 않는다.
1. **LineId를 확신 없이 잇지 않는다.** `Ambiguous`가 하나라도 나면 계획 전체를 적용하지 않는다(§4.4). LineId는 compositor가 연출을
   거는 열쇠라, 잘못 이으면 그쪽에서 조용히 틀린다.
2. **조건 해석은 `ConditionFlowResolver` 하나만 쓴다.** 각자 계산하면 화면과 그래프가 다른 구조를 보인다.
3. **색 · 좌표에서 조건을 거꾸로 추론하지 않는다.** 방향은 언제나 전환 데이터 → 갈래 계산 → 표시다. 색만으로 정보를 전하지도 않는다.
4. **실행 출구는 분기점 하나다**(§4.2) — 조건 갈래는 출구를 갖지 않는다.
5. **모델 변경은 `ProjectEditor`만 한다.** 화면이 직접 만지면 되돌리기와 알림이 빠진다.
6. **화면끼리 직접 이야기하지 않는다.** 그래프와 편집기는 세션만 보고, 그래프는 `GraphProjection`을 그린다.
7. **파일 순서와 그래프 좌표는 별개다.** 새 노드는 판의 맨 뒤에 붙는다 — 좌표를 순서에 반영하면 diff가 흔들린다.
8. **모르는 파일을 관대하게 읽지 않는다.** 열리지 않는 것은 되돌릴 수 있지만 덮어써진 원고는 되돌릴 수 없다(§8).
9. **조건과 출구는 줄 번호가 아니라 `LineId`에 매단다.** 줄이 대본에서 빠지면 고아로 남는다 — 자동으로 지우지 않는다.
10. **작가가 고른 조건을 도구가 조용히 바꾸지 않는다.** 쓸 수 없게 된 조건도 전환은 그대로 두고 "사용할 수 없음"으로 보인다.
11. **바인딩이 값을 넣을 때 나는 이벤트를 사용자 입력으로 보지 않는다.** `_building` 가드가 그 역할을 한다. XAML을 읽는 도중에도
    컨트롤은 이벤트를 낸다(`x:Name` 필드는 그 뒤에 채워진다).
12. **결정적 출력이 계약이다.** 같은 입력의 진행 JSON은 공백 · 순서 · 인코딩까지 바이트가 같다(`ChapterExportBytes` —
    `ChapterDeterministicExportTests`). 컬렉션 순서를 바꾸는 리팩터링에 주의.
13. **간선 순서 = 화면의 이은 줄 차례 = 런타임 `OptionIndex`.** 간선은 줄 자리에 끼운다(`AddEdge(before:)`). 출시된 선택지 사이에
    끼우면 과거 플레이 이력의 뜻이 바뀐다(`ChapterReleaseBaseline`이 비교한다).
14. **진행 스탯은 `stat("키")`로만 읽는다**(`YarnSyntax.StatRead`). 변수로 심으면 대사 중 쓰기가 문법적으로 열려 도달성 증명 밖의
    뒷길이 생긴다. 키는 정규화하지 않는다(진행 JSON이 원본 그대로 낸다).
15. **챕터의 것은 그 챕터의 판에서만 찾는다**(§4.6).
16. **compositor가 읽는 칸은 여기서 안 써도 지우지 않는다** — `SourceRevision`(⛔ 2026-11-17 — `ChoiceExits` · 링크 데이터는
    옛 데이터 읽기와 함께 걷었다 · `docs/work-orders/legacy-read-orders.md` L5). 이미 남은 연출 백업
    (`presentation-original/`)도 지우지 않는다(⛔ 2026-11-17 — 이 툴은 더 만들지 않는다). `game.definition.json`은 건드리지 않는다.
17. **`src/Ked.Progression`은 고치지 않는다.** 런타임 원본에서 옮겨 올 때만 바뀌고, 그때 지문(`runtime-sync.txt`)을 함께 갱신한다.
18. **골든 파일은 언제나 UTF-8로 읽는다.** PowerShell 5.1은 BOM이 없으면 ANSI로 읽는다 — 한국어를 담은 `.ps1`은 UTF-8 BOM으로.

---

## 7. 챕터 그래프 갱신 경로 — 성능의 급소

```
프로젝트 변경 ─→ QueueReload() ─→ Reload()
   (합친다)                          ├ ChapterEntries()   챕터마다 ToGraphModel (조건 다시 풀기 + 모델 검사)
                                     ├ AutoExport()       전 챕터 — 검증 캐시를 지나 통과한 것만 쓴다
                                     ├ Validate()         고른 챕터 — 같은 캐시
                                     └ Draw()
```

**세 가지를 깨면 노드가 늘 때 조용히 느려진다** (2026-08-18에 58초를 1.66초로 되돌린 자리):

1. **`SetStatus`는 프로젝트 변경이 아니다.** 상태줄은 `StatusChanged`로 따로 운다.
2. **재읽기는 `QueueReload()`로 합친다.** 편집 한 번이 변경을 여럿 내도 그리는 것은 마지막 한 번이다.
3. **검증은 챕터별 (지문, 결과) 캐시를 지난다**(`ChapterExportService`). 지문은 검증이 실제로 읽는 것 — 챕터 모델과 그 판의 노드
   이름 — 에서 짓는다. 내보내기는 그 결과를 받아 쓴다(`ExportValidated`).

고정은 시간(ms)이 아니라 **일의 횟수**로 건다 — `ChapterGraphWorkAmountTests`.

## 8. 이전 프로젝트 정책 — 형식 4만 연다

**형식 4 이전은 열지 않는다**(2026-11-17 · [`docs/work-orders/legacy-read-orders.md`](docs/work-orders/legacy-read-orders.md)).
`ProjectManifestJson.Read`가 한 줄로 거절한다. **거부는 파괴적이지 않다** — 원본 파일도 세션도 그대로 남는다.
⛔ 그 전에는 형식 3(대본이 `script/*.vnscript.json`에 따로 선 형식)을 읽어서 옮기고 저장할 때 옛 대본 파일을 지웠다.

옛 모양을 고쳐 받거나 알리던 길(선 모양 열쇠 `detour:{줄}` · 수로 적힌 `openDetours` · 걷힌 대사 안 선택 전환 · `cleared:`의 안내 ·
이름이 어긋난 조건 공급 사본)도 걷었다 — 그런 파일은 깨진 파일로 거절되거나 모르는 칸으로 지나간다. ⛔ 연출 층을 담은 옛 프로젝트(형식 3)는
연출을 읽지 않고 원본을 `presentation-original/`에 한 번 남겼다 — 이제 열지 않는다.

**형식 4 안에서도 쓰는 모양만 받는다**(2026-11-18 · [`docs/work-orders/old-shapes-orders.md`](docs/work-orders/old-shapes-orders.md)). 읽기는 늘 쓰는 칸이
빠졌거나 모양이 다르면 거절하고(`JsonSupport`의 엄격한 읽기 — 비어서 안 쓰는 칸만 없어도 된다), 저장 검증(`JsonSupport.ValidateProject` — 읽기 ·
저장 · 되돌리기 스냅샷이 모두 지난다)이 불변식을 지킨다: 판은 챕터마다 하나 · 에피소드마다 그 판에 표식 카드 하나(이름 = EpisodeId) · 분기 카드는
표식이 없다 · 에피소드는 장면에 속한다 · 분기는 같은 판의 분기 카드로만 · 설정 노드는 판의 챕터 조건 공급 노드 하나 · 화자 이름은 비지 않고 겹치지
않는다. 툴이 만들지 않는 모양을 견디는 길은 두지 않는다. ⛔ 그 전에는 열 때 카드 없는 에피소드에 빈 카드를 세우고(`AlignBoardsWithChapters`),
장면 없는 에피소드를 「미지정」 장면으로, 빠진 칸을 대신 값으로 읽었다.

## 9. 확인 방법

```powershell
dotnet build .\VnTool.sln
dotnet test .\VnTool.sln          # 요약 줄 둘 — Vn.Authoring.Tests · Vn.App.Tests
dotnet run --project .\src\Vn.App\Vn.App.csproj
powershell -ExecutionPolicy Bypass -File .\publish-windows.ps1
./publish-macos.sh                # 맥에서 — StoryDirector.app
```

- CI는 `windows-latest`가 정본이다(`.github/workflows/test.yml` — 빌드 · 테스트 · portable 실행 확인). `macos-latest`가 같은 것을 한 번 더 하고
  맥 꾸러미를 올려 둔다. 시험은 어느 OS에서 돌려도 「맥이 아님」으로 선다(`TestProcessIsolation` — 맥의 손을 보는 시험만 제 스코프에서 켠다).
- 창이 뜨지 않으면 `%LOCALAPPDATA%\VnTool\logs\startup-error.log`를 먼저 본다.

### 어느 테스트가 무엇을 지키는가

| 파일 | 지키는 것 |
|---|---|
| `ScriptSyncTests` · `ScenarioPasteTests` · `CardWritingTests` · `LineIdRoundTripTests` | 글을 다시 넣어도 LineId가 이어진다 · 애매하면 멈춘다 · 카드 글 칸 왕복 |
| `ConditionFlowTests` · `ConditionSupplyTests` | 조건 해석 · 조건 범위(판) |
| `ExitTests` · `ConnectionTests` · `EmptyBranchTests` | 출구는 여는 줄에 · 고아로 남는다 · 포트 |
| `EditingTests` · `RenameCarriesReferencesTests` | 화자 · 대사의 원본이 대본에만 · 되돌리기 · 개명이 참조를 끈다 |
| `ResultDocumentComposerTests` · `OutputPresetTests` | 조각 순서와 원본 매핑 · 프리셋 |
| `SerializationTests` · `StoryFileTests` · `Chapters/EpisodeCardInvariantTests` | 결정적 JSON · 원자적 저장 · 이전 형식 거부 · 쓰는 모양만 받는다(불변식 거절) |
| `GraphProjectionTests` · `EdgeSideTests` · `EdgeShapeTests` | 무엇이 그려지나 · 선의 쪽 · 선 모양 |
| `CoreCopyInStepTests` | `Ked.Progression` 사본을 여기서 고치지 않았다(지문 — 런타임과의 맞대조는 2026-11-18에 걷혔다) |
| `Chapters/ChapterReachabilityTests` · `ChapterReachabilityEquivalenceTests` | 도달성 증명 · 코어와 같은 답 |
| `Chapters/ChapterDeterministicExportTests` · `ProgressionSampleGoldenTests` · `ChapterExportAndFixtureTests` | 바이트 결정성 · 계약 표본 골든 · 코어 거부를 자리로 옮기기 |
| `Chapters/ChapterStructureCheckTests` · `ChapterAutoEdgeCheckTests` · `ChapterSceneEntryCheckTests` | 모델 검사 |
| `Chapters/SupplyScopeTests` · `EpisodeErrorMarkTests` | 챕터 범위 · 카드 오류 표식 |
| `Vn.App.Tests/ChapterGraphWorkAmountTests` | 갱신 경로의 일의 횟수 |
| `Vn.App.Tests/ChapterRailTests` · `ChoiceSlotTests` · `GraphChoiceSideTests` · `GraphEdgeShapeTests` | 선택지 줄 · 줄 차례 · 쪽 · 선 고치기(헤드리스) |
| `Vn.App.Tests/ShellUndoTests` · `TestProcessIsolation` | 셸 되돌리기 · 테스트가 사용자 설정을 건드리지 않는다 |
