# Ked.Progression 반입 기록

이 폴더의 순수 C# 소스는 Unity 런타임 진행 코어의 검증용 사본이다. 여기서 독자적으로
규칙을 수정하지 않는다.

## 현재 기준

- 반입일: 2026-11-18 — 사본이 가진 파일 전부가 아래 커밋과 같다(들일 때 아래 「재반입 검산」으로 견줬다)
- 저장소: `123456789qwaszx/ked-presentation-runtime` — **정본**(소유자 2026-11-18)
- 브랜치: `fix/progressionScene`
- 커밋: `036535be670adb3f4103207169cd3f1210eb1ee7`
- 원천: `Assets/Ked.Progression/Core/**/*.cs` 가운데 사본이 쓰는 것 — `Loading/` · `Spec/` · `State/` · `Transition/` · `Vocabulary/`
- 대상: `src/Ked.Progression/**/*.cs`
- 들이지 않는 것: 런타임에만 있는 장면 실행 — `SceneRun/` · `Contracts/` · `Response/` · `ProgressionDriver.cs` · `ISceneRunner.cs` · `AssemblyInfo.cs`
  (툴은 실행하지 않는다). 사본에만 있는 것: `Reachability/`(도달성 증명 — 저작 전용).
- ⚠ 자동 간선 변경(`ChapterInvariants` · `EpisodeOption`)은 아직 `fix/progressionScene`에만 있고 원본 `ked-progression-runtime`에도 없다(런타임
  `vendoring.md`). 다시 들일 때 다른 브랜치와 견주면 `ChapterInvariants`가 다르다(`EpisodeOption`은 주석만 다르다).
- 지키는 시험은 **지문**(`runtime-sync.txt` — `CoreCopyInStepTests`) 하나다 — 사본을 여기서 고치면 잡는다. 런타임 저장소와는 다시 들일 때
  사람이 아래 「재반입 검산」으로 견준다(이 저장소는 런타임 없이 작가에게 건네는 경우가 많아 옆 폴더를 보는 시험을 두지 않는다).

## 반입 기록

| 날 | 커밋 | 들인 것 |
|---|---|---|
| 2026-11-18 | `036535b` | 갈린 다섯(`Loading/LoadResults.cs` · `Loading/ProgressionLoader.cs` · `Spec/ChapterDefinition.cs` · `Transition/ChapterTransition.cs` · `Transition/ConditionEvaluator.cs`)과 이름이 바뀐 둘(`State/ProgressionState.cs` → `State/ChapterState.cs` · `Spec/ScenarioProgression.cs` → `Spec/ScenarioDefinition.cs` — `FoldChoices` → `ApplyChoices`). 런타임이 11-06~07에 배치를 `Core/`로 옮기며 바꾼 이름과 `ChapterTransition.Resolve`의 인자 검사다 — 판정 규칙은 그대로 · 툴 코드는 이 이름들을 쓰지 않는다 |
| 2026-11-18 | `036535b` | `Spec/ChapterInvariants.cs` · `Spec/EpisodeOption.cs` — 자동 간선의 「같은 장면」 검사가 걷혔다 — 자동 간선이 장면을 넘어도 된다 |
| 2026-09-02 | `d53cc2f5`(`server_DB`, 그때 원천 `Assets/Scripts/Ked.Progression/`) | 사본 전체 — 아래 「이번 반입의 의미 있는 변화」 |

## 반입 경계

다음은 Unity 또는 호스트 전용이므로 사본에 넣지 않는다.

- `*.meta`
- `Ked.Progression.asmdef`
- `Documentation~/`
- `Tests/EditMode/` — Unity NUnit 테스트이며, 에디터에서는 대응하는 xUnit 계약 테스트를 둔다.

`Ked.Progression.csproj`는 .NET/Avalonia 쪽 빌드 경계이므로 이 저장소가 소유한다.

## 이번 반입의 의미 있는 변화

- DTO가 `Ked.Progression.Dto`의 단일 파일에서 `Ked.Progression`의 타입별 파일로 바뀌었다.
- `EndingRule`, `ScenarioAdvance`, `ScenarioTransition`이 제거됐다.
- `EpisodeNode`에 `SceneId`가 추가되고, 빈 값은 `__scene_{EpisodeId}`로 읽힌다.
- `EpisodeOption`에 명시적 `Auto`가 추가됐다.
- 장면 외부 착지점 하나와 Auto 간선 불변식이 로더의 거부 규칙이 됐다.
- `ProgressionState`가 즉시 Commit 대신 Scene 내 선택 Fold를 지원한다.
- `ResolvedOption.SourceIndex`가 원본 `NextOptions` 순서를 보존한다.

## 재반입 검산

원천과 대상의 `*.cs` 상대 경로 및 바이트를 비교한다. 위 제외 목록 외 차이가 있으면
반입이 끝난 것이 아니다. 그 뒤 솔루션 전체 빌드와 테스트를 통과시킨다.
