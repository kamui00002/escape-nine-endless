// RelicDraftService.cs
// Unity Phase 5「ローグライク深化」設計文書 §2.1 (ドラフトの仕組み) / §6.1 に基づく。
// Swift正本には存在しない (Unity固有の追加機能)。
//
// Phase 5a スコープ: 重み付けなしの均等抽選 (弱点タグ重み付けは §2.2、Phase 5bで実装)。
// 重複禁止 (1ドラフト内で同じレリックは1回だけ) / スタック可・上限あり / プール枯渇時は
// 候補数が減る (0件なら空リスト) を実装する。

using System.Collections.Generic;

namespace EscapeNine.Core
{
    public sealed class RelicDraftService
    {
        private readonly IRandomSource _rng;
        private readonly IReadOnlyList<RelicDefinition> _pool;

        /// <param name="rng">省略時は既定の System 乱数。</param>
        /// <param name="pool">省略時は RelicCatalog.All (Phase 5aの8種)。テストでは差し替え可能。</param>
        public RelicDraftService(IRandomSource rng = null, IReadOnlyList<RelicDefinition> pool = null)
        {
            _rng = rng ?? new SystemRandomSource();
            _pool = pool ?? RelicCatalog.All;
        }

        /// <summary>
        /// ドラフト候補を生成する (既定3択)。
        /// character は Phase 5b の弱点タグ重み付けドラフトでシグネチャを変えずに済むよう、
        /// 5aの時点から受け取っておく (5aでは未使用)。
        /// </summary>
        /// <param name="ownedIds">現在のランで既に所持しているレリックID (スタック分は同じIDが複数回含まれる想定)。</param>
        /// <param name="character">ドラフト対象キャラクター (5aでは重み付けに使用しない)。</param>
        /// <param name="count">提示する候補数 (既定3)。</param>
        public List<RelicDefinition> DraftCandidates(IReadOnlyList<string> ownedIds, CharacterType character, int count = 3)
        {
            var ownedCounts = new Dictionary<string, int>();
            if (ownedIds != null)
            {
                foreach (var id in ownedIds)
                {
                    ownedCounts[id] = ownedCounts.TryGetValue(id, out var c) ? c + 1 : 1;
                }
            }

            // スタック上限に達していないレリックのみが抽選対象。
            var eligible = new List<RelicDefinition>();
            foreach (var def in _pool)
            {
                int owned = ownedCounts.TryGetValue(def.Id, out var c) ? c : 0;
                if (owned < def.StackLimit) eligible.Add(def);
            }

            var result = new List<RelicDefinition>();
            int draws = System.Math.Min(count, eligible.Count);
            for (int i = 0; i < draws; i++)
            {
                int idx = _rng.NextInt(eligible.Count);
                result.Add(eligible[idx]);
                eligible.RemoveAt(idx); // 同一ドラフト内での重複表示を禁止
            }
            return result;
        }
    }
}
