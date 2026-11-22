# 작가용 글 서식 — 설계와 단계

> 2026-11-22 소유자: *"시나리오 작가 입장에서는 한컴이나 엑셀 같은 문서 툴에서 흔히 하듯이, 자기가 현재 쓰는 텍스트를 직접 위쪽에서
> 크기나 색깔, bold, 기울기, 좌우 정렬, 글꼴을 지정하고 싶어 할 수 있겠다."* → *"이 툴에서 지정한 것을 Yarn에 반영시키진 않을 것입니다.
> 작가가 작업하기 편하려고 한 게 실제 게임에 섞여 버리니까. 작가가 보기 위한 서식으로 하겠습니다."* → *"구체적인 설계 계획과 완료 조건을
> 담아 주십시오."* → (검토 뒤) *"옛판은 없으니 상관없습니다. 팀 공유입니다. compositor는 완전히 버린 것이니 신경 쓰지 마십시오."*
>
> **상태 — 계획만. 아직 아무것도 만들지 않았다.** 이 문서는 다른 작업자(다른 세션)가 이것만 읽고 시작할 수 있게 적는다.
> 단계 하나가 끝나면 4절 표의 「상태」 칸에 커밋과 CI 번호를 적는다(이 저장소의 방식 — `ORIENTATION.md` §11).
> 2절의 코드 자리는 2026-11-22에 코드와 맞춰 봤다(그날의 작업 폴더 — UX38 뒤).

## 0. 시작하기 전에 (작업자에게)

- **먼저 읽을 것** — `ORIENTATION.md`(§9 빌드 · 테스트) → `docs/design/two-graphs.md` §7(카드 글 칸 · D4) → `ARCHITECTURE.md` §6(깨면 부서지는 규칙).
- **빌드 · 테스트** — Windows에서는 `dotnet test .\VnTool.sln`(요약 줄 2개). `TreatWarningsAsErrors` · `Nullable`이 켜져 있다. CI의 정본은 `windows-latest`.
  **클라우드 작업 공간(리눅스)에서 하는 법은 부록 A** — nuget.org에 닿지 않아 소유자 PC의 패키지 캐시를 들여와 빌드한다. 2026-11-22에 이 길로 전체 녹색을 봤다.
- **파일 모양** — 소유자 PC의 작업 폴더는 CRLF다(저장소 안은 LF · Windows git의 autocrlf). 리눅스 쪽 git으로 보면 손대지 않은 파일도 `M`으로 보인다 —
  진짜 바뀐 것은 `git diff --ignore-cr-at-eol --stat`로 본다. 고친 파일의 줄끝과 BOM 유무를 **그 파일이 쓰던 대로** 지킨다. `.cs` 가운데 BOM이 있는 것과
  없는 것이 섞여 있다(예: `GraphEditorView.axaml.cs`는 BOM 있음 · `StoryHierarchy.axaml.cs`는 없음).
- **선행 조건(끝남)** — 2026-11-22의 usability UX36 ~ UX38(선택지 끌기 안내 · 장면 입구 거절 · 하이어아키 잔상)은 빌드 · 테스트(전체 732 + 621 녹색)를
  마치고 `cccdd12c`로 커밋했다. 이 계획은 그 위에서 시작한다.
- **일하는 법** — 단계 하나 = 커밋 하나(또는 몇 개), 테스트 녹색을 보고 다음으로. 커밋 · 푸시는 소유자가 시킬 때. 고친 동작은 문서(`writer-guide.md` ·
  `작가에게.txt` · `two-graphs.md`)도 함께. 화면 시험은 헤드리스로(`tests/Vn.App.Tests` — `HeadlessUi.Run`), 눈으로 볼 것은 헤드리스 그림으로.
- **멈출 자리** — F0 끝과 각 단계 끝에서 소유자에게 보고하고 다음 단계의 허락을 받는다. 6절의 미정 항목은 소유자가 정한다.

## 1. 정한 것 (소유자 · 2026-11-22)

| # | 결정 | 까닭 |
|---|---|---|
| WF-D1 | 서식은 **작가가 보기 위한 것**이다 — 게임에 나가지 않는다 | 작업용 표시(고칠 곳 빨강 · 중요한 줄 굵게)가 게임 화면에 섞이면 안 된다 |
| WF-D2 | **대사 Yarn(`exported/yarn/`) · 진행 JSON(`exported/*.progression.json`)에 싣지 않는다** | 위와 같다. 내보낸 것은 서식 유무와 무관하게 바이트까지 같아야 한다 |
| WF-D3 | 게임에서 글자를 키우거나 색을 바꾸려면 **메모로 지시**하고 연출 과정에서 반영한다 | 게임의 글자 모양은 연출의 몫이다 |
| WF-D4 | 하이어아키의 순서는 자동 정렬 그대로 둔다(이 계획과 무관 — 같은 날 정했다) | — |
| WF-D5 | 서식은 **팀이 함께 본다** — 프로젝트 파일(대사 노드 JSON)에 저장한다 | 같이 쓰는 사람에게도 보여야 한다. 노드 JSON이면 되돌리기 · 「바뀐 것이 있는가」 비교가 저절로 따라온다(3.1) |
| WF-D6 | **옛 판 툴은 고려하지 않는다**(쓰는 사람이 없다) — 프로젝트 형식 번호(4)는 그대로 둔다 | 새 칸은 비면 쓰지 않는 선택 칸이다. 옛 판이 저장하면 그 칸이 빠지지만 옛 판이 없다 |
| WF-D7 | **ked-story-compositor는 버렸다** — 그쪽이 프로젝트 파일을 어떻게 읽는지 고려하지 않는다 | — |

**하지 않는 것** — 게임용 서식 표기(Yarn 마크업), 글자마다 다른 글꼴, 좌우 정렬(4절 「보류」), 인스펙터 줄 표 · 하이어아키 · 접은 장면 줄 목록에 서식 보이기(6절).

## 2. 지금의 모습 (출발점 · 코드 자리)

