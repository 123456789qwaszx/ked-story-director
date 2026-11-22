using Vn.Authoring.Model;
using Vn.Authoring.Script;

namespace Vn.Authoring.Editing;

/// <summary>
/// <b>화자 명령</b> — 프로젝트의 화자 목록(<see cref="StoryProject.Speakers"/>)을 바꾸는 길은 여기뿐이다(2026-11-17 · definition-split S3).
/// 조작 하나 = 되돌리기 한 칸이고, 같은 값은 기록하지 않는다. 이름은 [대본] 탭(작가)이, 캐릭터 키는 [구성] 탭(기획자)이 고친다.
///
/// ⛔ 그 전에는 세션이 <c>game.definition.json</c>에 목록을 통째로 바로 썼다(되돌리기 밖 · 저장 안 한 프로젝트에서는 못 고침).
/// </summary>
public sealed partial class ProjectEditor
{
    /// <summary>
    /// 화자를 목록 끝에 더한다. 이름이 비었거나 이미 있으면 거절한다(예외). 캐릭터 키는 <b>임시 키</b>로 선다 — <c>temp_</c> + 이름의 로마자
    /// (<see cref="CharacterKey.NewTemp"/> · usability UX29 — ⛔ 전에는 빈 채로 섰다). 화자와 키가 되돌리기 한 칸이다.
    /// </summary>
    public Speaker AddSpeaker(string name)
    {
        string typed = (name ?? string.Empty).Trim();

        if (typed.Length == 0)
        {
            throw new InvalidOperationException("화자 이름이 비어 있습니다.");
        }

        if (Project.FindSpeaker(typed) is not null)
        {
            throw new InvalidOperationException($"화자 '{typed}'은 이미 있습니다.");
        }

        var speaker = new Speaker(typed, CharacterKey.NewTemp(typed, Project.Speakers.Select(item => item.CharacterId)));
        Mutate(ProjectChangeKind.Content, () => Project.Speakers.Add(speaker));
        return speaker;
    }

    /// <summary>
    /// 여러 화자를 <b>한 칸에</b> 목록 끝에 더한다 — [대본] 「화자」 탭의 [모두 등록](대본에만 있는 이름들 · <see cref="UnlistedSpeakers"/> ·
    /// usability UX34). 이름마다 임시 키가 선다(<see cref="AddSpeaker"/>와 같고, 함께 더하는 이름끼리도 겹치지 않는다). 빈 이름 · 이미 있는 이름 ·
    /// 겹친 이름은 건너뛴다. 더한 것이 없으면 기록하지 않는다.
    /// </summary>
    /// <returns>더한 화자들 — 준 차례대로.</returns>
    public IReadOnlyList<Speaker> AddSpeakers(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var keys = Project.Speakers.Select(item => item.CharacterId).ToList();
        var added = new List<Speaker>();

        foreach (string name in names)
        {
            string typed = (name ?? string.Empty).Trim();

            if (typed.Length == 0 ||
                Project.FindSpeaker(typed) is not null ||
                added.Any(item => string.Equals(item.Name, typed, StringComparison.Ordinal)))
            {
                continue;
            }

            var speaker = new Speaker(typed, CharacterKey.NewTemp(typed, keys));
            keys.Add(speaker.CharacterId);
            added.Add(speaker);
        }

        if (added.Count > 0)
        {
            Mutate(ProjectChangeKind.Content, () => Project.Speakers.AddRange(added));
        }

        return added;
    }

    /// <summary>화자를 목록에서 지운다. 이미 대본에 적힌 이름은 그대로 남는다(화자 칸은 자유 입력). 없으면 false — 기록하지 않는다.</summary>
    public bool RemoveSpeaker(string name)
    {
        if (Project.FindSpeaker(name) is not { } speaker)
        {
            return false;
        }

        Mutate(ProjectChangeKind.Content, () => Project.Speakers.Remove(speaker));
        return true;
    }

    /// <summary>화자의 캐릭터 키 — 초상화 · 표정이 이 키로 붙는다. 비우면 붙지 않는다. 없는 화자이거나 같은 값이면 false — 기록하지 않는다.</summary>
    public bool SetSpeakerCharacterId(string name, string? characterId)
    {
        string typed = (characterId ?? string.Empty).Trim();

        if (Project.FindSpeaker(name) is not { } speaker ||
            string.Equals(speaker.CharacterId, typed, StringComparison.Ordinal))
        {
            return false;
        }

        int index = Project.Speakers.IndexOf(speaker);
        Mutate(ProjectChangeKind.Content, () => Project.Speakers[index] = speaker with { CharacterId = typed });
        return true;
    }

