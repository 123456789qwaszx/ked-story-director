using Vn.Authoring.Strings;
namespace Vn.Authoring.Chapters;

/// <summary>
/// 에피소드 하나.
///
/// 자리의 주인은 프로젝트이고 <c>ProjectEditor.MoveEpisode</c>가 고친다.
/// <see cref="X"/>·<see cref="Y"/>에 setter가 없는 것은 <b>레코드라서</b>다(<c>with</c>로 바꾼다).
/// </summary>
/// <param name="Index">"05"·"05A"·"★"처럼 사람이 붙이는 표시용 순번. 숫자가 아니어도 된다.</param>
/// <remarks>
/// 런타임이 재생할 대사 엔트리(Yarn 노드 이름)는 <see cref="EpisodeId"/>에서 짓는다(<c>ChapterProgressionExporter.DialogueEntryIdOf</c>) —
/// ⛔ 따로 적던 `대사엔트리` 칸은 2026-11-18에 걷혔다(늘 EpisodeId와 같았다 · <c>docs/work-orders/old-shapes-orders.md</c> O5).
/// </remarks>
public sealed record ChapterEpisode(
    string EpisodeId,
    string Title,
    string Index,
    double X,
    double Y,
    string? Memo)
{
    // ⛔ `AllowUnreachable`(`도달불가 허용` — 도달 불가를 알림으로 낮추던 깃발)은 2026-11-17에 걷혔다. 이어지지 않은 에피소드는 경고가
    //    됐고(`EpisodeNotConnected`), 조건 때문에 못 닿는 것은 고칠 설계 버그라 낮출 자리가 없다(화면에 켜는 곳도 없었다).
    // 선택지 수를 따로 세지 않는다: 이 에피소드에서 나가는 간선 하나가 곧 선택지 하나다.

    /// <summary>
    /// 보이는 이름 — <see cref="Title"/>이고, 비었으면 <see cref="EpisodeId"/>다. 게임의 끝 카드와 [대본] 탭이 이것을 보인다.
    /// </summary>
    public string ShownTitle => string.IsNullOrWhiteSpace(Title) ? EpisodeId : Title.Trim();

    /// <summary>
    /// [구성] 탭 인스펙터 머리글 — 「장면 - 제목」.
    /// </summary>
    public string SceneAndTitle => $"{SceneId} - {ShownTitle}";

    /// <summary>
    /// `이벤트키` — <b>유니티 전용 패스스루 인덱스</b>다. 특정 에피소드를 다 시청했을 때
    /// 유니티가 자체적으로 이벤트나 보상을 일으키도록 잇는 값이라, 챕터의 끝이 아니라
    /// <b>시청 완료 트리거</b>다.
    ///
    /// 툴은 이 값을 <b>해석하지 않는다</b> — 배지도 검증도 전이 의미도 없다. 툴 안에 고칠 칸은 없고,
    /// 내보내기가 노드의 <c>EventKey</c>로 그대로 싣는 것이 전부다. 간선이 아니라
    /// 에피소드에 사니 "같은 도착의 키 충돌"이라는 오류 부류 자체가 없다.
    /// 계약의 `EndingKey`/`EndingRules`(시나리오 층)와는 <b>다른 것</b>이다 — 그쪽은
    /// 싣지 않으며, 시나리오 층을 지을 때 그쪽 규격으로 정한다.
    /// </summary>
    public string? EventKey { get; init; }

    /// <summary>
    /// 장면 — 런타임의 저장·롤백 경계. <b>에피소드는 늘 장면에 속한다</b> — 비면 저장 검증이 받지 않는다(2026-11-18 · old-shapes O7 —
    /// ⛔ 그 전에는 비면 에피소드마다 혼자짜리 기본 장면(<c>__scene_{Id}</c>, <c>SceneId</c>)을 썼다).
    /// </summary>
    public string SceneId { get; init; } = string.Empty;

    /// <summary>
    /// 장면 메모 공간에 이 에피소드의 지시 카드를 붙였는가 — 지시(<see cref="Memo"/>)가 비어도 카드가 남는다. [빼기]만 내린다
    /// (2026-11-16 소유자). 에피소드를 따라다닌다(지시와 같다). 화면만 쓰는 표시라 내보내기에 싣지 않는다.
    /// </summary>
    public bool NoteCard { get; init; }

    /// <summary>장면 메모 공간에 카드가 서는가 — 붙였거나, 지시가 있다.</summary>
    public bool HasNoteCard => NoteCard || !string.IsNullOrWhiteSpace(Memo);

}

