# 작가에게 앱과 프로젝트를 건넨다 — 소유자(개발자)가 할 일

작가는 Windows에서 **스토리 디렉터**로 쓴다. **지금은 프로젝트(`project.vnproject.json`이 있는 폴더)를 zip으로 주고받고**, 본격적으로 함께
쓰게 되면 **GitHub 비공개 저장소(Git)** 로 옮긴다. 작가 쪽 절차는 앱 꾸러미의 「먼저 읽어주세요.txt」(= [`작가에게.txt`](작가에게.txt))
「프로젝트 주고받기」 절에 있다 — 이 문서는 그 짝이다. 판을 내는 법은 3절, 판마다 섞어 쓰면 안 되는 까닭은 6절.

| 지금 (zip) | 나중 (Git) |
|---|---|
| 3절(앱 건네기) → 0절(프로젝트 zip) | 1 · 2절(저장소 · 두 파일) → 4 · 5절 |

## 0. 지금 — 프로젝트를 zip으로 주고받는다

**건넬 때**
1. 스토리 디렉터에서 프로젝트를 저장(Ctrl+S)하고 닫는다.
2. 탐색기에서 프로젝트 폴더를 **복사해 붙여** 보낼 사본을 만들고, 이름에 날짜를 붙인다(예: `우리게임-1120`). 사본에서
   `runtime.target.json`(이 PC의 보낼 곳 — [⚙ 설정] → 「내보내기」)을 **지운다** — 경로가 이 PC의 것이다. `exported/`도 지워도 된다(작가가
   저장하면 다시 만들어진다). 원본은 건드리지 않는다.
3. 사본 폴더를 우클릭 → Windows 11은 [압축 대상] → [ZIP 파일], Windows 10은 [보내기] → [압축(ZIP) 폴더]. 다 묶었으면 사본 폴더는 지워도 된다.
4. 앱 zip(3절)과 함께 건넨다.

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

이 저장소(ked-story-director)의 `main`에서 굽는다. 일은 `dev`에서 하고, 판을 낼 때 `dev`를 `main`에 올린다(기본 가지는 `main`). `main`은 늘
`dev`의 앞감기다 — 강제 밀기는 하지 않는다.

0. `dev`의 CI(Actions의 「test」 — Windows · 맥)가 초록인지 본다. 그다음 `dev`를 `main`에 올린다(PowerShell · 저장소 폴더에서):
   `git checkout main` → `git pull --ff-only` → `git merge --ff-only dev` → `git push origin main` → `git checkout dev`. 이미 같으면 건너뛴다.
1. github.com의 ked-story-director → **Releases** → **Draft a new release**.
2. **Choose a tag** → `v0.1.0`처럼 적고 **Create new tag: v0.1.0 on publish** → **Target: `main`**.
3. 제목(예: `스토리 디렉터 0.1.0`)과 릴리스 노트 → **[Publish release]**. 노트는 「받을 파일」(두 zip · 안에 든 「먼저 읽어주세요.txt」 ·
   「Read me first.txt」 · 「THIRD-PARTY-NOTICES.txt」) · 「새로 생긴 것」 · 「고친 것 · 달라진 것」 · 「앞 판과 섞어 쓰지 마세요」(6절)로 쓴다.
   노트는 `docs/release-notes/{판}.md`에 먼저 적어 두고 붙여 넣는다(예: [`0.4.0.md`](release-notes/0.4.0.md)).
4. 10분쯤 뒤 그 릴리스에 `StoryDirector-0.1.0-windows.zip` · `StoryDirector-0.1.0-mac.zip`이 붙는다(Actions의 「release」 작업 —
   시험을 먼저 돌린다. 실패하면 붙지 않는다 — Actions 탭에서 까닭을 본다).
5. Windows zip을 받아 작가에게 건넨다(메신저 · 드라이브). 저장소가 비공개라 작가는 릴리스 페이지에 들어오지 못한다.

판 번호는 태그에서 온다 — 작가의 창 제목에 「스토리 디렉터 0.1.0」으로 선다. 새 판은 번호를 올려(`v0.1.1` · `v0.2.0`) 같은 길로 굽는다.
릴리스 페이지에 GitHub가 저절로 붙이는 「Source code」 둘은 코드 묶음이라 건네지 않는다.

