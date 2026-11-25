# 런타임 계약서

ked-story-director가 게임(유니티 런타임 `ked-presentation-runtime`)에 건네는 것과 그 규칙이다. 이 툴이 내는 것은 둘뿐이다:

| 산출물 | 어디 | 누가 읽나 |
|---|---|---|
| **진행 JSON** | `exported/{챕터Id}.progression.json` | 런타임의 진행 코어 `Ked.Progression` — 에피소드 · 선택지 · 조건 · 스탯변화 |
| **대사만 있는 Yarn** | `exported/yarn/{챕터}_{장면}.yarn` | 작가(대사만으로 굴러가는지 본다) · 연출을 붙이는 쪽. 연출 커맨드는 없다 |

연출(표정 · 이동 · 카메라 같은 줄 단위 커맨드)과 연출이 든 게임용 Yarn은 이 툴의 일이 아니다.

이 문서와 어긋나는 출력은 컴파일이 되어도 **조용히 깨진다** — 대부분 즉시 오류가 아니라 어긋난 재생으로 나타나므로 내보내기 단계에서 잡는다.
그래서 진행 JSON은 내기 전에 **런타임과 같은 진행 코어**에 실어 본다(§4).

---

## 1. 장면(Scene) — 툴이 알아야 하는 만큼

**장면은 표시 이름도 유니티 씬도 아니다. 여러 에피소드를 하나의 진행 확정 · 롤백 · 복원 수명으로 묶는 단위다.**

```text
Scenario(회차) > Chapter > Scene > Episode
```

### 툴이 지키는 것 넷

| | 규칙 | 근거 |
|---|---|---|
| ① | **정본은 `Episode.SceneId` 하나다.** 같은 값을 가진 에피소드들이 한 장면이다 | 런타임에도 저장되는 장면 표가 없다 — 실행할 때 만들어진다 |
| ② | **늘 채운다** — 에피소드는 늘 장면에 속한다. 런타임은 빈 값을 `__scene_{EpisodeId}`(에피소드 하나 = 장면 하나)로 받지만 이 툴은 그 모양을 내지도 읽지도 않는다 | 저장 검증이 빈 장면ID를 거절한다 |
| ③ | **장면에 밖에서 들어오는 자리는 하나**(= 시작점). 챕터 시작도 그 하나로 센다. 같은 시작점으로 여러 간선 · 재진입은 정상 | 코어의 `ChapterInvariants.VerifySceneEntries`. 롤백이 되돌아갈 곳과 이어하기가 재개할 곳이 그 자리다 |
| ④ | **자동 간선은 장면을 넘어도 된다** | 경계의 일(`Commit → Exit → Enter` — 무대 비우기 · 확정 · 저장)은 묻는 선택지로 넘을 때와 같고 박스만 뜨지 않는다 |

**툴은 이 판정을 따로 구현하지 않는다.** 내보내기가 낸 JSON을 코어 로더에 되먹여 코어의 오류를 `CoreRefusedChapter`로 세운다 —
판정의 주인은 코어 하나다(§4).

### 툴이 흉내 내지 않는 것

수명주기(`Commit` / `Rollback` / `Replay` / `Stop`)는 **실행 층의 것**이다. 툴은 그 경계를 **저작**할 뿐 재현하지 않는다. 참고로만 —
같은 장면 안 이동은 확정 없음, 다른 장면으로 가면 `Commit → Exit → Enter`, 롤백은 같은 장면을 유지한 채 시작점부터 다시 재생,
멈춤 · 새 게임 · 수동 불러오기는 확정하지 않는다. 화면의 장면 묶음도 `Episode.SceneId`에서 나오는 **파생**이지 따로 선 엔티티가 아니다.

---

## 2. 진행 JSON — `exported/{챕터Id}.progression.json`

`ChapterProgressionExporter`가 낸다. **키는 PascalCase다.** 런타임 코어가 읽는 칸:

```
{ ChapterId, DisplayName, StartEpisodeId, Stats[], Nodes[] }

Stat   : { Key, DisplayName, Type("Number"|"Bool"), Initial, Minimum, Maximum }

Node   : { EpisodeId, DialogueEntryId, Title, EventKey, SceneId, NextOptions[] }

Option : { TargetEpisodeId, ChoiceLabel, Auto, VisibleConditions[], Conditions[],
           LockedReasonText, StatChanges[{ Key, Amount, Op("Add"|"Set") }] }

조건   : { Kind("Stat"), Key, Op, IntValue }
```

이 밖에 툴이 함께 내는 칸 — 노드의 `IndexText` · 빈 `VisibleConditions` · `UnlockConditions` · `Position`(판 위 자리) — 은 코어 DTO에 없어
로더가 버린다.

### 2.1 칸 대응

| JSON | 코어 모델 | 이 툴에서 |
|---|---|---|
| `ChapterId` · `DisplayName` · `StartEpisodeId` | `ChapterDefinition` | `DisplayName`은 챕터 이름(비면 챕터ID). 시작은 챕터 에피소드 목록의 첫째 |
| `Stats[]` | `ChapterDefinition.Stats` | 챕터의 스탯 표. 이쪽 「숫자(정수)」가 `"Number"`, 「활성/비활성」이 `"Bool"`(0/1). `DisplayName`은 `Key`와 같은 글자 — 게임이 키로 로컬라이즈한다 |
| `Nodes[]` | `EpisodeNode` | 에피소드 하나 = 노드 하나. 분기 카드는 싣지 않는다 |
| `Node.DialogueEntryId` | `EpisodeNode.DialogueEntryId` | 그 에피소드의 Yarn 노드 이름 — 대사 Yarn의 `title:`과 **같은 함수**를 지난다(§5.2) |
| `Node.Title` | | 에피소드 제목(비면 에피소드ID) |
| `Node.EventKey` | `EpisodeNode.EventKey` | 해석 없이 실어 나른다. 장면 끝 확정에서 시청 보고의 열쇠가 된다 |
| `Node.SceneId` | `EpisodeNode.SceneId` | 에피소드의 장면ID — 늘 채운다(§1 ②) |
| `NextOptions[]` | `EpisodeOption` | 간선 하나 = 하나. **배열 순서 = 화면 순서 = 서버 이력의 `OptionIndex`**(§3). 정렬하지 않는다 |
| `Option.ChoiceLabel` · `Option.Auto` | `EpisodeOption` | 자동 길은 `Auto: true` · 빈 문구. 그 밖의 길은 문구가 있어야 한다 — 저작에서 문구가 빈 선택지(검증 경고)는 임시 문구 `[TODO] Missing choice text -> {TargetEpisodeId}`로 나간다(영어 한 벌 · ASCII · 2026-11-26) |
| `Option.VisibleConditions` · `Option.Conditions` | | 표시조건 · 해금조건. 조건 이름은 나가지 않고 그 식이 항으로 펴져 나간다. 식 미정이면 빈 배열(관문 없음) |
| `Option.LockedReasonText` | | 잠금 안내문 |
| `Option.StatChanges` | | 스탯변화. `Op`는 늘 적는다 — `"Add"`(정수 증감) · `"Set"`(깃발 지정, 값 0/1) |

**enum은 이름 문자열로 나간다** — 순서를 재배열해도 안 깨진다. 로더가 이름으로 맵핑하고, 모르는 이름은 로더가 잡는다.

### 2.2 규칙

**`IntValue`는 0이면 키 자체가 없다.** `JsonIgnoreCondition.WhenWritingDefault` — `trust >= 0`은 `{ "Kind":"Stat", "Key":"trust", "Op":"GreaterOrEqual" }`로
나간다. **DTO 필드는 반드시 `int`** — `int?`로 두면 가장 흔한 조건인 `flag == false`가 통째로 어긋난다.

**비교는 다섯이다** — `GreaterOrEqual` · `LessOrEqual` · `Equal` · `GreaterThan` · `LessThan`. **`NotEqual`은 저작 파서가 닫아 두어 나오지 않는다.**
조건 종류는 `Stat` 하나다 — 클리어 이력 같은 다른 종류는 없고, 깃발(Bool 스탯)이 그 자리를 대신한다.