/// <summary>
/// `간선` 시트 한 행 = 다음 에피소드로 가는 길 하나. <b>길 하나가 곧 선택지 하나</b>이고,
/// `선택지` 열(D)은 그 길에 붙는 <b>문구 그 자체</b>다(인덱스가 아니라 대사 글자).
/// 문구는 `선택지` 시트의 전역 사전에서 고르지만
/// 셀에 들어가는 값은 문구이고, 사전은 <b>고르기 편하라고 있는 어휘집</b>이다.
///
/// <b>신원은 (출발, 도착, 문구)</b> — 같은 곳으로 가되 스탯변화·관문이 다른 선택지 둘을
/// 여럿 둘 수 있다(흔한 패턴). 관문·잠금·스탯변화·도착은 전부 간선 소유.
/// </summary>
/// <param name="ConditionLabel">
/// <b>해금조건</b> — 이 선택지를 고를 수 있으려면.
/// 미달이면 <b>잠긴 채 보인다</b>. 아예 숨기려면 그 식을 <see cref="VisibleConditionLabel"/>에
/// 적는다 — 숨김을 따로 켜는 칸을 두지 않는다(같은 말을 두 번 하는 칸이 된다).
/// </param>
public sealed record ChapterEdge(
    string FromEpisodeId,
    string ToEpisodeId,
    string? OptionLabel,
    string? ConditionLabel,

    string? LockedMessage)
{
    /// <summary>
    /// 플레이어 입력 없이 다음 에피소드로 잇는 명시적 자동 진행 간선 — 장면을 넘어도 된다(2026-11-18 · ⛔ 그 전에는 같은 장면 안에서만).
    /// 문구 공백으로 추측하지 않는다. 저작 검증이 유일 간선·무조건·무증감·빈 문구 규칙을 강제한다.
    /// </summary>
    public bool Auto { get; init; }

    /// <summary>
    /// <b>표시조건</b> — 이 선택지가 목록에 보이려면. 보일지 말지는 간선이 정하고, 비면
    /// 언제나 보인다. 해금조건(<see cref="ConditionLabel"/>)과는 축이 다르다.
    /// </summary>
    public string? VisibleConditionLabel { get; init; }

    /// <summary>
    /// 문구가 없다. <see cref="Auto"/>이면 의도한 자동 진행이고, 아니면 규격 위반이다.
    /// 이것이 참이면 <b>고칠 것이 있다는 뜻</b>이지 길의 종류가 아니다.
    ///
    /// 모델이 이 상태를 담는 이유: 리더는 오류가 있어도 모델을 만든다(그래야 화면이
    /// 무엇이 잘못됐는지 짚어 준다). 담지 않으면 기획자는 빈 칸을 볼 수 없다.
    /// </summary>
    public bool HasNoOptionLabel => string.IsNullOrWhiteSpace(OptionLabel);

    /// <summary>
    /// 사람이 판에서 찾아갈 수 있게 적는 이 간선 — <c>간선 a → b "문구"</c>. 진단 · 거절 목록이 쓴다.
    /// </summary>
    public string Where =>
        string.Format(Msg.Diag_Path, FromEpisodeId, ToEpisodeId) +
        (OptionLabel is { Length: > 0 } text ? $" \"{text}\"" : string.Empty);

    /// <summary>표시·해금 중 하나라도 걸려 있으면 관문 있는 길이다 — 그래프가 색으로 알린다.</summary>
    public bool HasGate =>
        !string.IsNullOrWhiteSpace(VisibleConditionLabel) ||
        !string.IsNullOrWhiteSpace(ConditionLabel);

    // OptionLabel = `선택지` 열(D)의 값 그대로다. 파생도 참조도 아니다 —
    // 문구를 고쳐도 배선이 안 깨지는 대신, 같은 문구를 어느 에피소드에서든 쓸 수 있다.

    // 간선에 엔딩키를 두지 않는다 — 시나리오 층으로 나가는 통로(EndingRules)가 이 툴에 없어
    // 아무 데도 실리지 않는 칸이 된다. 툴의 보장은 챕터 안의 분기·그래프 구조까지다.

    /// <summary>
    /// `스탯변화` 열 — 이 간선을 타는 순간 1회 커밋되는 증감. 스탯이 변하는
    /// 유일한 자리다: 에피소드 안에서는 변하지 않으므로 세이브/로드 복귀가 일관되고,
    /// 도달성 증명이 근사 없이 정확값으로 전이한다. 조건 판정은 커밋 <b>전</b> 값으로 한다
    /// (플레이어가 선택지를 보는 시점의 값).
    /// </summary>
    public IReadOnlyList<StatDelta> StatChanges { get; init; } = [];

    /// <summary>
    /// 연출 그래프에서 이 길(선택지 레인)을 사람이 고친 모양. null이면 자동이다.
    /// 간선이 들고 있어야 도착 · 문구를 고치거나 에피소드 이름을 바꿔도(<c>with</c>) 모양이 함께 간다.
    /// 흐름과 산출물에는 아무 뜻이 없다 — 에피소드의 <see cref="ChapterEpisode.X"/>처럼 화면의 값이다.
    /// </summary>
    public Model.EdgeShape? Shape { get; init; }
}