**꾸러미로 볼 것** — 창 제목의 판 번호 · zip 안의 안내문 둘과 고지문 · [⚙ 설정] → 「기타」 → 「언어 (Language)」를 바꿔 다시 켜면 그 언어로 서는지
(맥은 메뉴 막대 · 독의 앱 이름도).

## 4. 개발자 쪽의 흐름

- 작가가 올린 것을 받는다(GitHub Desktop [Fetch origin] → [Pull origin]). 스토리 디렉터를 켜 둔 채 받아도 된다 — 저장하지 않은 것이 없으면
  다시 읽고, 있으면 위에 노란 띠가 떠서 고르게 한다.
- **게임 폴더로 내보내기는 개발자의 PC에서 저장할 때 일어난다** — `runtime.target.json`(보낼 곳)은 올리지 않으므로 각자의 것이다. 받은 뒤
  스토리 디렉터로 열고 저장(Ctrl+S)하면 진행 JSON이 게임 폴더로 간다.
- [구성] 탭의 일(ID · 조건 식 · 스탯 · 캐릭터 키)을 할 때는 작가와 때를 나눈다 — 둘이 같은 파일을 함께 고치면 Git이 합치지 못한다.
  작가에게 「지금 제가 고칩니다」, 끝나면 올리고 「다 했습니다」.

## 5. 충돌(conflict)이 났을 때

JSON 파일의 충돌은 손으로 합치기 어렵다. 한쪽을 고른다 — GitHub Desktop에서 충돌 파일마다 **Use the version from main / origin**(어느
쪽을 남길지)을 고르고 커밋한 뒤, 스토리 디렉터로 열어 [구성] 탭의 검증 보고를 본다. 버린 쪽의 내용은 Git 기록(History)에 남아 있어 필요한
것을 다시 옮겨 적을 수 있다.

## 6. 판마다 — 앞 판과 섞어 쓰지 않는다

한 프로젝트를 두 판의 앱으로 번갈아 열지 않는다. 판이 올라갈 때 무엇이 달라지는지:

| 판 | 프로젝트 형식 | 앞 판으로 열면 |
|---|---|---|
| 다음 판 (준비 중) | 5(판 파일 3) — 카드의 기본 크기 고정(`layout.pinnedToBase` · `unpinnedWidth` · `unpinnedWritingHeight`)이 판 파일에 실린다 | 0.4.0으로도 열리지만, 저장할 때 고정이 사라진다(카드는 기본 크기로 남는다) |
| 0.4.0 | **5**(판 파일 3) — 숨은 조건 공급 노드가 없고 대사의 조건이 챕터 조건의 이름을 가리킨다 | 0.3.x는 열지 않는다(「형식 버전 5」). 0.4.0도 0.3.x까지의 프로젝트(형식 4)를 열지 않는다 — 옮기는 길은 없다 |
| 0.3.0 | 4 | 0.2.0으로도 열리지만, 저장할 때 카드마다의 가로 · 세로 글 맞춤과 줄 사이에 띄운 빈 줄이 사라진다 |
| 0.2.0 | 4 | 0.1.0으로도 열리지만, 저장할 때 글 서식 · 화자 이름 색 · 바탕색 · 카드마다의 처음 글꼴 값이 사라진다 |
| 0.1.0 | 4 | — |

## 7. 제3자 소프트웨어 고지

꾸러미에는 저장소 맨 위의 `THIRD-PARTY-NOTICES.txt`가 함께 든다(`publish-windows.ps1` · `publish-macos.sh`). 실려 나가는 패키지(Avalonia ·
SkiaSharp · HarfBuzzSharp · ANGLE · .NET 런타임 등)의 고지문 원문을 고치지 않고 옮긴 것이다. 시험 `ThirdPartyNoticesTests`가 고지문이 싣는 패키지의
판을 지금 판과 견준다 — 패키지 판을 올려 시험이 깨지면 새 판 패키지의 `LICENSE` · `THIRD-PARTY-NOTICES.txt`(NuGet 캐시의 그 패키지 폴더)를 다시
옮기고 목록의 판 번호를 고친다. 새 패키지가 꾸러미에 실리면 목록과 시험의 `Shipped`에 더한다.
