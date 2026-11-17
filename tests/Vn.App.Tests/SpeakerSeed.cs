using Vn.App.Services;
using Vn.Authoring.Model;

namespace Vn.App.Tests;

/// <summary>시험의 준비 — 프로젝트의 화자 목록을 이 모양으로 맞춘다. 편집 명령을 지난다(조작마다 되돌리기 한 칸).</summary>
internal static class SpeakerSeed
{
    public static void SetSpeakers(this AuthoringSession session, params (string Name, string CharacterId)[] speakers)
    {
        foreach (Speaker old in session.Project.Speakers.ToList())
        {
            if (!speakers.Any(speaker => string.Equals(speaker.Name, old.Name, StringComparison.Ordinal)))
            {
                session.Editor.RemoveSpeaker(old.Name);
            }
        }

        foreach ((string name, string characterId) in speakers)
        {
            if (session.Project.FindSpeaker(name) is null)
            {
                session.Editor.AddSpeaker(name);
            }

            session.Editor.SetSpeakerCharacterId(name, characterId);
        }
    }
}
