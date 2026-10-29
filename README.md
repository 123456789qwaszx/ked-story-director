# ked-story-director

> **이야기의 뼈대와 대사를 쓰는 툴.** 2026-10-29에 VnTool(`VNStoryEditor_avalonia` `dev` @ `f2a7bdc`)에서 이력째 갈라졌다.
> 대본 탭과 엑셀 연동을 걷었고(D2), 연출 그래프를 위→아래로 세웠고(D3), 대사를 카드 안에서 직접 쓴다(D4).
> **연출(무대 프리뷰 · 연출 명령 · 게임용 Yarn · 발행)은 [`ked-story-compositor`](https://github.com/123456789qwaszx/ked-story-compositor)로
> 갔다** — 그 툴은 이 툴의 프로젝트 파일을 원고로 읽고, 연출은 제 파일에 둔다. 계획과 단계는
> [`docs/plans/director-split.md`](docs/plans/director-split.md) · [`docs/plans/presentation-removal.md`](docs/plans/presentation-removal.md),
> 지금 동작의 정본은 [`docs/design/two-graphs.md`](docs/design/two-graphs.md).
> 아래 "문서" 표의 안내서 중 일부는 아직 VnTool 시절(엑셀 워크플로 · 무대)을 설명한다.

비주얼 노벨 저작 도구. C#/.NET 10 + Avalonia, 한국어 코드베이스.

만들어 내는 것은 **원고(프로젝트 파일)** 와 유니티 런타임(별도 저장소)이 먹는 **`exported/{챕터}.progression.json`** 입니다.
게임용 Yarn 묶음은 compositor가 이 원고와 제 연출을 합쳐 냅니다.

## 두 그래프

| | **① 챕터 그래프** (기획) | **② 연출 그래프** (대사 · 흐름) |
|---|---|---|
| 만드는 것 | 에피소드 구조 · 선택지 · 조건 · 스탯 | 에피소드마다 대사 카드 · 카드 안 글 · 분기 · 조건 갈래 · 선택지 잇기 |
| 값이 사는 곳 | 프로젝트 | 프로젝트 |
| 도구의 역할 | 편집하고 검증하고 낸다 | 편집한다 |

## 원본은 프로젝트 하나

모든 값의 원본은 **프로젝트 파일**입니다. 엑셀 워크북을 들여오고 내던 연동과 [대본] 탭은 2026-10-29(D2)에
걷었습니다 — 나중에 다시 붙일 수 있지만 지금은 복잡하기만 하다는 판단입니다.

- **대사 본문·화자** → 연출 그래프의 **대사 카드**(카드마다 대본 하나). 카드 안의 글 칸에서 직접 씁니다(D4).
- **에피소드 구조·간선·조건·스탯** → 챕터 그래프. 간선은 연출 그래프에서도 긋습니다(2026-09-17, R7): 카드 아래 칸에
  문구를 적어 다른 카드로 끌면 그것이 간선입니다. **창구가 둘이지 값이 사는 곳은 하나**입니다.
- 챕터 조건은 판의 공급 노드로 저절로 건너가 대사 카드의 조건 목록에 뜹니다.
- **진행 JSON**은 검증을 통과하면 저절로 나갑니다. 저장은 프로젝트를 씁니다.
- 프로젝트를 열 때 **카드 없는 에피소드에는 빈 카드가 섭니다**(엑셀에서 들여온 옛 프로젝트를 위한 맞추기).

**각 값의 주인은 한 곳뿐**이라는 것이 설계 전체를 관통하는 규칙입니다. 같은 사실이 두 곳에
있으면 어긋나고, 어긋남은 최종 출력에서야 드러납니다.

## 저장 구조

```text
MyStory/
├─ 예제.vnproject.json         목차 · 챕터 · 파일을 넘나드는 관계
├─ game.definition.json        스탯 · 화자 (compositor의 연출 커맨드 어휘도 여기 산다 — 이 툴은 읽지도 지우지도 않는다)
├─ script/                     카드의 대본 (줄 신원 + 본문)
├─ story/                      판(챕터마다 하나)과 그 위의 카드·노드
└─ exported/
   └─ ch01.progression.json    런타임 수입물 — 검증을 통과하면 자동으로 나간다
```

연출을 담은 옛 프로젝트(VnTool 시절)를 열면 연출 데이터는 읽지 않고, 원본을 `presentation-original/`에 한 번 남깁니다
(저장하면 연출이 빠지기 때문입니다). compositor가 그 폴더에서 연출을 가져갑니다.

## 실행

```powershell
dotnet run --project .\src\Vn.App\Vn.App.csproj
```

## 검증

```powershell
dotnet test .\VnTool.sln
```

## 작가에게 전달할 Windows 휴대용 패키지

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-windows.ps1
```

.NET 런타임을 함께 담으므로 받는 PC에 개발 환경이 필요 없습니다.

## 문서

**처음 보는 사람은 [`docs/external-overview.md`](docs/external-overview.md), 작업을 이어받는
사람은 [`docs/handoff/current-state.md`](docs/handoff/current-state.md)에서 시작합니다.**
R0~R5의 순서·체크리스트·완료 근거는 [`docs/plans/PLAN.md`](docs/plans/PLAN.md)가 정본입니다.

| 문서 | 무엇이 있나 |
|---|---|
| [`docs/design/two-graphs.md`](docs/design/two-graphs.md) | **지금 동작의 정본.** 두 그래프의 기능 · 사용자 동작 · 다시 쓸 때 지킬 것 · D2~D4 설계 |
| [`docs/external-overview.md`](docs/external-overview.md) | **외부 진입점.** 도구의 책임 · 데이터 흐름 · 런타임 영향 · 자체 점검과 다음 작업 |
| [`docs/plans/PLAN.md`](docs/plans/PLAN.md) | **런타임 정렬 계획.** R0~R5 순서 · 결정 지점 · 단계별 완료 기준 |
| [`docs/handoff/current-state.md`](docs/handoff/current-state.md) | **진입점.** 계층 규칙 · 화면 · 코드 지도 · 진행 상태 |
| [`docs/run-log.md`](docs/run-log.md) | **결정의 정본.** 한 일 · 근거 · 되돌리는 법 |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | 코드 구조 — "이 기능은 어느 파일" |
| [`docs/handoff/architecture-decisions.md`](docs/handoff/architecture-decisions.md) | 확정된 설계와 **그 이유** |
| [`docs/runtime-contract.md`](docs/runtime-contract.md) | 유니티 런타임과의 계약 (Gate D) |
| [`docs/progression-handoff.md`](docs/progression-handoff.md) | 진행 코어 교환 이력·현행 상대·표본 JSON 안내 |
| [`tests/fixtures/chapter-samples/`](tests/fixtures/chapter-samples/) | 견본 챕터 둘 — `ch05`(복도) · `ch01`(Scene·Auto·조건·스탯·EventKey를 담은 계약 표본). 2026-10-29까지 `docs/*.xlsx`였다 |
| [`docs/work-orders/chapter-graph-orders.md`](docs/work-orders/chapter-graph-orders.md) | 챕터 계층 규격 원본 |
| [`docs/chapter-layer-guide.md`](docs/chapter-layer-guide.md) | 기획자용 사용 안내 |
| [`docs/writer-guide.md`](docs/writer-guide.md) | 작가용 사용 안내 |
| [`docs/handoff/io-reference.md`](docs/handoff/io-reference.md) | 모든 입출력의 형식 |
| [`docs/runtime-ui-tooling-principles.md`](docs/runtime-ui-tooling-principles.md) | 도구 설계 원칙 (`원칙 §…` 인용의 출처) |
