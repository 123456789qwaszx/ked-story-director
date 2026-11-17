# 옛 데이터 읽기를 걷는다 (L0–L5) — 2026-11-17

**프로젝트는 형식 4만 연다.** 소유자의 프로젝트는 형식 4다(2026-11-17 소유자 — 「옛 데이터 읽기를 전부 걷읍시다」). 옛 모양을 읽어
옮기거나 · 견디거나 · 알리던 코드와 그것만 지키던 시험 · 견본을 흔적 없이 걷는다.

> **상태: L1–L3 끝.** L1–L4는 형식 4 파일에 없는 모양이라 소유자의 파일에서 잃는 것이 없다 — 바로 한다. L5는 형식 4에 들어 있을 수
> 있는 칸이라 걷으면 다음 저장에서 그 데이터가 빠진다 — 소유자가 정한 뒤에 한다(4절).

## 1. 정한 것

| | 내용 |
|---|---|
| 형식 4 이전 | **열지 않는다** — 한 줄로 거절한다(「형식 버전 N 프로젝트는 열지 않습니다 — 형식 4만 엽니다」). 옮기지도 견디지도 않는다. 거절은 읽기가 아니다 — 없으면 옛 파일이 「manifest가 아닙니다」로 엉뚱하게 거절된다 |
| 견디기 · 알리기 | 옛 파일의 모양을 고쳐 받던 것(선 모양 열쇠 · 분기점 수 · 문자열 대본 참조)과 「버리고 알린다」(대사 안 선택 전환 · 연출 데이터 · `cleared:`)를 걷는다. 그런 파일은 이제 오류로 거절되거나 모르는 칸으로 지나간다 |
| 남기는 것 | 지금 툴이 쓰는 모양과 사람이 손으로 고친 파일을 위한 너그러움(3절) |

## 2. 걷는 것

| | 무엇 | 코드 | 시험 · 견본 |
|---|---|---|---|
| L1 | **형식 3** — 대본을 `script/*.vnscript.json`에 따로 두던 형식. 읽어서 옮기기 · 저장할 때 옛 대본 파일 지우기 · 대본 파일을 프로젝트로 잘못 연 것 알리기 | `StoryProject.LegacyScriptFilesFormatVersion` · `LastUnsupportedFormatVersion`(→ `CurrentFormatVersion` 하나) · 안 읽히는 `FormatVersion`, `ProjectManifestJson`의 `scripts` 갈래 · `ScriptFileReference` · `ProjectManifest.LegacyScripts`, `ProjectStore`의 `LooksLikeStandaloneScript` · `LegacyScriptFilesOf` · `RemoveSupersededScriptFiles` · 대본 표 짓기, `ScriptTable` 파일, `ScriptDocumentJson.ReadLegacyFile` · `Copy` · `LegacyFileExtension` · `LegacyFileFormatVersion`, `StoryFileJson.LegacyFormatVersion`(판 형식 1) · `scripts` 인자, `StoryNodeJson.ReadScript`의 문자열 참조 | `FormatThreeProjectTests`(8) · `tests/fixtures/format3-project/`, `SerializationTests` 둘(대본 파일 거절 · 형식 4의 `scripts`), 판 형식 1 줄. 거절 시험은 형식 3까지 넓힌다 |
| L2 | **연출을 담은 옛 프로젝트**(2026-10-29 전) — 연출 칸을 알아보고 원본을 `presentation-original/`에 한 번 남기기 | `ProjectManifestJson.PresentationKeys` · `PresentationLinkKinds` · `CarriesPresentation`, `StoryNodeJson.PresentationKinds`(→ 모르는 노드 종류로 거절), `ProjectStore.BackUpPresentationOriginal` · `PresentationBackupFolder` · `LegacyResultsExtension`, `ProjectLoadResult.PresentationBackupDirectory`, `AuthoringSession.PresentationBackupNote` | `LegacyPresentationProjectTests`(6). 이미 남은 `presentation-original/` 폴더는 건드리지 않는다(compositor가 읽는다 — 만들지만 않는다) |
| L3 | **대사 안 선택 전환**(2026-11-04 폐지) — 읽으며 버리고 열 때 알리기. 그 알림이 유일한 열기 알림이라 알림 배관도 함께. 모르는 전환 종류를 `endIf`로 돌리던 「종전대로」도 걷는다 — 그대로 두면 걷힌 이름이 조용히 `endIf`가 된다. 이제 `beginIf` · `beginElseIf` · `endIf` 밖은 오류다 | `StoryNodeJson.RetiredChoiceKindNames` · 읽으며 건너뛰기 · `ParseKind`의 뒷길, `ProjectStore.NoticeRetiredChoices` · `CountOccurrences`, `ProjectLoadResult.Notices` · `LoadNotices` · 상태줄 꼬리 | `FormatThreeProjectTests`의 하나(L1과 함께 간다) |
| L4 | **작은 옛 모양** — 선 모양 열쇠 `detour:{줄}`, 수로 적힌 `openDetours`, 폐지된 `cleared:`의 안내(→ 다른 모르는 연산자처럼 오류) · 개명 막기, 이름이 어긋난 조건 공급 사본 이어받기, 스탯 `displayName` · `sourceRow` · `excelEpisode` · `excelLines` 같은 「읽고 지나간다」 주석 | `StoryNodeJson.AdoptLineOnlyDetourShapes` · `ReadDetourSlots`의 수 갈래, `ConditionExpressionParser`의 `ClearedRetired`, `ProjectEditor.RenameEpisode`의 `cleared:` 막기, `ChapterBoardSupply`의 옛 사본 이어받기 | `BranchMarkerTests` 하나 · `StatKeyOnlyTests` 하나 · `ChapterSchemaV5Tests` 하나 · `ChapterEditingTests` 하나 · `ConditionSupplyByNameTests` 하나, `OpenDetourTests`의 수 부분. 견본의 스탯 `displayName`(`tests/fixtures/chapter-samples/ch01` · `ch05`, 축제 전날 매니페스트) |
| L5 | **형식 4에 남은 옛 데이터 칸** — 툴이 만들지 않고 읽지도 않으며 그대로 다시 쓰기만 하는 것: 선택지 출구 `ChoiceExits`, 공급 링크 `links`(`NodeLink`), 작가 화자 `writerSpeakers` | (소유자가 정한 뒤) | (4절) |

