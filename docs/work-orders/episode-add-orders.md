# 「에피소드 추가」는 장면 줄에서, 길 없이 · 예제 festival_eve를 걷는다 (K0–K2) — 2026-11-18

**소유자**:
- 「에피소드를 우클릭해서 에피소드 추가를 하는 건 별로 좋지 않은 선택 같네요.」 「에피소드 추가를 자동 길로 잇는 것은 기본값이 아니라 … 이게 적절한
  경우가 없어보입니다. 자동길 외에도 선택지라는 개념이 있는데 이걸 자동으로 연결해준다는 게 그렇게 편해보이진 않네요.」 「각각의 에피소드의 내용은
  서로 분리되어 있기 때문에 굳이 연결성을 줘야하나 싶습니다.」 → **장면 줄에만, 길 없이**.
- 「festival_eve 예제를 없앱시다. 불필요합니다.」 → **예제를 걷고 원고는 시험 재료로 옮긴다** · **툴 쪽만**(런타임의 예제는 그대로 — 이제 툴이 다시 내지
  않으므로 런타임이 직접 든다).

> **상태: 진행 중.**

## 1. 지금

- [대본] 하이어아키의 에피소드 줄 우클릭 「에피소드 추가」는 그 에피소드에서 「계속」 길을 이어 새 에피소드를 세운다(`EpisodeAdder` →
  `AddNextEpisode`). 같은 에피소드에서 여러 번 누르면 「계속」 선택지가 셋을 넘어 늘어난다. 장면 줄의 「에피소드 추가」도 그 장면의 마지막
  에피소드에서 길을 잇는다.
- `samples/RuntimeSamples/FestivalEve`는 런타임 예제 `festival_eve`의 원고다(보낼 곳으로 런타임의 `ChapterProgression/`에 진행 JSON을 냈다).
  내보내기 시험 여럿(대사 Yarn · 장면 파일 · 장면 메모 · 설정의 최근 프로젝트)이 이것을 재료로 쓴다.

## 2. 단계

| # | 일 | 자리 |
|---|---|---|
| K0 | 지시서 | 이 문서 |
| K1 | **「에피소드 추가」는 장면 줄에서, 길 없이** — 에피소드 줄의 차림표에서 걷는다. 장면 줄의 것은 그 장면에 길을 잇지 않은 에피소드를 세운다(잇기는 [대본] 판에서 선택지 칸을 끌어서). 이 명령만 쓰던 `ProjectEditor.AddNextEpisode` · `ChapterBranchPlanner.SuggestPlacement` · 문구 「계속」(`EpisodeAdder.DefaultOptionLabel`) · 카드 자리의 부모 인자(`NodePlacement.For`의 `parentEpisodeId`)를 걷는다. 시험 · 문서 | `StoryHierarchy` · `MainWindow.HierarchyEdit` · `EpisodeAdder` · `ProjectEditor.Chapters` · `ChapterBranchPlanner` · `NodePlacement` |
| K2 | **예제 festival_eve를 걷는다** — `samples/RuntimeSamples/FestivalEve`의 예제로서의 것(보낼 곳 · 연출 파일 · 결과 · Yarn · 산출물)을 걷고, 원고(매니페스트 · 판)만 `tests/fixtures`로 옮겨 시험 재료로 쓴다. 예제 시험(`RuntimeSampleTests`의 festival_eve 줄)과 README · 문서의 예제 안내 | `samples/RuntimeSamples` · `tests/fixtures` · 시험 · 문서 |

## 3. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-18 | K0 | (이 커밋) | 지시서 — 소유자의 결정 셋(장면 줄에만 길 없이 · 시험 재료로 옮긴다 · 툴 쪽만) |
