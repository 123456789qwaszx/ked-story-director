# 옛 모양을 견디는 코드를 걷는다 (O0–O9) — 2026-11-18

**소유자**: 「이런 쓸모없는 마이그레이션 기능 필요 없습니다. 지금 툴을 제작하는 단계입니다.」 · 「예. 모두 훑어서 걷으십시오.」

지금의 편집 명령(`ProjectEditor*`)이 만들지 않는 모양 — 옛 프로젝트 · 손으로 고친 파일에서만 오는 것 — 을 받아 주고, 고치고, 대신하고,
따로 그리고, 알리던 코드를 흔적 없이 걷는다. [`legacy-read-orders.md`](legacy-read-orders.md) 3절이 그때 「남긴」 너그러움도 대상이다.

> **상태: 진행 중.**

## 1. 규칙

- **그런 모양이 파일에 오면 읽을 때 거절한다** — 저장 검증(`JsonSupport.ValidateProject`, 읽기 · 저장 · 되돌리기가 모두 지난다)과 읽기 한 곳이 한 줄로
  말한다. 화면 · 내보내기 · 검증 보고에서 그 모양을 위한 갈래는 걷는다.
- 모르는 칸은 지나간다(읽기의 기본 — 견디는 코드가 아니다).
- **남기는 것**
  - 챕터 구조 검사(빈 · 겹친 EpisodeId, 없는 에피소드를 가리키는 간선, 겹친 간선, 없는 스탯 · 조건 참조) — 툴의 버그를 잡는 안전망(소유자).
    주석의 「옛 프로젝트」 이야기만 걷는다.
  - 지금 툴도 만드는 모양: 판 밖의 카드(판 넓혀 그리기 — 카드 끌기 · 크기 바꾸기가 판에 묶이지 않는다), [대본]의 IF 포트를 에피소드 카드로
    끌기(공급 경고), 줄의 조건 전환(분기 패널 · 대본 글의 `<<if>>`가 만든다), 두 꼴의 분기 칸(문자열 · 배열), 기본값을 안 쓰는 규칙,
    내보낸 파일의 지난 것 걷기, 보낼 곳의 대사 Yarn 막기.

## 2. 걷는 것

| | 묶음 | 툴이 만드나 | 걷는 것 | 그런 데이터는 |
|---|---|---|---|---|
| O1 | 옛 견본 `samples/Authoring`(챕터가 아닌 판 「1장」 · 표식 없는 카드 · 「1장 설정」 노드) | — | 견본 · `SampleProjectTests`(소유자) | — |
| O2 | 에피소드로 이어진 분기 | 아니다 — 잇는 길은 모두 분기 카드만 받는다(`CanBranchTo`) | 검증 `BranchIntoEpisode` · `ChapterBranchMarker.ToBranchCard` · [구성]의 에피소드로 긋는 점선 | 거절 |
| O3 | 챕터가 아닌 판 | 아니다 — 판은 `EnsureChapterBoard`로만 서고 챕터와 함께 바뀌고 지워진다 | 판 → 챕터 null 갈래(`EpisodeNaming.BoardOf` · 카드 거절 · 칸 없는 투영 · 첫 챕터로 대신하기 …), 판 · 프로젝트 이름의 대신 값 | 거절 |
| O4 | 카드 없는 에피소드 · 표식 없는 에피소드 카드 · 사라진 에피소드를 가리키는 카드 · 같은 표식 둘 | 아니다 — 에피소드와 카드는 한 칸에 서고 함께 지워진다 | 열 때 판 맞추기(`AlignBoardsWithChapters` · 세션 `AlignBoards` — 공급 다시 맞추기 포함), 이름으로 카드를 찾는 뒷길, 화면의 「에피소드도 분기 카드도 아닌 카드」 갈래, 검증 `DialogueEntryNodeMissing` | 거절 |
| O5 | 대사엔트리가 EpisodeId와 다르다 | 아니다 — 늘 같다 | **칸째 걷는다**(소유자) — `ChapterEpisode.DialogueEntry` · `UpdateEpisode`의 인자 · 저장 칸 · 「손으로 쓴 대사엔트리를 지킨다」, 진행 JSON의 `DialogueEntryId`는 EpisodeId 그대로 | 칸은 읽지 않는다(모르는 칸) |
| O6 | 작가 층 「X 설정」 노드(챕터 조건 공급 노드가 아닌 설정 노드) | 아니다 — 설정 노드는 공급 노드뿐이다 | `AvailableConditionSourceKind.SetNode` · `Selectable`, **줄 조건 드롭다운**(소유자 — 줄 조건은 분기 패널과 대본 글의 `<<if>>`로만), 옛 「X 설정」 카드 숨기기 설명, 이름 무늬로 공급 노드를 알아보던 뒷길 | 거절(이름이 `챕터 {판} 조건`이 아닌 설정 노드) |
| O7 | 장면 없는 에피소드 | 아니다 — 화면의 길은 모두 장면을 준다 | `EffectiveSceneId`(→ `SceneId`), 경고 `EpisodeWithoutScene`, 「미지정 · …」 장면(`ChapterScene.IsDefault`), 하이어아키 · [대본] · [구성]의 그 갈래, `AddEpisode` · `AddNextEpisode` · `AddDialogueNode`의 장면 없는 호출(장면을 받는다) | 거절 |
| O8 | 읽기의 너그러움 | 아니다 — 쓰기는 늘 깨끗이 쓴다 | 빠진 칸에 대신 값(이름 · 종류 · 자리 · 끝점 · 스탯 범위 …), 배열의 객체 아닌 항목 건너뛰기, 걸 곳 없는 출구 · 잘못된 열린 분기점 버리기, 모르는 선 모양 · 스탯 종류 · 변화 종류의 기본값, 화자 목록의 빈 · 겹친 이름, 판 크기 들이기, 다른 판의 사라진 분기 대상 | 거절 |
| O9 | 작은 것 · 주석 · 문서 | — | 선택지 사전 밖의 문구 합치기(`ChoiceLabels`), 엑셀 시절 0 자리 다시 배치(`NeedsSeeding`), 걷힌 기능을 말하는 주석, 문서 | — |