| 무엇 | 어디 | 서식과의 관계 |
|---|---|---|
| 카드 글 칸 | `GraphEditorView.BuildWritingBox` — Avalonia `TextBox`(여러 줄 · 줄바꿈), 칸은 `_writingBoxes[nodeId]` | **칸 전체가 한 모양**이다. 글자 일부만 굵게 · 색을 줄 수 없다 |
| 칸의 글 | `WorkingDialoguePreview.ScenarioWriting(project, nodeId)` → `ScenarioWritingLayout(Text, RowOfLine)` — 한 줄이 `화자: 대사`(화자 없으면 지문), `RowOfLine`은 LineId → 칸의 몇 번째 줄 | 칸의 줄 ↔ 대본 줄(LineId)을 여기서 안다 |
| 칸의 줄 → 화자 · 대사 | `ScenarioTextParser.SplitSpeaker` — 첫 `:` 기준, 접두에 공백이 있으면 등록된 화자명과 정확히 같을 때만 화자, 대사는 `line[(colon + 1)..].TrimStart()`. 줄 끝의 줄 ID 꼬리를 먼저 떼어 낸다(그 위의 함수) | **문자열만 돌려준다** — 칸의 글자 자리를 대사 글의 자리로 바꾸려면 자리를 돌려주는 판이 필요하다(3.4) |
| 글 넣기 | `GraphEditorView.ApplyWriting` → `ScenarioLineIdentity.CarryLineIds` → `ProjectEditor.ApplyScenarioText`(→ `ScriptSynchronizer` 계획 → `ApplyScriptSync`) | 동기화 계획의 항목(`ScriptSyncEntry`)이 **고치기 전 · 고친 줄(`Previous` · `Next`)을 함께 든다**. 문구 수정(`Modified`) · 이동(`Moved`)은 LineId를 지킨다 |
| 대사 글이 바뀌는 자리 | ① `ProjectEditor.Scripts.cs`의 `ApplyScriptSync` — `locale.Entries[lineId] = entry.Next` 한 곳, ② `SetScriptLineText`(인스펙터 줄 표 — 주석: 「대사 본문을 바꾸는 유일한 명령」). `RenameSpeaker`는 화자만 바꾼다(`line with { Speaker = to }`) | 서식 자리 옮기기(3.3)가 붙을 곳은 **이 둘뿐**이다. 줄 더하기는 빈 글로 시작한다 |
| 사라진 줄 | `ApplyScriptSync`가 지우지 않고 **은퇴**시킨다(`IsRetired` · 본문도 남긴다 — 「고아 연출을 볼 때 그 줄이 무엇이었는지」). 계획이 그 LineId를 다시 쓰면 `IsRetired = false`로 되살린다 | 서식도 같은 규칙을 따른다(3.1) |
| 대사의 원본 | `DialogueNode.Script`(`ScriptDocument` — `Lines` · `Locales[locale].Entries[lineId]` = `LocalizedLine(Speaker, Text)`) | 서식의 자리는 기본 언어(`PrimaryLocale`)의 `Text` 기준으로 센다. **화자는 `Text` 밖이다** |
| 줄에 덧붙는 논리 | `DialogueLineExtension`(LineId · 조건 전환 · 분기) · 저장은 `StoryNodeJson` | 「대사 논리」의 자리다 — 서식은 논리가 아니므로 **여기 섞지 않는다**(3.1) |
| 노드 저장 · 읽기 | `StoryNodeJson.Write` / 읽기 — 읽기는 칸을 이름으로 골라 읽는다(모르는 칸은 무시) | 새 칸 `writerMarks`를 더하는 자리 |
| 되돌리기 · 「바뀌었나」 | `ProjectEditor`의 기록 한 칸 = `ProjectSnapshotCodec.Encode(Project)` — 파일마다 `StoryFileJson.WriteObject`(곧 노드 JSON)를 통째로 담는다 | **노드 JSON에 넣으면 되돌리기 · 다시 실행 · dirty 비교에 저절로 든다** — 따로 손댈 것이 없다 |
| 변경 알림 → 화면이 할 일 | `ProjectChangeKind`(`ProjectChangedEventArgs.cs`) → `Vn.App/Services/ProjectRefreshPlanner.cs` — **두 갈래**: 보통 변경과 되돌리기 · 다시 실행(`fromHistory`). 되돌리기 갈래에서 모르는 종류는 `Structure`(판을 통째로)로 떨어진다 | 새 종류 `WriterMarks`를 **두 갈래 모두에** 넣어야 판을 다시 짓지 않는다 |
| 내보내기 | `ChapterGraphView`의 다시 읽기가 `ChapterExportService.ExportAll`(대사 Yarn · 진행 JSON)을 부른다 — 계획이 [구성] 다시 읽기를 고를 때만 | 새 종류가 다시 읽기를 고르지 않으면 **내보내기가 돌지 않는다**. 내보내기 코드는 `ScriptDocument`와 챕터만 읽는다 |
| 글꼴 · 크기 · 줄 간격 | `ScriptFont`([⚙ 설정] 「대본 글 칸」 · 이 PC에만 · `ScriptFont.Apply(box)`) | 그대로 둔다. 서식은 그 위에 얹힌다 |
| 줄 높이를 재는 것 | `MeasureLineAnchors`(분기 점 — `TextPresenter`를 찾아 `RowMiddle(presenter, text, row)`로 **그 줄의 첫 화면 줄 가운데**만 잰다) · `FitSizeOf` / `IsFitted`(「글에 맞춤」 · 「줄이기」) · `CardHeightOf` | 띠(F1)에는 그 줄의 **모든 화면 줄의 위 · 아래**가 필요하다 — 새 측정 함수(3.4). 편집기를 바꾸면(F2 A안) 모두 다시 맞춘다 |
| 칸 다시 채우기 · 지키기 | `RefreshWriting` · `RefreshAllWriting` · `CaptureWritingFocus` · `CanKeep` · `RebuildCard` | 쓰던 글 · 커서 · 초점을 지키는 규칙(usability UX13 · UX17 · UX18)이 `TextBox`에 기댄다 |
| 단축키 | `Views/Shortcuts.cs`(표) — 지금 쓰는 Ctrl 조합은 S · Z · Y · Tab · Enter뿐 | Ctrl+B · Ctrl+I · Ctrl+Shift+H는 비어 있다 |
| 기존 시험 | `CardWritingBoxTests` · `CardLiveWritingTests` · `CardFitTests` · `DirectorFitTests` · `ScriptFontTests` · `SpeakerRenameWritingTests` · `BranchGateWritingTests` | 글 칸의 지금 동작을 못 박는다 — 단계마다 그대로 지나가야 한다 |

