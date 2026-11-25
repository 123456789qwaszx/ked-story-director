using Vn.Authoring.Script;

namespace Vn.Authoring.Model;

/// <summary>
/// <b>대본에만 있는 화자</b> (2026-11-19 소유자 — 「작가들은 화자를 추가하지 않고도 그대로 대사에 speaker로 바로 쓰기도 합니다. 이런 등록되지 않은
/// speaker들 역시 화자 탭에서 모아 둔 뒤에 관리하는 기능」 · usability UX34). 대사의 화자 칸에 적혔는데 화자 목록
/// (<see cref="StoryProject.Speakers"/>)에 없는 이름 — [대본] 「화자」 탭이 모아 보이고, 거기서 목록에 올리거나(임시 키) 이름을 고친다.
///
/// 프로젝트의 모든 대사 카드(에피소드 · 분기 카드)의 살아 있는 줄을, 기본 언어의 화자 칸으로 본다. 지문(화자 없는 줄)은 세지 않는다.
/// 이름 차례(가나다)로 선다 — 비슷한 이름(오타)이 나란히 서서 눈에 띈다.
/// </summary>
public static class UnlistedSpeakers
{
    /// <summary>대본에만 있는 이름 하나 — 이름과 그 이름이 화자인 줄 수(모든 챕터 합계).</summary>
    public sealed record Entry(string Name, int Lines);

    public static IReadOnlyList<Entry> Of(StoryProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var lines = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (DialogueNode node in project.EnumerateNodes().OfType<DialogueNode>())
        {
            foreach (ScriptLine line in node.Script.ActiveLines)
            {
                string speaker = node.Script.Text(line.Id).Speaker?.Trim() ?? string.Empty;

                if (speaker.Length > 0 && project.FindSpeaker(speaker) is null)
                {
                    lines[speaker] = lines.GetValueOrDefault(speaker) + 1;
                }
            }
        }

        return lines
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new Entry(pair.Key, pair.Value))
            .ToList();
    }
}
