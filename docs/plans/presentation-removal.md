# 연출 층을 걷는다 — 무대는 `ked-story-compositor`로

> 2026-10-29 소유자: *"이제 무대 프리뷰는 ked-story-compositor 이 레포로 옮겨 주시겠습니까? ked-story-director에서는
> 무대 프리뷰를 사용하지 않을 것입니다."*
>
> 같은 날 정한 것(소유자 선택): compositor는 이 저장소를 **이력째** 옮기고 무대 쪽만 남긴다 · 여기서는 **연출 층 전체**를
> 걷는다 · 게임이 읽는 **Yarn(대사 + 연출 명령)은 compositor만 낸다** · 대사 **발행**도 compositor로 옮긴다.

`director-split.md` D5("무대 프리뷰 · 에셋 탐색기는 연출가의 화면이라 따라온다")를 뒤집는 결정이다. 이 툴은 이야기의 뼈대와
대사 글을 쓰는 곳이 된다 — 챕터 그래프 · 연출 그래프(배치 · 선택지 · 분기 · 조건) · 카드 안 글 칸 · 곁기둥의 줄 편집.

## 1. 경계

| | director (이 저장소) | compositor |
|---|---|---|
| 프로젝트 파일 | **원고 — 이 툴만 쓴다.** 챕터 · 판 · 대사 노드 · 설정 노드 · 대본 · 화자 · 조건 · 설정 배선 | 읽기만 한다 |
| 연출 데이터 | 없다 | 자기 파일 — 연출 노드 · 명령 공급 · 연출 배선 · 발행본(대사 · 연출) · 곡선 · 빠른 명령 · 에셋 설정 |
| 출력 | 챕터 진행 JSON | 게임용 Yarn 묶음 · 곡선 · 문서(CSV) |
| 발행(`vN`) | 없다 | 한다 — director의 대사를 읽어 고정본을 남긴다 |

## 2. 걷는 것

| 층 | 무엇 |
|---|---|
| 화면 | 무대 프리뷰 탭(`MiniStagePreview` · `StageSceneView` · 재생 · 표시 · 곡선 편집 · 연출 스크립트 패널) · 연출 노드 편집기 · 에셋 탐색기 · 대사 편집기의 무대 연동(재생 · 줄 따라가기 · 분기 복귀 재생) · 카드 더블클릭 → 무대 · 셸의 연출 채널 · Yarn/CSV 내보내기 단추 |
| 출력 | 라이브 Yarn 출력(`LiveOutputService` · `LiveNodeComposer`) · Yarn 묶음 · 곡선 · CSV 묶음 · 출력 장부 · 컴파일 검증 |
| 저작 | 연출 흐름(무대 접기 · 재생 경로 · 전환 · 페이드 · 모션 · 곡선 굽기 · 연출 바인딩 · 쓸 수 있는 명령) · 연출 편집 명령 · 연출 채널 · 발행(대사 · 연출 결과와 저장소) · 연출 명령 카탈로그 · 에셋(초상 · 미리보기 · 런타임 튜닝) |
| 모델 | 연출 노드 · 명령 공급 노드 · 연출 · 명령 공급 배선 · 결과 · 곡선 · 빠른 명령 · 에셋 설정 · 출력 형식 |
| 프로젝트 | `Ked.Presentation.Core` 와 그 테스트 |

남는 것: 대본 · 대사 흐름(조건 · 분기 · 선택지) · 대사 미리보기 중 연출과 무관한 것(카드 글 칸의 ScenarioOnly 등) · 챕터 ·
진행 JSON · 검증.

## 3. 옛 프로젝트

연출을 담은 옛 프로젝트를 이 툴로 열면 연출 데이터는 읽지 않고, 저장하면 사라진다. 그래서 **연출 데이터를 버리기 전에 원본을
한 번 남긴다**. compositor는 그 원본에서 연출을 가져온다(compositor C2).

R3에서 정한 방법(`ProjectStore.Load`):

