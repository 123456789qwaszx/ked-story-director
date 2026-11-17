# Yarn 출력 지시서 (Y0–Y5) — 2026-11-17

**원고에서 대사만 있는 Yarn을 다시 이 툴이 낸다.** 2026-10-29(R2 · R3 · L1)에 게임용 Yarn을 compositor로 넘기며 이미터 · 컴파일 확인 ·
`Vn.Core`를 걷었는데, 소유자가 그 방향을 바꿨다: compositor를 사람이 쓸 일이 없고(연출은 AI가 넣는다), 작가는 **대사만으로 굴러가는지**
확인해야 한다. 이 지시서는 그 1단계(대사만)다 — 연출을 합치는 2단계는 따로 쓴다.

> **상태: 1단계 끝 (Y0–Y5, 2026-11-17).** 3절의 A · B · C는 소유자가 정했다. 연출을 합치는 2단계는 따로 쓴다.

## 1. 정한 것 (소유자 2026-11-17)

| | 내용 |
|---|---|
| 내는 쪽 | 대사만 있는 Yarn은 **이 툴이 낸다**. ⛔ 2026-10-29의 「게임용 Yarn은 compositor만 낸다」를 이 범위에서 뒤집는다 |
| 놓는 곳 | **`exported/yarn/`에만.** 게임 폴더(`runtime.target.json`)에는 쓰지 않는다 — compositor가 연출 든 Yarn을 거기 쓰고 있으면 덮여서 연출이 사라진다 |
| 메모 | 장면 메모 · 에피소드 지시는 **`.yarn` 파일에 주석으로** 함께 적는다. 장면 메모는 파일 맨 위에 `// 장면이름`처럼 남긴다 |
| 파일 단위 | **처음부터 장면 단위** — `exported/yarn/{챕터ID}_{장면ID}.yarn` 하나에 그 장면의 에피소드 노드가 모두 선다 (3절 A) |
| `DesignerNote` | **진행 JSON에서 칸째 없앤다** (3절 B). ⛔ 인계 문서의 「계약 모양은 바꾸지 않는다 — 칸은 남기고 값만」을 소유자가 바꿨다. 런타임 DTO에는 이 칸이 없어(`runtime-contract.md` F절) 읽는 쪽이 없다 |
| 컴파일 확인 | **시험 프로젝트에서만** (3절 C) — 앱은 컴파일러를 싣지 않는다 |
| (곁) 선택지 조건 | 선택지의 표시조건 · 해금조건은 **지금대로** — [대본]은 읽기만, 고치는 것은 [구성]에서. 인계 4절 3번은 닫는다 |

## 2. 설계 — 지킬 것

1. **노드 = 에피소드, 타이틀 = 진행 JSON의 `DialogueEntryId`와 같은 글자.** 판의 노드 이름(`ChapterBoard.EpisodeNodeNameFor`)이 주인이고
   못 찾을 때만 `YarnNaming.StoryNodeTitleOf` — `ChapterProgressionExporter.Node`와 **같은 함수를 지난다**(규칙 사본 금지).
   갈리면 진행 JSON이 없는 노드를 부른다(2026-08-23의 `new01` / `Story_new01`).
2. **본문은 `ResultDocumentComposer`의 Segment 목록에서 짓는다** — 카드 글 칸과 같은 합성기다(화면에서 본 것과 파일이 갈리지 않는다).
   대사 줄 `화자: 글 #line:{LineId}`, 조건 `<<if>>` · `<<elseif>>` · `<<endif>>`(식은 `stat("키")` 표기 그대로), 조건 갈래의 출구
   `<<detour 타이틀>>`. 식 미정 조건은 `true`(항상 통과 — 간선 · 분기와 같은 규칙).
3. **내지 않는 것** — `<<set>>` · `<<declare>>`(2026-09-17 — 런타임에 Yarn 변수 층이 없다), 선택지(챕터 간선의 것이다 — 진행 JSON이 낸다),
   연출 명령(2단계).
4. **빈 줄 · 빈 에피소드** — 글이 빈 줄은 U+00A0 한 글자로 채운다(걷어 낸 이미터의 규칙 그대로: `#line:`만 남은 줄은 파싱되지 않는다).
   카드나 줄이 없는 에피소드도 **노드는 선다** — 진행 JSON이 그 타이틀을 부르기 때문이다.
