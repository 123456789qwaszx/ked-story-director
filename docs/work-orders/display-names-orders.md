# 이름과 ID 지시서 (N0–N6) — 2026-11-16

[대본] 탭(작가 자리)은 **이름만** 다룬다. 작가는 ID를 보지도 정하지도 않는다. ID는 자동으로 붙고, 바꿀 일이 있으면 개발자가 [구성] 탭에서 바꾼다.
커밋은 N 단위, 이 저장소의 관행대로(작성자 Claude · 표시 줄 · `dev`에 로컬 커밋만).

## 0. 왜

같은 손짓(하이어아키에서 두 번 눌러 이름 바꾸기)인데 에피소드는 **제목**을, 장면 · 챕터는 **ID**를 바꿨다(2026-11-16 확인).

| 무엇 | 지금 [대본]의 이름 바꾸기 | ID가 무엇에 쓰이나 |
|---|---|---|
| 에피소드 | 제목(`Title`)만 — EpisodeId 그대로 | 진행 JSON 노드 신원 |
| 장면 | **SceneId** — 장면에는 이름 칸이 없다 | 런타임 저장 · 롤백 경계, `scenes.json` 열쇠 |
| 챕터 | **ChapterId**(`ChapterRenamer` — 판 · 조건 노드 · 진행 JSON 파일 이름까지) | 진행 JSON 신원 · 파일 이름 |

작가는 이름을 자주 바꾸고 같은 이름을 다시 쓴다(아침의 「교실」, 저녁의 「교실」). 이름이 곧 ID면
- **같은 이름이면 장면이 조용히 합쳐진다** — 장면은 SceneId가 같은 에피소드의 묶음이다. 저장 · 롤백 경계가 하나가 되고 장면 메모는 받는 쪽만 남는다.
- 이름을 다듬을 때마다 파일 이름과 신원이 움직인다(출시 뒤에는 세이브가 흔들린다).

## 1. 정한 것 (소유자 2026-11-16)

| | 내용 |
|---|---|
| 방향 | [대본] 탭은 이름만 다룬다. 작가는 ID를 보지도 정하지도 않는다. 새 챕터 · 장면 · 에피소드는 ID가 자동으로 붙고, ID는 개발자가 [구성] 탭에서 바꾼다 |
| 장면 이름 | 장면 메모 항목에 `name` 칸 — `sceneNotes[]`가 `{sceneId, name?, text?}`. 장면ID를 바꿀 때 이름과 메모가 함께 따라가는 규칙 한 벌 |
| 챕터 ID 모양 | `ch01`, `ch02` … (그 프로젝트에서 안 쓰인 첫 번호). 작가가 적은 이름은 표시 이름(`displayName`) |
| 처음 이름 | 새 장면 「새 장면」, 새 에피소드 「새 에피소드」 — 같은 이름이 여럿이어도 ID가 달라 문제없다 |
| [구성] 탭 | 챕터 · 장면 인스펙터에 **ID 칸**을 더하고(에피소드ID 칸처럼), 챕터의 표시 이름 칸은 걷는다 — 이름은 [대본]에서만 고친다 |

## 2. 설계 — 지킬 것

1. **보이는 이름 한 규칙.** 챕터 = 표시 이름, 비면 ChapterId. 장면 = 장면 이름, 비면 SceneId(미지정 장면은 지금처럼 「미지정 · {EpisodeId}」).
   에피소드 = 제목, 비면 EpisodeId(`ShownTitle` 그대로). **옛 프로젝트는 옮기지 않는다** — 이름이 비어 있으니 지금과 똑같이 보인다.
2. **장면 이름은 `ChapterSceneGrouping`이 싣는다** — `ChapterScene.DisplayName`이 장면 이름을 쓰면 하이어아키 · 판 이름표 · 인스펙터 머리 · `scenes.json`이 함께 따른다.
3. **장면은 여전히 엔티티가 아니다**(계약 §0-0). 이름은 메모와 같은 곁 자료다 — 구성원 · 순서 · 루트를 저장하지 않는다. 빈 항목(이름도 글도 빈)은 쓰지 않는다.
4. **ID 바꾸기는 [구성]만.** 장면ID 바꾸기(`RenameScene`)는 이름 · 메모를 데려간다(지금 규칙 그대로 — 받는 쪽에 항목이 있으면 그것을 두고 보낸 쪽은 주인 없이 남는다).
   챕터ID 바꾸기는 `ChapterRenamer` 그대로. 에피소드ID는 지금처럼 [구성] 인스펙터의 에피소드ID 칸.
