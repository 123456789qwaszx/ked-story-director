namespace Vn.Authoring.Chapters;

/// <summary>
/// 장면 메모(<see cref="ChapterSceneNote"/>)의 규칙 — 누가 주인인가, 어떤 차례로 쓰는가.
///
/// <b>주인</b>: 그 열쇠를 <see cref="ChapterEpisode.EffectiveSceneId"/>로 가진 에피소드가 하나라도 있으면 그 장면의 메모다.
/// 없으면 <b>주인 없는 메모</b> — 장면을 풀거나, 마지막 에피소드를 지우거나, 다른 장면으로 합쳤을 때 생긴다. 남겨 두고
/// 검증이 알린다(조용히 지우지 않는다 — §6-9와 같은 생각).
///
/// <b>차례</b>(결정적 출력 — 같은 입력이면 같은 바이트): 에피소드 차례에서 그 장면이 처음 나오는 차례, 그 뒤에 주인 없는
/// 메모를 열쇠의 서수 차례로. 글이 빈 메모는 없는 것이다.
/// </summary>
public static class ChapterSceneNotes
{
    /// <summary>글도 이름도 빈 항목 — 쓰지 않는다.</summary>
    public static bool IsBlank(ChapterSceneNote note) =>
        string.IsNullOrWhiteSpace(note.Text) && string.IsNullOrWhiteSpace(note.Name);

    /// <summary>그 장면의 메모 글 — 없거나 비었으면 null.</summary>
    public static string? TextOf(IEnumerable<ChapterSceneNote> notes, string sceneId) =>
        Of(notes, sceneId) is { Text: var text } && !string.IsNullOrWhiteSpace(text) ? text : null;

    /// <summary>그 장면의 이름 — 없거나 비었으면 null.</summary>
    public static string? NameOf(IEnumerable<ChapterSceneNote> notes, string sceneId) =>
        Of(notes, sceneId) is { Name: { } name } && !string.IsNullOrWhiteSpace(name) ? name.Trim() : null;

    /// <summary>그 장면의 에피소드가 하나라도 있는가.</summary>
    public static bool IsOwned(IReadOnlyList<ChapterEpisode> episodes, string sceneId) =>
        episodes.Any(episode => string.Equals(episode.EffectiveSceneId, sceneId, StringComparison.Ordinal));

    /// <summary>쓰는 차례 — 주인 있는 메모를 장면이 처음 나오는 차례로, 그 뒤에 주인 없는 메모를 열쇠 차례로. 빈 메모는 뺀다.</summary>
    public static List<ChapterSceneNote> Ordered(
        IReadOnlyList<ChapterEpisode> episodes, IEnumerable<ChapterSceneNote> notes)
    {
        var byScene = new Dictionary<string, ChapterSceneNote>(StringComparer.Ordinal);

        foreach (ChapterSceneNote note in notes)
        {
            if (!IsBlank(note))
            {
                byScene.TryAdd(note.SceneId, note);
            }
        }

        var ordered = new List<ChapterSceneNote>(byScene.Count);

        foreach (ChapterEpisode episode in episodes)
        {
            if (byScene.Remove(episode.EffectiveSceneId, out ChapterSceneNote? note))
            {
                ordered.Add(note);
            }
        }

        ordered.AddRange(byScene.Values.OrderBy(note => note.SceneId, StringComparer.Ordinal));
        return ordered;
    }

    /// <summary>주인 없는 메모 — 열쇠 차례로. 빈 메모는 뺀다.</summary>
    public static List<ChapterSceneNote> Orphans(
        IReadOnlyList<ChapterEpisode> episodes, IEnumerable<ChapterSceneNote> notes) =>
        Ordered(episodes, notes).Where(note => !IsOwned(episodes, note.SceneId)).ToList();

    /// <summary>
    /// 주인 없는 메모마다 경고 하나(<see cref="ChapterDiagnosticCode.SceneNoteOrphaned"/>) — 내보내기를 막지 않는다.
    /// 메시지는 메모의 첫머리를 보여 어느 메모인지 짚는다.
    /// </summary>
    public static IReadOnlyList<ChapterDiagnostic> Diagnostics(
        IReadOnlyList<ChapterEpisode> episodes, IEnumerable<ChapterSceneNote> notes, string path) =>
        Orphans(episodes, notes)
            .Select(note => new ChapterDiagnostic(
                ChapterDiagnosticSeverity.Warning,
                ChapterDiagnosticCode.SceneNoteOrphaned,
                path,
                ChapterSheetNames.SceneNotes,
                EpisodeId: null,
                $"장면 '{note.Name?.Trim() ?? ShownSceneName(note.SceneId)}'의 메모에 주인이 없습니다 — 그 장면의 에피소드가 없습니다" +
                (string.IsNullOrWhiteSpace(note.Text) ? ". " : $"(「{Lead(note.Text)}」). ") +
                "장면을 되살리거나 챕터 인스펙터에서 지웁니다."))
            .ToList();

    /// <summary>사람에게 보이는 장면 이름 — 미지정 장면(<c>__scene_{Id}</c>)은 화면과 같은 「미지정 · {Id}」.</summary>
    public static string ShownSceneName(string sceneId) =>
        sceneId.StartsWith(UnnamedPrefix, StringComparison.Ordinal)
            ? ChapterSceneGrouping.DefaultSceneName(sceneId[UnnamedPrefix.Length..])
            : sceneId;

    /// <summary>메모의 첫머리 한 줄 — 길면 자른다.</summary>
    public static string Lead(string text, int length = 24)
    {
        string line = text.Trim().Split('\n')[0].Trim();
        return line.Length <= length ? line : line[..length].TrimEnd() + "…";
    }

    private const string UnnamedPrefix = "__scene_";

    /// <summary>
    /// 그 장면에 적힌 것이 있는가 — 장면 메모나, 그 장면 에피소드의 지시(<see cref="ChapterEpisode.Memo"/>)가 하나라도.
    /// 하이어아키의 장면 줄과 판의 장면 이름표가 「메모」를 다는 근거다.
    /// </summary>
    public static bool HasMemo(IEnumerable<ChapterSceneNote> notes, ChapterScene scene) =>
        TextOf(notes, scene.SceneId) is not null || scene.Episodes.Any(episode => !string.IsNullOrWhiteSpace(episode.Memo));

    /// <summary>그 장면의 메모 — 없거나 비었으면 null.</summary>
    public static ChapterSceneNote? Of(IEnumerable<ChapterSceneNote> notes, string sceneId) =>
        notes.FirstOrDefault(note =>
            string.Equals(note.SceneId, sceneId, StringComparison.Ordinal) && !IsBlank(note));
}
