# 정의 파일을 가른다 — 스탯은 챕터에, 화자는 프로젝트에 (S0–S4) — 2026-11-17

**소유자**: [구성]에서 스탯을 더해도 `game.definition.json`에 바로 반영되지 않고, 지운 스탯이 거기 남는다. 「variables는 챕터 단위로
해야 하고, speaker는 전체 프로젝트 단위인데 구분해야 하는 거 아니야?」 · 「애초에 저 game.definition은 그냥 중복 아니야? 프로젝트든
뭐든 그런 곳에 저장해도 될 것 같은데. ked-presentation-runtime에 쓰이고 있는지도 모르겠어.」

> **상태: 끝(S0–S4).** 남은 것 — ① compositor가 정의 파일의 `speakers`를 읽고 있었다면 매니페스트의 `speakers`로 옮겨 읽어야 한다(그쪽 저장소 ·
> 여기서는 닿지 못했다) ② 열 때 옮기는 길(`GameDefinitionFile.SpeakersBeside`)은 소유자의 프로젝트가 한 번 저장된 뒤 걷는다(빚).

## 1. 지금 (조사)

`game.definition.json`은 프로젝트 폴더에 프로젝트와 따로 선 파일이다. 칸마다 쓰는 곳:

| 칸 | 누가 쓰나 | 문제 |
|---|---|---|
| `variables` (스탯의 「존재」) | 이 툴만 — 챕터 스탯이 여기 없으면 경고(`StatMissingFromGameDefinition`) · 경고 밑 [등록] · **새 챕터를 만들 때 이 목록으로 스탯을 깐다**(0~100 정수) | 챕터 스탯(키 · 초기 · 최소 · 최대 · 형 — 프로젝트의 챕터, 진행 JSON의 `stats`로 나간다)의 **중복**이다. [등록]은 더하기만 해서 지우거나 개명한 스탯이 남고, 새 챕터마다 되살아난다. bool 스탯도 `number`로 적는다 |
| `speakers` (화자 이름 ↔ 캐릭터 키) | 이 툴 — [대본] 이름 · [구성] [화자] 탭 캐릭터 키 · 미등록 화자 검사 · 대본 붙여넣기의 화자 가르기 | 중복은 아니지만 **프로젝트 밖**이라 되돌리기 밖이고, Ctrl+S와 상관없이 고칠 때 바로 디스크에 쓴다. 저장 안 한 프로젝트에서는 못 고친다 |
| `conditions` (게임 전역 조건) | 이 툴의 조건 목록(`AvailableConditionSourceKind.GameGlobal`) | 쓰는 창구가 없고 견본 셋 모두 비어 있다. 조건의 자리는 챕터의 [조건]이다(계층 하나 — `one-stat-layer-orders.md`) |
| `presentationCommands` 등 | compositor(README — 「compositor의 연출 커맨드 어휘도 여기 산다」) | 이 툴의 것이 아니다 |

- **런타임은 이 파일을 읽지 않는다** — `ked-presentation-runtime` · `ked-vn-workspace/runtime`(유니티)을 코드 · 에셋 · 문서까지 찾았다.
  `SCOPE-BOUNDARY.md`에 「게임 어휘는 코드가 아니라 데이터가 공급한다」는 원칙 문장으로만 나온다. 런타임은 스탯을 진행 JSON에서 읽는다.
- compositor는 이 툴의 프로젝트 파일(`.vnproject.json`)을 읽고(`StoryProject` 주석), 같은 폴더를 연다. 정의 파일의 화자를 읽는지는
  그 저장소에 닿지 못해 확인하지 못했다.
- 「스탯 존재는 사람이 [등록]을 누를 때만 정의 파일에 쓴다」는 규칙은 스탯을 엑셀 시트에 손으로 적던 때(오타가 게임 어휘가 되는 것을
  막으려고)의 것이다. 2026-09-16(R-F)부터 원본은 툴이다.

## 2. 정한 것 (소유자 2026-11-17)

