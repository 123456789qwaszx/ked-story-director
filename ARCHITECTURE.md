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
| **읽는 것** | 프로젝트 하나(`*.vnproject.json` · `story/`)와 `game.definition.json` |
| **쓰는 것** | 같은 프로젝트(원고), 그리고 `exported/{챕터}.progression.json`(유니티 런타임 수입물 — 검증을 통과하면 저절로) |
| **읽어 가는 쪽** | [`ked-story-compositor`](https://github.com/123456789qwaszx/ked-story-compositor)가 원고를 **읽기만** 해서 제 연출과 합쳐 게임용 Yarn을 낸다 |

두 화면이 같은 프로젝트를 고친다.

| | ① 챕터 그래프 (기획) | ② 연출 그래프 (대사 · 흐름) |
|---|---|---|
| 값 | `StoryProject.Chapters` — 에피소드 · 간선 · 조건 · 스탯 · 선택지 사전 · 픽스처 | `StoryProject.Files`(판 = 챕터 1:1) — 카드마다 제 대본(`DialogueNode.Script`) |
| 화면 | `Vn.App/Views/ChapterGraphView.axaml{,.cs}` | `Vn.App/Views/GraphEditorView.axaml.cs` · `DialogueNodeEditor.axaml.cs` |

**각 값의 주인은 한 곳뿐이다.** 창구가 둘이어도 값은 한 곳에 산다 — 간선은 챕터 그래프에서도 연출 그래프의 선택지 칸에서도
긋지만 둘 다 `ChapterDocument.Edges`를 고친다.

## 2. 저장 구조와 형식 버전

```text
MyStory/
├─ 예제.vnproject.json         매니페스트 — 판 목록 · 챕터(chapters[]) · 링크 · 작가 화자
├─ game.definition.json        스탯 후보(variables) · 게임 전역 조건 · 화자(speakers). compositor의 키도 산다 — 모르는 키는 보존한다
├─ story/*.vnstory.json        판 — 노드(대사 · 설정). 대사 카드는 제 대본(`script` 칸 — 줄 신원(LineId) · 개정 번호 ·
│                              locale별 화자 · 글)과 그 위의 줄 논리 · 출구 · 선 모양을 든다
└─ exported/{챕터}.progression.json
```

| 형식 | 상수 | 파일 |
|---|---|---|
| 프로젝트 | `StoryProject.CurrentFormatVersion` = 4 (3은 읽어서 옮기고 2 이하는 거부 — §8) | `*.vnproject.json` |
| 판 | `StoryFileJson.CurrentFormatVersion` = 2 (1은 형식 3과 함께 읽는다) | `*.vnstory.json` |
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
| `tests/Vn.Authoring.Tests` · `tests/Vn.App.Tests` | xUnit · Avalonia 헤드리스 | `tests/fixtures/chapter-samples/` 견본 챕터 둘 · `tests/fixtures/format3-project/` 형식 3 견본 |

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
| `StoryProject.cs` | 프로젝트 — 판 · 대본 · 챕터 · 링크 · 작가 화자(`WriterSpeakers`) |
| `StoryFile.cs` | 판 하나 — 노드 목록(순서 = 파일 순서, 그래프 좌표와 별개 — §6-7) |
| `StoryNode.cs` | `DialogueNode`(제 대본 `Script`를 들고, 줄 논리 · 갈래 출구(`BranchExits`) · 선택지 출구(`ChoiceExits` — compositor가 읽는다) · 기본 출구 · 에피소드 표식(`MarkedEpisodeId`)을 든다) · `SetNode`(조건 공급자 — 카드가 없다) · `ConditionDefinition` |
| `DialogueLineExtension.cs` | 줄에 얹는 대사 논리 — 그 줄 **앞**의 조건 · 선택 전환(`Transitions`)과 분기 표식(`DetourTargetNodeId`). 화자 · 글은 없다 |
| `EdgeShape.cs` | 사람이 고친 선 모양(꼴 · 굽이 — 자동 자리에서 옮긴 양) |
| `NodeLink.cs` | 옛 공급 연결 데이터. 범위를 좁히지 않는다(조건 범위는 판이다 — `ConnectedSetNodeResolver`). 편집 API는 L2에 걷혔고 데이터는 남는다 |
| `Identifier.cs` | 식별자 형식 |

### 3.3 `Vn.Authoring/Chapters` — 챕터 층

| 파일 | 하는 일 |
|---|---|
| `ChapterDocument.cs` | **저장 모델**(매니페스트의 `chapters[]`). `ToGraphModel()`이 조건식을 다시 풀고 모델 검사를 돌려 펼친다 |
| `ChapterGraphModel.cs` | **펼친 모델** — `ChapterEpisode` · `ChapterEdge`(`Where` = 사람이 읽는 자리) · `ChapterCondition` · `ChapterStat` · `ChapterChoiceOption` · `ChapterFixture` · `Diagnostics` · `EpisodeHasError` |
| `ChapterDiagnostic.cs` | 진단 하나 — 심각도 · 코드 · 자리(`Sheet`) · 대상 에피소드(`EpisodeId`) · 메시지 |
| `ChapterStructureCheck.cs` · `ChapterAutoEdgeCheck.cs` · `ChapterSceneEntryCheck.cs` | **모델 검사** — 값의 흠(빈 · 겹친 Id, 끝점, 관문 라벨, 문구, 스탯 어휘, 자동 길 규칙, 장면 진입점) |
| `ChapterValidator.cs` | 모델 검사 + **판과 맞대는 검사**(대사엔트리의 카드가 있나 · 재생할 줄이 있나) + 도달성 증명을 한 벌로 |
| `ChapterReachabilityProver.cs` | (에피소드, 스탯 벡터) 상태공간을 끝까지 걸어 "닿을 수 있는가"를 증명. 도착 스탯 폭도 걷는 김에 적는다 |
| `ChapterProgressionExporter.cs` | 진행 JSON. 낸 것을 `Ked.Progression.ProgressionLoader`에 다시 실어 보고 거부 사유를 에피소드 · 간선으로 옮긴다(`CoreRefusals`) |
| `ChapterExportService.cs` | 검증 캐시(지문 = 챕터 모델 + 판의 노드 이름) · 전 챕터 자동 내보내기 · 거부 장부 |
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
| `BranchAnchor.cs` | 조건 · 선택 갈래의 신원을 짓는 유일한 자리 |
| `NodeConnections.cs` | 실행 출구 포트(`PortsOf`)와 간선 문구 |
| `AvailableConditionResolver.cs` · `ConnectedSetNodeResolver.cs` · `ConditionChoices.cs` | 쓸 수 있는 조건(게임 전역 + 같은 판의 설정노드) · 드롭다운에 무엇을 보일지 |

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
| `ProjectEditor.Scripts.cs` | 대본 줄(`InsertScriptLine` · `RetireScriptLine` · `MoveScriptLine` · `SetScriptLineText`) · 카드 글 반영(`ApplyScenarioText`) · 화자 개명 |
| `ProjectEditor.Chapters.cs` | 챕터 · 에피소드 · 간선(`AddEdge(before:)` — 줄 자리에 끼운다) · 조건 · 스탯(개명 · 삭제 — 그 챕터의 공급 노드만) · 판 맞추기(`AlignBoardsWithChapters`) |
| `ProjectChangedEventArgs.cs` | 변경의 종류(`ProjectChangeKind`) — 화면이 무엇을 다시 그릴지(`Vn.App/Services/ProjectRefreshPlanner`)의 근거 |

### 3.7 `Vn.Authoring/Rendering` · `Results` — 평평한 글로 펴기

| 파일 | 하는 일 |
|---|---|
| `Results/DialoguePublisher.cs` | 대사 노드를 펼친 불변 초안(`Draft`) — 줄마다 그 순간의 화자 · 글 · 얼린 조건식 · 출구. 문제(`PublishProblem`)를 함께 낸다 |
| `Rendering/ResultDocumentComposer.cs` | 그 초안을 의미 조각(`RenderedSegment`)으로 — 프리셋 둘(`RuntimeFull` · `ScenarioOnly`, `DocumentOutputOptions`) |
| `Rendering/WorkingDialoguePreview.cs` · `DocumentPreviewFormatter.cs` | **카드 글 칸의 글**(`ScenarioText`) — 넣을 때 `ScenarioTextParser`가 같은 문법으로 되읽는다 |
| `Rendering/YarnSyntax.cs` · `YarnNaming.cs` | 조건 줄 표기 · 스탯 읽기(`stat("키")`) · 노드 이름 규칙 |

이름의 「발행」 · 「결과」는 남은 이름이다 — 발행(고정본 · 결과 저장소)은 compositor로 갔다.

### 3.8 `Vn.Authoring/Serialization` · `Definition`

| 파일 | 하는 일 |
|---|---|
| `ProjectStore.cs` | 파일 배치 · 원자적 저장 · 연출 백업(`presentation-original/`) |
| `ProjectManifestJson.cs` · `ChapterDocumentJson.cs` | 매니페스트 · 챕터 |
| `StoryFileJson.cs` · `StoryNodeJson.cs` · `EdgeShapeJson.cs` | 판 · **노드 직렬화는 여기 하나뿐** · 선 모양 |
| `ScriptDocumentJson.cs` | 대본 |
| `ProjectSnapshotCodec.cs` · `JsonSupport.cs` | 되돌리기 · 저장 여부 비교 · 공통 |
| `Definition/GameDefinition.cs` · `GameDefinitionStore.cs` | 정의 읽기 · 정의에 쓰는 유일한 길(`speakers` 통째로 · `variables` 더하기만, 다른 키는 보존) |

### 3.9 `Vn.App` — 화면

| 파일 | 하는 일 |
|---|---|
| `MainWindow.axaml{,.cs}` | 도구 모음 · 열기 · 저장 · 탭 · 창 제목 |
| `Views/ChapterGraphView.axaml{,.cs}` | 챕터 그래프 · 편집 패널 · [화자] · [챕터] 탭 · 검증 보고 |
| `Views/GraphEditorView.axaml.cs` | 연출 그래프 — 카드 · 글 칸 · 포트 · 선택지 줄 · 선 고치기 · 미니맵 |
| `Views/DialogueNodeEditor.axaml.cs` | 곁기둥의 대사 편집기(줄 표 · 조건 · 분기) |
| `Views/StatChangeEditor.cs` · `ChapterAddFlyout.cs` · `ConfirmFlyout.cs` · `BranchPalette.cs` | 스탯변화 칸 · 챕터 더하기 · 확인 · 갈래 색 |
| `Services/AuthoringSession.cs` | 열린 프로젝트 · 저장 여부 · 선택 · 정의 · 상태줄 |
| `Services/ProjectRefreshPlanner.cs` | 변경 종류 → 그래프 · 곁기둥 중 무엇을 다시 만들지 |
| `Services/AppSettingsService.cs` · `StartupLog.cs` · `UiGuard.cs` | 최근 프로젝트 · 시작 오류 로그 · 이벤트 핸들러의 예외를 붙잡아 알린다(크래시 금지) |

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
| 이전 프로젝트는 | 형식 2 이하는 거부한다(§8) |

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
- 조건 갈래 안의 선택 블록은 정식 구성이다(W54). 그 밖의 중첩은 문제로 알린다.

### 4.2 조건 갈래 출구는 갈래를 *여는 줄*에 매단다

마지막 줄에 매달면 줄을 하나 넣는 순간 출구가 갈래 중간에 파묻힌다. 여는 줄은 갈래 그 자체이므로 줄을 넣고 옮겨도 출구는 그
갈래의 것으로 남는다.

**전환을 지우면** `ProjectEditor.PruneBranchExits`가 출구를 함께 버린다(남기면 어느 화면에서도 못 지우는 간선이 생긴다).
**대본에서 줄이 빠지면** 출구를 버리지 않는다 — 그것은 **고아**이지 쓰레기가 아니다. 되돌리면 살아나야 하고, 그때까지는
`FlowProblemKind.OrphanedLineExtension`으로 보인다.

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
| if/elseif/endif · 선택 블록의 뜻 | `Flow/ConditionFlowResolver.cs` ← **이 파일이 조건 모델 그 자체다** |
| 전환 종류 추가 | `Model/DialogueLineExtension.cs`의 `ConditionTransitionKind` → 위 해석자 → `Flow/ConditionChoices.cs` |
| 드롭다운에 무엇을 보일지 · 쓸 수 있는 조건 | `Flow/ConditionChoices.cs` · `Flow/AvailableConditionResolver.cs` |
| 갈래 신원 | `Flow/BranchAnchor.cs` |
| 갈래 색 | 자리는 `Flow/DialogueFlow.cs`의 `PaletteIndex`, 색은 `App/Views/BranchPalette.cs` |
| 잘못된 구조를 알리는 방식 | `Flow/DialogueFlow.cs`의 `FlowProblemKind` |

**출구 · 연결 · 그래프**

| 하고 싶은 일 | 여는 곳 |
|---|---|
| 출구를 어디에 저장하나 | `Model/StoryNode.cs`의 `DialogueNode.BranchExits` · `ChoiceExits` · `DefaultExitTargetNodeId` |
| 포트를 만드는 규칙 · 간선 문구 | `Flow/NodeConnections.cs`의 `PortsOf` · `LabelFor` |
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
| 챕터 조건 → 대사 카드 | `Chapters/ChapterBoardSupply.cs` · `ConditionYarnTranslator.cs` |
| 이름 바꾸기 · 지우기 | `ChapterRenamer` · `EpisodeRenamer` · `ChapterDeleter` · `EpisodeDeleter` · `ProjectEditor.RenameChapterStat` |

**편집 · 저장 · 화면**

| 하고 싶은 일 | 여는 곳 |
|---|---|
| 새 편집 명령 | `Editing/ProjectEditor*.cs` — **모델을 바꾸는 코드는 전부 여기에만** |
| 그 변경이 어느 화면을 다시 만들지 | `Editing/ProjectChangedEventArgs.cs`의 `ProjectChangeKind` → `App/Services/ProjectRefreshPlanner.cs` |
| 노드에 저장 칸 추가 | `Serialization/StoryNodeJson.cs` — 노드 직렬화는 여기 하나뿐 |
| 프로젝트 · 챕터 저장 칸 | `Serialization/ProjectManifestJson.cs` · `ChapterDocumentJson.cs` |
| 파일 배치 · 열지 말지 | `Serialization/ProjectStore.cs` · `JsonSupport.ValidateProject` |
| 형식 버전 올리기 | `Model/StoryProject.cs`의 `CurrentFormatVersion` + 위 읽기 경로들 |
| 게임별 정의 | `Definition/GameDefinition.cs` · 쓰기는 `GameDefinitionStore.cs` |
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

0. **화자와 대사를 쓰는 코드는 `ProjectEditor.Scripts.cs`에만 둔다.** `DialogueNode`, 결과, 화면 어디에도 편집 가능한 본문 복사본을
   만들지 않는다.
1. **LineId를 확신 없이 잇지 않는다.** `Ambiguous`가 하나라도 나면 계획 전체를 적용하지 않는다(§4.4). LineId는 compositor가 연출을
   거는 열쇠라, 잘못 이으면 그쪽에서 조용히 틀린다.
2. **조건 해석은 `ConditionFlowResolver` 하나만 쓴다.** 각자 계산하면 화면과 그래프가 다른 구조를 보인다.
3. **색 · 좌표에서 조건을 거꾸로 추론하지 않는다.** 방향은 언제나 전환 데이터 → 갈래 계산 → 표시다. 색만으로 정보를 전하지도 않는다.
4. **조건 갈래 출구는 여는 줄에 매단다**(§4.2).
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
16. **compositor가 읽는 칸은 여기서 안 써도 지우지 않는다** — `ChoiceExits` · 링크 데이터 · `SourceRevision` · 연출 백업
    (`presentation-original/`). 정의 파일의 모르는 키도 보존한다.
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

## 8. 이전 프로젝트 정책 — 1 · 2는 거부하고 3은 옮긴다

형식 버전 1과 2는 **읽지 않는다.** `ProjectManifestJson.Read`가 이유를 담아 거부한다 — 그 형식에서는 노드가 화자 · 대사를 직접
소유했고, 지금 구조로 바꾸려면 어느 문장이 어느 LineId인지 도구가 임의로 정해야 한다. 옮기려면 새 프로젝트를 만들고 대사를 카드
글 칸에 붙여 넣는다. **거부는 파괴적이지 않다** — 원본 파일도 세션도 그대로 남는다.

**형식 3은 읽어서 옮긴다**(2026-10-29 · `script-merge`). 옮기기가 기계적이라서다 — 카드마다 가리키던 대본 파일의 내용을
그대로 카드 안에 넣는다(LineId · 개정 번호 · 은퇴한 줄 그대로, `ScriptTable`). 한 대본을 두 카드가 가리켰으면 둘째는 같은 내용의
사본을 받고, 어느 카드도 안 가리키는 대본은 버린다. 열기만 해서는 아무것도 지우지 않는다. **저장할 때** 옛 매니페스트가 가리키던
대본 파일을 지운다(새 판 파일과 매니페스트를 다 쓴 뒤 · 매니페스트가 가리키지 않던 파일은 남긴다). 고정 자료
`tests/fixtures/format3-project`(견본의 형식 3 시절)를 옮기면 지금 견본과 바이트까지 같다(`FormatThreeProjectTests`).

형식 3 · 4 안에서 걷힌 칸은 **읽지 않고 지나간다**(깨지지 않는다): `excelLines` · `excelEpisode` · 대본의 `sourcePath` · 챕터의
`sourceRow`. 연출 층을 담은 옛 프로젝트는 연출을 읽지 않고 원본을 `presentation-original/`에 한 번 남긴다(compositor가 가져간다).
열 때 카드 없는 에피소드에는 빈 카드가 선다(`AlignBoardsWithChapters`).

## 9. 확인 방법

```powershell
dotnet build .\VnTool.sln
dotnet test .\VnTool.sln          # 요약 줄 둘 — Vn.Authoring.Tests · Vn.App.Tests
dotnet run --project .\src\Vn.App\Vn.App.csproj
powershell -ExecutionPolicy Bypass -File .\publish-windows.ps1
```

- CI는 `windows-latest`가 정본이다(`.github/workflows/test.yml` — 빌드 · 테스트 · portable 실행 확인).
- 창이 뜨지 않으면 `%LOCALAPPDATA%\VnTool\logs\startup-error.log`를 먼저 본다.

### 어느 테스트가 무엇을 지키는가

| 파일 | 지키는 것 |
|---|---|
| `ScriptSyncTests` · `ScenarioPasteTests` · `CardWritingTests` · `LineIdRoundTripTests` | 글을 다시 넣어도 LineId가 이어진다 · 애매하면 멈춘다 · 카드 글 칸 왕복 |
| `ConditionFlowTests` · `ConditionChoiceTests` · `ConditionSupplyTests` | 조건 해석 · 드롭다운 · 조건 범위(판) |
| `ExitTests` · `ConnectionTests` · `EmptyBranchTests` | 출구는 여는 줄에 · 고아로 남는다 · 포트 |
| `EditingTests` · `RenameCarriesReferencesTests` | 화자 · 대사의 원본이 대본에만 · 되돌리기 · 개명이 참조를 끈다 |
| `ResultDocumentComposerTests` · `OutputPresetTests` · `ChoiceTests` | 조각 순서와 원본 매핑 · 프리셋 · 선택 블록 |
| `SerializationTests` · `StoryFileTests` · `SampleProjectTests` | 결정적 JSON · 원자적 저장 · 이전 형식 거부 · 손으로 쓴 견본(`samples/Authoring/`) |
| `LegacyPresentationProjectTests` | 연출을 담은 옛 프로젝트 — 연출을 읽지 않고 백업한다 |
| `GraphProjectionTests` · `EdgeSideTests` · `EdgeShapeTests` | 무엇이 그려지나 · 선의 쪽 · 선 모양 |
| `CoreCopyInStepTests` | `Ked.Progression` 사본이 런타임과 발맞춰 있다 |
| `Chapters/ChapterReachabilityTests` · `ChapterReachabilityEquivalenceTests` | 도달성 증명 · 코어와 같은 답 |
| `Chapters/ChapterDeterministicExportTests` · `ProgressionSampleGoldenTests` · `ChapterExportAndFixtureTests` | 바이트 결정성 · 계약 표본 골든 · 코어 거부를 자리로 옮기기 |
| `Chapters/ChapterStructureCheckTests` · `ChapterAutoEdgeCheckTests` · `ChapterSceneEntryCheckTests` | 모델 검사 |
| `Chapters/SupplyScopeTests` · `EpisodeErrorMarkTests` | 챕터 범위 · 카드 오류 표식 |
| `Vn.App.Tests/ChapterGraphWorkAmountTests` | 갱신 경로의 일의 횟수 |
| `Vn.App.Tests/ChapterRailTests` · `ChoiceSlotTests` · `GraphChoiceSideTests` · `GraphEdgeShapeTests` | 선택지 줄 · 줄 차례 · 쪽 · 선 고치기(헤드리스) |
| `Vn.App.Tests/ShellUndoTests` · `OpenAlignmentTests` · `TestProcessIsolation` | 셸 되돌리기 · 열 때 판 맞추기 · 테스트가 사용자 설정을 건드리지 않는다 |