## 3. 설계

### 3.1 저장 — 서식은 글 밖, 대사 노드 JSON에 둔다

대사 글(`LocalizedLine.Text`)은 지금처럼 순수한 글자다. 서식은 **대사 카드(DialogueNode)마다, 줄(LineId)마다** 따로 둔다.

```csharp
// Vn.Authoring.Model (새 파일 WriterMarks.cs)
public enum WriterColor { Red, Orange, Yellow, Green, Blue, Purple, Gray }   // 색 목록은 F0에서 확정

/// 대사 글의 한 구간에 입힌 모양. 구간은 [Start, Start + Length) — 기본 언어 Text의 UTF-16 자리.
public sealed record WriterSpan(int Start, int Length, bool Bold = false, bool Italic = false, bool Strike = false,
                                WriterColor? Color = null, WriterColor? Highlight = null);

/// 한 줄의 서식 — 줄 통째의 형광펜(F1)과 글자 구간들(F2).
public sealed class WriterLineMarks
{
    public WriterColor? LineHighlight { get; set; }
    public List<WriterSpan> Spans { get; init; } = new();   // 정규형: 겹치지 않고 Start 차례 · 모양이 없는 구간은 두지 않는다 · 이웃한 같은 모양은 합친다
    public bool IsEmpty => LineHighlight is null && Spans.Count == 0;
}

// DialogueNode
public Dictionary<string, WriterLineMarks> WriterMarks { get; init; } = new(StringComparer.Ordinal);   // LineId → 서식 (빈 서식은 두지 않는다)
```

저장 — 대사 노드 JSON의 `writerMarks` 칸. **비었으면 칸 자체를 쓰지 않는다**(서식 없는 프로젝트의 파일이 지금과 같다). 줄은 LineId 차례로 쓴다(결정적 출력).

```json
"writerMarks": {
  "line_ab12": { "highlight": "yellow" },
  "line_cd34": { "spans": [ { "start": 3, "length": 4, "bold": true, "color": "red" } ] }
}
```

- **왜 글 안의 표기(`[b]…[/b]`)가 아닌가** — 표기를 섞으면 Yarn 내보내기 · 글자 수 세기(`ChapterTextCount`) · 화자 이름 바꾸기 · 줄 맞추기(`ScriptSynchronizer`) ·
  찾기가 모두 그 표기를 걸러야 하고, 하나라도 놓치면 게임에 샌다(WF-D2). 밖에 두면 새는 길이 없다.
- **왜 `DialogueLineExtension`이 아닌가** — 그 객체는 「대사 논리」다. 서식은 논리가 아니고, 흐름 계산 · 합성 코드가 볼 이유가 없다.
- **왜 노드 JSON인가(곁 파일이 아닌가)** — 되돌리기 기록과 「바뀐 것이 있는가」 비교가 노드 JSON을 통째로 담는다(`ProjectSnapshotCodec`). 곁 파일이면 스냅샷
  코덱 · 저장 · 열기 · 「밖에서 바뀌었나」 검사를 모두 따로 손봐야 한다. 팀이 함께 보므로(WF-D5) 프로젝트 안에 있는 것이 맞다.
- **화자에는 서식을 입히지 않는다** — 화자는 `LocalizedLine.Speaker`라 `Text`의 자리 체계 밖이다(구간을 붙일 곳이 없다). 「이 화자의 줄」을 표시하려면 줄 형광펜을 쓴다.
  칸에서 고른 범위가 화자 부분에 걸치면 대사 부분만 먹인다.
- **다른 언어** — 서식은 기본 언어의 글에만 붙는다(칸이 보이는 글이 그것이다). 다른 언어의 글이 바뀌어도 서식은 그대로다.
- **은퇴한 줄** — 본문처럼 서식도 **남긴다**(지우는 코드를 두지 않는다). 칸에는 은퇴한 줄이 없으니 보이지 않고, 그 LineId가 되살아나면 서식도 돌아온다.
  저장도 그대로 한다(은퇴한 줄의 본문을 남기는 규칙과 같다 — 2절 표).
- **읽기는 너그럽게** — 모르는 색 이름 · 음수나 글 길이를 넘는 구간 · 겹친 구간은 버리거나 잘라 정규형으로 만들고 계속한다. **서식 때문에 프로젝트가 안 열리는 일이
  없다**(글과 논리는 깨진 파일을 거절하지만 서식은 아니다). 칸 이름 철자는 `JsonSupport`의 다른 선택 칸과 같은 방식으로 읽는다.

### 3.2 편집 API (Vn.Authoring · 화면을 모른다)

```csharp
// ProjectEditor (새 partial ProjectEditor.WriterMarks.cs)
void SetLineHighlight(string nodeId, IReadOnlyList<string> lineIds, WriterColor? color);          // null = 지우기
void ApplyWriterStyle(string nodeId, IReadOnlyList<WriterRange> ranges, WriterStyleChange change);  // F2 — 여러 줄을 한 칸에
void ClearWriterMarks(string nodeId, IReadOnlyList<string> lineIds);                               // 줄의 서식 모두
// WriterRange(string LineId, int Start, int Length) — 대사 글 기준
```

- `WriterStyleChange` — 굵게 · 기울임 · 취소선은 「켠다 / 끈다」, 색 · 형광펜은 「이 색 / 없앰」. **토글의 판정**은 문서 도구와 같다: 고른 범위(여러 줄이면 모두)가
  모두 굵으면 끄고, 하나라도 아니면 모두 켠다(`WriterMarkMath.IsAll`). 화면은 판정까지 편집기에 맡긴다(단추가 켤지 끌지 정하지 않는다).