**Bool 스탯은 0/1 + `Equal`뿐이다.** 경계 0 · 1 고정, **크기 비교와 증감은 오류**(지정 `Set`만). 정수 스탯에는 `Set`을 쓰지 않는다.
한 간선이 같은 깃발을 두 번 지정하면 오류다.

**관문은 간선의 것이다.** `VisibleConditions` 미달 → **목록에 만들지 않는다**. `Conditions` 미달 → **잠긴 채 보인다**(`LockedReasonText`와 첫 미달
조건을 든다). 규칙의 주인은 코어의 `ChapterTransition.Resolve` 하나다.

**전이는 세 갈래다.**
① 나가는 간선이 자동 간선 하나면 **묻지 않고** 그리로 간다.
② 고를 수 있는 선택지가 하나라도 있으면 **플레이어의 선택을 기다린다** — 고른 간선의 `TargetEpisodeId`로 간다.
③ 고를 수 있는 것이 없으면 **챕터가 끝난다** — 잠긴 것만 남아도 끝이다.

**스탯변화는 장면 끝에 확정된다**(런타임). 장면 안의 선택은 쌓여 있다가(롤백이 그것을 물린다) 장면을 나가는 순간 순서대로 접혀 한 번에
확정된다. 판정은 진입 상태에 지금까지의 선택을 접은 값으로 한다 — 플레이어가 선택지를 보는 시점의 값이다. 값은 스탯의 최소 · 최대로 잘린다.

**자동 간선의 불변식** — 그 에피소드의 **유일한** 간선이고, 조건도 스탯변화도 없다. 문구가 빈 간선은 `Auto`가 켜져 있어야 한다 —
「보이지 않는 기본」은 없다.

**로드를 통과했다는 것의 뜻** — 에피소드 ID · 스탯 키 중복 없음 · 시작 실재 · **모든 간선이 실재하는 에피소드에 착지** · 조건과 스탯변화가
**정의된 스탯만** 가리킴 · Bool 어휘 준수 · 자동 간선 불변식 · **장면마다 밖에서 들어오는 자리 하나**. 그래서 전이기와 도달성 증명은
「허공으로 가는 간선」이나 「없는 키를 읽는 조건」을 다시 걱정하지 않고, 이어하기는 **장면 시작점에서만** 재개한다.

**침묵 금지.** 없는 스탯 키를 0으로 읽지 않고 던진다. 처음 값이 경계 밖이면 생성 거부 — 조용히 자르면 작가가 쓴 값과 다르게 시작한다.
문구 없는 간선 · 엉터리 장면 데이터도 **로드에서** 죽인다 — 재생 중에 발견되면 디버깅 비용이 크다.

---

## 3. 바이트와 순서 — 서버가 기대는 것

- **같은 프로젝트면 같은 바이트다.** UTF-8(BOM 없음) · LF · 들여쓴 JSON(`ChapterExportBytes`). 서버는 JSON의 뜻이 아니라 원본 바이트의
  **SHA-256**을 버전 열쇠로 쓴다 — 맞는 버전이 없으면 동기화를 접는다. 내보내기는 바이트가 다를 때만 파일을 쓴다.
- **선택지 순서가 서버 이력의 열쇠다.** 선택은 `OptionIndex`(원본 `NextOptions`의 서수)로 서버에 쌓인다. 출시 뒤 기존 선택지 앞에
  끼우거나 지우거나 자리를 바꾸면 화면이 아니라 **이미 쌓인 이력의 뜻이 바뀐다.**
- **출시 기준선** — 서버에 수입한 판을 프로젝트 폴더의 `release-baselines/{챕터Id}.progression.json`에 두면(사람이 `exported/`에서 복사한다),
  내보내기가 각 에피소드의 `NextOptions`를 견준다(`ChapterReleaseBaseline.FindOrderChanges`). 기존 항목의 앞 끼우기 · 지우기 · 자리 바꾸기가
  있으면 그 챕터의 진행 JSON 갱신을 막고 검증 보고에 그 에피소드를 짚는다. 끝에 더하기와 같은 자리의 문구 · 조건 · 효과 수정은 막지 않는다.

