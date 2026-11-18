# 자동 길이 장면을 넘는다 — 툴이 따라간다 (X0–X2) — 2026-11-18

**런타임이 먼저 바꿨다** — `ked-presentation-runtime` `fix/progressionScene` @ `036535b`
(`docs/PLAN_auto-edge-across-scenes.md`). 자동 간선의 규칙 넷 가운데 넷째(같은 장면)를 걷었다. 장면 경계에서 일어나는 일(무대
비우기 · 확정 · 저장)은 묻는 선택지로 넘을 때와 같고 박스만 뜨지 않는다. 자동(`Auto`)도 장면 소속(`SceneId`)도 저작자가 적은
것이라 런타임이 몰래 정하는 것이 없다. 남은 규칙 셋: 그 에피소드의 유일한 간선 · 조건 없음 · 스탯 변화 없음.

툴은 그때까지 장면을 넘는 자동 길을 세 겹으로 막았다 — [대본]에서 문구를 지울 때와 문구 없는 칸을 다른 장면 카드로 끌 때 거절하고,
[구성] 검증이 `AutoEdgeCrossesScene`으로 짚고, 내보낼 때 툴의 코어 사본(옛 규칙)이 거부했다. 런타임 계획서 §5의 B1~B3을 한다.

> **상태: 진행 중.**

## 1. 규칙

- **코어 사본은 런타임에서 옮겨 올 뿐 여기서 고치지 않는다.** 이번에는 바뀐 두 파일만 들인다(아래 3절 — 사본 전체는 따로).
- 툴은 새 규칙을 짓지 않는다 — 코어의 규칙 셋을 그대로 미리 말한다(`ChapterAutoEdgeCheck`).
- 단계마다 빌드(경고 0) · 두 시험 프로젝트가 통과한 채로 커밋한다.

## 2. 단계

| # | 일 | 자리 |
|---|---|---|
| X0 | 지시서 | 이 문서 |
| X1 | **규칙을 걷는다** — 코어 사본의 `Spec/ChapterInvariants.cs` · `Spec/EpisodeOption.cs`를 런타임 것으로(바이트 그대로) · `runtime-sync.txt` · `UPSTREAM.md`. 툴의 미리 알림 — `ChapterAutoEdgeCheck`의 같은 장면 검사와 진단 코드 `AutoEdgeCrossesScene`, [대본] 선택지 문구를 지울 때의 거절(`MainWindow.LineInspector`), 문구 없는 칸을 다른 장면 카드로 끌 때의 거절(`GraphEditorView`). 시험 — 거절을 보던 것을 「받는다」로 | `src/Ked.Progression` · `Chapters/ChapterAutoEdgeCheck.cs` · `ChapterDiagnostic.cs` · `MainWindow.LineInspector.cs` · `GraphEditorView.axaml.cs` · 시험 |
| X2 | **문서** — `runtime-contract.md`의 규칙 ④, `chapter-layer-guide.md`, `design/two-graphs.md`(7절 AG), 주석 | 문서 |

## 3. 이번에 하지 않는 것

- **소유자가 정할 것**(런타임 계획서 B4) — ① [대본] 「에피소드 추가」가 앞 에피소드의 유일한 길로 이을 때 「계속」인가 자동 길인가(이 명령은
  같은 장면 안에만 세우고, 「장면 추가」는 길을 잇지 않는다) ② 예제(`samples/RuntimeSamples/FestivalEve`)에 장면을 넘는 자동 길을 두고
  다시 내는가 — 지금 견본의 장면 경계(`Festival_Wrapup → Festival_Staffroom`)는 선택지 셋(조건 · 스탯)이라 자동 길이 될 수 없어
  새 장면 · 에피소드가 든다.
- **코어 사본 전체 다시 반입** — 사본은 2026-09-02(`d53cc2f5`)에 떴고 그 뒤 런타임에서 이름이 바뀌었다(`ProgressionState` → `ChapterState`,
  `ScenarioProgression` → `ScenarioDefinition`, 배치 `Assets/Ked.Progression/Core`). 코드가 다른 파일은 이번 둘 밖에 다섯
  (`LoadResults` · `ProgressionLoader` · `ChapterDefinition` · `ChapterTransition` · `ConditionEvaluator` — 이름 바꿈과 인자 검사). 맞대조
  시험(`CoreCopyInStepTests`)은 옛 자리(`ked-progression-runtime/Assets/Progression/Runtime`)를 찾아 건너뛰고 있었다. 무엇을 정본으로
  맞댈지(원본 `ked-progression-runtime` 또는 사본 `ked-presentation-runtime` — 이번 변경은 사본에만 있다)는 소유자가 정한다.

## 4. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-18 | X0 | (이 커밋) | 지시서 |
| 11-18 | X1 | (이 커밋) | 코어 사본 `Spec/ChapterInvariants.cs` · `Spec/EpisodeOption.cs`를 런타임 `036535b`에서 바이트 그대로(코드 차이는 `VerifyAuto`의 같은 장면 검사뿐이었다) · `runtime-sync.txt` · `UPSTREAM.md`(부분 반입 기록 — 나머지는 2026-09-02 그대로). `ChapterAutoEdgeCheck`의 같은 장면 검사와 진단 코드 `AutoEdgeCrossesScene`(쓰지 않게 된 에피소드 인자도), [대본] 문구를 지울 때의 거절, 문구 없는 칸을 다른 장면 카드로 끌 때의 거절(쓰지 않게 된 `SceneOf`도)을 걷었다. 시험 — 거절을 보던 셋을 「받는다」로(검증 · 끌기 · 문구 지우기), 내보내기 관문이 받는 시험 하나. Authoring 656 · App 442 |