- 모두 **되돌리기 한 칸**(여러 줄이어도), 바뀐 것이 없으면 적지 않는다(two-graphs §5-8).
- 새 변경 종류 `ProjectChangeKind.WriterMarks` + `NodeId`(그 카드). `ProjectRefreshPlanner`의 **보통 갈래와 되돌리기 갈래 모두에** 넣고, 계획은 「그 카드의 글 칸 꾸밈만
  다시 입힌다」 하나 — 판을 다시 짓지 않는다 · 철도를 다시 긋지 않는다 · [구성] 다시 읽기(곧 진행 JSON · 대사 Yarn 다시 내기)를 하지 않는다 · 곁기둥(인스펙터)도
  그대로. 되돌리기 기록에 노드를 싣는다(usability UX21 — `HistoryEntry.NodeId`).
- 구간 셈은 순수 함수로 모은다 — `WriterMarkMath`(Vn.Authoring): `Apply`(구간에 모양 입히기 → 쪼개고 합쳐 정규형으로) · `IsAll` · `Rebase`(3.3) ·
  `Normalize`(자르기 · 합치기 · 빈 것 버리기). 화면 없이 시험한다.

### 3.3 글이 바뀌면 서식의 자리를 옮긴다 (`WriterMarkMath.Rebase` · F2)

**F1에는 필요 없다** — 줄 형광펜은 LineId에 붙으므로 글을 고치고 · 줄을 옮기고 · 화자 이름을 바꿔도 저절로 따라간다. 구간이 생기는 F2에서 넣는다.

붙는 자리는 대사 글이 바뀌는 두 곳(2절 표)이다 — **같은 `Mutate` 안에서**(되돌리기 한 칸이 글과 서식을 함께 돌린다), 기본 언어일 때만.

- `ApplyScriptSync` — 항목마다 `entry.Previous?.Text`(옛 글 `a`)와 `entry.Next?.Text`(새 글 `b`)로. `Inserted`(새 줄)는 서식이 없고, `Deleted`(은퇴)는 그대로 둔다.
- `SetScriptLineText` — 지금 글과 새 글로.

알고리즘 — 공통 앞부분 길이 `p`, 공통 뒷부분 길이 `s`(`p + s ≤ min(|a|, |b|)` — 앞부분을 먼저 최대로 잡는다). 옛 글에서 바뀐 자리는 `[p, |a| - s)`,
길이 차 `d = |b| - |a|`. 구간 `[x, y)`마다:

| 경우 | 처리 |
|---|---|
| `y ≤ p` 이고 **순수 넣기가 아니거나** `y < p` | 그대로 |
| **순수 넣기**(`p = |a| - s`, 곧 지운 글자 없음)이고 `y = p` · `x < p` | **늘린다**(`y + d`) — 구간 끝에 바로 이어 친 글자는 앞 글자의 모양을 잇는다(워드 · 한글과 같다) |
| `x ≥ |a| - s` (바뀐 자리 뒤에서 시작 — 순수 넣기에서 `x = p`인 구간 포함) | `d`만큼 민다 — 구간 앞에 바로 친 글자는 모양을 잇지 않는다 |
| `x ≤ p` 이고 `y ≥ |a| - s` (바뀐 자리를 감싼다) | 길이를 `d`만큼 바꾼다 — 안에 쓴 글자는 그 모양을 잇는다 |
| 바뀐 자리에 걸친다(그 밖) | 바뀐 자리 바깥 부분만 남긴다(앞쪽 조각은 그대로, 뒤쪽 조각은 `|b| - s` 기준으로 옮긴다) |
| 바뀐 자리 안에 들어 있다 | 버린다 |

끝에 `Normalize`. 같은 글자가 이어지는 곳(「ㅋㅋ」 → 「ㅋㅋㅋ」)은 넣은 자리가 뒤로 잡히는데 글은 같으니 상관없다. 한 번에 두 군데를 고친 넣기(붙여넣기 · 긴 멈춤 뒤)는
바뀐 자리가 넓게 잡혀 그 사이 서식이 잘릴 수 있다 — 받아들인다(글이 깨지는 일은 없다).

⚠ **F2를 A안(편집기 교체)으로 하면** 편집기가 쓰는 동안 그리는 모양의 경계 규칙(끝에 이어 치면 잇고 앞에 치면 잇지 않는다)이 이 표와 **같아야** 한다 — 다르면
넣는 순간 서식이 한 글자씩 튄다. 넣은 뒤에는 늘 모델의 서식으로 다시 그린다.

### 3.4 화면

**도구 막대** — [대본] 탭 위쪽 줄(`GraphEditorView.axaml`의 첫 줄 · `ChapterCombo` · `AddEpisodeButton`이 선 자리)의 오른쪽에 한 무리:

| 단추 | 단축키 | 단계 | 동작 |
|---|---|---|---|
| 줄 형광펜(▾ 색 고르기) | Ctrl+Shift+H(마지막에 쓴 색) | F1 | 커서가 선 줄 · 고른 범위에 걸친 줄 통째에 배경색. 그 줄들이 모두 이미 그 색이면 지운다 |
| 서식 지우기 | — | F1(줄) → F2(글자까지) | 고른 범위에 걸친 줄(없으면 커서 줄)의 서식 모두 |
| **B** · *I* · ~~S~~ | Ctrl+B · Ctrl+I · Ctrl+Shift+X | F2 | 고른 글자에 토글 |
| 글자색 · 글자 형광펜(▾) | — | F2 | 고른 글자에. 목록의 색 + 「없음」 |

- **초점과 고른 범위를 지킨다** — 단추는 `Focusable = false`. 색을 고르는 차림(플라이아웃)은 열릴 때 초점을 가져갈 수 있다 — 그러면 글 칸이 「딴 데를 눌렀다」로 받아
  글을 넣고 고른 범위를 잃는다. 그래서 **단추를 누르는 순간**(`PointerPressed`, 차림이 열리기 전) 글 칸 · 커서 · 고른 범위를 붙잡아 두고, 적용은 붙잡은 값으로 하며,
  끝나면 그 칸에 초점과 고른 범위를 되돌린다. 차림 안의 색 단추도 초점을 받지 않게 한다.