/// <summary>
/// `조건` 시트 한 행. 라벨↔식 표이며 <c>ConditionDefinition{Name, Expression}</c>에 대응한다 (G-7).
/// <see cref="Parsed"/>는 툴이 검증에 쓰는 해석 결과일 뿐, <see cref="Expression"/> 원문이 정본이다 —
/// 게임이 평가하는 것은 원문이고 툴은 그 의미를 소유하지 않는다(무해석성, §0.5).
/// </summary>
public sealed record ChapterCondition(
    string Label,
    string Expression,
    string? Description,
    IReadOnlyList<ConditionTerm> Parsed,
    bool IsValid);

/// <summary>스탯의 타입 — `타입` 열(선택, 비면 int)이 정한다.</summary>
public enum ChapterStatType
{
    /// <summary>정수. 조건은 &lt; &gt; == &gt;= &lt;= 비교, 스탯변화는 증감.</summary>
    Int,

    /// <summary>참/거짓. 값 공간은 0/1이고 조건은 <c>== true/false</c>뿐이다.</summary>
    Bool
}

/// <summary>
/// `스탯` 시트 한 행 = Tier 2 키 하나. <see cref="Minimum"/>·<see cref="Maximum"/>는 장식이 아니라
/// 도달성 증명의 <b>탐색 경계</b>다 (§7-5). bool 스탯은 편집기가 경계를 0·1로 고정하고 모델 검사가 어긋남을 짚는다(ChapterStructureCheck) —
/// 프루버·픽스처 워커는 타입을 몰라도 그대로 옳게 돈다.
///
/// <b>키만 있다</b> — 표시 이름은 2026-11-16에 걷혔다(writer-fields W2). 게임 화면에 보일 글자는 게임 쪽이 키로 로컬라이즈한다. 작가가 대사에
/// 쓰는 <c>{stat("키")}</c>의 따옴표 안이 이 키다.
/// </summary>
public sealed record ChapterStat(
    string Key,
    int Initial,
    int Minimum,
    int Maximum,
    ChapterStatType Type = ChapterStatType.Int);

/// <summary>
/// `선택지` 시트 한 행 = <b>선택지 문구 하나</b>. 이 시트는 어느
/// 에피소드의 소유물이 아니라 <b>챕터 전체가 함께 쓰는 문구 사전</b>이다 — `조건` 시트가
/// 라벨↔식의 사전인 것과 같은 자리다. 여기서 한 번 적어 두면 <b>어떤 에피소드의 어떤
/// 간선에서든 가져다 쓴다.</b>
///
/// 행이 갖는 것은 <b>인덱스(사전 안의 순서)와 대본 text뿐</b>이다. 도착·조건·잠금·
/// 스탯변화는 전부 간선의 것이고, 간선은 이 인덱스가 아니라 <b>문구 자체</b>를 적는다 —
/// 그래서 이 사전에서 행을 지워도 이미 쓰인 문구는 살아 있다(사전은 어휘집이지 배선이 아니다).
/// </summary>
public sealed record ChapterChoiceOption(
    string Index,
    string Text,
    string? Memo);