| | 내용 |
|---|---|
| 스탯 | **챕터의 스탯이 유일한 원천.** 정의 파일의 `variables`를 걷는다 — 경고 · [등록] · 정의 파일로 새 챕터를 깔던 길까지. **새 챕터는 스탯 없이 시작한다** |
| 화자 | **프로젝트 파일로 옮긴다**(`.vnproject.json`의 `speakers`). 화자 편집이 되돌리기 · 저장(Ctrl+S)을 같이 탄다 |
| 전역 조건 | **걷는다.** 조건의 자리는 챕터의 [조건] 하나다 |
| 정의 파일에 남은 칸 | **저장할 때 걷는다** — 이 툴이 쓰던 `variables` · `conditions` · `speakers`만. 다른 칸(compositor의 것)은 그대로 둔다. 이 툴은 정의 파일을 더 읽지도 만들지도 않는다 |

## 3. 규칙

- 화자 편집(더하기 · 이름 바꾸기 · 지우기 · 캐릭터 키)은 `ProjectEditor`의 명령이다 — **조작 하나 = 되돌리기 한 칸**, 같은 값은 기록하지 않는다.
  이름 바꾸기는 대사 줄의 화자가 따라가는 것까지 한 칸이다.
- **옮김**: 프로젝트를 열 때 프로젝트에 화자가 없고 정의 파일에 `speakers`가 있으면 그것을 프로젝트로 들인다(상태줄이 말한다 · 저장해야
  남는다). 소유자의 프로젝트를 잃지 않으려는 한 번짜리 길이다 — 옮겨진 뒤 걷는다(빚).
- **걷기**: 저장할 때 정의 파일에서 `variables` · `conditions` · `speakers`를 걷는다(바이트를 다시 쓰는 것은 걷을 칸이 있을 때만). 남는 칸이
  이 툴이 깔던 안내(`_안내`)뿐이면 파일을 지운다.
- compositor가 정의 파일의 화자를 읽고 있다면 프로젝트 파일의 `speakers`로 옮겨 읽어야 한다 — 그쪽에 부탁할 일이다.

## 4. 단계

| | 할 일 | 끝 |
|---|---|---|
| S0 | 이 지시서 | — |
| S1 | 스탯 — `variables` · `VariableSpec` · 경고 `StatMissingFromGameDefinition` · [등록](`OfferStatRegistration` · `RegisterVariables` · `AddVariables`) · 새 챕터 씨앗 · `ToGraphModel`의 정의 인자 | 시험 |
| S2 | 전역 조건 — `conditions` · `ConditionSpec` · `GameGlobal` · 조건 해석기 · 발행 · 내보내기 · 판 투영에 실려 다니던 정의 인자 | 시험 |
| S3 | 화자 — `StoryProject.Speakers` · 매니페스트 · 스냅숏 · 편집 명령 · 세션 창구 · 화면 · 대본 붙여넣기, 열 때 옮김 · 저장할 때 걷기, `GameDefinition` · `GameDefinitionStore` 걷기 | 시험 · 헤드리스 |
| S4 | 문서 · 견본(`samples/*`의 정의 파일 → 매니페스트) | 문서가 지금 동작과 맞다 |

각 단계는 빌드(경고 0) · 두 시험 프로젝트가 모두 통과한 채로 커밋한다.

