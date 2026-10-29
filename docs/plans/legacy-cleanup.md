# 레거시 정리

> 2026-10-29 소유자: *"현재 불필요한 레거시 코드들이 많은데, 그것들을 정리하기 위한 Plan을 세우고, Plan이 세워지면 바로 작업을
> 시작해. 만약 판단해야할 것이 있으면, 당신의 추천에 맡기겠습니다."*
>
> 이 저장소는 VnTool에서 이력째 갈라 나왔고(`director-split.md`), 그 뒤로 엑셀 연동 · 대본 탭 · Script Preview · 연출 층(무대 ·
> 명령 공급 · 재생) · 작가 변수가 차례로 걷혔다. 걷힌 기능을 받치던 코드가 아직 곳곳에 산다 — 이 계획은 그것을 걷는다.

## 1. 어떻게 찾았나

| 방법 | 무엇을 잡나 |
|---|---|
| 분석기 (`IDE0051` · `IDE0052` · `IDE0060`) | 안 쓰는 private 멤버 · 읽지 않는 필드 · 안 쓰는 매개변수 |
| 선언-참조 스캔 (src · tests 낱말 단위) | 아무도 안 부르는 / **테스트만** 부르는 public · internal 멤버 |
| 영역별 정독 세 갈래 (Vn.App · Vn.Authoring · 그 밖) | 걷힌 기능에만 닿는 경로, 옛 파일만 만드는 상태, 쓰지 않는 저장소 산출물 |

판정의 기준은 하나 — **지금의 앱(Vn.App)이 닿는가.** 테스트만 부르는 제품 코드는 그 테스트와 함께 걷는다(테스트가 지키는 것이
이미 없는 기능이다). 옛 프로젝트 파일을 읽기만 하는 길은 따로 가르고(§3), 파일 형식을 바꾸는 것은 이유가 있을 때만 한다.

## 2. 결정 (소유자가 추천에 맡김)

| # | 항목 | 결정 | 이유 |
|---|---|---|---|
| L-D1 | Yarn 도구 묶음 — `Vn.Core` · `Vn.Cli` · `Vn.Core.Tests` · `samples/Valid` · `Invalid` · `Real` · `YarnSpinner.Compiler` · CI의 Yarn 견본 단계 · `build-and-run.ps1` | **걷는다** | 앱이 참조하지 않는다(2026-10-29 연출 층과 함께 끊겼다). Yarn은 compositor가 내고, compositor에 같은 도구가 이력째 있다 — `presentation-removal.md`의 "옮길지는 따로 정한다"를 여기서 정한다 |
| L-D2 | 조건 공급(Settings) 링크 | **편집 API · 그래프의 공급선 갈래 · `ProjectChangeKind.Connections`를 걷는다. 데이터(`links`)는 그대로 읽고 쓴다** | 2026-08-17부터 그리지도 읽지도 않는다. 데이터는 compositor가 읽는 파일 내용이라(`presentation-removal.md` §1) 지우지 않는다 |
| L-D3 | 선택지 칩의 옛 길 — 구판 칩(`LegacyChip`) · 칩 상세(`ShowRailChip`) · 자유 씬 웹 경로(`PortFor` · `RouteInto` · `DrawAdvanceReturns` …) · `ChapterSpine` · 챕터 스냅샷 대체(`_chapters` 폴백) | **화면 쪽을 걷는다. `DialogueNode.ChoiceExits` 데이터는 그대로 둔다** | 지금 만들 수 있는 프로젝트로는 닿지 않는다(옛 파일 · 테스트가 꾸며 넣은 상태만). `ChoiceExits`는 compositor 코드가 읽는다 |
| L-D4 | 작가 변수 · 줄 `set`의 남은 길 (`SetOperations` · `Assignments` · `StatFold` · 시뮬레이션의 set) | **걷는다** | 2026-09-17에 폐지됐고 늘 빈 값이 흐른다. 메모리 안의 일이라 파일 형식은 그대로다 |
| L-D5 | 프리뷰용 갈래 고르기(W35) — 대사 편집기에서 갈래를 고르면 나머지가 흐려지는 것 | **걷는다** | 프리뷰 · 재생을 위한 것이었고 둘 다 걷혔다. 지금은 이유 없이 줄이 흐려진다 |
| L-D6 | 챕터의 「X 설정」 카드 | **새 판에는 세우지 않고, 있는 것은 연출 그래프에서 숨긴다** (데이터는 그대로) | 작가 조건 · assignment를 담던 자리인데 편집기(SetNodeEditor)가 걷혀 눌러도 빈 칸이다. 옛 파일의 조건은 그대로 고를 수 있다. 조건을 나르는 「챕터 X 조건」 배관 노드는 원래 숨어 있고 그대로다 |
| L-D7 | 엑셀 행 번호 `SourceRow` | **조사한 뒤 정한다(L8)** | 진단이 에피소드를 행 번호로 짚는데 툴에서 만든 에피소드는 0이라 **에피소드 오류 표시가 안 켜지는 결함**이 있다(`ChapterGraphModel.EpisodeHasError`). 걷으면 그 결함도 함께 고친다 — 다만 테스트 · 견본 JSON 90곳이 걸려 크다 |
| L-D8 | 옛 파일 호환 | `excelEpisode` 읽기는 **걷는다**(2026-09-17 소유자: "이전의 프로젝트들은 모두 버리고"). 연출 층 백업(`presentation-original/`) · 픽스처 · `WriterSpeakers` · 링크 데이터 · `ChoiceExits` · 판 맞추기(`AlignBoardsWithChapters`)는 **남긴다** | 남기는 것은 compositor가 읽거나, 소유자가 일부러 남겼거나, 안전망이다 |
| L-D9 | 문서 | `run-log.md`를 **고친다**(2026-09-03 커밋 `44bf3f1`에서 이진 덩어리가 됐다 — 되살린다). 문서가 아닌 산출물(`Claude outputs/` · `samples/Runtime` · `docs/game.definition.json`)은 걷는다. 이력 문서는 **제자리에 둔다**(D-R5). 현행 안내(README · ARCHITECTURE · ORIENTATION)는 없는 코드를 가리키지 않게 마지막에 고친다 | |
| L-D10 | 남기는 것 | `Ked.Progression`(런타임 사본 — 해시 테스트가 지킨다) · `AddScript`(테스트의 기본 짓기) · 결과 문서의 `RuntimeFull` 프리셋(분기 · 점프 자리를 재는 테스트의 기준) · 챕터 그래프 [자동 정렬] · 판 접기 · `ScriptSynchronizer`(카드 글 칸이 쓴다) · 걷힘 주석(⛔ — 결정의 기록) | |

