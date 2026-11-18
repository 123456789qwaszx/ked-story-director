using System;
using System.Collections.Generic;

namespace Ked.Progression
{
    // 지금 에피소드에서 갈 수 있는 길을 확정.
    // - 계획을 먼저 확정하고 그 뒤엔 고르기만 함.
    // - 여기서 한 번 판정하면 그 결과를 화면에 표시.
    // - 플레이어가 고른 뒤에야 값 변경.
    public static class ChapterTransition
    {
        private static readonly ResolvedOption[] None = new ResolvedOption[0];

        public static ChapterAdvance Resolve(
            ChapterDefinition chapter,
            ChapterState state)
        {
            chapter.TryGetNode(state.CurrentEpisodeId, out EpisodeNode node);
            
            IReadOnlyList<EpisodeOption> options = node.NextOptions;

            // 자동 간선 - 불변식이 "유일한 간선·조건 없음"을 보장하므로 판정할 것이 없다.
            if (options.Count == 1 && options[0].IsAuto)
            {
                return new ChapterAdvance(
                    ChapterAdvanceKind.AutoAdvance, 
                    new[] { ResolvedOption.Shown(options[0], 0) }, 
                    0);
            }
            
            var shownOrLocked = new List<ResolvedOption>();
            int hidden = 0;
            bool anySelectable = false;

            for (int i = 0; i < options.Count; i++)
            {
                EpisodeOption option = options[i];
                
                // 실패한 표시 조건을 목록에서 숨긴다
                if (FirstUnmet(option.VisibleConditions, state).IsConstructed)
                {
                    hidden++;
                    continue;
                }

                ProgressionCondition blocking = FirstUnmet(option.Conditions, state);

                // 막고 있는 조건이 없으면, 이 선택지를 선택 가능한 상태로 추가
                if (!blocking.IsConstructed)
                {
                    shownOrLocked.Add(ResolvedOption.Shown(option, i));
                    anySelectable = true;
                    continue;
                }

                shownOrLocked.Add(ResolvedOption.Locked(option, i, blocking));
            }

            if (anySelectable)
                return new ChapterAdvance(
                    ChapterAdvanceKind.AwaitPlayerChoice, shownOrLocked, hidden);
            
            return new ChapterAdvance(
                ChapterAdvanceKind.ChapterEnded, None, hidden);
        }

        // 미달 조건 중 첫번째 것.
        private static ProgressionCondition FirstUnmet(
            IReadOnlyList<ProgressionCondition> conditions,
            ChapterState state)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!ConditionEvaluator.IsMet(conditions[i], state))
                    return conditions[i];
            }

            return default;
        }
    }
}