각 단계는 빌드(경고 0) · 두 시험 프로젝트가 모두 통과한 채로 커밋하고, 문서(`ARCHITECTURE.md` 형식 표 · 8절 · 시험 표, `README.md`)를 같은
커밋에서 고친다.

## 3. 남기는 것 (옛 데이터가 까닭이었지만 지금도 쓰인다)

| 무엇 | 왜 남기나 |
|---|---|
| 열 때 판과 챕터 맞추기(`AlignBoardsWithChapters`) | 챕터 조건 공급을 열 때마다 다시 채운다 — 지금 동작이다 |
| 표식 없는 카드의 이름 뒷길(`EpisodeNaming` · `ChapterBoard.EpisodeNodeFor`) | 분기 카드는 표식이 없고, 맞추기가 이름으로 찾는다 |
| 장면 없는 에피소드 경고 · 혼자짜리 장면(`__scene_{Id}`) | 소유자가 정했다(경고) — 형식 4에 있는 모양이고 런타임 계약에도 있다 |
| 분기점이 에피소드로 가는 오류(`BranchIntoEpisode`) | 분기 카드 전(2026-11-17 오늘)에 이은 형식 4 데이터를 검증이 짚는다 |
| 챕터가 아닌 판 | 낙서판(「기본 파일」)은 툴이 더 만들지 않지만 소유자의 형식 4 프로젝트에 남아 있을 수 있다 — 열리고 보이되 카드를 더하지 않는다 |
| 판 크기 넓히기 · 겹친 카드 다시 심기 | 손으로 고친 파일 · 엑셀에서 온 배치를 위한 너그러움 — 옛 형식이 아니다 |
| `SourceRevision` · 단일 문자열 `detour` · `condition` 한 칸 | 지금 쓰는 모양이다 |
| `src/Ked.Progression`의 장면ID 없는 옛 진행 JSON | 런타임 사본 — 고치지 않는다(`ARCHITECTURE.md` 17) |

## 4. 물을 것 (L5 앞에서)

`ARCHITECTURE.md` 16 — **「compositor가 읽는 칸은 여기서 안 써도 지우지 않는다 — `ChoiceExits` · 링크 데이터」**. 이 칸들은 형식 4
파일에 들어 있을 수 있어, 읽기를 걷으면 다음 저장에서 소유자의 파일에서 빠지고 compositor도 더는 못 본다. compositor 저장소는 이 세션에서
볼 수 없어 그쪽이 지금 무엇에 쓰는지 확인하지 못했다.

- `ChoiceExits` — 대사 안 선택지(2026-11-04 폐지)의 배선. 선택지 줄은 이미 읽으며 버려지므로 가리킬 줄이 없는 배선이다.
- `links` — 옛 조건 공급 연결(L2에서 편집이 걷혔다). 조건 범위는 판이다.
- `writerSpeakers` — 유물(16에 없다). 아무도 읽지 않는다.

걷으면 16을 고친다(⛔).

## 5. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-17 | L0 | (이 커밋) | 지시서 — 조사는 읽기 전용 훑기 한 번(코드 · 시험 · 견본 · 문서) |
| 11-17 | L1 · L2 | (이 커밋) | 한 커밋 — 연출을 담은 옛 프로젝트는 모두 형식 3이라 형식 3을 거절하면 연출 백업도 함께 걷힌다. 형식 4 이전은 한 줄 거절(`ProjectManifestJson.Read` — 「형식 버전 N 프로젝트는 열지 않습니다 — 이 툴은 형식 4만 엽니다」). 걷음: `ScriptTable` 파일 · 대본 파일 읽기/지우기/거절 · 판 형식 1 · 문자열 대본 참조 · 대본 없는 카드(→ 깨진 파일) · 안 읽히던 `StoryProject.FormatVersion` · 연출 키/배선/노드 알아보기 · `presentation-original/` 만들기 · 열 때 그 알림. 시험 — `FormatThreeProjectTests`(8) · `tests/fixtures/format3-project/` · `LegacyPresentationProjectTests`(6) · `SerializationTests` 둘(대본 파일 거절 · 형식 4의 `scripts`) · 판 형식 1 줄을 걷고, 거절 시험에 형식 3을 더했다. 문서 — `ARCHITECTURE.md` 형식 표 · 8절 · 16(이미 남은 백업은 지우지 않는다) · 시험 표, `README.md`, `two-graphs.md` 1절 표, `io-reference.md` 머리. Authoring 689 · App 442 |
| 11-17 | L3 | (이 커밋) | 대사 안 선택 전환을 알아보고 버리기(`RetiredChoiceKindNames`) · 열 때 그 알림(`NoticeRetiredChoices`) — 유일한 열기 알림이라 `ProjectLoadResult.Notices` · `LoadNotices` · 상태줄 꼬리도 걷음. 모르는 전환 종류를 `endIf`로 돌리던 뒷길도 걷어 `beginIf` · `beginElseIf` · `endIf` 밖은 깨진 파일이다. 새 시험 1(이론 2 — `beginChoice` · `else`). Authoring 691 · App 442 |