## 3. 단계

한 단계 = 커밋 하나(또는 몇 개), CI(Windows) 녹색을 보고 다음으로. 걷는 코드의 테스트 · 주석 · 문서도 함께 걷는다.

| | 무엇 | 상태 |
|---|---|---|
| L0 | 이 문서 · `run-log.md` 되살리기 · 산출물 걷기(`Claude outputs/vntool-map.html` · `samples/Runtime` · `docs/game.definition.json`) · `.gitattributes` · `.gitignore`의 없는 길 | `c475006` (run 39) |
| L1 | Yarn 도구 묶음을 걷는다 (L-D1) | `d9d5dac` (run 40) |
| L2 | Vn.App의 죽은 코드 — 탐색기 · 재생 속도 · 음량 설정, 공급선 갈래 + 링크 편집 API (L-D2), 레일의 닿지 않는 갈래(⏹ 종료 스텁 등), 챕터 어휘 공급 · `DefinitionChanged`, 숨은 관문 콤보 · `AutoSaveGates`, `DrawDirectEdge`, 테스트만 부르는 멤버, 고아 주석 · 낡은 안내 글 | a 앱의 죽은 코드 `f6b0a8f` (run 41) · b 링크 편집 API `847bdc3` (run 42) |
| L3 | 선택지 칩의 옛 길을 걷는다 (L-D3) — 레일 테스트는 챕터를 프로젝트에 두고 다시 쓴다 | `1bfc9f5` (run 43) |
| L4 | Vn.Authoring의 죽은 묶음 — `ChapterRunState` · 판 파일 관리(가져오기 · 지우기 · 개명 · 노드 옮기기) · 프리뷰 흐름(`ChoiceOptionBundle` · `Cycle` · `PassedBlocks`) · 안 쓰는 `Clone` 사슬 · 엑셀 · 탐색기 시절 편집(`TryDecomposeSingle` · `MoveEpisodesToChapter` · `RemoveChoiceLabel` …) · 아무도 안 부르는 멤버 | a 기능째 죽은 묶음 `370e86c` (run 44) · b 아무도 안 부르는 멤버 · 주석 `ca79145` (run 45) |
| L5 | 대본 가져오기 · 작가 변수의 남은 길 — `PlanScriptSync` · `ScriptParser` · 대본 개명 · 지우기 · `SourcePath`, set · assignment (L-D4). 일치 엔진 테스트는 `ScriptSynchronizer.Plan`으로 옮긴다. **갈래 고르기(L-D5)도 여기서 걷는다** — 시뮬레이션의 set 누적이 그것의 유일한 독자라 한 뿌리다 | `6e460b6` (run 46) |
| L6 | 걷힌 기능의 화면 흔적 — 「X 설정」 카드(L-D6) · 옛 파일 호환 `excelEpisode`(L-D8) · 형식 거절 안내의 "대본을 가져오세요" | `f31111a` (run 47) |
| L7 | 현행 안내 문서 — README · ARCHITECTURE · ORIENTATION이 없는 코드 · 걷힌 기능을 가리키지 않게 | 이번 — ARCHITECTURE · ORIENTATION을 지금 구조로 다시 썼고(옛 판은 `git show 82fc34d:ARCHITECTURE.md`), README · `two-graphs.md` · run-log 머리를 고쳤다 |
| L8 | `SourceRow` 조사 → 걷기와 에피소드 오류 표시 결함 (L-D7) | `82fc34d` (run 48) — L7보다 먼저 했다(안내 문서는 코드가 다 선 뒤에) |

