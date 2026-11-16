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
    /// <summary>글이 빈 메모 — 쓰지 않는다.</summary>
    public static bool IsBlank(ChapterSceneNote note) => string.IsNullOrWhiteSpace(note.Text);

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

    /// <summary>그 장면의 메모 — 없거나 비었으면 null.</summary>
    public static ChapterSceneNote? Of(IEnumerable<ChapterSceneNote> notes, string sceneId) =>
        notes.FirstOrDefault(note =>
            string.Equals(note.SceneId, sceneId, StringComparison.Ordinal) && !IsBlank(note));
}
