# 기본 출구를 걷는다 — 2026-11-17

**소유자 보고**: [대본] 분기 카드 왼쪽 위에 「기본」이라는 원과 점이 있다 — 무엇인지 조사하고 필요 없으면 지운다.

## 조사

- 「기본」은 **기본 출구**(`DialogueNode.DefaultExitTargetNodeId` · 판 파일 `defaultExit`)였다 — 「이 카드를 다 읽으면 갈 다음 카드」. 챕터 간선이 흐름을
  갖기 전, 자유 씬 시절의 배선이다. 대사 Yarn에는 `<<jump>>`로 나갔다(`RenderedSegmentKind.DefaultJump`).
- 에피소드 카드는 이미 숨겼다(흐름은 챕터 간선의 일 — T2). 숨기는 조건이 「에피소드 카드」였고, 분기 카드는 에피소드가 아니라(2026-11-17)
  그 조건에 걸리지 않아 보였다.
- 분기 카드는 들렀다 **돌아오는** 카드라 기본 출구를 이으면 돌아오는 대신 `<<jump>>`로 다른 곳에 간다 — 분기 카드의 뜻과 어긋난다.
- 분기 카드에서 숨기면 기본 출구를 쓰는 화면이 하나도 남지 않는다(챕터가 아닌 판은 툴이 더 만들지 않는다).

## 정한 것 (소유자 2026-11-17)

**통째로 걷는다** — 모델 칸 · 판 파일 `defaultExit` · 포트(`ExitPortKind.Default` · `GraphOutputPortKind.ExecutionDefault` ·
`GraphConnectionKind.ExecutionDefault`) · 편집 명령의 갈래 · 지운 노드 따라 끊기 · 흐름 검사 · 착지 카드 경고의 한 갈래 · 펼친 결과와 문서
합성의 `DefaultJump` · 대사 Yarn의 `<<jump>>`(`YarnSyntax.AppendJump`) · [대본] 선 힌트 · 선 인스펙터의 「기본 출구」. 형식 4 파일에 이미 적힌
`defaultExit`는 다음 저장에서 빠진다(옛 데이터 읽기 L5와 같은 처지 — compositor의 쓰임은 이 세션에서 확인하지 못했다).

## 진행 기록

| 날 | 커밋 | 메모 |
|---|---|---|
| 11-17 | (이 커밋) | 위 전부. 시험 — 기본 출구를 보던 다섯 걷음(`ExitTests` 넷 — 독립 · 모든 노드의 포트 · 표식 없는 노드 · 끊긴 배선, `StoryFileTests.파일이_달라도_실행_간선은_프로젝트_전체에서_계산된다`), 곁들여 쓰던 시험은 그 줄만 걷거나 갈래 출구로(`ConnectionTests` 둘 · `EdgeShapeTests`), 포트 수 · 문서 조각 차례 · 견본 간선 수를 고쳤다. 견본 `samples/Authoring`의 `defaultExit` 둘을 걷었다. `Sample.TargetDefault` → `TargetC`. 문서 `runtime-contract.md` A2-1 · A2-2, `two-graphs.md`, `ARCHITECTURE.md`. Authoring 696 · App 446 |