- 글 칸에 초점이 없으면(붙잡은 칸이 없으면) 꺼져 있다 — 풍선: 「카드의 글 칸에서 글자를 고르면 켜집니다」.
- **넣지 않은 글이 있으면** 먼저 넣고(`ApplyWriting(..., live: true)` — 줄 신원이 정해져야 자리를 셀 수 있다 · 지우기 확인 창이 뜨지 않는 살아 있는 넣기와 같은 길),
  그다음 서식을 적는다. 넣기가 거절되면(동기화가 확신하지 못함 등) 서식도 적지 않고 상태줄이 까닭을 말한다. 넣기와 서식은 되돌리기 두 칸이다(넣기가 먼저 한 칸).
- **칸의 자리 → (LineId, 대사 글의 자리)** — 칸의 줄 번호로 LineId를 찾고(`RowOfLine`을 거꾸로 — 넣은 뒤의 새 `_writingLayouts`로), 그 줄에서 대사 글이 시작하는
  자리는 `ScenarioTextParser`에 **자리를 돌려주는 판**(예: `SplitSpeakerAt(line, speakers) → (Speaker, TextStart, TextLength)`)을 더해 센다. 지금의
  `SplitSpeaker`는 그것을 불러 문자열을 만들게 바꾼다 — **같은 규칙을 한 벌로**(화면이 따로 `:`를 찾지 않는다). 줄 ID 꼬리를 떼는 규칙도 같은 함수 안에서.
  이 변환은 순수 함수로 두고 시험한다(지문 · 공백 있는 등록 화자 · `윌로 : …`(지문) · 콜론 뒤 공백 여럿 · 대사 안의 콜론).
- 색은 정해 둔 목록에서만(자유 색 없음). 형광펜은 옅은 배경, 글자색은 진한 색 — 같은 이름이라도 두 벌의 실제 색을 둔다(`WriterPalette` · Vn.App). 풍선에 색 이름.
- 안내 — `writer-guide.md`에 「글 서식(작가용)」 절: **게임에 나가지 않는다는 것을 첫 문장**에, 팀이 함께 본다는 것을 둘째 문장에. `작가에게.txt`에도 한 줄. 단축키 표에 무리 하나.

**그리기**

- **F1(줄 형광펜)** — `TextBox`는 그대로 두고 칸 **뒤**에 색 띠를 깐다.
  - **측정** — 새 함수 `RowBand(presenter, text, row) → (top, bottom)`: 그 줄(칸의 글에서 `row`번째 줄)의 첫 글자부터 끝 글자까지를 `TextPresenter`의 글자 배치에서
    범위로 재어(줄바꿈된 화면 줄 모두) 위 · 아래를 낸다. `RowMiddle`과 같은 자리 셈(줄 → 글자 자리)을 함께 쓴다. 빈 줄도 한 화면 줄 높이를 갖는다.
  - **바탕** — Fluent 테마의 `TextBox`는 마우스가 올라갈 때 · 초점이 있을 때 템플릿 안에서 바탕을 바꾼다(그 자리의 바탕 붓은 `TextBox.Background`가 아니라 테마
    리소스를 쓴다). 그래서 글 칸마다 그 리소스들(`TextControlBackground` · `…PointerOver` · `…Focused`)을 투명으로 덮어쓰고, 카드 바탕색은 띠 밑의 겹이 진다.
    **F0에서 실제로 확인한다** — 안 되면 띠를 글 **위**에 옅게(알파) 얹는 길로 간다(글자가 조금 물든다).
  - **따라가기** — 칸이 스크롤하면 띠가 따라간다(분기 점이 칸 끝에 붙는 `LineOutOfView` 처리와 같은 자리에서), 칸 크기 · `ScriptFont`가 바뀌면 다시 잰다.
    **넣지 않은 글을 쓰는 동안**은 줄 짝(`RowOfLine`)이 마지막으로 넣은 때의 것이라 위에 새 줄을 치면 아래 띠가 한 줄 어긋난다 — 분기 점도 지금 같은 방식이다.
    살아 있는 넣기(멈추면 넣는다)가 곧 맞추므로 **받아들인다**(넣은 뒤에는 맞는다는 것을 시험한다).
- **F2(글자 서식)** — F0에서 고른 길:
  - **A안 — 편집기 교체**(AvaloniaEdit `TextEditor` 등): 글자마다 모양을 입히는 변환기로 구간을 그린다. 쓰는 중에도 서식이 보인다. `TextBox`에 기대던 것
    (2절 표의 「줄 높이를 재는 것」 · 「칸 다시 채우기 · 지키기」 · `InWritingBox` · `ScriptFont.Apply` · 시험들의 `TextBox` 찾기)을 모두 옮긴다 — 칸을 감싸는
    얇은 겹(`IWritingBox`: 글 · 커서 · 고른 범위 · 스크롤 · 줄 높이 재기 · 글꼴)을 먼저 세우고 그 뒤에서 바꾼다. 위험 — Avalonia **12.1.1**과 맞는 판이 있는지,
    카드 160장에 편집기 160개의 무게, 한글 조합 입력.
  - **B안 — 보는 겹과 쓰는 겹**: 초점이 없는 카드는 서식을 입힌 읽기 전용 글(`TextBlock` + `Inlines`)로 보이고, 누르면 그 카드만 `TextBox`로 바뀐다
    (쓰는 동안에는 줄 · 글자 형광펜만 띠로 보이고 굵게 · 글자색은 초점을 떼면 보인다). 편집기 의존이 없고 한글 입력이 지금과 같으며, usability-orders §4의
    「고른 카드만 글 칸, 나머지는 가벼운 글」(판 속도)과 같은 방향이다. **보이는 것보다 무겁다** — ① 분기 점 · 「글에 맞춤」 · 카드 높이를 읽기 전용 글에서도 재는
    두 번째 측정 길, ② 읽기 전용 글을 누른 자리에 커서를 두기(누른 자리 → 글자 자리 → `TextBox`의 커서), ③ 굵은 글자는 폭이 달라 두 겹의 줄바꿈이 갈릴 수 있다
    (누를 때 글이 튄다), ④ 시험들이 카드에서 `TextBox`를 찾는 방식.
  - 고르는 기준(F0) — **한글 조합 입력이 온전한가 > 판 속도 > 쓰는 중에도 보이는가.** A안이 앞의 둘을 지키면 A, 아니면 B.

