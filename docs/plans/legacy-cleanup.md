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
| L2 | Vn.App의 죽은 코드 — 탐색기 · 재생 속도 · 음량 설정, 공급선 갈래 + 링크 편집 API (L-D2), 레일의 닿지 않는 갈래(⏹ 종료 스텁 등), 챕터 어휘 공급 · `DefinitionChanged`, 숨은 관문 콤보 · `AutoSaveGates`, `DrawDirectEdge`, 테스트만 부르는 멤버, 고아 주석 · 낡은 안내 글 | a 앱의 죽은 코드 — 이번 · b 링크 편집 API |
| L3 | 선택지 칩의 옛 길을 걷는다 (L-D3) — 레일 테스트는 챕터를 프로젝트에 두고 다시 쓴다 | |
| L4 | Vn.Authoring의 죽은 묶음 — `ChapterRunState` · 판 파일 관리(가져오기 · 지우기 · 개명 · 노드 옮기기) · 프리뷰 흐름(`ChoiceOptionBundle` · `Cycle` · `PassedBlocks`) · 안 쓰는 `Clone` 사슬 · 엑셀 · 탐색기 시절 편집(`TryDecomposeSingle` · `MoveEpisodesToChapter` · `RemoveChoiceLabel` …) · 아무도 안 부르는 멤버 | |
| L5 | 대본 가져오기 · 작가 변수의 남은 길 — `PlanScriptSync` · `ScriptParser` · 대본 개명 · 지우기 · `SourcePath`, set · assignment (L-D4). 일치 엔진 테스트는 `ScriptSynchronizer.Plan`으로 옮긴다 | |
| L6 | 걷힌 기능의 화면 흔적 — 갈래 고르기(L-D5) · 「X 설정」 카드(L-D6) · 옛 파일 호환 `excelEpisode`(L-D8) · 형식 거절 안내의 "대본을 가져오세요" | |
| L7 | 현행 안내 문서 — README · ARCHITECTURE · ORIENTATION이 없는 코드 · 걷힌 기능을 가리키지 않게 | |
| L8 | `SourceRow` 조사 → 걷기와 에피소드 오류 표시 결함 (L-D7) | |