5. **[대본]의 이름 바꾸기는 되돌리기 한 칸, 구조를 건드리지 않는다**(같은 이름의 장면 둘이 합쳐지지 않는다).
6. **[구성] 탭은 ID를 보인다** — 하이어아키(읽기 전용)는 지금처럼 ID, 인스펙터에 ID 칸.
7. **런타임 계약은 그대로.** 진행 JSON의 챕터 `DisplayName` · 노드 `Title` · `SceneId`의 모양은 바뀌지 않는다. `scenes.json`은 장면 `displayName`에 장면 이름을 싣는다.

## 3. 단계

| | 할 일 | 닿는 곳 | 끝 |
|---|---|---|---|
| N0 | 이 지시서 | 이 문서 | — |
| N1 | 모델 · 저장 — `ChapterSceneNote`에 `Name`(빈 항목 규칙 · 차례 그대로), `ChapterSceneGrouping`이 장면 이름을 `DisplayName`에, 챕터의 보이는 이름 한 자리 | `Chapters/` · `Serialization/ChapterDocumentJson.cs` | 왕복 · 옛 프로젝트 바이트 그대로 · 같은 이름 두 장면이 따로 선다 |
| N2 | 편집 — `SetSceneName`, 새 장면 「새 장면」 · 새 에피소드 「새 에피소드」 · 새 챕터 ID `ch01`과 표시 이름, 장면ID 바꾸기가 이름을 데려간다 | `Editing/ProjectEditor.Chapters.cs` · `App/Services/AuthoringSession.cs` | 되돌리기 한 칸 · 자동 ID 시험 |
| N3 | [대본] 화면 — 하이어아키의 챕터 · 장면 줄이 이름을 보이고 이름만 바꾼다, 챕터 차림표 · [＋ 챕터] 창구가 이름을 받는다, 인스펙터 머리(「{챕터}챕터 - {장면}」 · 「챕터 · …」 · 「장면 · …」) · 판의 챕터 · 장면 이름표가 이름 | `App/MainWindow*.cs` · `Views/StoryHierarchy` · `ChapterAddFlyout` · `GraphEditorView` | 헤드리스 시험 — 이름을 바꿔도 ID · 구조가 그대로 |
| N4 | [구성] 화면 — 챕터 · 장면 인스펙터에 ID 칸(Enter로 바꾼다 — 챕터는 `ChapterRenamer`, 장면은 `RenameScene`), 챕터 표시 이름 칸을 걷는다 | `Views/ChapterGraphView` | 헤드리스 시험 |
| N5 | 내보내기 — `scenes.json`의 장면 `displayName` = 장면 이름 | `Chapters/ChapterScenesExporter.cs` | 결정성 시험 그대로 |
| N6 | 문서 — `two-graphs.md` · `ARCHITECTURE.md` · 작가 안내 · 이 문서의 진행 기록 | `docs/` | 문서가 지금 동작과 맞다 |

각 단계는 빌드 · 두 시험 프로젝트가 모두 통과한 채로 커밋한다(`TreatWarningsAsErrors`).

## 4. 하지 않는 것

- Scene 엔티티(구성원 · 루트를 저장하는 표)를 만들지 않는다.
- 진행 JSON 모양 · `src/Ked.Progression` · compositor를 고치지 않는다.
- 옛 프로젝트의 ID를 바꾸거나 이름을 채우지 않는다(비면 ID가 보인다).
- 같은 이름을 막지 않는다 — 이름은 글자다.

## 5. 조심할 것

- 견본(`samples/RuntimeSamples/`)에 소유자의 커밋 전 변경이 있다 — 덮어쓰거나 커밋에 넣지 않는다. 견본 시험은 커밋된 상태로 돈다.
- [대본] · [구성] 하이어아키는 같은 `StoryHierarchy`다 — [대본]만 이름(`ShowEpisodeTitles`를 챕터 · 장면까지 넓힌다), [구성]은 ID.
- 원격 셸(VM)의 세션 디스크가 거의 차 있다(2026-11-16) — 끊기면 클라우드 작업공간에서 같은 기준으로 빌드 · 시험한다.