## 4. 단계

| | 무엇 | 크기 | 상태 |
|---|---|---|---|
| **F0** | 조사 · 결정(4.0) | 소 | — |
| **F1** | 저장 모델 · 편집 API · 줄 형광펜 · 도구 막대의 첫 단추(4.1) | 중 | — |
| **F2** | 글자 서식 — 굵게 · 기울임 · 취소선 · 글자색 · 글자 형광펜 · 자리 옮기기(4.2) | 대 | — |
| **F3** | 글자마다 크기(4.3) | 중 | F2를 써 본 뒤 소유자가 정한다 |
| 보류 | 글자마다 글꼴 · 좌우 정렬 | — | **권하지 않는다** — 대본은 `화자: 대사` 줄 목록이라 정렬이 뜻을 갖기 어렵고, 줄마다 다른 글꼴은 얻는 것에 견줘 품과 어지러움이 크다. 글꼴은 지금처럼 설정에서 전체로 |

### 4.0 F0 — 조사 · 결정 (제품 코드를 바꾸지 않는다 · 시험 판은 버리는 가지에)

할 일

1. **F1의 바탕** — 글 칸의 테마 리소스를 덮어써 마우스 · 초점 상태에서도 띠가 비치는지(3.4 「바탕」). 헤드리스 그림으로 세 상태(보통 · 올림 · 초점)를 본다.
2. **편집기 시험 판**(A안) — AvaloniaEdit에 Avalonia 12.1.1과 맞는 판이 있는지부터. 있으면 카드 글 칸 자리에 넣어 본다. 잴 것:
   (ㄱ) **한글 조합 입력** — 조합 중 글자 · 커서 · 한자 변환 · 조합 중 Enter(Windows에서 손으로 — 소유자에게 부탁한다),
   (ㄴ) **판 속도** — usability-orders §4의 잰 법 그대로(에피소드 160개 견본 · 헤드리스): 판 통째로 짓기 · 새 줄 · 글자 고치기 · 탭 돌아오기,
   (ㄷ) 여러 줄 줄바꿈에서 줄 높이 재기(분기 점 · 「글에 맞춤」), (ㄹ) `ScriptFont` 적용, (ㅁ) 초점 · 커서 · 넣지 않은 글 지키기를 지금 규칙대로 옮길 수 있는가,
   (ㅂ) 쓰는 동안 구간 경계 규칙을 3.3의 표와 같게 할 수 있는가.
3. **B안의 줄바꿈** — `TextBlock`(Inlines)과 `TextBox`가 같은 글 · 같은 폭 · 같은 `ScriptFont`에서 줄바꿈 자리와 줄 높이가 같은가 — 보통 굵기일 때, 굵은 글자가 섞였을 때.
4. **색 목록** 제안 — 형광펜 · 글자색 각각 실제 색 값(밝은 테마 기준 · 카드 바탕 넷(에피소드 · 장면 시작점 · 분기 카드 · 고른 카드) 위에서 대비 확인).

완료 조건

- [ ] 이 문서에 「F0 결과」 절이 생겼다 — 1 ~ 4의 답과 **잰 숫자**, A / B 가운데 권하는 안과 까닭, F1 바탕의 길(덮어쓰기 / 위에 얹기), 색 목록.
- [ ] 제품 코드 · 패키지 목록(`Directory.Packages.props`)은 바뀌지 않았다.
- [ ] 소유자가 A / B와 색 목록을 정했다(1절 표에 WF-D8 ~ 로 적는다).

### 4.1 F1 — 저장 모델 · 줄 형광펜

할 일

1. 모델 `WriterColor` · `WriterSpan` · `WriterLineMarks` · `DialogueNode.WriterMarks`(3.1). F1에서 쓰는 것은 `LineHighlight`뿐이지만 **파일 모양은 구간까지
   정해 둔다** — 읽기 · 쓰기가 `spans`도 다루고(정규형으로), 그리기와 편집만 F2다.
2. 저장 · 읽기(`StoryNodeJson`) — 빈 서식은 쓰지 않고, 읽기는 너그럽게(3.1).
3. `ProjectEditor.SetLineHighlight` · `ClearWriterMarks` · `ProjectChangeKind.WriterMarks` · `ProjectRefreshPlanner` 두 갈래(3.2).
4. 화면 — 도구 막대의 줄 형광펜(색 차림) · 서식 지우기, 단축키, 띠 그리기 · `RowBand`(3.4), `WriterMarks` 변경을 받아 **그 카드의 띠만** 다시 입히기.
   `CanKeep`(카드를 그대로 둘 수 있는가)에 서식을 넣지 않는다 — 띠는 제자리에서 다시 입힌다(제목 · 조건 표와 같은 「제자리에서 고쳐지는 것」). 판을 다시 지으면
   새 카드도 띠를 입는다.
5. 문서 — `writer-guide.md` · `작가에게.txt` · `two-graphs.md`(카드 표) · `ARCHITECTURE.md`(모델 · 변경 종류 · 찾기 표) · 이 문서의 단계 표.

완료 조건

- [ ] **내보낸 것이 같다** — 같은 프로젝트에 줄 형광펜을 여럿 입힌 것과 입히지 않은 것의 대사 Yarn · 진행 JSON 내보내기 결과가 바이트까지 같다(도메인 시험 —
      `ChapterExportService`로 두 번 내서 견준다).
- [ ] **서식 없는 프로젝트의 저장이 그대로다** — 서식이 없는 견본(`tests/fixtures`의 프로젝트 · `samples/RuntimeSamples`)을 열어 저장한 결과가 고치기 전 코드로
      저장한 결과와 같다(노드 JSON에 `writerMarks` 칸이 생기지 않는다 · 기존 왕복 시험이 고치지 않고 지난다).