---

## 4. 진행 코어 사본 — `src/Ked.Progression/`

진행 JSON을 읽는 코드는 **런타임과 이 툴이 같은 구현을 쓴다** — 규칙이 양쪽에 각자 있으면 갈린다. 정본은 런타임 저장소
(`ked-presentation-runtime`의 `Assets/Ked.Progression/Core/`)이고, 이 툴은 그 순수 C# 부분을 **복사해 들인다**. 어느 커밋을 들였는지 ·
무엇을 들이지 않는지는 [`src/Ked.Progression/UPSTREAM.md`](../src/Ked.Progression/UPSTREAM.md)에 적는다.

- **들이는 것** — `Loading/` · `Spec/` · `State/` · `Transition/` · `Vocabulary/`(무엇이 유효한 진행인가).
- **들이지 않는 것** — 장면 실행 층(`SceneRun/` · `ProgressionDriver` 등 — 언제 무엇을 실행하는가). 툴이 실행 층을 흉내 내면 수명주기의 주인이 둘이 된다.
- **사본에만 있는 것** — `Reachability/`(도달성 증명 — 저작 전용).
- **사본은 여기서 고치지 않는다.** 바꿀 것은 런타임에서 고친 뒤 다시 들인다. 지문 `src/Ked.Progression/runtime-sync.txt`(사본 파일의 **코드** 해시)를
  `CoreCopyInStepTests`가 지킨다 — 사본을 여기서 고치면 잡는다.
- **다시 들이는 절차** — 런타임을 고친다 → 파일을 이쪽으로 복사한다(UPSTREAM.md 「재반입 검산」으로 원천과 바이트를 견준다) → 테스트를 돌린다 →
  나온 `runtime-sync.txt.actual`을 확인하고 `runtime-sync.txt`로 바꾼다 → UPSTREAM.md의 기준 커밋과 반입 기록을 고친다.
  **지문을 갱신하는 것은 「옮겨 왔다」는 선언**이다 — 사본을 이쪽에서 고쳐 놓고 지문만 맞추면 관문과 게임이 갈린다.
- `ChapterProgressionDto` 같은 JSON 계약의 이름은 코어의 것이고, `ChapterProgressionExporter`는 이쪽 것이다.

**내보내기가 코어 로더를 지난다.** `ChapterProgressionExporter`가 낸 JSON을 `ProgressionLoader`에 **실제로 실어 보고**, 오류 진단이 하나라도
나오면 파일을 내지 않는다(`CoreRefusedChapter`). 코어 진단의 경로(`Nodes[ep].NextOptions[i]`)는 에피소드 · 간선으로 옮겨 싣는다
(`ChapterProgressionExporter.CoreRefusals`) — 「어딘가 잘못됐다」는 보고는 이 층에서 실패다. 코어의 **경고는 막지 않는다.**
툴 검증이 같은 규칙을 베껴 심각도를 맞추는 길은 쓰지 않는다 — 같은 규칙이 두 곳에 살면 저쪽이 하나 늘릴 때마다 또 갈린다.

---

## 5. 대사 Yarn — `exported/yarn/{챕터}_{장면}.yarn`

`ChapterYarnExporter`가 진행 JSON을 쓴 챕터마다 함께 낸다. **장면 = 파일, 에피소드 = 노드.** 분기 카드는 그 집(부르는 에피소드)의 노드
바로 뒤에 노드로 선다. 막는 문제(아래)를 안은 장면은 쓰지 않고 그 장면의 옛 파일을 둔다.

### 5.1 모양

```yarn
// 장면: 마감 전날의 교실 (scene01)
// (장면 메모 — 줄마다 //)

// 에피소드: 간판 완성
// (에피소드 지시 — 줄마다 //)
title: ch01_scene01_new01
---
하린: 다시 그린 간판, 훨씬 낫다. #line:ln_0001
<<detour ch01_scene01_branch01>>
카스미: 그럼 이제 붙이자. #line:ln_0002
<<if stat("trust") >= 3>>
    하린: 고마워. #line:ln_0003
<<endif>>
===
// 분기 카드: ch01_scene01_branch01 — 「간판 완성」에서 들른다
title: ch01_scene01_branch01
---
<<if stat("trust") >= 1>>
    하린: 잠깐, 여기 한 글자 틀렸어. #line:ln_0004
<<endif>>
===
```

