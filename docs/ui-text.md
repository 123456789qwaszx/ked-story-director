# 화면 문구 — 두 언어 표 · 고치는 법 · 용어집

화면은 한국어(기본)와 영어 두 벌이다. 처음 언어는 PC 언어를 따르고, [⚙ 설정] → 「기타」 → 「언어 (Language)」에서 바꾸면 다시 켤 때 적용된다.
새로 만드는 것의 처음 이름 · 검증 보고의 자리 이름 · 대사 Yarn 주석의 머리말 · 툴이 쓰는 JSON의 안내 칸도 그때의 화면 언어로 적힌다 — 이미 적힌
이름과 작가가 적은 글은 바꾸지 않는다.

## 1. 문구 표 고치기

- **어디** — 화면 문구는 `src/Vn.App/Strings/Ui.resx`(한국어 · 기본)와 `Ui.en.resx`(영어)에 **같은 키로 한 줄씩** 있다. 검증 보고 · 고치는 법 ·
  거절 사유 · 파일 읽기 오류(`Vn.Authoring`)는 `src/Vn.Authoring/Strings/Msg.resx` · `Msg.en.resx`. 두 표 모두 같은 규칙이다.
- **Rider** — 표 파일을 열면(또는 Localization Manager 창) 키 · 한국어 · 영어 · 메모가 한 표로 보인다. 칸을 고치고 저장한 뒤 빌드한다.
- **그냥 편집기** — `<data name="키"><value>문구</value><comment>어디에 나오는지</comment></data>`. 문구 안의 `<` · `&`는 `&lt;` · `&amp;`로 적는다.
- **`{0}` · `{1}`** — 값이 들어가는 자리다. 지우거나 번호를 바꾸지 않는다. 영어는 한국어보다 자리를 덜 써도 된다(조사가 드는 자리처럼 한국어에만
  있는 자리). 중괄호를 글자로 보이려면 `{{` · `}}`.
- **키 이름** — `자리_무엇`(예: `Settings_LanguageLabel`). 키를 바꾸거나 지우면 코드도 고쳐야 한다 — 빌드가 그 자리를 알려 준다.
- **영어 칸이 비면** 그 문구는 한국어로 보인다. 시험(`UiLanguageTests`)이 두 표의 키가 같은지 · 영어 칸이 비지 않았는지 · 영어가 한국어에 없는
  자리를 쓰지 않는지를 본다. 한국어 문구를 고치면 그 문장을 확인하던 시험이 깨질 수 있다.
- **고친 문구가 칸에 드는지** — `tests/Vn.App.Tests/UiTextFitProbe.cs`. 평소 시험에서는 아무것도 하지 않는다. 환경 변수를 켜고 이것만 돌리면
  주요 화면을 그 언어로 그려(창 폭 1400 · 최소 1000) 그림과 「칸보다 긴 글」 목록(`report.tsv`)을 임시 폴더의 `vn-ui-probe`에 남긴다 —
  `$env:VN_UI_PROBE="en"; dotnet test tests/Vn.App.Tests --filter "FullyQualifiedName~UiTextFitProbe"; Remove-Item Env:VN_UI_PROBE`(한국어는 `ko`).
  판 위의 글(카드 제목 · 선택지 문구)과 하이어아키의 긴 이름은 데이터 길이라 원래 줄임표로 줄어든다 — 문구 표의 글을 본다.
- **수 뒤의 낱말(영어)** — `{2:line|lines}`처럼 적으면 그 자리의 수가 1일 때 앞 낱말(*1 line*), 그 밖(0 · 2 …)이면 뒤 낱말(*3 lines*)로 펴진다.
  수는 그 자리에 함께 찍힌다 — `{2} lines`라고 따로 쓰지 않는다. 동사까지 수를 따르는 문장(*1 line is / 3 lines are*)은 수와 상관없는 문장으로
  짓는다(*Dialogue lines reading this stat were kept (3)*). 한국어 칸에는 쓰지 않는다. 이 꼴은 코드가 `Plural.Format`으로 펴야 해서, **처음으로 이 꼴을
  넣는 키**는 코드도 바꿔야 한다 — 시험(`UiLanguageTests` · `MessageTableTests`)이 그 자리를 알려 준다.
- 「지금 문구 → 바꿀 문구」로 말해 주셔도 된다 — 찾아 고치고 시험까지 맞춘다.

## 2. 용어집 — 말투

- **단추 · 메뉴 · 탭 · 칸 이름** — 첫 글자만 대문자(sentence case): `New project` · `Fit to screen` · `Conditions & stats`. 동사로 시작하는 단추는
  명령형(`Delete`, `Copy report`).
