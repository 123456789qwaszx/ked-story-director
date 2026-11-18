# 코어 사본을 런타임에 맞추고 맞대조를 되살린다 (V0–V2) — 2026-11-18

**소유자**: 「그 다섯 파일만 들이고 맞대조 시험을 되살립시다.」 정본은 `ked-presentation-runtime`(소유자).
⛔ 같은 날 뒤집었다 — 「맞대조 시험은 제거하는게 맞겠습니다. 이 ked-story-director만 별도 시나리오 작가에게 툴로 제공하는 경우가 많을
것이기 때문입니다.」(V2). 들인 다섯 파일은 그대로다.

> **상태: 끝 (2026-11-18).**

## 1. 지금

- 사본(`src/Ked.Progression`)은 2026-09-02(`d53cc2f5`)에 떴고, 2026-11-18에 `ChapterInvariants` · `EpisodeOption` 두 파일만 런타임
  `fix/progressionScene` `036535b`에서 들였다(`auto-edge-across-scenes-orders.md`).
- 코드가 다른 파일이 다섯 남았다 — `Loading/LoadResults.cs` · `Loading/ProgressionLoader.cs` · `Spec/ChapterDefinition.cs` ·
  `Transition/ChapterTransition.cs` · `Transition/ConditionEvaluator.cs`. 런타임이 타입 이름을 바꿨기 때문이다(`ProgressionState` →
  `ChapterState`, `ScenarioProgression` → `ScenarioDefinition`, `FoldChoices` → `ApplyChoices`) — 그리고 `ChapterTransition.Resolve`의 인자 검사.
  판정 규칙은 같다. 툴 코드는 이 이름들을 쓰지 않는다.
- **맞대조 시험(`CoreCopyInStepTests`)이 조용히 건너뛰고 있었다** — 옆 폴더 `ked-progression-runtime`의 `Assets/Progression/Runtime`을 찾는데,
  그 자리가 11-06~07에 `Assets/Ked.Progression/Core`로 옮겨졌다. 찾지 못하면 건너뛰는 설계라 아무도 몰랐다.

## 2. 단계

| # | 일 |
|---|---|
| V0 | 지시서 |
| V2 | **맞대조를 걷는다**(소유자 — 이 저장소만 작가에게 건넨다) — `CoreCopyInStepTests`의 맞대조와 그 손잡이(옆 폴더 · 환경 변수 `KED_PRESENTATION_RUNTIME` · 코어 자리). 지문은 남긴다(이 저장소 안에서만 돈다 — 사본을 여기서 고치면 잡는다). 런타임이 바뀐 것은 옮겨 올 때 사람이 견준다(`UPSTREAM.md`의 「재반입 검산」) |
| V1 | **다섯 파일을 들이고 맞대조를 되살린다** — 다섯을 런타임 `036535b`의 것으로(바이트 그대로). 다섯이 새 이름을 부르므로 옛 이름의 두 파일(`State/ProgressionState.cs` · `Spec/ScenarioProgression.cs`)이 새 이름의 파일(`State/ChapterState.cs` · `Spec/ScenarioDefinition.cs`)로 바뀐다 — 이름 바꿈과 `ApplyChoices`뿐이고 더 끌고 오는 것은 없다. 런타임에만 있는 장면 실행(`SceneRun/` · `ProgressionDriver` · `Contracts/` …)은 들이지 않는다. 맞대조 시험은 옆 폴더 `ked-presentation-runtime`의 `Assets/Ked.Progression/Core`와 맞대고(환경 변수 `KED_PRESENTATION_RUNTIME`이 먼저), **런타임 저장소는 있는데 코어 자리가 없으면 실패한다**(건너뛰지 않는다 — 배치가 또 바뀌어도 조용히 지나가지 않게). 지문 · `UPSTREAM.md` · 문서 |

## 3. 알아 둘 것

- 맞대조는 옆 폴더 런타임의 **지금 체크아웃**과 견준다. 자동 길 변경(`036535b`)은 아직 `fix/progressionScene`에만 있으므로, 그 브랜치가 합쳐지기 전에
  다른 브랜치를 꺼내 두면 `ChapterInvariants`가 갈렸다고 나온다(`EpisodeOption`은 주석만 다르다) — 그것이 맞는 알림이다.

## 4. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-18 | V0 | (이 커밋) | 지시서 |
| 11-18 | V1 | (이 커밋) | 다섯 파일과 이름이 바뀐 둘(`ChapterState` · `ScenarioDefinition`)을 런타임 `036535b`에서 바이트 그대로 들였다 — 옛 이름의 둘은 걷혔다. 맞대조 시험은 옆 폴더 `ked-presentation-runtime`의 `Assets/Ked.Progression/Core`와 맞대고(`KED_PRESENTATION_RUNTIME`이 먼저), 저장소는 있는데 코어 자리가 없으면 실패한다. 확인 — `036535b`와는 맞고, 자동 길 변경 전 커밋(`511e655`)과는 `ChapterInvariants`를 짚고, 코어 자리가 없는 폴더는 실패한다. 지문 · `UPSTREAM.md`(반입 기록 표) · `runtime-contract.md`(맞대조 · 정본). Authoring 648 · App 442 |
| 11-18 | V2 | (이 커밋) | 맞대조를 걷었다 — 시험 하나(`런타임_저장소가_옆에_있으면_맞대조한다`)와 그 손잡이. 지문 시험은 남겼다. `UPSTREAM.md` · `runtime-contract.md` · `ARCHITECTURE.md`의 맞대조 줄. Authoring 647 · App 443 |
