// RelicConfig.cs
// Unity Phase 5c「ドラフト頻度/取得上限レバー」導入。
// docs/unity-phase5-roguelike-design.md §2.1 が予告済みの定数化 (`RelicConfig.DraftInterval`)、
// および unity/verify/BALANCE_REPORT_PHASE5.md の分析メモ (5b) が提案した案A/Bに対応する。
//
// 背景 (BALANCE_REPORT_PHASE5.md 参照): 「毎階層クリアでドラフト」× プール18種のもとでは、
// 長命キャラ (魔法使い/ナイト) が1ランでプールをほぼ全取得してしまい、重み付け (RelicDraftService)
// が「取得の順序」しか変えられず「取得の集合」を変えられない。この構造により §6.5 c (魔法使いギャップ
// 縮小) と d (100階クリア率 <=10%) が FAIL していた。ドラフト頻度を絞り (DraftInterval)、
// 1ラン取得数に上限を設ける (MaxRelicsPerRun) ことで、重み付けレバーが再び意味を持つようにする。
//
// 値の決定はシミュレーション実測 (unity/verify/Sim, `dotnet run -- phase5-sweep`) に基づく。
// 全組の比較・選定理由は BALANCE_REPORT_PHASE5.md を参照。
//
// 置き場所の判断: GameConfig.cs (Swift Constants.swift の忠実移植を前提とする定数集) には置かず、
// 本ファイルを新設した。DraftInterval/MaxRelicsPerRun は Swift 正本に存在しない Unity 固有の
// 追加パラメータであり、RelicCatalog/RelicDraftService と同じ「レリック機能」の関心事のため。
namespace EscapeNine.Core
{
    public static class RelicConfig
    {
        /// <summary>
        /// N階層クリアごとに1回ドラフトを提示する。1 = 毎階層 (Phase 5a/5b 当初値、案A導入前の挙動)。
        ///
        /// 確定値 1 (=案A未使用): `dotnet run -- phase5-sweep` で (1,999)/(1,8)/(2,999)/(2,8)/(2,6)/(1,4)/(1,2)
        /// の7組を実測した結果 (BALANCE_REPORT_PHASE5.md 参照)、interval を2にする効果は cap 単体の効果に
        /// ほぼ埋もれる (同cap同士で比較すると (1,8)=52.8 と (2,8)=52.7 でギャップはほぼ同値)。
        /// 優先度 c > d > a' > b で全組を比較すると、d を満たす組の中で c (魔法使いギャップ) が最小なのは
        /// interval=1 のまま cap だけを絞った (1,4) (ギャップ 39.7、(2,6) の 47.6 より小さい)。
        /// つまり本タスクが導入した2レバーのうち、実際にデータで効果が確認できたのは MaxRelicsPerRun
        /// (案B) のみで、DraftInterval (案A) は「インフラとして実装したが最終値は無効化 (=1)」という
        /// 結論になった。DraftInterval を有効化する価値がある状況 (例: 実機プレイテストで「毎階層は
        /// ドラフトUIが煩雑」と判明した場合) に備え、定数自体は残す。
        /// </summary>
        public const int DraftInterval = 1;