- [ ] 형광펜을 입히고 저장 → 다시 열면 그대로다. 손으로 고친 이상한 서식(모르는 색 · 범위 밖 · 겹침)이 든 파일도 열리고, 다음 저장에서 정규형으로 나간다.
- [ ] 입히기 · 색 바꾸기 · 지우기가 각각 되돌리기 한 칸이고, 되돌리기 · 다시 실행이 서식을 돌린다. 그 줄들이 이미 그 색인데 같은 색을 다시 입히는 것은 「지우기」다(토글).
- [ ] 줄의 글을 고치고 · 줄을 위아래로 옮기고(▲ ▼ · 글 칸에서 줄 순서 바꾸기) · 화자 이름을 바꿔도 형광펜이 **그 줄을 따라간다**(LineId). 줄을 지우면 칸에서 띠가 없어진다.
- [ ] 여러 화면 줄로 줄바꿈된 대사는 그 전부에 띠가 서고, 칸을 스크롤하면 띠가 따라가며, 칸 크기를 바꾸거나 `ScriptFont`를 바꿔도 맞는다. 마우스를 올린 칸 · 초점이 있는
      칸에서도 띠가 보인다(헤드리스 그림 포함).
- [ ] **판을 다시 짓지 않는다** — 형광펜 입히기 · 되돌리기 · 다시 실행에서 `RailDrawCount`가 그대로이고 모든 카드가 그 객체 그대로다(usability UX16 · UX21의 시험 방식).
      [구성] 다시 읽기 · 내보내기가 일어나지 않는다.
- [ ] 단추 · 색 차림 · 단축키로 입혀도 글 칸의 초점 · 커서 · 고른 범위가 그대로다. 넣지 않은 글이 있으면 먼저 들어간 뒤 서식이 적힌다(넣기가 거절되면 서식도 적히지
      않고 상태줄이 말한다).
- [ ] 2절 표의 기존 시험이 고치지 않고 지난다. 전체 테스트 녹색(요약 줄 2개) · CI(Windows) 녹색.
- [ ] 속도 — usability-orders §4의 견본에서 글자 고치기 · 새 줄 · 판 다시 그리기 · 형광펜 입히기를 다섯 번씩 재어 **가운데값**을 단계 표에 적는다. 고치기 전의 1.25배를
      넘으면 멈추고 소유자에게 보고한다(헤드리스 시간은 흔들림이 커서 1.1배는 잡음 안이다 — 확실한 기준은 위의 「다시 짓지 않는다」 시험이다).

### 4.2 F2 — 글자 서식

할 일(F0에서 정한 안에 따라 갈린다 — 공통만 적는다)

1. `WriterMarkMath.Apply` · `IsAll` · `Rebase` · `Normalize`, `ProjectEditor.ApplyWriterStyle`(3.2), 자리 옮기기를 두 자리(`ApplyScriptSync` · `SetScriptLineText`)에
   붙이기(3.3).
2. `ScenarioTextParser.SplitSpeakerAt`과 칸의 고른 범위 → `WriterRange`들(3.4 — 여러 줄에 걸치면 줄마다, 화자 부분은 뺀다). 순수 함수로 두고 시험한다.
3. 도구 막대의 나머지 단추 · 단축키. 고른 범위가 바뀌면 단추가 **지금 모양**(모두 굵으면 B가 눌린 모습)을 보인다.
4. 그리기 — A안이면 편집기 교체(먼저 `IWritingBox` 겹을 세우는 커밋, 그다음 바꾸는 커밋, 그다음 서식 그리기 커밋으로 나눈다), B안이면 보는 겹(3.4의 ① ~ ④).
5. 「글에 맞춤」 · 분기 점 · 카드 높이가 굵은 글자가 섞인 줄에서도 맞는지.
6. 문서(F1과 같은 목록).

완료 조건

- [ ] F1의 완료 조건이 모두 그대로 선다(특히 **내보낸 것이 같다** — 이번에는 굵게 · 색 · 형광펜 · 취소선을 잔뜩 입힌 프로젝트로).
- [ ] 토글 — 모두 굵은 범위는 끄고, 섞인 범위는 모두 켠다. 겹쳐 입히면 구간이 쪼개지고 이웃한 같은 모양은 합쳐진다(정규형 · 도메인 시험).
- [ ] **자리 옮기기** — 3.3 표의 여섯 경우가 각각 도메인 시험으로 선다(앞에 넣기 · 끝에 이어 치기 · 앞에 바로 치기 · 안에 쓰기 · 걸쳐 지우기 · 통째로 지우기), 글 칸에서
      친 것과 인스펙터 줄 표에서 고친 것이 같은 결과다(화면 시험 하나씩). 붙여넣기로 줄이 늘어도 다른 줄의 서식은 그대로다. 다른 언어의 글을 고쳐도 서식은 그대로다.
- [ ] 화자 이름을 바꿔도(`RenameSpeaker`) 모든 카드에서 서식이 같은 글자에 붙어 있다.
- [ ] 고른 범위가 화자 부분에 걸치면 대사 부분만 서식이 입는다. 여러 줄을 고르면 줄마다 입고, 되돌리기 한 칸이다.
- [ ] **한글 조합 입력**이 고치기 전과 같다 — 서식이 있는 줄 · 없는 줄, 조합 중 글자와 커서, 조합 중 Enter. Windows에서 손으로 확인하고(소유자) 확인한 판 · 날짜를
      단계 표에 적는다(헤드리스는 조합 입력을 내지 못한다).
- [ ] 쓰던 글 · 커서 · 초점을 지키는 기존 규칙(usability UX13 · UX17 · UX18의 시험)이 그대로 지난다.
- [ ] 속도 — F1과 같은 기준. 넘으면 멈추고 소유자에게 보고한다.
- [ ] 전체 테스트 녹색 · CI 녹색 · portable 실행 확인(패키지를 더했으면 `publish-windows.ps1` 꾸러미에 들어가는지).

### 4.3 F3 — 글자마다 크기 (정해지면)