/// <summary>
/// <b>장면 메모 하나</b> — 작가가 장면마다 자유롭게 적는 사정(의도 · 인물의 감정 · 고비 · 자리). AI가 장면 하나씩
/// 연출 초안을 낼 때 대사와 함께 읽는다(<c>docs/work-orders/scene-notes-orders.md</c>).
///
/// <b>장면이 아니라 장면에 붙는 곁 자료다.</b> 장면의 구성원 · 순서 · 루트는 여기 없다 — 늘 <see cref="ChapterEpisode.SceneId"/>에서
/// 계산한다(계약 §0-0 "Scene 엔티티를 만들지 않는다"). 열쇠는 <see cref="ChapterEpisode.SceneId"/>다. 그 장면의
/// 에피소드가 하나도 없으면 <b>주인 없는 메모</b>다 —
/// 조용히 지우지 않는다(<see cref="ChapterSceneNotes"/>).
///
/// 에피소드마다의 지시는 여기 없다 — 그 에피소드의 <see cref="ChapterEpisode.Memo"/>다(에피소드를 따라다닌다).
///
/// <b>장면 이름</b>(<paramref name="Name"/>)도 여기 산다 — 작가가 [대본] 탭에서 붙이는 이름이고, SceneId(런타임 저장 · 롤백 경계)와 따로다.
/// 같은 이름의 장면이 여럿이어도 SceneId가 달라 합쳐지지 않는다(<c>docs/work-orders/display-names-orders.md</c>). 비면 SceneId가 보인다.
/// </summary>
/// <param name="Text">장면 메모 — 비어도 된다(이름만 있는 항목).</param>
/// <param name="Name">장면 이름 — 비면 null.</param>
public sealed record ChapterSceneNote(string SceneId, string Text, string? Name = null);

/// <param name="From">고정 선택의 출발 에피소드.</param>
/// <param name="To">그 선택이 향하는 도착 에피소드.</param>
public sealed record ChapterFixtureChoice(string From, string To);

/// <summary>
/// `픽스처` 시트 한 행. <b>내보내기에 섞이지 않는다</b> (§3.1) — 재생루트를 눈으로 보기 위한
/// 테스트 데이터다. 픽스처 워커가 이걸로 경로를 하이라이트한다.
/// </summary>
public sealed record ChapterFixture(
    string Name,
    bool IsActive,
    IReadOnlyDictionary<string, int> Stats,
    IReadOnlyList<ChapterFixtureChoice> Choices);

/// <summary>
/// 챕터 하나를 편 모양 — 검증·증명·내보내기·화면이 읽는다(<see cref="ChapterDocument.ToGraphModel"/>).
/// <b>오류가 있어도 모델은 만들어진다</b> — 그릴 수 있는 데까지 그려 놓고
/// 무엇이 잘못됐는지 옆에 세워야 기획자가 고칠 자리를 찾는다. 조용히 빈 화면을 주지 않는다(규칙 14).
/// 내보내기는 <see cref="HasErrors"/>를 보고 거부한다.
/// </summary>
public sealed class ChapterGraphModel
{
    public ChapterGraphModel(
        string chapterId,
        string sourcePath,
        IReadOnlyList<ChapterEpisode> episodes,
        IReadOnlyList<ChapterEdge> edges,
        IReadOnlyList<ChapterCondition> conditions,
        IReadOnlyList<ChapterStat> stats,
        IReadOnlyList<ChapterFixture> fixtures,
        IReadOnlyList<ChapterDiagnostic> diagnostics,
        IReadOnlyList<ChapterChoiceOption>? choiceOptions = null)
    {
        ChapterId = chapterId;
        SourcePath = sourcePath;
        Episodes = episodes;
        Edges = edges;
        Conditions = conditions;
        Stats = stats;
        Fixtures = fixtures;
        Diagnostics = diagnostics;
        ChoiceOptions = choiceOptions ?? [];
    }

    /// <summary>챕터 Id — 판 이름과 같다(챕터 = 판 1:1).</summary>
    public string ChapterId { get; }

    /// <summary>사람이 읽는 챕터 이름 — <see cref="ChapterDocument.DisplayName"/>. 비면 null.</summary>
    public string? DisplayName { get; init; }