## 6. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-16 | N0 | (N0 커밋) | 지시서. 1절의 넷은 소유자가 정했다. 코드 변경 0 |
| 11-16 | N1 | (N1 커밋) | `ChapterSceneNote(SceneId, Text, Name)` — 이름만 · 글만 · 둘 다 항목, 둘 다 비면 안 쓴다(파일 `name` · `text` 각각 비면 안 쓴다). `ChapterSceneNotes.TextOf` · `NameOf`, `ChapterSceneGrouping`이 장면 이름을 `DisplayName`에(같은 이름 두 장면이 따로 선다). `ChapterDocument` · `ChapterGraphModel.ShownName`. 편집 `SetSceneName` · `RemoveSceneNote`(통째) — `SetSceneNote`는 글만(이름을 남긴다). N5의 `scenes.json`도 함께: 장면 `displayName` = 이름, `note`는 글이 있을 때만, `orphanNotes`에 `name`. 시험 `SceneNameTests` 7. Vn.Authoring.Tests 658 · Vn.App.Tests 395 |
| 11-16 | N2 | (N2 커밋) | `NewNames` — 처음 이름 「새 에피소드」 · 「새 장면」 · 「새 챕터」, `NextChapterId`(`ch01`… 대소문자 · 판 이름도 피한다), `NextSceneId`(`scene01`… 주인 없는 항목의 열쇠도 피한다 — 옛 이름 · 메모를 물려받지 않게). [대본]의 장면 추가는 장면ID 자동 + 이름 「새 장면」(한 되돌리기 칸), 에피소드 추가 · 판의 「+ 대사 노드」는 제목 「새 에피소드」, [＋ 챕터]는 이름을 받아 표시 이름으로(`AuthoringSession.CreateNamedChapter`, ID 자동). 상태줄이 ID 대신 이름을 말한다. 시험 `NewNamesTests` 2, `HierarchyEditTests` · `ChapterAddButtonTests` 고침. Vn.Authoring.Tests 660 · Vn.App.Tests 395 |
| 11-16 | N3 | (N3 커밋) | [대본] 하이어아키: 챕터 · 장면 줄이 이름(비면 ID)을 보이고, 두 번 눌러 고치기는 이름만(챕터 `UpdateChapterDisplayName` · 장면 `SetSceneName` — 비울 수 있다, 같은 이름이어도 합쳐지지 않는다). [구성] 하이어아키는 ID. 챕터 차림표의 이름 바꾸기도 표시 이름. 인스펙터 머리 「챕터 · {이름}」 · 「장면 · {이름}」 · 에피소드 「{챕터 이름}챕터 - {장면 이름}」(형식은 그대로 — 이름이 길면 어색하다, 소유자에게 물음), 판의 챕터 이름표, 툴바 챕터 콤보(항목은 ID, 보이는 글은 이름). 시험 `DirectorNamesTests` 2, `ChapterEpisodeFieldsTests` · `HierarchyEditTests` 고침(이름 바꾸기가 ID를 바꾸지 않는다) |
| 11-16 | N4 | (N4 커밋) | [구성] 챕터 인스펙터: 표시 이름 칸을 걷고 「챕터ID」 칸(Enter → `ChapterRenamer`, 이름은 따라간다). 장면 인스펙터: 「장면ID」 칸(Enter → `RenameScene`, 이름 · 메모가 따라간다 · 미지정 장면에는 안 선다). **다른 장면이 쓰는 ID로는 바꾸지 않는다**(합쳐진다 — 합치기는 [대본]에서 끌어 옮긴다), 비울 수도 없다(풀기는 「장면 풀기」). 시험 `StructureIdFieldsTests` 2, `ChapterEpisodeFieldsTests` 고침 |
| 11-16 | N5 · N6 | (N5 · N6 커밋) | N5: `scenes.json`의 장면 `displayName`은 N1에서 이름이 됐다 — 시험(`ChapterScenesExportTests` 이름 · 주인 없는 이름만 항목). N6: `two-graphs.md` 7절 P(이름 ↔ ID 표 · [대본] · [구성] · 새로 만들 때 · scenes.json)와 N · O절의 옛 말, `ARCHITECTURE.md` §2 · §5.1, 작가 안내 둘. **남은 것**: 에피소드 머리 「{챕터 이름}챕터 - {장면 이름}」 형식이 이름에서 어색할 수 있다(소유자 확인), 실제 화면 확인(소유자) |
| 11-16 | N3 뒤 | (커밋) | 소유자: 에피소드 머리 「{챕터 이름}챕터 - {장면 이름}」이 어색하다 → **「{챕터 이름} - {장면 이름}」**(「챕터」 꼬리를 뗐다, 장면이 없으면 챕터 이름만). 시험 넷 고침 |