- **노드 하나 = 에피소드 하나**(카드나 줄이 없는 에피소드도 노드는 선다 — 진행 JSON이 그 이름을 부른다).
- **대사 줄** — `화자: 대사 #line:{LineId}`(화자가 없으면 지문). 본문이 빈 줄은 보이지 않는 한 글자(U+00A0)로 채운다 — 빈 본문이면 `#line:`만 남아
  Yarn이 읽지 못한다. 줄을 빼지 않는 까닭은 LineId다.
- **조건** — `<<if 식>>` · `<<elseif 식>>` · `<<endif>>`, 안쪽은 네 칸 들여 쓴다. 식은 챕터 조건을 옮긴 것이다 — 항마다 `stat("키") 비교 값`,
  여럿이면 `&&`. 식 미정이면 `true`.
- **분기** — 분기점은 `<<detour 노드>>`다. 분기 카드를 재생하고 **부른 자리로 돌아와** 나머지를 계속한다. 분기 카드의 조건은 그 노드 첫머리의 `<<if>>`다.
  `detour`는 YarnSpinner 3.x 키워드라 런타임 등록이 필요 없다.
- **주석** — 장면 메모는 파일 머리에, 에피소드 지시는 그 노드 앞에 `//`로 선다. 게임은 읽지 않는다.
- **없는 것** — 연출 커맨드 · 선택지(`->`) · `<<jump>>` · `<<set>>` · `<<declare>>`. 에피소드 사이는 진행 JSON이 잇고, 선택지는 간선의 것이다.
- **결정적이다** — 같은 입력이면 같은 바이트(UTF-8 BOM 없음 · LF). 장면은 하이어아키 차례, 에피소드는 장면 안 차례.

### 5.2 노드 이름

```
DialogueEntryId = title = SanitizeNodeName(EpisodeId)
                          └ 영숫자 · 밑줄이 아닌 글자를 전부 '_'로  (`장면 1` → `장면_1`)
```

진행 JSON의 `DialogueEntryId`와 대사 Yarn의 `title:`은 **같은 함수**(`YarnNaming.StoryNodeTitleOf`)를 지난다 — 둘이 한 글자라도 갈리면 진행 JSON이
없는 노드를 부르고, 로드 · 검증 · 증명은 다 통과하는데 재생만 안 된다. **바깥에서 이름을 손으로 조립하지 말 것.** 런타임은 이 글자로 Yarn
프로젝트에서 노드를 찾는다 — **한 번 출시한 노드 이름은 바꾸지 않는다.** 분기 카드의 노드 이름은 그 카드의 ID다(자동 `ch01_scene01_branch01`).

### 5.3 스탯 읽기 — `stat("키")`

```yarn
<<if stat("trust") >= 3>>      // ✅ 읽기
<<set $trust = 5>>             // ⛔ 만들 수 없다 — 변수가 아니다
```

- 대사가 물을 수 있는 낱말은 **챕터 스탯 하나뿐**이고, 읽기만 한다. **변수가 아니라 함수인 것이 핵심이다** — 함수에는 왼쪽 변이 없어 쓰기가
  문법적으로 불가능하다. 대사 중의 스탯 쓰기는 세이브 / 로드 복귀와 도달성 증명이 못 보는 뒷길이다.
- 런타임이 `stat` 함수를 등록하고, **간선 관문이 보는 것과 같은 상태**를 매번 읽는다. 둘이 갈리면 「선택지는 잠겼는데 대사는 통과」가 생긴다.
- **없는 키 · 진행 없는 호출은 런타임이 던진다** — 조용히 0으로 떨어뜨리지 않는다. Yarn은 어떤 키가 유효한지 모르므로 1차 방어는 이쪽의
  산출 전 검증이다(없는 스탯을 읽는 대사는 검증 보고가 짚는다).