각 단계는 빌드(경고 0) · 두 시험 프로젝트가 모두 통과한 채로 커밋한다. 시험 견본(`tests/fixtures/chapter-samples`)은 지금 모양으로 다시 짠다.

## 3. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-18 | O0 | (이 커밋) | 지시서 — 조사(코드 갈래 목록)와 소유자의 네 결정(대사엔트리 칸 · 줄 조건 드롭다운 · 구조 검사 · 옛 견본) |
| 11-18 | O1 | (이 커밋) | 옛 견본 `samples/Authoring`과 `SampleProjectTests`(6)를 걷었다. 지금 모양의 견본은 `samples/RuntimeSamples` 둘이다. 설정 시험의 최근 프로젝트 경로는 축제 전날 견본으로. Authoring 694 · App 450 |
| 11-18 | O2 | (이 커밋) | 에피소드로 이어진 분기 — 저장 검증(`JsonSupport.ValidateProject` — 읽기 · 저장 · 되돌리기)이 분기 카드가 아닌(또는 없는) 분기 대상을 거절한다. 검증 `BranchIntoEpisode`, 표식의 `ToBranchCard`, [구성]이 에피소드 카드로 긋던 점선 갈래를 걷었다. 시험 — 오류를 보던 시험을 「받지 않는다」로. Authoring 694 · App 450 |
| 11-18 | O3 | (이 커밋) | 챕터가 아닌 판 — 저장 검증이 챕터 없는 판 · 한 챕터의 판 둘을 거절한다. `EpisodeNaming.ChapterOfBoard` · `ChapterOf`는 늘 챕터를 낸다(없으면 예외 — 불변식이 깨진 것). 판 → 챕터 null 갈래를 걷었다 — 카드 세우기의 거절, 투영의 「칸 없는 판」 · 「장면 표 없는 판」, [대본]의 챕터 아닌 판 이름표 · 장면 틀 · 레일 · 분기 팔레트 · 삭제 단추 · 에피소드 찾기 · 새 카드의 챕터, 하이어아키 · 인스펙터 머리글, 분기 조건 표 · 고르기, 챕터 지우기의 「챕터 없는 판」. 시험 — 일부러 「본편」 판을 더하던 준비물 셋에서 그 판을 걷고, 판 A · B를 쓰던 준비물에 챕터를 붙였다. 걷힌 갈래를 보던 시험 하나(`챕터를_못_찾는_판은_묶지_않는다`)를 지우고 거절 시험 하나를 세웠다. Authoring 694 · App 450 |
| 11-18 | O4 | (이 커밋) | 에피소드 ↔ 카드 — 저장 검증이 「에피소드마다 그 챕터 판에 표식 카드 하나 · 에피소드 카드는 그 챕터의 에피소드를 가리킨다 · 분기 카드는 표식이 없다」를 지킨다. 열 때 판 맞추기(`AlignBoardsWithChapters` · `BoardAlignment` · 세션 `AlignBoards` — 공급 다시 맞추기 포함), 이미 있는 에피소드에 표식만 붙이던 카드 세우기의 뒷길, 이름으로 카드를 찾던 뒷길(`EpisodeNaming.EpisodeFor` · `NodeNameOf` · `ChapterBoard`의 이름 찾기 · 같은 표식 둘 견디기), 검증 `DialogueEntryNodeMissing`, 화면의 「에피소드도 분기 카드도 아닌 카드」 갈래(레일 · 선택지 칸 · 이름 칸 · 놓기 안내)를 걷었다. 편집 명령의 틈 셋을 막았다 — `RemoveEpisode`가 카드까지 한 칸에 걷고(`EpisodeDeleter`는 그것을 부른다), `RenameEpisode`가 카드의 표식을 옮기고, `RemoveNode`는 에피소드 카드를 거절한다(에피소드와 함께 지운다). 시험 견본(챕터만 든 파일)은 시험 쪽 `SampleChapters.StandCards`가 판 · 카드 · 공급을 세운다. 시험 — `BoardAlignmentTests` · `OpenAlignmentTests` · 판 맞추기 · 카드 없는 챕터 내보내기 시험(넷) · 이름 뒷길 시험을 지웠고, 일부러 반쪽 모양을 짓던 준비물(카드를 따로 세워 붙이기 · 카드만 지우기 · 이름만 맞춘 카드)을 지금 길로, 새 `EpisodeCardInvariantTests` 4. Authoring 682 · App 448 |
| 11-18 | O5 | (이 커밋) | 대사엔트리 칸째 — `ChapterEpisode.DialogueEntry` · 저장 칸 `dialogueEntry`(이제 읽지도 쓰지도 않는다) · `UpdateEpisode`의 인자 · 개명의 「손으로 쓴 대사엔트리 지키기」 · 구조 검사 `DialogueEntryBlank` · [구성] 툴팁 줄을 걷었다. `DialogueEntryId`(= Yarn 노드 타이틀)는 EpisodeId에서 짓는다(`ChapterProgressionExporter.DialogueEntryIdOf(episode)` — 판 카드의 이름에서 짓던 길 · `ChapterBoard.EpisodeNodeNameFor`를 걷었다). 에피소드 카드의 이름은 늘 EpisodeId다 — 저장 검증이 지키고, `RenameEpisode`가 카드의 이름까지 옮기며(`EpisodeRenamer`는 그것을 부른다), `RenameNode`는 에피소드 카드를 거절한다. 견본 — 시험 견본 ch01 · ch05의 `dialogueEntry`를 걷었고, 런타임 견본 둘은 툴로 다시 저장했다(칸만 빠졌다). 건네는 표본 `docs/ch01.progression.sample.json`은 다시 냈다 — `DialogueEntryId` 넷이 `Story_ch01_…` → EpisodeId. 시험 — 대사엔트리를 보던 시험(규약 · 손으로 적기 · 빈 칸 · 판 노드 개명 · 남의 판)을 지우고, 이름 규칙 시험은 EpisodeId로 다시 짰다. Authoring 676 · App 448 |
| 11-18 | O6 | (이 커밋) | 옛 「X 설정」 노드 · 줄 조건 드롭다운 — 저장 검증이 설정 노드를 「판의 챕터 조건 공급 노드 하나」로만 받는다. [대본] 카드 인스펙터의 줄 조건 드롭다운(분기 카드의 「조건」 칸 · 전환 태그의 고르기)을 걷었다 — 줄 조건은 분기 패널과 대본 글의 `<<if>>`로만(소유자). 전환 태그는 보이기만 한다. 그 드롭다운만 쓰던 `ConditionChoices`, 조건의 출처 구분(`AvailableConditionSourceKind` · `Selectable` — 작가 층 대 챕터 층), 이름 무늬로 공급 노드를 알아보던 뒷길(`IsConditionSupplyNode` · `IsConditionSupplyNodeName` · `ConditionSupplyNodeIds`), 쓰는 곳 없던 `RemoveCondition` · `AddSetNode`의 기본 이름(`NextName`), 챕터 개명의 「새 이름의 배관이 이미 있으면」 갈래를 걷었다. `AddSetNode` · `AddCondition` · `UpdateCondition`은 공급만 부르는 `internal`이다. 시험 — `Sample`의 조건은 챕터 조건(공급 사본)이다. `ConditionChoiceTests` · `ChapterLayerSeparationTests` · 같은 판 설정 노드 둘을 보던 공급 시험 둘 · 옛 설정 노드 시험을 지웠고, 작가 설정 노드를 손으로 세우던 준비물(대본 붙여넣기 · 카드 글 · 개명 · 세션)을 챕터 조건으로, 거절 시험 하나. Authoring 666 · App 447 |
| 11-18 | O7 | (이 커밋) | 장면 없는 에피소드 — 읽기가 빈 `sceneId`를 거절하고 저장 검증도 그것을 지킨다. `ChapterEpisode.EffectiveSceneId`(→ `SceneId` 하나)와 혼자짜리 기본 장면 `__scene_{Id}`, 「미지정 · …」 장면(`ChapterScene.IsDefault` · `DefaultSceneName` · `ChapterSceneNotes.ShownSceneName`), 경고 `EpisodeWithoutScene` · `SceneDiagnostics`, 투영의 「챕터 직속」 묶음(`CollapsedSceneGroup.ChapterDirect`), 하이어아키의 「장면 미지정」 곁글 · 챕터 직속 줄 · 경고 표식 · 기본 장면 거르기, [대본]의 「장면을 하나도 안 적은 판에는 장면 틀 · 루트 색을 안 그린다」 · 새 카드가 장면 없는 카드의 장면을 피하던 갈래, [구성]의 장면ID 칸 숨기기 · 기본 장면 이름 거르기, 에피소드 개명이 기본 장면의 메모를 데려가던 갈래, 쓰는 곳 없던 `UpdateEpisodeScenes`를 걷었다. 편집 명령이 장면을 꼭 받는다 — `AddEpisode` · `AddNextEpisode` · `AddDialogueNode` · `EpisodeAdder.Add`의 `sceneId`가 필수 인자이고(비면 거절), `UpdateEpisode`는 장면ID를 비우지 않는다. 견본 — 시험 견본 ch05의 에피소드 여섯에 장면(= EpisodeId)을 적었다(그래서 [구성] 검증 보고의 경고가 7 → 1). 시험 — 걷힌 갈래를 보던 시험 열넷(미지정 이름 · 빈 장면 묶기 · 챕터 직속 줄 · 틀 · 프록시 · 경고 · 메모 · 한꺼번에 옮기기)과 머리글 Theory의 장면 없는 두 줄을 지웠고, 장면 없이 세우던 준비물은 장면을 주며(하이어아키 견본 ch02는 장면 street), 기본 장면을 보던 시험 하나를 거절 시험으로 다시 짰다. Authoring 654 · App 443 |
| 11-18 | O8 | (이 커밋) | 읽기의 너그러움 — 읽기가 쓰는 모양만 받는다(`JsonSupport`의 엄격한 읽기: 늘 쓰는 칸은 꼭 있고 모양이 맞아야 하고, 비어서 안 쓰는 칸만 없어도 된다 — 있으면 빈 글자가 아니어야 하고, 켤 때만 쓰는 깃발은 `true`뿐). 걷은 것: 빠진 칸의 대신 값(노드 Id · 이름 「이름 없음」 · 종류 · 자리 0, 파일 이름 「이름 없는 파일」 · 프로젝트 제목 「제목 없음」, 에피소드 자리, 간선 끝점, 스탯 범위 · 변화량, 대본의 locale · 판 · 줄 판 · 대사, 스냅숏의 형식 · 경로), 배열 · 사전의 객체 아닌 항목 건너뛰기(챕터 안 · 판의 노드 · 대본 줄 · locale · 설정 노드 조건 · 화자), 모르는 선 모양 · 스탯 종류 · 변화 종류의 기본값, 빈 글자 칸을 「없음」으로 읽기, 분기(`detour`)의 빈 · 겹친 · 문자열 아닌 대상 버리기, 어긋난 `openDetours` 자리 버리기 · 뒤로 물리기(모델의 「남은 분기를 뒤에 붙이기」도), 닫는 전환에 붙은 출구 · 앵커 없는 꼬리 출구 무시, 화자 목록의 빈 · 겹친 이름 건너뛰기와 공백 걷기(저장 검증이 거절한다), 판 크기 들이기(범위 밖이면 거절), 장면 메모의 빈 항목 무시, 쓰는 곳 없던 `ProjectStore.DefaultRelativePath`. **다른 판의 분기 대상** — `CanBranchTo`가 판을 안 봐 [대본](모든 챕터를 한 캔버스에 그린다)에서 다른 챕터의 분기 카드에도 이어졌고 [구성]은 그 분기를 그릴 카드가 없어 뺐다. 이제 분기는 같은 판의 분기 카드에만 잇고(`CanBranchTo(from, to)` · 놓기 거절의 까닭 `IsBranchCardElsewhere`) 저장 검증도 그것을 지킨다 — [구성]의 「다른 판으로 가는 분기 빼기」와 [대본] 인스펙터의 「(사라진 노드)」 갈래를 걷었다. 그대로 둔 것: 보낼 곳 파일(`runtime.target.json`)의 너그러운 읽기 — compositor와 함께 쓰는 설정이라 모르는 칸을 지키고 깨지면 없는 것으로 본다(옛 모양이 아니다). 시험 — 너그러움을 보던 넷(빈 조건 · 화자 · 판 크기 · 다른 판 분기)을 거절 시험으로 다시 짰고, 다른 챕터 분기 카드 거절 시험 하나, 제목 없는 매니페스트 준비물에 제목. Authoring 657 · App 443 |