    /// <summary>보이는 챕터 이름 — 표시 이름, 비면 ChapterId. [대본] 탭이 보이는 이름이다.</summary>
    public string ShownName => ChapterDocument.ShownNameOf(ChapterId, DisplayName);

    public string SourcePath { get; }

    public IReadOnlyList<ChapterEpisode> Episodes { get; }

    /// <summary>
    /// 챕터 시작점 또는 다른 장면에서 처음 들어오는 에피소드는 장면 루트다.
    ///
    /// <b>판정은 여기 없다</b> — <see cref="ChapterSceneEntryCheck.IsEntry"/>가 갖는다.
    /// 화면의 ⌂ 표식과 진단이 <b>같은 것</b>을 불러야 사람이 어느 쪽을
    /// 믿을지 묻지 않는다.
    /// </summary>
    public bool IsSceneRoot(ChapterEpisode episode) =>
        ChapterSceneEntryCheck.IsEntry(Episodes, Edges, episode);

    public IReadOnlyList<ChapterEdge> Edges { get; }

    public IReadOnlyList<ChapterCondition> Conditions { get; }

    public IReadOnlyList<ChapterStat> Stats { get; }

    public IReadOnlyList<ChapterFixture> Fixtures { get; }

    /// <summary>`선택지` 시트 — 챕터가 함께 쓰는 문구 사전. 인덱스 순으로 선다.</summary>
    public IReadOnlyList<ChapterChoiceOption> ChoiceOptions { get; }

    /// <summary>장면 메모 — <see cref="ChapterDocument.SceneNotes"/>. 런타임은 읽지 않는다.</summary>
    public IReadOnlyList<ChapterSceneNote> SceneNotes { get; init; } = [];

    public IReadOnlyList<ChapterDiagnostic> Diagnostics { get; }

    public bool HasErrors =>
        Diagnostics.Any(item => item.Severity == ChapterDiagnosticSeverity.Error);

    public IEnumerable<ChapterDiagnostic> Errors =>
        Diagnostics.Where(item => item.Severity == ChapterDiagnosticSeverity.Error);

    /// <summary>
    /// 시작 에피소드 = `에피소드` 시트의 첫 행. 규격에 시작 열이 없어 <b>읽는 순서가 곧 정의</b>다.
    /// 도달성 증명과 내보내기의 `StartEpisodeId`가 같은 이 규칙 하나를 쓴다 —
    /// 런타임은 이 값이 비면 도달 제한을 아예 걸지 않으므로, 두 곳이 갈리면 안 된다.
    /// </summary>
    public ChapterEpisode? StartEpisode => Episodes.Count > 0 ? Episodes[0] : null;

    public ChapterEpisode? FindEpisode(string episodeId) =>
        Episodes.FirstOrDefault(item => string.Equals(item.EpisodeId, episodeId, StringComparison.Ordinal));

    public ChapterCondition? FindCondition(string label) =>
        Conditions.FirstOrDefault(item => string.Equals(item.Label, label, StringComparison.Ordinal));

    /// <summary>
    /// 그 에피소드에 오류 진단이 붙어 있는가. 뷰가 노드에 표식을 다는 근거다 — 진단이 짚는
    /// 에피소드 Id로 찾는다(<see cref="ChapterDiagnostic.EpisodeId"/>).
    /// </summary>
    public bool EpisodeHasError(ChapterEpisode episode) =>
        Diagnostics.Any(item =>
            item.Severity == ChapterDiagnosticSeverity.Error &&
            string.Equals(item.EpisodeId, episode.EpisodeId, StringComparison.Ordinal));
}

/// <summary>시트 이름 한 곳. 리더와 뷰·테스트가 같은 문자열을 본다.</summary>
public static class ChapterSheetNames
{
    public const string Episodes = "에피소드";
    public const string Edges = "간선";
    public const string Conditions = "조건";
    public const string Stats = "스탯";
    public const string Choices = "선택지";
    public const string SceneNotes = "장면 메모";

    /// <summary>화자 — 프로젝트의 화자 목록과 그 캐릭터 키(<see cref="ChapterSpeakerKeys"/>).</summary>
    public const string Speakers = "화자";
}