- **키는 정규화하지 않는다.** 진행 JSON의 `Stats[].Key`가 원본 그대로 나가므로 대사도 그대로다. 인자는 문자열 리터럴이라 `"`와 `\`만 이스케이프한다
  (`YarnSyntax.StatRead`). 스탯 키는 영문자로 시작하고 영문자 · 숫자 · `_`만 받는다.
- 런타임에는 Yarn 변수 층이 없다 — 아무도 초기화 · 저장 · 되감지 않는다. 그래서 **`<<set>>`은 한 줄도 내지 않는다**: 남으면 롤백에서 안 되감겨
  리플레이가 다른 갈래를 탄다. 손으로 적은 `$변수`도 같은 까닭으로 막는다.
- Yarn 내장 `visited()` · `visited_count()`도 변수 저장소에 기대 롤백에서 안 되감긴다 — 쓰지 않는다. 「지나갔는가」는 챕터 깃발(Bool 스탯)로 적는다.

### 5.4 글자 · 막는 문제

- 대사에 `#` · `//` · `<<` · `\`를 써도 된다 — Yarn이 읽을 수 있게 바꿔 낸다. `{stat("키")}`는 스탯 읽기라 그대로 간다.
- 화자 없이 `->`나 `===`로 시작하는 줄, 짝이 안 맞는 `{`는 Yarn이 읽지 못한다 — **막는 문제**라 그 장면의 파일은 나가지 않고 검증 보고 맨 위에
  「챕터 · 장면: 무엇이 문제인지」가 선다.

### 5.5 보낼 곳

대사 Yarn은 **게임 폴더(진행 JSON의 보낼 곳)로 보내지 않는다** — 게임의 Yarn 프로젝트가 연출 든 Yarn과 같은 노드 이름을 둘 읽어 컴파일이
깨진다. 따로 정한 보낼 곳(`runtime.target.json`의 `dialogueYarnFolder`)이 있으면 저장할 때 그리로 비춘다(`ExportDelivery`).

---

## 6. 런타임 쪽에서 이 툴이 기대는 것

- **노드 이름으로 시작한다** — 런타임의 재생기는 Yarn 노드 이름 하나만 받는다. 어느 노드를 틀지는 진행 코어가 진행 JSON으로 정한다.
- **`#line:` 태그는 런타임이 요구하지 않는다** — 롤백은 한 세션 안에서 되감고, 세이브는 줄이 아니라 장면 시작점에서 재개한다. LineId는 **이쪽의
  열쇠**다 — 줄을 고쳐도 그 줄에 붙은 것(분기 · 서식 · 연출)이 따라오게 하는 최상위 불변식이다. 연출을 붙이는 쪽도 이 열쇠로 줄을 찾는다.
- **이어하기는 장면 시작점에서 재개한다** — 그래서 장면마다 밖에서 들어오는 자리가 하나여야 한다(§1 ③). 진행 상태는 **챕터 수명**이다 —
  챕터를 넘어 사는 값은 없다.
- **선택 이력은 `OptionIndex`, 버전은 바이트의 SHA-256**으로 서버에 간다(§3).

---

## 7. 열린 것

| 무엇 | 상태 |
|---|---|
| `NotEqual`(`!=`) | 저작 파서가 닫아 두었다 — 런타임에서 확인한 뒤 파서 · 문서 · 견본을 함께 연다 |
| 챕터를 넘어 사는 값 | 없다. 코어의 `ScenarioDefinition`은 챕터를 묶는 자리뿐이다(영구 계층 없음). 챕터 사이 진행이 생기면 그때 새 모양으로 정한다 |
| 다른 조건 종류(`ChapterCleared` · 아이템 보유) | 없다. 필요하면 갈 곳은 챕터 스탯(깃발)이다 |
| 선택지의 비용 미리보기 | 간선의 스탯변화를 선택지 단추에 보이는 길이 아직 없다 |
| 진행 JSON의 남은 칸 | `IndexText` · 노드의 빈 `VisibleConditions` · `UnlockConditions`는 코어가 읽지 않는다 — 걷으면 모든 챕터의 바이트가 바뀐다(§3) |