`WriterSpan`에 `Scale`(예: 작게 · 보통 · 크게 · 아주 크게 — 자유 숫자가 아니라 단계). 줄 높이가 줄마다 달라지므로 분기 점 · 카드 높이 어림 · 「글에 맞춤」 · 띠가
실제 배치를 재는지 다시 확인한다. 완료 조건은 F2와 같고, 더해 「크기가 섞인 줄에서 분기 점이 그 대사 높이에 서고 띠가 그 줄을 감싼다」.

## 5. 늘 지킬 것 (어느 단계든)

1. **내보낸 대사 Yarn · 진행 JSON은 서식과 무관하다.** 내보내기 코드가 `WriterMarks`를 읽는 순간 이 계획은 틀린 것이다.
2. **대사 글에 표기를 섞지 않는다.** `LocalizedLine.Text`는 순수한 글자다.
3. **서식 때문에 프로젝트가 안 열리는 일이 없다.**
4. **서식 없는 프로젝트의 저장 파일은 지금과 같다.**
5. **판 속도를 잃지 않는다** — board-speed · usability UX7 ~ UX21로 줄여 둔 「판을 통째로 다시 짓지 않는다」를 서식이 깨지 않는다.
6. **한글 입력을 잃지 않는다.**

## 6. 소유자가 정할 것

| | 무엇 | 언제 |
|---|---|---|
| ① | 시작 시기 · F0만 먼저 할지 | 시작 전 |
| ② | A안(편집기 교체) / B안(보는 겹과 쓰는 겹) | F0 뒤 |
| ③ | 색 목록과 가짓수 | F0 뒤 |
| ④ | F3(글자 크기)을 할지 | F2를 써 본 뒤 |
| ⑤ | 인스펙터 줄 표 · 접은 장면의 줄 목록에도 서식을 보일지, 「형광펜 친 줄만 모아 보기」를 붙일지 | F1을 써 본 뒤 |

## 부록 A. 클라우드 작업 공간(리눅스)에서 빌드 · 테스트 (2026-11-22에 해 본 길)

이 저장소의 일은 소유자 PC(Windows)의 폴더를 연결한 대화 세션에서 하는 일이 많다. 그 세션의 셸 두 가지 — **소유자 PC 쪽 셸**(`device_bash` · 연결한 폴더가
`$HOME/mnt/<이름>`)은 디스크가 거의 없어(100MB 남짓) 빌드할 수 없고, **클라우드 작업 공간**(`Bash` · Ubuntu 24.04 · 메모리 7GB)은 nuget.org · dot.net에 닿지
않는다(archive.ubuntu.com은 닿는다). 그래서 SDK는 우분투 꾸러미로, 패키지는 소유자 PC의 NuGet 캐시에서 들여온다.

1. **SDK** — 클라우드에서 `apt-get install -y dotnet-sdk-10.0 netstandard-targeting-pack-2.1-8.0`(`Ked.Progression`이 `netstandard2.1`이다).
2. **패키지** — 소유자에게 `C:\Users\river\.nuget\packages`를 세션에 연결해 달라고 한다. PC 쪽 셸에서 패키지마다 가장 새 판의 `.nupkg`만 골라(`*.runtime.win-x64` ·
   `crossgen2` · `host.win-x64` · `windowsdesktop` · 쓰지 않는 `npoi` · `documentformat*` · `bouncycastle`은 빼도 된다 — 약 400MB) tar로 묶고, **40MB 조각으로 잘라**
   (`split -b 40M`) 저장소 폴더 안 임시 폴더(예: `.claude-build/`)에 둔다 — 200MB짜리 하나는 올리기가 시간 초과로 실패했다. 조각을 `device_stage_files`로 올려
   클라우드에서 이어 붙여 풀고(`cat part-* | tar -xif -` — 두 tar를 이어 붙였으면 `-i`), `.nupkg`를 한 폴더에 모아 **평평한 로컬 원본**으로 쓴다. 가장 새 판만
   고르면 `Newtonsoft.Json 13.0.1`이 빠진다(테스트 호스트가 그 판을 찾는다 — 경고가 오류다) — 따로 올린다.
3. **저장소** — PC 쪽 셸에서 `tar -cf .claude-build/src.tar --exclude=bin --exclude=obj VnTool.sln Directory.Build.props Directory.Packages.props src tests samples docs`
   (`docs`의 진행 견본 · `samples/RuntimeSamples`를 읽는 시험이 있다)를 올려 클라우드의 빌드 폴더에 푼다. **소유자 폴더 안에서 빌드하지 않는다** — `obj`의
   `project.assets.json`이 리눅스 경로로 덮여 소유자의 Windows 빌드가 다시 복원해야 한다.
4. **설정** — 빌드 폴더에 `nuget.config`(`<clear />` + 로컬 원본 하나), `NUGET_PACKAGES`를 클라우드 안의 빈 폴더로. `dotnet restore` → `dotnet build`.
5. **테스트** — `Vn.Authoring.Tests`는 한 번에. **`Vn.App.Tests`는 클래스 묶음 넷으로 나눠** 돌린다(`--list-tests`로 클래스를 모아 `--filter`) — 한 번에 돌리면
   메모리를 넘어 테스트 호스트가 죽는다(exit 137 · 「Test host process crashed」 · 그 전까지의 「Passed!」 줄은 일부일 뿐이다 — 셈이 `--list-tests`와 맞는지 본다).
6. **고칠 때** — 고치는 것은 소유자 폴더의 파일(PC 쪽 셸 · 줄끝 · BOM을 지켜)이고, 빌드 폴더에는 src.tar를 다시 올려 덮어 푼다(bin · obj는 그대로라 빌드가 빠르다).
7. **치우기** — 임시 폴더를 지우려면 지우기 허락(`device_request_delete_permission`)이 필요하다. 허락이 없으면 PC 쪽 셸의 git도 잠금 파일을 지우지 못해
   `git commit`이 `.lock`을 남긴다 — 커밋도 그 허락 뒤에 한다.

2026-11-22 결과: 빌드 경고 0, `Vn.Authoring.Tests` 732 · `Vn.App.Tests` 621(179 + 155 + 133 + 154) 모두 녹색.