        /// <summary>
        /// 所持レリック数 (スタック分を含む、このランで ChooseRelic/DraftOneRelic が確定させた総数) が
        /// この値に達したら、以後 DraftInterval に関わらずドラフトを提示しない (案B)。
        ///
        /// 確定値 4: 上記スイープで d (100階クリア率 <=10%) を全15構成で確実に満たし (魔法使い x
        /// Normal/Hard の勝率 0.8%/1.7%)、かつ「d を満たす組」の中で魔法使いギャップを (1,999) の
        /// 56.7 から 39.7 まで最も縮小できた値。cap を 2 まで下げるとようやく数値上 c が PASS する
        /// (ギャップ 38.7 vs baseline 38.8) が、それは盗賊の中央値も 8〜9 まで落ち込みレリック取得数が
        /// run あたり約2個まで減って「深化」機能そのものが実質無効化される退化点でしかなく、しかも
        /// cap=1→39.5, cap=2→38.7, cap=3→39.9, cap=4→39.7 と非単調 (BALANCE_REPORT_PHASE5.md 参照) で
        /// 統計的に頑健な閾値越えとは言えない (採用しない)。よって d を確実に解消できる範囲で c を
        /// 最大限縮小する cap=4 を採用した。それでも c はベースライン (38.8) を下回れず FAIL のまま
        /// ([要検証]: §6.5 c はレリック無効時点の格差 (=ベースゲームの魔法使い優位) を基準にしており、
        /// レリックという報酬系だけではこの格差を割れない構造限界。BALANCE_REPORT_PHASE5.md 参照)。
        /// </summary>
        public const int MaxRelicsPerRun = 4;

        /// <summary>
        /// レリック機能自体を解放する最小クリア済み階層 (RELIC_COHERENCE_AUDIT.md §4「レリック自体を
        /// 階層10クリア後に解放」)。この階層をクリアするまではドラフトを一切提示しない
        /// (= 階層10クリア後の最初のドラフトから解放)。序盤の体験を守る初回クリア特例
        /// (clearedFloor &lt;= 1 は常に true) より優先して評価する。
        /// </summary>
        public const int RelicUnlockFloor = 10;

        /// <summary>
        /// 階層クリア時にドラフトを提示すべきか判定する。GameController (本番) と
        /// unity/verify/Sim (バランスシミュレーション) の両方から呼ばれる共通ロジック。
        ///
        /// 判定ルール:
        /// - クリア済み階層がまだ解放階層 (relicUnlockFloor) 未満なら、常に false。
        /// - 所持数が上限に達していたら、常に false (以後そのランではドラフト提示しない)。
        /// - 序盤の体験 (最初のクリアで必ず1個もらえる) を守るため、初回クリア (clearedFloor &lt;= 1) は
        ///   上限未達なら常に true (ただし解放階層のゲートを通過している場合のみ)。
        /// - それ以外は「clearedFloor が draftInterval の倍数」のときのみ true。
        /// </summary>
        /// <param name="clearedFloor">
        /// 直前にクリアした階層。GameSession.NextFloor で加算される前の CurrentFloor
        /// (GameController.ResolveTurnNow / Sim.SimulateRun はどちらもこのタイミングで呼ぶ)。
        /// </param>
        /// <param name="ownedRelicCount">現在のランで既に確定済みのレリック所持数 (スタック分を含む)。</param>
        /// <param name="draftInterval">
        /// 省略時は本番既定値 (DraftInterval)。Sim の実験モード (phase5-sweep) のみ、
        /// 定数を書き換えずに一時的な値を明示的に渡して複数組を比較する。
        /// </param>
        /// <param name="maxRelicsPerRun">省略時は本番既定値 (MaxRelicsPerRun)。用途は draftInterval と同様。</param>
        /// <param name="relicUnlockFloor">
        /// 省略時は本番既定値 (RelicUnlockFloor)。draftInterval/maxRelicsPerRun と同様、テストや
        /// 将来の実験モードのみ明示的な値を渡す。
        /// </param>
        /// <remarks>draftInterval は 1 以上を前提とする (0 を渡すと clearedFloor >= 2 で0除算)。</remarks>
        public static bool ShouldOfferDraft(
            int clearedFloor,
            int ownedRelicCount,
            int draftInterval = DraftInterval,
            int maxRelicsPerRun = MaxRelicsPerRun,
            int relicUnlockFloor = RelicUnlockFloor)
        {
            if (clearedFloor < relicUnlockFloor) return false;
            if (ownedRelicCount >= maxRelicsPerRun) return false;
            if (clearedFloor <= 1) return true;
            return clearedFloor % draftInterval == 0;
        }
    }
}
