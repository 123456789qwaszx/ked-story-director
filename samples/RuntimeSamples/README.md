# 런타임 예제의 원고

`ked-presentation-runtime`의 예제 챕터를 손 대본 대신 이 툴로 낸다 — 이 폴더가 그 **원고**다.

| 폴더 | 챕터 | |
|---|---|---|
| `PresentationDemo/` | `presentation_demo` 연출 데모 | 에피소드 하나 |

- `*.vnproject.json` · `story/` — 원고(챕터 · 에피소드 · 간선 · 조건 · 화자 · 카드의 대사).
- `exported/{챕터}.progression.json` — 이 툴이 낸 진행 JSON. 런타임 예제도 같은 이름(`presentation_demo.progression.json`)을 쓴다.
- `runtime.target.json` — 보낼 곳. 저장소가 나란히 있으면(`GitHub/ked-story-director` · `GitHub/ked-presentation-runtime`) 상대 경로가 그대로 통해,
  저장할 때 진행 JSON이 런타임의 `ChapterProgression/`으로도 나간다(`gameFolder`). 폴더가 없으면 아무것도 안 쓴다.

연출(무대 명령 · 줄 태그)이 든 게임용 Yarn은 이 툴이 내지 않는다 — 런타임 예제의 Yarn은 런타임 저장소에 있다.

`tests/Vn.Authoring.Tests/Chapters/RuntimeSampleTests.cs`가 원고가 검증 오류 없이 읽히고, 다시 낸 진행 JSON이 `exported/`와 바이트까지 같은지 본다.
