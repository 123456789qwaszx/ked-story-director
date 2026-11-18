# 런타임 예제의 원고 (2026-11-04)

`ked-presentation-runtime`의 예제 챕터를 손 대본 대신 두 툴로 낸다 — 이 폴더가 그 **원고**다.
소유자: *"목표는 런타임 예제를 손 대본 대신 두 툴로 내는 것입니다."*

| 폴더 | 챕터 | |
|---|---|---|
| `PresentationDemo/` | `presentation_demo` 연출 데모 | 에피소드 하나 |

⛔ 축제 전날(`FestivalEve/` · `festival_eve`)은 2026-11-18에 예제에서 걷혔다(소유자 — 불필요, `docs/work-orders/episode-add-orders.md` K2). 원고(매니페스트 ·
판)는 시험 재료로 `tests/fixtures/festival-eve`에 남았다 — 보낼 곳 · 연출 파일 · 결과 · Yarn · 산출물은 걷었다. 런타임의 `festival_eve`는 그대로이고
이제 이 툴이 다시 내지 않는다(런타임이 직접 든다).

- `*.vnproject.json` · `story/` — 원고(챕터 · 에피소드 · 간선 · 조건 · 화자 · 카드의 대사). ⛔ 화자는 2026-11-17에 `game.definition.json`에서
  매니페스트(`speakers`)로 옮겼고 그 파일은 걷었다 — compositor 쪽 사본도 같이 바꾼다.
- `exported/{챕터}.progression.json` — 이 툴이 낸 진행 JSON. 런타임 예제도 같은 이름(`festival_eve.progression.json` · `presentation_demo.progression.json`)을 쓴다.
- `runtime.target.json` — 보낼 곳(2026-11-04 게임 폴더 · 2026-11-17 형식마다). 저장소가 나란히 있으면(`GitHub/ked-story-director` · `GitHub/ked-presentation-runtime`) 상대 경로가 그대로 통해, 저장할 때 진행 JSON이 런타임의 `ChapterProgression/`으로도 나간다(`gameFolder`). compositor가 같은 칸을 읽어 Yarn을 거기로 쓴다. 폴더가 없으면 아무것도 안 쓴다.

연출(무대 명령 · 줄 태그)과 게임용 Yarn은 **ked-story-compositor**가 이 원고의 사본 옆에서 짓고 낸다 —
compositor `samples/RuntimeSamples/README.md`에 손 대본과 다른 점이 있다.
원고를 고치면 compositor 쪽 사본도 같이 바꾼다. 앱에서는 두 툴이 **같은 폴더**를 연다 — 이 폴더를 compositor로 열면 연출 파일이 그 옆에 선다.

`tests/Vn.Authoring.Tests/Chapters/RuntimeSampleTests.cs`가 원고가 검증 오류 없이 읽히고, 다시 낸 진행 JSON이 `exported/`와 같은지 본다.