    /// <summary>
    /// 화자의 이름 색(작가용 · writer-formatting WF-D15) — [대본] 카드 글 칸에서 그 화자의 이름이 이 색으로 보인다. null이면 걷는다. 없는 화자이거나 같은 값이면
    /// false — 기록하지 않는다. 글 칸의 꾸밈만 바뀌므로 서식과 같은 종류로 알린다(판 · 철도 · [구성]은 그대로).
    /// </summary>
    public bool SetSpeakerColor(string name, WriterRgb? color)
    {
        if (Project.FindSpeaker(name) is not { } speaker || speaker.Color == color)
        {
            return false;
        }

        int index = Project.Speakers.IndexOf(speaker);
        Mutate(ProjectChangeKind.WriterMarks, () => Project.Speakers[index] = speaker with { Color = color });
        return true;
    }

    /// <summary>
    /// 화자 개명을 <b>목록과 이미 쓰인 대사 줄까지 한 칸에</b> 끌고 간다 — 화자의 이름을 고쳐도 연결이 이어진다. 캐릭터 키는 그대로다.
    ///
    /// 화자의 신원은 <b>목록의 그 줄</b>이고 이름은 그것의 표시다. 그런데 대사 줄이
    /// 붙드는 것은 이름 문자열 하나뿐이라, 목록에서만 이름을 갈면
    /// 그 줄들이 전부 <b>미등록 화자</b>가 된다 — 초상화 매핑이 끊기고, 공백 있는 이름은
    /// 파서가 산문으로 읽어 대사와 합쳐지기까지 한다.
    ///
    /// <b>locale마다 따로 본다</b> (로컬라이징 대비). <see cref="LocalizedLine.Speaker"/>는
    /// locale별 값이라, 일본어 판이 이미 <c>ウィロー</c>로 번역돼 있으면 그것은 이 개명의
    /// 대상이 아니다 — <b>옛 이름과 글자가 같은 항목만</b> 갈아 끼운다. 아직 번역 전이라
    /// 한국어 이름을 그대로 들고 있는 locale은 자연히 함께 따라온다.
    ///
    /// <b><see cref="ScriptLine.Revision"/>은 올리지 않는다.</b> 그것은 "이 줄의 문구가
    /// 바뀌었으니 번역·녹음이 다시 보라"는 신호인데, 화자의 개명은 줄이 <em>말하는 내용</em>을
    /// 바꾸지 않는다. 올리면 화자 하나 고칠 때마다 그 화자의 모든 줄이 재작업 대상이 된다.
    ///
    /// 새 이름이 이미 목록에 있으면 거절한다(예외) — 두 화자의 줄이 한 이름으로 합쳐진다. 단 <b>목록에 없는 이름</b>(대본에만 있는 이름)은
    /// 목록의 이름으로 고칠 수 있다 — 그 줄들이 그 화자로 합쳐진다(오타 고치기 · usability UX34).
    /// </summary>
    /// <returns>이름이 바뀐 줄 수(모든 대본·모든 locale 합계). 목록에도 줄에도 없으면 0이고 기록하지 않는다.</returns>
    public int RenameSpeaker(string oldName, string newName)
    {
        string from = (oldName ?? string.Empty).Trim();
        string to = (newName ?? string.Empty).Trim();

        if (from.Length == 0 || to.Length == 0 ||
            string.Equals(from, to, StringComparison.Ordinal))
        {
            return 0;
        }

        Speaker? listed = Project.FindSpeaker(from);

        if (listed is not null && Project.FindSpeaker(to) is not null)
        {
            throw new InvalidOperationException($"화자 '{to}'은 이미 있습니다.");
        }

        List<(ScriptLocale Locale, string LineId, LocalizedLine Line)> hits = Project.EnumerateScripts()
            .SelectMany(script => script.Locales)
            .SelectMany(locale => locale.Entries
                .Where(entry => string.Equals(entry.Value.Speaker, from, StringComparison.Ordinal))
                .Select(entry => (Locale: locale, LineId: entry.Key, Line: entry.Value)))
            .ToList();

        if (hits.Count == 0 && listed is null)
        {
            return 0;
        }

        Mutate(hits.Count > 0 ? ProjectChangeKind.DialogueContent : ProjectChangeKind.Content, () =>
        {
            foreach ((ScriptLocale locale, string lineId, LocalizedLine line) in hits)
            {
                locale.Entries[lineId] = line with { Speaker = to };
            }

            if (listed is not null)
            {
                Project.Speakers[Project.Speakers.IndexOf(listed)] = listed with { Name = to };
            }
        });

        return hits.Count;
    }
}
