using System;
using System.Collections.Generic;

namespace Ked.Progression
{
    // 시나리오 전체 룰
    internal static class ScenarioInvariants
    {
        public static void Collect(
            IReadOnlyList<ChapterDefinition> chapters,
            string startChapterId,
            ICollection<ProgressionDiagnostic> into,
            out Dictionary<string, ChapterDefinition> chaptersById)
        {
            chaptersById = IndexChapters(chapters, into);

            VerifyStart(startChapterId, chaptersById, into);
        }

        private static Dictionary<string, ChapterDefinition> IndexChapters(
            IReadOnlyList<ChapterDefinition> chapters, ICollection<ProgressionDiagnostic> into)
        {
            var byId = new Dictionary<string, ChapterDefinition>(StringComparer.Ordinal);

            for (int i = 0; i < chapters.Count; i++)
            {
                ChapterDefinition chapter = chapters[i];

                if (chapter == null)
                {
                    into.Add(ProgressionDiagnostic.Error($"Chapters[{i}]", "A chapter is null."));
                    continue;
                }

                if (byId.ContainsKey(chapter.ChapterId))
                {
                    into.Add(ProgressionDiagnostic.Error(
                        $"Chapters[{i}]", $"Chapter ID '{chapter.ChapterId}' is duplicated."));
                    continue;
                }

                byId[chapter.ChapterId] = chapter;
            }

            return byId;
        }

        private static void VerifyStart(
            string startChapterId,
            Dictionary<string, ChapterDefinition> chaptersById,
            ICollection<ProgressionDiagnostic> into)
        {
            if (!string.IsNullOrEmpty(startChapterId) && chaptersById.ContainsKey(startChapterId))
                return;

            into.Add(ProgressionDiagnostic.Error(
                "StartChapterId",
                string.IsNullOrEmpty(startChapterId)
                    ? "The start chapter is empty. The scenario has nowhere to start."
                    : $"Start chapter '{startChapterId}' doesn't exist. Existing: {Join(chaptersById.Keys)}"));
        }

        private static string Join(IEnumerable<string> keys) => string.Join(", ", keys);
    }
}