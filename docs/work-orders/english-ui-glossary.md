# 영어 화면 용어집 (E0) — 소유자 확인용

**상태: 확인됨(2026-11-25 소유자 — 「용어집 확인 함」).** 옮기는 동안 이 표를 따른다 — 낱말을 바꾸려면 이 표와 문구 표를 함께 고친다. 지시서는
[`english-ui-orders.md`](english-ui-orders.md)(문구 표를 고치는 법은 그 5절).

## 1. 말투

- **단추 · 메뉴 · 탭 · 칸 이름** — 첫 글자만 대문자(sentence case): `New project` · `Fit to screen` · `Conditions & stats`. 동사로 시작하는 단추는
  명령형(`Delete`, `Copy report`).
- **탭 · 패널을 글 안에서 부를 때** — 지금처럼 대괄호를 쓴다: `[Script] tab`, `[Structure] tab`. 한국어의 「」는 영어에서 큰따옴표 “ ”로 — `the “Speakers” tab`.
- **안내문 · 풍선** — 작가에게 말하듯 2인칭 · 현재형, 짧게: *Press Enter to rename. Esc cancels.* 한국어의 「— 까닭」 꼴은 em dash 그대로 둔다.
- **상태줄** — 한 일을 과거형으로, 수를 붙여: *Renamed 'trust' → 'favor' — 1 path, 2 conditions, 3 dialogue lines followed.*
- **검증 보고 · 거절 사유** — 한국어와 같은 규칙: **무엇이 · 어디서 · 어떻게 고치는지**를 한 문장씩. 「~할 수 없습니다」는 *Can't …* 보다 *… isn't possible because …*
  처럼 까닭을 앞세운다. 오류 = **Error**, 경고 = **Warning**.
- **조사(`Josa`)를 쓰던 문장**은 영어에서 문장을 새로 짓는다 — 단수 · 복수(`1 line` / `3 lines`)를 꼭 맞춘다.
- **만들어지는 글도 화면 언어로**(2026-11-25 소유자 바로잡음 · E8): 새로 만든 것의 처음 이름(아래 2절), 검증 보고의 자리 이름(5절), 대사 Yarn 주석의
  머리말 · 툴이 쓰는 JSON의 안내 칸(6절)은 만들 때 · 내보낼 때의 화면 언어로 적힌다. **이미 적힌 이름 · 작가가 적은 글은 바꾸지 않는다.** 숨은 신원(조건
  공급 노드의 이름 같은)은 언어와 상관없이 그대로다. ⛔ E0–E7은 「한국어 그대로」였다(잘못 알아들었다).

## 2. 이야기의 짜임

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
| 조건 없음 (분기 카드 머리의 표) | Always | 「⑂ Always」 — [구성] 조건 칸의 *(always)*와 같은 말. 카드 머리가 좁아 *No condition* 대신(E7) |
| 새 에피소드 · 새 장면 · 새 챕터 · 새 분기 (처음 이름) | New episode · New scene · New chapter · New branch | 만들 때의 화면 언어로 적힌다(E8) |
| 장면 시작점(⌂) | Scene start | |
| 선택지 | Choice | |
| 선택지 문구 | Choice text | 「문구를 적어 주세요」 → *Write the choice text* |
| 자동 길 · 자동 진행 | Auto path | 「자동 진행 길」 → *Auto path — moves on without player input* |
| 길 · 선 · 간선 | Path | 화면에서는 셋 모두 Path. 「간선」은 데이터 낱말로 남는다(위 1절) |
| 이어진 · 잇기 · 끊기 | Connected · Connect · Disconnect | |
| 빈 선택지 | Empty choice | 「빈 선택지 숨기기」 → *Hide empty choices* |

## 3. 조건 · 스탯

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
| 공급 조건 | Supplied condition | 판에 내려 준 조건 사본 — 화면에는 드물다 |
| 쓰는 곳 | Used in | 지우기 거절 목록의 머리 |
| 모두 비우고 지우기 | Clear all uses and delete | |
| 세이브 | Save data | 게임의 세이브. 툴의 「저장」(Save)과 갈라 쓴다 |

## 4. 글쓰기

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

## 5. 화면

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
| 검증 보고의 자리 — 에피소드 · 간선 · 조건 · 스탯 · 선택지 · 장면 메모 · 화자 | Episodes · Paths · Conditions · Stats · Choices · Scene notes · Speakers | 「ch01 · 간선 — …」 → *ch01 · Paths — …*(E8) |
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
| 언어 | Language | 설정의 새 칸(E1) — *Applies after restart* |

## 6. 파일 · 편집

| 한국어 | 영어 | 메모 |
|---|---|---|
| 새 프로젝트… · 열기… · 저장 | New project… · Open… · Save | |
| 되돌리기 · 다시 실행 | Undo · Redo | |
| 작업 폴더 | Work folder | `exported/` |
| 내보내기 | Export | |
| 진행 JSON | Progression JSON | |
| 대사 Yarn | Yarn dialogue | |
| Yarn 주석의 머리말 — 장면 · 에피소드 · 분기 카드(「…」에서 들른다) | Scene · Episode · Branch card (visited from “…”) | 내보낼 때의 화면 언어로(E8) |
| 안내 칸(툴이 쓰는 JSON) | `_note` | 한국어는 `_안내`(E8) |
| 다시 열기 (여기서 고친 것 버림) | Reload (discard changes here) | 바깥에서 파일이 바뀌었을 때 |
| 내 것으로 덮어쓰기 | Overwrite with mine | |
| 삭제 · 지우기 | Delete | |
| 추가 · 더하기 | Add | 「＋ 새 조건」 → *＋ New condition* |
| 그만두기(Esc) | Cancel | |