5. **주석** — 파일 머리에 장면, 노드마다 그 앞에 에피소드. 여러 줄 메모는 줄마다 `// `를 붙인다(Yarn 주석은 한 줄짜리다).

   ```
   // 장면: 축제 전야 (scene01)
   // 하린이 처음 웃는다. 민우는 눈치를 못 챈다.

   // 에피소드: 교문 앞
   // 짧게, 해 질 무렵
   title: ch01_scene01_ep01
   ---
   <<if stat("trust") >= 3>>
       하린: 왔구나. #line:a1
   <<endif>>
   하린: 안녕. #line:a2
   ===
   ```

   주석 든 이 모양이 YarnSpinner.Compiler 3.2.1에서 진단 없이 컴파일되는 것을 이 지시서를 쓰며 확인했다(노드 둘 · `stat()` · `<<detour>>` 포함).
   주인 없는 장면 메모는 Yarn에 싣지 않는다(붙을 장면이 없다 — `scenes.json`의 `orphanNotes`에 그대로).
6. **검증을 통과한 챕터만 나간다** — 진행 JSON과 같은 관문(G8), 같은 순간(`ChapterExportService.ExportAll`). 거부되면 그 챕터의 옛 파일은
   손대지 않는다. `exported/yarn/`은 이 툴이 모델에서 짓는 자리라 **지금 없는 장면의 `.yarn`은 걷는다**(진행 JSON · 장면 파일과 같은 규칙).
7. **결정적이다** — 같은 입력이면 같은 바이트(`ChapterExportBytes`: UTF-8 BOM 없음 · LF). 장면은 하이어아키 차례, 에피소드는 장면 안 차례.
8. **순수 문자열이 먼저, 파일은 그다음** — 이미터는 화면도 파일도 모른다. 막는 문제(LineId 없는 줄 · Yarn이 읽지 못하는 글 · 파일 이름 겹침)를 안은 장면은 쓰지 않는다.
9. `scenes.json`(`ked-scenes/1`)은 **그대로 낸다** — 지금 AI가 읽는 파일이다. Yarn과 겹치는 것을 걷을지는 2단계에서 정한다.

## 3. 소유자에게 확인받을 것

| | 물음 | 제안 |
|---|---|---|
| A ✅ 장면 단위 | 파일 단위를 **처음부터 장면 단위**로 할까, 에피소드마다 한 파일로 먼저 내고 나중에 바꿀까 | **처음부터 장면 단위** — `exported/yarn/{챕터ID}_{장면ID}.yarn`. 읽는 쪽이 아직 없어서 지금 정하면 이름을 두 번 바꾸지 않는다. 장면 메모가 설 「파일 맨 위」도 장면 단위일 때만 뜻이 선다 |
| B ✅ 칸째 없앤다 | 진행 JSON의 `DesignerNote` — 메모의 집이 Yarn 주석이 되면 | (제안했던 것) **비워 낸다**(칸은 남기고 값만 — 계약 모양 그대로). 런타임 DTO에는 이 칸이 없어 지금도 버려지고(`runtime-contract.md` F절), 작가 안내의 「메모는 게임에 나가지 않습니다」가 그대로 참이 된다. `exported/yarn/`은 게임 폴더가 아니다 |
| C ✅ 시험에서만 | 컴파일 확인을 **어디서** 하나. 인계 문서는 「저장소에 남은 `Vn.Core`」라고 했지만 **`Vn.Core`는 L1(`d9d5daca`)에 걷혀 없다** — `YarnSpinner.Compiler` 패키지를 다시 들여야 한다 | **내보낼 때마다 앱에서 컴파일한다** — 「대사만으로 굴러가는지」를 작가가 보는 곳이 그 자리다. 작가가 글에 `<<` · `#` · `{`를 적으면 시험은 통과해도 그 원고의 Yarn은 깨진다. 실패한 장면은 쓰지 않고 내보내기 보고에 어느 줄인지 적는다. (작게 가려면: 시험 프로젝트에만 들이고 앱은 컴파일하지 않는다) |

## 4. 단계