- **탭 · 패널을 글 안에서 부를 때** — 지금처럼 대괄호를 쓴다: `[Script] tab`, `[Structure] tab`. 한국어의 「」는 영어에서 큰따옴표 “ ”로 — `the “Speakers” tab`.
- **안내문 · 풍선** — 작가에게 말하듯 2인칭 · 현재형, 짧게: *Press Enter to rename. Esc cancels.* 한국어의 「— 까닭」 꼴은 em dash 그대로 둔다.
- **상태줄** — 한 일을 과거형으로, 수를 붙여: *Renamed 'trust' → 'favor' — 1 path, 2 conditions, 3 dialogue lines followed.*
- **검증 보고 · 거절 사유** — 한국어와 같은 규칙: **무엇이 · 어디서 · 어떻게 고치는지**를 한 문장씩. 「~할 수 없습니다」는 *Can't …* 보다 *… isn't possible because …*
  처럼 까닭을 앞세운다. 오류 = **Error**, 경고 = **Warning**.
- **조사(`Josa`)를 쓰던 문장**은 영어에서 문장을 새로 짓는다 — 단수 · 복수(`1 line` / `3 lines`)를 꼭 맞춘다. 수 뒤 낱말은 문구 표에
  `{2:line|lines}`로 적는다(1절). 동사까지 수를 따르는 문장은 수와 상관없는 꼴로 짓는다.
- **만들어지는 글도 화면 언어로**: 새로 만든 것의 처음 이름(아래 3절), 검증 보고의 자리 이름(6절), 대사 Yarn 주석의
  머리말 · 툴이 쓰는 JSON의 안내 칸(7절)은 만들 때 · 내보낼 때의 화면 언어로 적힌다. **이미 적힌 이름 · 작가가 적은 글은 바꾸지 않는다.**

## 3. 이야기의 짜임

| 한국어 | 영어 | 메모 |
|---|---|---|
| 프로젝트 | Project | |
| 챕터 | Chapter | |
| 장면 | Scene | |
| 에피소드 | Episode | |
| 챕터ID · 장면ID · 에피소드ID | Chapter ID · Scene ID · Episode ID | |
| 장면 이름 | Scene name | |
| (카드) 제목 | Title | |
| 카드 · 대사 카드 | Card | 「노드」는 개발 문서에만 — 화면은 Card |
| 분기 카드 | Branch card | |
| 분기 — 반드시 한 번 다녀오는 통로 | Branch — a detour that always comes back | 분기 카드 부제 「들렀다 돌아오는 카드」 → *A detour card* · 판의 분기 선 이름표 → *Branch* |
| 분기 카드 ID | Branch card ID | Yarn 노드 이름 |
| 조건 없음 (분기 카드 머리의 표) | Always | 「⑂ Always」 — [구성] 조건 칸의 *(always)*와 같은 말. 카드 머리가 좁아 *No condition* 대신 |
| 새 에피소드 · 새 장면 · 새 챕터 · 새 분기 (처음 이름) | New episode · New scene · New chapter · New branch | 만들 때의 화면 언어로 적힌다 |
| 장면 시작점(⌂) | Scene start | |
| 선택지 | Choice | |
| 선택지 문구 | Choice text | 「문구를 적어 주세요」 → *Write the choice text* |
| 자동 길 · 자동 진행 | Auto path | 「자동 진행 길」 → *Auto path — moves on without player input* |
| 길 · 선 · 간선 | Path | 화면에서는 셋 모두 Path. 「간선」은 데이터 낱말로 남는다(위 2절) |
| 이어진 · 잇기 · 끊기 | Connected · Connect · Disconnect | |
| 빈 선택지 | Empty choice | 「빈 선택지 숨기기」 → *Hide empty choices* |

## 4. 조건 · 스탯

| 한국어 | 영어 | 메모 |
|---|---|---|
| 조건 | Condition | |
| 조건 이름 | Condition name | |
| 식 | Expression | 「조건 (이름 → 식)」 → *Conditions (name → expression)* |
| 식 미정 | Undecided | *Undecided — always passes* |
| 표시조건 | Show condition | *Shown only when this holds* |
| 해금조건 | Unlock condition | *Can be picked only when this holds (otherwise shown locked)* |
| 잠금 안내문 | Locked hint | 예: *Not enough trust* |
| 스탯 | Stat | |
| 스탯 키 | Stat key | 「세이브의 열쇠」 → *the key in save data* |
| 스탯변화 | Stat change | *Stat change — when this path is picked* |
| 처음 값 · 최소 · 최대 | Initial · Min · Max | |
| 숫자 · 예/아니오 | Number · Yes/No | 스탯 종류 |
| 픽스처 | Test start | 재생루트를 시험할 때의 시작 값들. 개발 문서는 fixture |
| 재생루트 | Playthrough | |
| 쓰는 곳 | Used in | 지우기 거절 목록의 머리 |
| 모두 비우고 지우기 | Clear all uses and delete | |
| 세이브 | Save data | 게임의 세이브. 툴의 「저장」(Save)과 갈라 쓴다 |

