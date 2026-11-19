using Vn.Authoring.Model;
using Vn.Authoring.Text;

namespace Vn.Authoring.Chapters;

/// <summary>
/// <b>캐릭터 키가 없는 화자</b>(2026-11-18 소유자 — 「구성탭에서 캐릭터 키가 없을 경우, 검증보고에서 오류 알림을 띄우긴 하는 게 좋겠습니다」 ·
/// <c>docs/work-orders/writer-comfort-orders.md</c> WC6). 그 챕터의 대사(에피소드 카드 · 분기 카드)에 나오는데 캐릭터 키가 없는 화자마다
/// 한 줄을 검증 보고에 낸다 — 그 화자의 대사에는 초상화 · 표정이 붙지 않는다.
///
/// <b>경고다</b> — 진행 JSON을 막지 않는다. 키는 연출 쪽(초상화 · 표정)의 것이고 키가 없어도 이야기는 돈다. 지문(화자 없는 줄)은 세지 않는다.
/// 화자 목록에 아예 없는 이름도 키가 없는 것이라 함께 짚는다(어디서 더하는지가 달라 말이 다르다).
///
/// <b>임시 키도 짚는다</b>(usability UX29) — 화자를 더하면 키가 <c>temp_</c> + 이름의 로마자로 서는데(<see cref="CharacterKey"/>), 그대로면
/// 그림과 맞지 않는다. 아직 정하지 않은 키라 같은 경고로, 말만 다르게 낸다.
/// </summary>
public static class ChapterSpeakerKeys
{
    /// <summary>키가 없는 화자 하나 — 이름 · 화자 목록에 있는가 · 그 챕터의 대사 줄 수 · 처음 나오는 곳 · 임시 키(있으면).</summary>
    public sealed record Missing(string Speaker, bool Registered, int Lines, string FirstPlace, string? TempKey = null);

    public static void Verify(ChapterGraphModel chapter, StoryProject project, List<ChapterDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(diagnostics);

        foreach (Missing missing in MissingOf(chapter, project))
        {
            string speaker = missing.Speaker;
            string where = $"대사 {missing.Lines}줄(처음 나오는 곳 — {missing.FirstPlace})에 초상화 · 표정이 붙지 않습니다.";
            diagnostics.Add(new ChapterDiagnostic(
                ChapterDiagnosticSeverity.Warning,
                ChapterDiagnosticCode.SpeakerCharacterKeyMissing,
                chapter.SourcePath,
                ChapterSheetNames.Speakers,
                null,
                missing.TempKey is { } temp
                    ? $"화자 '{speaker}'의 캐릭터 키가 아직 임시 키 '{temp}'입니다 — 그림과 맞지 않으면 {where} [구성] 탭 오른쪽 「화자」 탭에서 " +
                      "그림에 맞는 키로 바꿔 주세요."
                    : missing.Registered
                    ? $"화자 '{speaker}'의 캐릭터 키가 없습니다 — {where} [구성] 탭 오른쪽 「화자」 탭에서 키를 적어 주세요."
                    : $"화자 '{speaker}'{Josa.은는(speaker)} 화자 목록에 없어 캐릭터 키가 없습니다 — {where} [대본] 탭 「화자」 탭에서 더한 뒤 " +
                      "[구성] 탭 「화자」 탭에서 키를 적어 주세요."));
        }
    }

    /// <summary>
    /// 그 챕터의 대사에 나오는데 캐릭터 키가 없는 화자 — 처음 나오는 차례(에피소드 차례, 그다음 분기 카드). 검증 캐시의 지문도 이것을 싣는다 —
    /// 화자의 키는 챕터 모델 밖(프로젝트의 화자 목록)이라 싣지 않으면 키를 적어도 옛 경고가 남는다.
    /// </summary>
    public static IReadOnlyList<Missing> MissingOf(ChapterGraphModel chapter, StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(project);

        var found = new Dictionary<string, (int Lines, string FirstPlace)>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach ((DialogueNode node, string place) in ChapterScripts.PlacesOf(chapter, project))
        {
            foreach (Vn.Authoring.Script.ScriptLine line in node.Script.ActiveLines)
            {
                string speaker = node.Script.Text(line.Id).Speaker?.Trim() ?? string.Empty;

                if (speaker.Length == 0 || project.FindSpeakerCharacterId(speaker) is { } key && !CharacterKey.IsTemp(key))
                {
                    continue;   // 지문이거나 정한 키가 있다
                }

                if (found.TryGetValue(speaker, out (int Lines, string FirstPlace) seen))
                {
                    found[speaker] = (seen.Lines + 1, seen.FirstPlace);
                }
                else
                {
                    found[speaker] = (1, place);
                    order.Add(speaker);
                }
            }
        }

        return order
            .Select(speaker => new Missing(
                speaker, project.FindSpeaker(speaker) is not null, found[speaker].Lines, found[speaker].FirstPlace,
                project.FindSpeakerCharacterId(speaker) is { } key && CharacterKey.IsTemp(key) ? key : null))
            .ToList();
    }
}