| | 할 일 | 끝 |
|---|---|---|
| Y0 | 이 지시서 | 소유자 확인(3절) |
| Y1 | 이미터 — `Chapters/ChapterYarnExporter`(순수 문자열): 장면 하나 → 파일 글. `YarnSyntax`에 들여쓰기 · `<<detour>>` 표기를 되살린다(R3 `86944596` 직전의 것). 타이틀은 진행 JSON과 같은 함수 | 골든 시험(주석 · 조건 · detour · 빈 줄 · 빈 에피소드 · 식 미정), 타이틀 = `DialogueEntryId` 시험 |
| Y2 | 컴파일 확인 — `YarnSpinner.Compiler` 3.2.1을 **시험 프로젝트에만**. 작가 글의 Yarn 글자(`#` · `//` · `<<` · `\`)는 이스케이프, 막을 수 없는 것(`->` · `===`로 시작 · 짝 없는 `{`)은 막는 문제로 | 견본 프로젝트 전 장면이 컴파일된다, 작가 글자가 문자열 표에 그대로 선다, 깨진 글이 막힌다 |
| Y3 | 내보내기에 건다 — `ExportAll`이 `exported/yarn/`에 쓰고 낡은 파일을 걷는다. `ChapterExportRun`에 Yarn 결과, 화면의 내보내기 보고에 한 줄 | 쓰기 · 거부 시 보존 · 낡은 파일 걷기 · 게임 폴더에 안 쓴다 시험 |
| Y4 | 진행 JSON에서 `DesignerNote` 칸을 걷는다 — 골든 · 견본 산출물 고침 | 내보내기 시험 |
| Y5 | 문서 — `two-graphs.md` 7절 V, `ARCHITECTURE.md` · `README.md` · `runtime-contract.md`(「이 툴은 Yarn을 내지 않는다」 자리에 ⛔ 날짜), `YarnNaming` · `YarnSyntax` 주석, 작가 안내(`작가에게.txt` · `writer-guide.md` — `exported/yarn/`이 무엇인지), 인계 | 문서가 지금 동작과 맞다 |

각 단계는 빌드(경고 0) · 두 시험 프로젝트가 모두 통과한 채로 커밋한다.

## 5. 하지 않는 것

- 게임 폴더에 Yarn을 쓰지 않는다. 진행 JSON 모양 · `src/Ked.Progression` · compositor를 고치지 않는다.
- 연출 명령을 합치지 않는다(2단계 — AI가 쓴 연출 파일을 LineId로 합친다. 명령 → Yarn 글 규칙을 가져올지, AI가 Yarn 명령 글을 그대로 적을지부터 정한다).
- 걷어 낸 것을 통째로 되살리지 않는다 — 위 띠의 [내보내기…] · 노드 고르기 창 · 출력 폴더 지정 · 출력 장부 · CSV 묶음 · `Vn.Core` · `Vn.Cli`는 돌아오지 않는다.
  되살리는 것은 Segment → Yarn 글 조립과 컴파일 확인뿐이다.

## 6. 진행 기록

| 날 | 단계 | 커밋 | 메모 |
|---|---|---|---|
| 11-17 | Y0 | (Y0 커밋) | 지시서. 1절은 소유자가 정했다. 확인: 이미터(`YarnBundleEmitter`)는 R3 `86944596`에, 컴파일 확인(`YarnOutputVerification`)은 R2 `d26fb601`에, `Vn.Core`와 `YarnSpinner.Compiler` 패키지는 L1 `d9d5daca`에 걷혔다 — 인계 문서의 「저장소에 남은 `Vn.Core`」는 틀렸다. 남은 것: `YarnNaming` · `YarnSyntax`(조건 · `stat()` · 이름 정규화) · `ConditionYarnTranslator` · `ResultDocumentComposer`(Segment 목록 — `BranchDetour` · `DefaultJump` 포함). 주석 든 견본이 3.2.1에서 컴파일되는 것을 따로 돌려 봤다. 이 세션은 클라우드 컨테이너다 — 빌드 경고 0 · Vn.Authoring.Tests 679 · Vn.App.Tests 415(apt의 .NET SDK 10.0.115). 코드 변경 0 |
| 11-17 | Y0 답 | (Y1 커밋) | 소유자 답 — A 처음부터 장면 단위, B `DesignerNote`는 진행 JSON에서 칸째 없앤다(인계 문서의 「계약 모양은 그대로」를 바꿨다), C 컴파일 확인은 시험 프로젝트에서만. 1 · 3 · 4절에 적었다 |
| 11-17 | Y1 · Y2 | (Y1 커밋) | `Chapters/ChapterYarnExporter` — 장면 하나 → `ChapterYarnFile`(파일 이름 · 글 · 문제). 머리 `// 장면: 이름 (ID)` + 장면 메모, 노드마다 `// 에피소드: 제목` + 지시, 여러 줄은 줄마다 `//`. 본문은 `DialoguePublisher.Draft` → `ResultDocumentComposer`(RuntimeFull)의 조각 — 조건 · 대사 · `<<detour>>` · `<<jump>>`. 타이틀은 `ChapterProgressionExporter.DialogueEntryIdOf`(진행 JSON의 `Node`에서 꺼낸 함수 — 둘이 같은 함수를 지난다). `YarnSyntax`에 들여쓰기 · jump · detour(R3 직전의 것)와 `EscapeLineText`(컴파일러로 하나씩 돌려 보고 정했다: `#` · `//` · `<<` · `>>` · `\`는 `\`로 막고, `{ }` · `[ ]` · 홑 `<` `>` `/`는 그대로). 막는 것: LineId 없는 줄, 화자 없이 `->` · `===`로 시작하는 줄, 짝 없는 `{`, 정규화로 겹치는 파일 이름, 펼침의 막는 문제(줄 없는 카드는 빈 노드로 — 막지 않는다). 빈 줄은 U+00A0. `YarnSpinner.Compiler` 3.2.1을 `Vn.Authoring.Tests`에만 — `stat`은 `Library`에 (글자 → 수)로 등록하고 파일들을 한 프로젝트로 컴파일. 시험 `ChapterYarnExportTests` 12(견본 두 프로젝트 전 장면 컴파일 · 타이틀 = `DialogueEntryId` · 주석 · 이스케이프가 문자열 표에 원문으로 · 빈 줄 · 카드 없는 노드 · 식 미정 `<<if true>>` · 막힘 · 이름 겹침 · 결정성) |
| 11-17 | Y3 | (Y3 커밋) | `ChapterExportService.ExportAll` — 진행 JSON · 장면 파일이 나간 챕터마다 `ExportYarn`이 `exported/yarn/`에 장면 파일을 쓴다(같은 글이면 안 쓴다). **게임 폴더에는 쓰지 않는다.** 막힌 장면은 쓰지 않고 옛 파일을 둔다 — 보고(`Notice`)에 「챕터 · 장면: 첫 문제 (외 N개)」. 다른 챕터가 이미 쓴 파일 이름이면 막는다(`a`의 `b_c` = `a_b`의 `c`). 낡은 파일 걷기(`RemoveStaleYarn`): 파일의 주인은 이름 머리가 **가장 길게** 맞는 살아 있는 챕터, 주인이 없으면 걷고, 주인이 이번에 나갔는데 지금 짓지 않는 이름이면 걷고, 주인이 거부 · 막힘으로 안 나갔으면 둔다. `ChapterExportRun`에 `YarnWritten` · `YarnProblems`, 나간 자리 안내에 「대사 Yarn → exported/yarn/ (N개)」. 정의 파일(`GameDefinition`)을 인자로 받아 카드 글 칸과 같은 조건 해석을 지난다 — 화면은 세션의 것을 넘긴다. 견본 폴더의 `exported/`는 진행 JSON만 저장소에 있다(장면 파일처럼 Yarn도 올리지 않는다). 시험 `ChapterYarnExportServiceTests` 5 |
| 11-17 | Y4 | (Y4 커밋) | 진행 JSON의 노드에서 `DesignerNote` 칸을 걷었다(모델의 `ChapterEpisode.Memo`는 그대로 — [대본]의 에피소드 지시 칸이고 이제 대사 Yarn의 주석으로 나간다). 골든 `docs/ch01.progression.sample.json`을 다시 지었고(지시 줄 7개만 빠졌다), 표본 시험에 「`DesignerNote`가 없다」를 걸었다. 견본 산출물 둘(`FestivalEve` · `PresentationDemo`의 `exported/*.progression.json`)도 그 줄만 걷었다 — `RuntimeSampleTests`가 다시 낸 것과 실린 것을 견준다. ⚠ 인계 문서가 말한 소유자의 커밋 안 한 `FestivalEve/` 변경과 같은 파일이면 받을 때 겹친다. `runtime-contract.md` F절 표에 줄 하나, G-11의 「재반입 때 걷을 칸」에서 뺐다. `src/Ked.Progression`은 그대로(칸이 없었다) |
| 11-17 | Y5 | (Y5 커밋) | 문서 — `two-graphs.md` 7절 V(새 절) · 5절 22번(컴파일 확인의 자리) · 7절 O의 `designerNote` 줄에 ⛔, `ARCHITECTURE.md`(쓰는 것 · 폴더 그림 · 파일 표 · 「여는 곳」 표), `README.md` · `ORIENTATION.md`(폴더 · 흐름 그림), 작가 안내 둘(`yarn/` 폴더 · 막히는 글자), `YarnNaming` 주석, 인계 [`2026-11-17-yarn-handoff.md`](../handoff/2026-11-17-yarn-handoff.md)(앞 인계 머리에 ⛔ 가리킴). 1단계 끝 |