## 5. 글쓰기

| 한국어 | 영어 | 메모 |
|---|---|---|
| 대본 | Script | |
| 대사 | Dialogue | 「대사 3줄」 → *3 dialogue lines* |
| 줄 | Line | |
| 화자 | Speaker | |
| 지문 | Narration | 화자 없는 줄 |
| 캐릭터 키 | Character key | 임시 키 → *Temporary key* |
| 글 칸 | Writing box | |
| 가로 글 맞춤 · 세로 글 맞춤 | Fit width · Fit height | 단추 풍선 |
| 서식 | Formatting | |
| 화자 이름 색 · 바탕색 | Speaker name color · Background color | |
| 처음 글꼴 값 | Default font size | |
| 쓴 글의 양 | Text written | 세는 것은 그대로 — *Characters · Characters (no spaces) · Lines* |
| 대사에 붙일 글 | Paste into dialogue | `{stat("키")}` 복사 칸 |

## 6. 화면

| 한국어 | 영어 | 메모 |
|---|---|---|
| [대본] 탭 | [Script] tab | 코드의 `MainTab.Script`와 같다 |
| [구성] 탭 | [Structure] tab | 코드의 `MainTab.Structure`와 같다 |
| 판 | Board | |
| 하이어아키 | Hierarchy | |
| 인스펙터 | Inspector | |
| 오른쪽 기둥 · 곁기둥 | Side panel | |
| 「조건 및 스탯」 탭 | “Conditions & stats” tab | |
| 「화자」 탭 | “Speakers” tab | |
| 검증 보고 | Validation report | 「보고 복사」 → *Copy report* |
| 검증 보고의 자리 — 에피소드 · 간선 · 조건 · 스탯 · 선택지 · 장면 메모 · 화자 | Episodes · Paths · Conditions · Stats · Choices · Scene notes · Speakers | 「ch01 · 간선 — …」 → *ch01 · Paths — …* |
| 오류 · 경고 | Error · Warning | 🔴 · 🟡 표는 그대로 |
| 고치는 법 | How to fix | |
| 미니맵 | Minimap | |
| 화면 맞춤 | Fit to screen | |
| 전체 | Fit all | 미니맵 아래 단추 |
| 자동 정렬 | Auto layout | |
| ID로 보기 | Show IDs | |
| 찾기 | Find | 「앞의 것 · 다음 것」 → *Previous · Next* |
| 접기 · 펴기 | Collapse · Expand | |
| 제목 고치기 | Rename | 카드 우클릭 차림표 |
| [구성] 탭에서 보기 | Show in [Structure] tab | |
| 상태줄 | Status bar | |
| 설정 | Settings | 「⚙ 설정」 → *⚙ Settings* |
| 설명 간소화 | Fewer hints | |
| 휠로 판 옮기기 | Wheel pans the board | |
| [대본] 판 크기 | [Script] board size | |
| 복제할 폴더 | Mirror folder | 게임 폴더 등 |
| 단축키 | Shortcuts | |
| 언어 | Language | 설정의 새 칸 — *Applies after restart* |

## 7. 파일 · 편집

| 한국어 | 영어 | 메모 |
|---|---|---|
| 새 프로젝트… · 열기… · 저장 | New project… · Open… · Save | |
| 되돌리기 · 다시 실행 | Undo · Redo | |
| 작업 폴더 | Work folder | `exported/` |
| 내보내기 | Export | |
| 진행 JSON | Progression JSON | |
| 대사 Yarn | Yarn dialogue | |
| Yarn 주석의 머리말 — 장면 · 에피소드 · 분기 카드(「…」에서 들른다) | Scene · Episode · Branch card (visited from “…”) | 내보낼 때의 화면 언어로 |
| 안내 칸(툴이 쓰는 JSON) | `_note` | 한국어는 `_안내` |
| 다시 열기 (여기서 고친 것 버림) | Reload (discard changes here) | 바깥에서 파일이 바뀌었을 때 |
| 내 것으로 덮어쓰기 | Overwrite with mine | |
| 삭제 · 지우기 | Delete | |
| 추가 · 더하기 | Add | 「＋ 새 조건」 → *＋ New condition* |
| 그만두기(Esc) | Cancel | |
