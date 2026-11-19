# 작가에게 앱과 프로젝트를 건넨다 — 소유자(개발자)가 할 일

작가는 Windows에서 **스토리 디렉터**로 쓴다. **지금은 프로젝트(`project.vnproject.json`이 있는 폴더)를 zip으로 주고받고**, 본격적으로 함께
쓰게 되면 **GitHub 비공개 저장소(Git)** 로 옮긴다(2026-11-19 소유자 · [`docs/work-orders/writer-release-orders.md`](../work-orders/writer-release-orders.md)).
작가 쪽 절차는 앱 꾸러미의 「먼저 읽어주세요.txt」(= [`docs/작가에게.txt`](../작가에게.txt)) 「프로젝트 주고받기」 절에 있다 — 이 문서는 그 짝이다.

| 지금 (zip) | 나중 (Git) |
|---|---|
| 3절(앱 건네기) → 0절(프로젝트 zip) | 1 · 2절(저장소 · 두 파일) → 4 · 5절 |

## 0. 지금 — 프로젝트를 zip으로 주고받는다

**건넬 때**
1. 스토리 디렉터에서 프로젝트를 저장(Ctrl+S)하고 닫는다.
2. 탐색기에서 프로젝트 폴더를 우클릭 → [압축(ZIP) 파일로 보내기]. 이름에 날짜를 붙인다(예: `우리게임-1120.zip`). `exported/`는 빼도 된다
   (작가가 저장하면 다시 만들어진다). `runtime.target.json`(이 PC의 보낼 곳 — [⚙ 설정] → 「내보내기」)은 **빼고 보낸다** — 경로가 이 PC의 것이다.
3. 앱 zip(3절)과 함께 건넨다.

**돌려받을 때**
1. 작가의 zip을 새 폴더에 푼다 — 지금 쓰는 프로젝트 폴더 위에 바로 풀지 않는다.
2. 지금 폴더는 이름에 날짜를 붙여 남기고(백업), 받은 폴더를 그 자리에 둔다 — 게임 폴더로 내보내려면 `runtime.target.json`을 받은 폴더로 옮긴다.
3. 스토리 디렉터로 열고 저장(Ctrl+S)하면 진행 JSON이 게임 폴더로 간다.

**때를 나눈다** — 작가에게 보낸 뒤 돌려받을 때까지 이쪽에서 그 프로젝트를 고치지 않는다([구성] 탭의 일도). 둘이 따로 고친 것은 zip으로는 합칠
수 없다. 고칠 것이 있으면 작가에게 「보내 주세요」 → 고치고 → 다시 보낸다.

> 아래 1 · 2 · 4 · 5절은 **Git으로 옮길 때** 쓴다.

## 1. 프로젝트 저장소를 만든다 (한 번)

프로젝트 폴더는 지금 소유자의 PC에만 있다. GitHub Desktop으로 올리는 것이 가장 짧다.

1. 프로젝트 폴더에 아래 두 파일을 만든다(2절) — 첫 올리기 전에.
2. GitHub Desktop → File → **Add local repository** → 프로젝트 폴더를 고른다 → 「This directory does not appear to be a Git repository」가
   뜨면 **create a repository** → Name은 그대로, [Create repository].
3. 위쪽 **[Publish repository]** → **Keep this code private** 를 켠 채로 [Publish repository].
4. github.com의 그 저장소 → Settings → **Collaborators** → [Add people] → 작가의 GitHub 아이디. 작가는 메일로 초대를 수락한다.

> 저장소 이름은 무엇이든 된다(예: `ked-story`). 프로젝트 폴더가 이미 다른 저장소(게임 저장소 등) 안에 있다면 그 저장소를 작가에게
> 열지 말고, 프로젝트 폴더만 따로 저장소로 둔다 — 작가에게 보일 것은 프로젝트뿐이다.

## 2. 저장소에 둘 두 파일

**`.gitignore`** — 올리지 않을 것.

```gitignore
# 스토리 디렉터가 저장할 때마다 다시 만드는 결과물(진행 JSON · 대사 Yarn) — 각자의 PC에서 저장하면 같은 것이 나온다
exported/

# 이 PC의 보낼 곳([⚙ 설정] → 「내보내기 (복제할 폴더 선택)」) — PC마다 경로가 다르다
runtime.target.json

# 저장 도중 꺼졌을 때 남는 임시 파일
*.tmp

# OS가 만드는 파일
Thumbs.db
desktop.ini
.DS_Store
```

**`.gitattributes`** — 줄 끝을 LF로 고정한다. 스토리 디렉터는 늘 LF로 쓴다(UTF-8 · BOM 없음). Windows의 Git이 받을 때 CRLF로 바꾸면
저장할 때마다 파일 전체가 바뀐 것처럼 보인다.

```gitattributes
*.json text eol=lf
*.yarn text eol=lf
```

## 3. 앱을 건넨다 — GitHub 릴리스

이 저장소(ked-story-director)의 `dev`에서 굽는다.

1. github.com의 ked-story-director → **Releases** → **Draft a new release**.
2. **Choose a tag** → `v0.1.0`처럼 적고 **Create new tag: v0.1.0 on publish** → **Target: `dev`**.
3. 제목(예: `스토리 디렉터 0.1.0`)과 바뀐 것 몇 줄 → **[Publish release]**.
4. 10분쯤 뒤 그 릴리스에 `StoryDirector-0.1.0-windows.zip` · `StoryDirector-0.1.0-mac.zip`이 붙는다(Actions의 「release」 작업 —
   시험을 먼저 돌린다. 실패하면 붙지 않는다 — Actions 탭에서 까닭을 본다).
5. Windows zip을 받아 작가에게 건넨다(메신저 · 드라이브). 저장소가 비공개라 작가는 릴리스 페이지에 들어오지 못한다.

판 번호는 태그에서 온다 — 작가의 창 제목에 「스토리 디렉터 0.1.0」으로 선다. 새 판은 번호를 올려(`v0.1.1` · `v0.2.0`) 같은 길로 굽는다.

## 4. 개발자 쪽의 흐름

- 작가가 올린 것을 받는다(GitHub Desktop [Fetch origin] → [Pull origin]). 스토리 디렉터를 켜 둔 채 받아도 된다 — 저장하지 않은 것이 없으면
  다시 읽고, 있으면 위에 노란 띠가 떠서 고르게 한다(WR1).
- **게임 폴더로 내보내기는 개발자의 PC에서 저장할 때 일어난다** — `runtime.target.json`(보낼 곳)은 올리지 않으므로 각자의 것이다. 받은 뒤
  스토리 디렉터로 열고 저장(Ctrl+S)하면 진행 JSON이 게임 폴더로 간다.
- [구성] 탭의 일(ID · 조건 식 · 스탯 · 캐릭터 키)을 할 때는 작가와 때를 나눈다 — 둘이 같은 파일을 함께 고치면 Git이 합치지 못한다.
  작가에게 「지금 제가 고칩니다」, 끝나면 올리고 「다 했습니다」.

## 5. 충돌(conflict)이 났을 때

JSON 파일의 충돌은 손으로 합치기 어렵다. 한쪽을 고른다 — GitHub Desktop에서 충돌 파일마다 **Use the version from main / origin**(어느
쪽을 남길지)을 고르고 커밋한 뒤, 스토리 디렉터로 열어 [구성] 탭의 검증 보고를 본다. 버린 쪽의 내용은 Git 기록(History)에 남아 있어 필요한
것을 다시 옮겨 적을 수 있다.