- **알아보기** — manifest에 연출 키(`results` · `compositions` · `easeCurves` · `quickCommands` · `recentCommands` · `assetRoots`)나
  연출 배선(`commandSupply` · `presentationSupply` · 형식 2의 `presentation`)이 있거나, StoryFile에 연출 노드(`presentation` ·
  `commandSupply`)가 있으면 "연출을 담은 옛 프로젝트"다. 그 데이터는 모델에 들어오지 않는다.
- **남기기** — **열 때** manifest 옆 `presentation-original/`에 manifest · 대본 · StoryFile · 발행 결과 파일 · `game.definition.json`을
  같은 상대 경로로 복사한다. 저장 때가 아니라 열 때인 것은 "버리기 전에"를 확실히 하려는 것이다 — 그 뒤 무엇을 누르든 원본은 이미 있다.
- **두 번 남기지 않는다** — 열기만 하고 닫으면 다음에 또 연출을 만난다. 이미 있는 폴더의 내용이 지금 원본과 바이트까지 같으면 그
  폴더를 그대로 쓰고, 다르면(옛 툴에서 그 사이 고쳤다) `presentation-original-2`, `-3`…을 단다. 먼저 남긴 원본은 덮지 않는다.
- **알리기** — 상태줄에 "연출 데이터는 읽지 않습니다 — 원본을 'presentation-original' 폴더에 남겼습니다(연출은
  ked-story-compositor에서)"가 붙는다.
- 한 번 저장하면 파일에 연출이 없으니 다음부터는 아무것도 남기지 않는다. 옛 `results.vnresults.json`은 지우지 않는다(manifest가
  더 가리키지 않을 뿐이다).

`game.definition.json`의 연출 카탈로그(`presentationCommandCategories` · `presentationCommands`)는 이 툴이 읽지 않지만 **지우지도
않는다** — 화자를 저장할 때도 다른 키는 그대로 둔다(그 파일은 두 툴이 함께 읽는다).

**남은 이름** — 대사 노드를 펼친 모양은 여전히 `DialogueResult` · `DialoguePublisher.Draft` · `PublishProblem`이라는 이름이다.
미리보기 · 카드 글 칸 · 흐름 해석이 같은 펼침을 읽는다. 발행(`vN` · 내용 해시 · 결과 저장소)은 없다 — 이름만 그 시절의 것이다.

## 4. 단계

한 단계 = 커밋 하나(또는 몇 개), CI(Windows) 녹색을 보고 다음으로.

| | 무엇 | 상태 |
|---|---|---|
| R0 | 이 문서 · compositor 반입(`4e6d3a2` 이력째 — compositor run 1 · 계획 문서 `0658d61` run 2, 둘 다 녹색) | R1과 함께 `8949b2b` |
| R1 | 화면 — 무대 프리뷰 탭과 그것만 쓰는 화면 · 대사 편집기의 무대 연동 · 카드 더블클릭 · 세션의 에셋 · 튜닝 · 오디오 · NAudio · 내장 기본 튜닝 | `8949b2b` (run 21 · 60 · 356 · 963 · 264) |
| R2 | 출력 — 라이브 Yarn · 내보내기 단추 · 양식 선택 · 출력 검증 · CSV 묶음 · 모델의 내보내기 양식 · 출력 폴더 (Yarn 묶음 · 곡선은 연출과 얽혀 R3에서) | `d26fb60` (run 22 · 60 · 356 · 951 · 250) |
| R3 | 저작 · 모델 — 연출 흐름(무대 접기 · 재생 경로 · 전환 · 모션 · 곡선 굽기) · 연출 편집(명령 · 빠른 명령 · 곡선 · 에셋 폴더) · 발행과 결과 저장소 · 결과 조합 · 연출 노드 · 명령 공급 노드 · 연출 배선 · 연출 카탈로그 · Yarn 이미터(노드 이름 규칙만 `YarnNaming`으로 남김) · 출력 장부 · 미리보기의 연출 조각과 Direction Sheet · 카드의 "vN 발행" · 직렬화 · 옛 프로젝트(§3) · `Vn.Authoring`의 `Ked.Presentation.Core` 참조 · R1이 남긴 더블클릭 시험 도우미 | 이번 |
| R4 | `Ked.Presentation.Core` · 문서(README · two-graphs) | |
