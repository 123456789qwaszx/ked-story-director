# 런타임 예제의 원고 (2026-11-04)

`ked-presentation-runtime`의 예제 두 챕터를 손 대본 대신 두 툴로 낸다 — 이 폴더가 그 **원고**다.
소유자: *"목표는 런타임 예제를 손 대본 대신 두 툴로 내는 것입니다."*

| 폴더 | 챕터 | |
|---|---|---|
| `FestivalEve/` | `festival_eve` 축제 전날 | 장면 둘(교실 · 교무실 앞) · 에피소드 다섯 · 분기 카드 다섯 · 스탯 둘(성실 · 요령) · 간선 일곱(선택지 다섯 — 숨김 하나 · 잠금 하나 — 와 자동 둘) |
| `PresentationDemo/` | `presentation_demo` 연출 데모 | 에피소드 하나 |

- `*.vnproject.json` · `story/` · `game.definition.json` — 원고(챕터 · 에피소드 · 간선 · 조건 · 카드의 대사).
- `exported/{챕터}.progression.json` — 이 툴이 낸 진행 JSON. 런타임의 `FestivalEve.progression.json` · `Demo_Presentation.progression.json`이 된다.

연출(무대 명령 · 줄 태그)과 게임용 Yarn은 **ked-story-compositor**가 이 원고의 사본 옆에서 짓고 낸다 —
compositor `samples/RuntimeSamples/README.md`에 런타임에 넣는 법과 손 대본과 다른 점이 있다.
원고를 고치면 compositor 쪽 사본도 같이 바꾼다.

## 손 대본에서 바뀐 모양

- 조건 블록(`<<if>>` · `<<elseif>>` · `<<else>>`)은 **분기 카드**다 — 부르는 카드의 줄 앞에 분기 표식, 조건은 다녀갈 카드의 첫머리
  (챕터 조건 시트의 라벨). else 쪽은 여집합 조건(`;`으로 잇는 AND)이다.
- 에피소드 Id = 카드 이름(`Festival_Classroom` …). 챕터 표시 이름 · 에피소드 제목은 2026-11-04에 되살린 칸이다.
- 주석은 없다.

`tests/Vn.Authoring.Tests/Chapters/RuntimeSampleTests.cs`가 원고가 검증 오류 없이 읽히고, 다시 낸 진행 JSON이 `exported/`와 같은지 본다.