## 5. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-17 | S0 | (이 커밋) | 지시서 — 2절은 소유자가 정했다 |
| 11-17 | S1 | (이 커밋) | 경고 `StatMissingFromGameDefinition` · 검증 보고의 [등록](`OfferStatRegistration`) · 세션 `RegisterVariables` · `GameDefinitionStore.AddVariables` · `GameDefinition.Variables` · `VariableSpec`을 걷었다. `ChapterDocument.ToGraphModel`은 정의 인자를 받지 않는다(스탯 검사는 범위만 본다). 새 챕터는 스탯 없이 선다(`CreateChapter`). 정의 파일 뼈대에서 `variables`를 뺐다(뼈대 자체는 S3에서 걷는다). 시험 — 등록 시험 · 견본의 변수 후보 시험을 지웠고, 씨앗 프로젝트는 챕터에 스탯을 더한다(`AddChapterStat`), [구성] 렌더 시험의 경고 11 → 7(스탯 넷). Authoring 699 · App 450 |
| 11-17 | S2 | (이 커밋) | `GameDefinition.Conditions` · `ConditionSpec` · `AvailableConditionSourceKind.GameGlobal` · 해석기의 전역 갈래를 걷었다. 조건 하나 때문에 정의를 들고 다니던 인자를 걷었다 — `AvailableConditionResolver` · `ConditionChoices` · `ConditionFlowResolver` · `NodeConnections` · `DialoguePublisher` · `WorkingDialoguePreview` · `GraphProjectionBuilder` · `BranchEntryCondition` · `ChapterYarnExporter` · `ChapterExportService.ExportAll`(대본 붙여넣기 `ApplyScenarioText`는 화자 때문에 S3까지 남는다). 「설정노드에도 게임 전역 조건에도 없습니다」 → 「이 챕터에 없습니다」. 시험 — 공급 시험의 전역 조건 부분을 걷고 같은 판 조건으로 흐름을 본다. Authoring 699 · App 450 |
| 11-17 | S3 | (이 커밋) | 화자 → 프로젝트: `Model/Speaker`(이름 · 캐릭터 키) · `StoryProject.Speakers` · `FindSpeaker` · `FindSpeakerCharacterId`, 매니페스트와 되돌리기 스냅숏의 `speakers`(비면 안 쓴다 · 손으로 고친 빈 이름 · 겹친 이름은 견딘다), 저장 검증(빈 이름 · 겹친 이름). 편집 명령 `ProjectEditor.Speakers.cs` — `AddSpeaker` · `RemoveSpeaker` · `SetSpeakerCharacterId` · `RenameSpeaker`(목록과 대사 줄이 한 칸 — Scripts에서 옮겼다). 세션 화자 창구는 명령을 부른다(저장 안 한 프로젝트에서도 고친다), `SpeakersChanged`는 목록이 바뀔 때마다(되돌리기 · 열기 포함) 낸다. 대본 붙여넣기(`ApplyScenarioText` · `ScenarioTextParser.Parse`)는 프로젝트의 화자를 본다 — 정의 인자를 걷었다. 정의 파일은 `GameDefinitionFile` 하나(`SpeakersBeside` — 열 때 옮김 · `StripBeside` — 저장할 때 `variables` · `conditions` · `speakers` · 이 툴의 `_안내`를 걷고, 남는 것이 없으면 지운다 · 깨진 파일은 둔다). `GameDefinition` · `GameDefinitionStore` · 세션 `Definition` · 정의 파일 뼈대를 걷었다. 화면 글(설정 창 툴팁 · [화자] 탭 안내)에서 정의 파일을 뺐다. 렌더로 [대본] 화자 목록 · [구성] [화자] 탭을 봤다. 시험 — 새 `SpeakerCommandTests` 6, 세션 시험 셋을 새 모양으로(프로젝트의 것 · 옮김 · 저장의 걷기), `GameDefinitionStoreTests` 걷음, 화자 시험들은 `SetSpeakers`(시험 준비 — 편집 명령을 지난다)로. Authoring 701 · App 451 |
| 11-17 | S4 | (이 커밋) | 문서 — `README.md`(저장 구조) · `ARCHITECTURE.md`(읽는 것 · 저장 구조 · 모델 표 · 편집 파일 표 · 규칙 0 · 16 · 고칠 곳 표) · `ORIENTATION.md` · `two-graphs.md`(1절 표 · 7절 Q · 새 7절 AE) · `chapter-layer-guide.md` · `external-overview.md`, 지난 인계 · 출입구 문서에는 ⛔ 날짜만. 견본 셋(`samples/Authoring` · `FestivalEve` · `PresentationDemo`)을 툴의 길(열기 → 옮김 → 저장)로 옮겼다 — 화자가 매니페스트로 가고, 런타임 견본 둘의 정의 파일은 남는 칸이 없어 지워졌으며, `samples/Authoring`의 것은 compositor의 연출 어휘만 남았다. Authoring 701 · App 451 |