## 4. 걷다가 찾은 것

- **L4b · 공급 조건의 범위** — 스탯 개명 · 스탯 비우고 지우기 · 조건 지우기 · 조건 쓰임 찾기가
  이름 모양(`챕터 … 조건`)만 보고 **모든 챕터의** 공급 노드를 훑었다. 스탯 · 조건은 챕터마다
  따로라 같은 이름을 쓰는 다른 챕터의 판까지 바뀌거나 걷혔다. 안 쓰이던 `chapterId` 매개변수가
  가리킨 자리다 — 걷지 않고 **쓰게** 고쳤다(`SupplyScopeTests`).
- **L4b · 남긴 것** — `ChapterRenameEffect.BoardRenamed`는 읽는 곳이 없지만 `SupplyRenamed`와 짝인
  결과 값이라 남긴다.
- **L5 · 순서 바꿈** — 갈래 고르기(L-D5)를 L6에서 L5로 당겼다. 조건 시뮬레이션
  (`ConditionSimulation` · `ConditionExpression` · `StatFold` · `SetOperatorKind`)이 걷을 set 칸 위에 서
  있었고, 그 시뮬레이션을 읽는 곳이 갈래 고르기 하나뿐이었다. 함께 걷어야 테스트가 한 번에 선다.
- **L5 · 남긴 것** — `ScriptDocument.SourceRevision` · `SourceContentHash`는 가져오기 시절 이름이지만
  카드 글 반영(`ApplyScenarioText`)이 지금도 올리고, 결과 문서가 개정 번호로 싣는다(compositor도 읽는다).
- **L5 · 함께 걷은 잔손** — 쓰이지 않는 `using`, 테스트의 고아 도우미 셋, 깨진 cref 둘(분석기
  IDE0005 · IDE0051 · CS1574로 찾았다).
- **L6 · 숨김의 범위** — 「X 설정」만 골라 숨기지 않고 **설정노드 전부**를 숨긴다
  (`GraphProjectionBuilder.IsHiddenPlumbing`). 남은 설정노드는 조건 공급 배관(원래 숨었다)과 옛
  「X 설정」뿐이고, 둘 다 카드로 할 일이 없다. 숨은 노드는 접힌 판의 행도 차지하지 않는다.
- **L6 · 남긴 것** — `GraphNodeKind.Set`은 투영이 노드 종류를 그대로 옮기는 자리라 남긴다(지금은
  카드로 나가지 않는다).
- **L8 · 조사 결과 (L-D7)** — `SourceRow`의 쓰임은 셋뿐이었다: 진단이 자리를 짚는 행 번호(D2부터 문장에는
  안 실렸다), 검증 보고의 정렬, 챕터 그래프 카드의 오류 표식. 셋째가 **고장 나 있었다** — 표식이 진단의
  행과 에피소드의 행을 맞대는데, 툴에서 만든 에피소드는 행이 0이고 진단의 행은 null이라 엑셀이 걷힌 뒤로
  어떤 카드도 붉어지지 않았다. **걷었다**: 진단은 행 대신 대상 `EpisodeId`를 들고(표식이 그것으로 찾는다 —
  `EpisodeErrorMarkTests`), 간선을 짚던 행은 메시지의 `간선 a → b "문구"`(`ChapterEdge.Where`)가 대신한다.
  여섯 레코드의 `SourceRow`와 저장 키 `sourceRow`(옛 파일의 키는 읽지 않고 지나간다), 견본 픽스처의
  키도 함께 걷었다. 보고의 정렬은 심각도 → 자리 → 낸 순서다.
- **L8 · 남긴 것** — `ChapterSheetNames`·`ChapterDiagnostic.Sheet`는 이름이 엑셀 시절 것이지만 지금은
  "챕터 안의 어느 자리(에피소드 · 간선 · 조건 · 스탯)"를 뜻하고 보고 문장에 실린다. 이름만 바꾸는 일은
  이 정리의 몫이 아니다.
- **L7 · 다시 쓴 까닭** — ARCHITECTURE는 걷힌 코드 115곳(워크북 리더/라이터 · 연출 층 · 발행 · 합성 · Yarn 이미터 · `Vn.Core`)을
  가리켰고 ORIENTATION은 VnTool 시절 지도였다. 고쳐 쓰기보다 지금 구조로 다시 쓰는 편이 짧고 맞았다. 여전히 맞는 설계 판단
  (조건은 전환만 · 출구는 여는 줄 · 포트는 계산 · 애매하면 멈춤)과 규칙은 살렸고, 챕터 층의 판단(코어 재적재 관문 · 챕터 범위)을
  더했다. 옛 판의 까닭은 git에 있다.
- **L7 · 남긴 것** — `docs/handoff/*` · `external-overview` · `writer-guide` · `chapter-layer-guide` · `PLAN`·`R*` 계획은 이력이다(D-R5).
  머리의 표식이 무엇이 걷혔는지 말하고, README 표가 「이력」이라 적는다.
