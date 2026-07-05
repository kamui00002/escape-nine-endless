# レリック整合性監査 (RELIC_COHERENCE_AUDIT)

調査日: 2026-07-06
対象: `unity/EscapeNine/Assets/Scripts/Core/{RelicCatalog,RelicDefinition,RelicEffects,RelicConfig,RelicDraftService,Floor,GameSession}.cs`, `Runtime/GameController.cs`
方針: 実コードの行番号を根拠とする。特殊ルールの発動階層は `Floor.cs` / `GameConfig.cs` / `GameSession.cs` を正とし、`docs/game-spec.md` は二次情報として突合のみ行う。

---

## 0. 先に結論: 階層しきい値そのものにドキュメント齟齬はない

`docs/game-spec.md` の「階層21-40: 霧 / 41-60: マス消失 / 61-100: 複合」は、実コードと完全一致していた。

- `GameConfig.cs:69-71`: `FogStartFloor=21` / `DisappearStartFloor=41` / `CombinedRulesStartFloor=61`
- `Floor.cs:35-41` (`GetSpecialRule`): `floor<21→None`, `floor<41→Fog`, `floor<61→Disappear`, `else→FogDisappear`

→ **ズレなし。CONFIRMED**。矛盾の原因はしきい値の誤りではなく、「ドラフト側がこのしきい値を一切参照せずに候補を出している」ことにある（詳細は §3, §4）。

---

## 1. 全レリック一覧表 (18種)

| ID | 名称 | レアリティ | タグ | 効果の要旨 | 依存する仕組み | 有効になる最小階層/条件 | 現状いつから出るか |
|---|---|---|---|---|---|---|---|
| `shadow_footwork` #1 | 影の軽業 | Common | ThiefRescue | 盗賊斜め移動時25%でスキル残数を消費しない | 盗賊のDiagonalスキル (`SkillType.Diagonal`) | 盗賊使用時のみ意味を持つ | 階層1から、**全キャラ対象**（非盗賊は重み×0.15で除外されない） |
| `afterimage_veil` #2 | 残像のヴェール | Rare | ThiefRescue\|HardAICounter | 斜め移動使用ターンの敵移動をEasy相当に強制 | 盗賊のDiagonalスキル | 盗賊使用時のみ意味を持つ | 階層1から、**全キャラ対象**（魔法使いのみ重み0で除外） |
| `shadow_clone_art` #3 | 影分身の型 | Epic | ThiefRescue | 盗賊のスキル最大回数+3（**コメントで「盗賊専用」と明記**） | 盗賊 (`CurrentCharacter.Type==Thief`のみ加算、`GameSession.cs:126`) | 盗賊使用時のみ意味を持つ | 階層1から、**全キャラ対象**（非盗賊は重み×0.15のみ） |
| `veteran_stance` #4 | 老練の構え | Uncommon | General\|Safety | 必要ターン数-1 (最低3) | なし（常時有効） | 階層1から有効 | 階層1から（問題なし） |
| `phoenix_ember` #5 | 不死鳥の残り火 | Epic | Safety | 1ラン1回、衝突死を無効化 | なし（常時有効） | 階層1から有効 | 階層1から（問題なし） |
| `combo_ward` #6 | コンボの守り | Rare | Score | 1ラン1回、Missでもコンボ継続 | なし（常時有効） | 階層1から有効 | 階層1から（問題なし） |
| `grounding_charm` #7 | 地固めの護符 | Uncommon | LateGame | マス消失数-1 | マス消失ルール (`SpecialRule.Disappear`/`FogDisappear`) | **階層41+** | 階層1から出現可能（重みは41+で×2.0、61+で×2.5だが下限カットなし） |
| `lantern_ring` #8 | 灯火の指輪 | Common (stack3) | LateGame | 霧視界半径+1 | 霧ルール (`SpecialRule.Fog`/`FogDisappear`) | **階層21-40 または 61+** (41-60は霧が無いため無効) | 階層1から出現可能。**さらに41-60 (マス消失のみ、霧なし)の期間に重み×2.0で逆に増える** |
| `bewildering_dust` #9 | 幻惑の粉 | Epic | HardAICounter | 実効AIがHardの回のみNormal相当に格下げ | 実効AI=Hard (`Floor.GetEffectiveAILevel`、自然Hardは**階層36+**、選択Hard込みでも**階層16+**が下限) | 実効Hardが発生し得る階層（下限16、選択次第） | 階層1から出現可能、選択AI=Hardなら階層に関係なく重み×2.5 |
| `safe_start` #10 | 護りの起点 | Common | General\|Safety | 初期配置距離2以上を保証 | なし（常時有効） | 階層1から有効 | 階層1から（問題なし） |
| `shadow_passage` #11 | 影の抜け道 | Rare | LateGame\|Safety | 1階層につき1回、消失マス進入死を無効化 | マス消失ルール | **階層41+** | 階層1から出現可能（重みは41+で×2.0、61+で×2.5） |
| `improvised_shield` #12 | 二段構えの盾 | Rare | Safety | 盾スキルが無いキャラでも1ラン1回衝突無効化 | なし（常時有効） | 階層1から有効 | 階層1から（問題なし） |
| `reserve_breath` #13 | 予備の呼吸 | Uncommon | General | 全キャラのスキル最大回数+1 | なし（常時有効） | 階層1から有効 | 階層1から（問題なし、魔法使いのみ重み×0.5で減衰） |
| `chain_proof` #14 | 連鎖の証 | Common | Score | コンボ閾値-1 (3→2, 5→4) | なし（常時有効） | 階層1から有効 | 階層1から（問題なし） |
| `accel_proof` #15 | 加速の証 | Uncommon | Score | BPM+8%、コンボ倍率+0.5 | なし（常時有効、リスク型） | 階層1から有効 | 階層1から（問題なし） |
| `grace_of_time` #16 | 刻の猶予 | Rare | **LateGame** | 1ターンの締切拍数+1 | **なし（常時有効、階層非依存）** | 階層1から有効 | 階層1から出現可能だが、**タグがLateGameのため41+/61+で不要に重みが上がる** |
| `heartbound_pact` #17 | 心話の絆 | Rare | HardAICounter\|General | 拘束スキルが無いキャラでも1ラン2回まで拘束可 | なし（常時有効…ただし下記③参照） | 階層1から有効（**エルフだけは例外的に常に無価値**） | 階層1から、エルフにも出現し得る |
| `collectors_eye` #18 | 蒐集家の目 | Legendary | General | 以後3階層、ドラフト候補数3→4 | ドラフトが今後も提示されること (`RelicConfig.MaxRelicsPerRun=4`) | ラン内の残りドラフト回数次第 | 階層1から有効（ただし上限直前で引くと効果が丸ごと無駄になり得る、§5参照） |

---

## 2. 矛盾レリックの表

| # | レリック | 矛盾の種類 | なぜ矛盾か | 根拠 (行番号) | 判定 |
|---|---|---|---|---|---|
| A | #7 地固めの護符 | ① 仕組み未発動 | マス消失ルールが無い階層1-40でも通常の重みでドラフト候補になる（ゼロ除外されない）。オーナー指摘の「マス消失-1」の実例そのもの。 | `RelicDraftService.cs:125-129`（LateGameタグは`floor>=41`で×2.0、`floor>=61`で×2.5にするだけで、それ未満を0にしない）／有効階層は`GameConfig.cs:70`, `Floor.cs:38-39` | CONFIRMED |
| B | #11 影の抜け道 | ① 仕組み未発動 | 同上。消失マスが存在しない階層1-40で「消失マス進入死の無効化」が候補に出る。 | `RelicDraftService.cs:125-129`／`RelicEffects.cs:47-49`（DisappearForgivenessPerFloorは`SpecialRule.Disappear`/`FogDisappear`時のみ意味を持つ、`GameSession.cs:325-337`で消費判定） | CONFIRMED |
| C | #8 灯火の指輪 | ① 仕組み未発動（さらに逆方向の悪化あり） | 霧が無い階層1-20で通常重みのまま出現する。**加えて階層41-60（マス消失のみ・霧なし）では霧が存在しないにもかかわらずLateGameタグの`floor>=41`条件で重みが×2.0に増加する**（霧無効期間に重みが上がるという二重の矛盾）。 | `RelicDraftService.cs:125-129`／有効条件は`GameSession.cs:656-665`（`IsCellVisible`が`SpecialRule.Fog`/`FogDisappear`時のみ半径を参照） | CONFIRMED |
| D | #16 刻の猶予 | ④ 効果とタグの不一致 | 効果（ターン締切+1拍）はマス消失/霧と無関係で階層非依存の汎用強化なのに、タグが`LateGame`になっている。もし「LateGame＝41+/61+でのみ出す」という文脈連動フィルタをタグ単位でそのまま実装すると、本来階層1から使える#16まで巻き込んで序盤に出なくなる誤ロックが起きる。 | `RelicCatalog.cs:206-215`（コメントにも「Runtime専用フック、GameSessionは無関係」と明記）／`RelicEffects.cs:88-91` | CONFIRMED（タグ設計のミス） |
| E | #1 影の軽業 / #2 残像のヴェール / #3 影分身の型 | ③ キャラ相性で常に無価値 | 3つとも効果が`SkillType.Diagonal`（盗賊専用スキル）にのみ紐づいており、非盗賊キャラには**常に・完全に**無効。にもかかわらず重みは×0.15に減衰されるだけで0にはならず、非盗賊にも一定確率で提示される。同ファイル内で魔法使い×HardAICounterは明示的に重み0で除外している（`RelicDraftService.cs:111-118`）のに、盗賊専用群には同水準の除外が適用されておらず、**同一ファイル内でのルール適用の一貫性がない**。 | 効果の盗賊限定性: `GameSession.cs:311`(`usedDiagonalSkillThisTurn`は`Skill.Type==SkillType.Diagonal`が前提)、`GameSession.cs:126`(`ThiefSkillMaxUsageBonus`は`CurrentCharacter.Type==Thief`のみ加算)／重み計算: `RelicDraftService.cs:106-109` | CONFIRMED |
| F | #17 心話の絆 × エルフ | ③ キャラ相性で常に無価値 | エルフは既に拘束スキル(`SkillType.Bind`)持ちだが、`GameSession.BindEnemy()`は`Skill.Type==SkillType.Bind`分岐に入ると**スキル残数が0でも即return**し、`Relics.PseudoBindCharges`の分岐に絶対に落ちない。よってエルフが#17を装備しても、そのラン中一度も`PseudoBindCharges`が消費されることはなく、実質100%無価値。ドラフト側にはエルフを除外する仕組みが無い。 | `GameSession.cs:585-602`（特に589-595: `if (Skill.Type==Bind){ if (RemainingSkillUses<=0) return; ...; return; }` で常にここで終わる） | CONFIRMED |
| G | #9 幻惑の粉 / #2 残像のヴェール (HardAICounterタグ全般) | ① 仕組み未発動（階層と選択AIの組合せ） | 重み計算は`selectedAI==Hard`のみで判定し階層を見ない(`RelicDraftService.cs:119-123`)。しかし`Floor.GetEffectiveAILevel`(`Floor.cs:55-89`)により、自然AIがEasyの階層(1-15)ではプレイヤーが「Hard」を選んでも実効Hardには**絶対にならない**(Easy選択Hard→実効Normalが上限、`Floor.cs:74-83`)。つまり階層1-15で選択AI=Hardの場合、実効Hardが発生しないのに関連レリックの重みだけ×2.5に増える。 | `RelicDraftService.cs:111-123`／`Floor.cs:44-89`／`GameConfig.cs:74`(`AINaturalNormalFloor=16`) | CONFIRMED (要検証: 意図的な簡略化か未検討か不明なので設計判断としては要確認) |
| H | ドラフト提示条件全般 | ⑤ その他（実装箇所の欠落） | `RelicConfig.ShouldOfferDraft`(`RelicConfig.cs:75-84`)は「クリア階層が閾値の倍数か」と「所持数が上限未満か」だけを見ており、**「その階層で有効な仕組みが存在するか」は一切判定していない**。文脈連動フィルタを実装するなら、この関数ではなく候補生成側（§4参照）に置く必要がある。 | `RelicConfig.cs:56-84` | CONFIRMED |
| I | #18 蒐集家の目 | ⑤ その他（要確認、序盤無意味とは別種） | `MaxRelicsPerRun=4`(`RelicConfig.cs:52`)により生涯ドラフト回数が実質4回に制限されている。#18の効果は「以後3階層分、次のドラフトの候補数を+1する」ものだが、**もし4回目（最後）のドラフトで#18を引いた場合、その後ドラフト自体が二度と提示されないため効果が丸ごと空振りになる**。CONFIRMEDな「無意味」ではなく確率的偶発だが、Legendary最高レア枠が構造的に無駄撃ちし得る点は要確認。 | `RelicConfig.cs:37-52`, `81`／`RelicCatalog.cs:217-226` | 要確認 |
| J | ドラフト生成に使う`floor`引数の基準 | ⑤ その他（要確認、境界の1ずれ） | `GameController.OfferRelicDraft`は`floor: Session.CurrentFloor`(`GameController.cs:464`)を渡すが、これは「直前にクリアした階層」であり「次に入る階層」ではない(`AdvanceToNextFloor`が`Session.NextFloor()`で加算するのはこの後)。さらに`RouteChoice.Abyss`を選ぶと次階層の特殊ルールが1段階前倒しされる(`RouteChoice.cs:67-78`)ため、生の階層番号比較だけでは次階層の実際の特殊ルールと食い違う場合がある。文脈連動フィルタは「次に入る階層で実際に有効になる`SpecialRule`」を基準にすべき。 | `GameController.cs:452-464`／`RouteChoice.cs:43-78` | 要確認 |

---

## 3. 文脈連動ドラフトの提案ルール表

各レリックの「候補化してよい最小階層/条件」。fog=21+、disappear=41+、combined=61+ は `GameConfig.cs:69-71` / `Floor.GetSpecialRule` (`Floor.cs:35-41`) を正とする。

| # | レリック | 現行タグ | 提案する出現条件 | 備考 |
|---|---|---|---|---|
| #1 影の軽業 | ThiefRescue | `character == Thief` のときのみ候補化（他キャラは重み0で完全除外） | 現行の0.15倍は撤廃 |
| #2 残像のヴェール | ThiefRescue\|HardAICounter | `character == Thief` かつ 魔法使い除外は不要（Thief専用化で自動的に非該当） | 同上。HardAICounterの2.5倍ボーナスはThief確定後にのみ適用 |
| #3 影分身の型 | ThiefRescue | `character == Thief` のときのみ（コメント「盗賊専用」と整合） | 同上 |
| #4 老練の構え | General/Safety | 制限なし（階層1から） | 現状通り |
| #5 不死鳥の残り火 | Safety | 制限なし | 現状通り |
| #6 コンボの守り | Score | 制限なし | 現状通り |
| #7 地固めの護符 | LateGame→提案`RequiresDisappear` | `Floor.GetSpecialRule(nextFloor) ∈ {Disappear, FogDisappear}` （実質 `nextFloor >= 41`） | nextFloorは「次に入る階層」。Abyss前倒しも`GetSpecialRule`経由なら自動追随 |
| #8 灯火の指輪 | LateGame→提案`RequiresFog` | `Floor.GetSpecialRule(nextFloor) ∈ {Fog, FogDisappear}` （実質 `21<=nextFloor<41` または `nextFloor>=61`） | 41-60は明示的に除外すること（現行はここで重みが上がるバグがある） |
| #9 幻惑の粉 | HardAICounter | `Floor.GetEffectiveAILevel(nextFloor, selectedAI) == Hard` になり得るか（実質 `selectedAI==Hard`かつ`nextFloor>=16`、または`selectedAI!=Easy`かつ`nextFloor>=36`） | 魔法使い除外は既存ルール維持 |
| #10 護りの起点 | General/Safety | 制限なし | 現状通り |
| #11 影の抜け道 | LateGame→提案`RequiresDisappear` | #7と同条件 (`nextFloor>=41`) | |
| #12 二段構えの盾 | Safety | 制限なし | 現状通り |
| #13 予備の呼吸 | General | 制限なし | 現状通り |
| #14 連鎖の証 | Score | 制限なし | 現状通り |
| #15 加速の証 | Score | 制限なし | 現状通り |
| #16 刻の猶予 | **LateGame→提案`General`に変更** | 制限なし（階層1から） | タグ誤り(§2-D)の是正が前提。タグを変えずにフィルタだけ足すと誤ロックする |
| #17 心話の絆 | HardAICounter/General | `character != Elf` のときのみ候補化 | エルフ除外は新規追加（現状漏れ、§2-F） |
| #18 蒐集家の目 | General | 制限なし。ただし`_draftAcquiredCount >= MaxRelicsPerRun - 1`の状態では効果が無駄になるため、候補からの除外 or 警告表示を検討（要確認、§2-I） | 任意対応 |

**現行の`LateGame`タグは意味が3つの用途（霧依存/消失依存/階層非依存の汎用強化）に混在しているため、そのままでは文脈連動フィルタの基盤にできない。** タグの分割（`RequiresFog` / `RequiresDisappear` を新設し、`LateGame`は#16のような「階層非依存だが後半に強い」ものだけに残すか廃止する）が前提作業になる。

---

## 4. 実装の当て所メモ

### フィルタを差し込むべき箇所

1. **候補生成のハード除外**: `RelicDraftService.ComputeWeight`(`RelicDraftService.cs:101-144`)。
   既に「魔法使い×HardAICounter→即0.0 return」(`:111-118`)という前例パターンがあるので、同じ書き方で
   - `RequiresDisappear`タグ かつ `Floor.GetSpecialRule(floor)` が Disappear/FogDisappear でない → `return 0.0`
   - `RequiresFog`タグ かつ 同様に Fog/FogDisappear でない → `return 0.0`
   - `ThiefRescue`タグ かつ `character != Thief` → `return 0.0`（現行の0.15倍を廃止）
   - `HardAICounter`タグ(#17のような汎用系を除く) かつ `character==Elf` かつ 対象が#17 → `return 0.0`（#17専用ルールとして個別追加。タグだけでは表現しづらいので`def.Id==RelicCatalog.HeartboundPactId`の個別分岐が現実的）
   という形で追記するのが最小差分。

2. **`floor`引数の受け渡し元**: `RelicDraftService.DraftCandidates`は既に`floor`パラメータを受け取っている(`RelicDraftService.cs:46-51`)ので、Core側のシグネチャ変更は不要。呼び出し元の`GameController.OfferRelicDraft`(`GameController.cs:452-464`)が`Session.CurrentFloor`（クリア済み階層）を渡している点だけ、「次に入る階層」（`Session.CurrentFloor + 1`、あるいは`RouteChoice`前倒しを反映した実効値）に是正する必要がある（§2-J）。この呼び出し元修正はRuntime層（`GameController.cs`）の1箇所で完結する。

3. **Core純粋性 (noEngineReferences) への影響**: 上記1・2はどちらも`Floor.GetSpecialRule` / `Floor.GetEffectiveAILevel`という既存のCore静的メソッドを呼ぶだけで、UnityEngine型・MonoBehaviour・時間軸には一切依存しない。したがって**フィルタ本体はCore側 (`RelicDraftService.cs`) に置くのが適切**であり、`noEngineReferences`原則（`RelicCatalog.cs`冒頭コメント等が前提とする「Core純粋性」）を壊さない。GameController側の修正は「どのfloor値を渡すか」という配線の是正のみで、ロジック自体はCoreに閉じ込められる。

4. **タグ分割の実装コスト**: `RelicTag`は`[Flags]`enum (`RelicDefinition.cs:20-30`)なので`RequiresFog = 1<<6`, `RequiresDisappear = 1<<7`を追加するだけで済む。該当レリック(#7,#8,#11)の`RelicCatalog.cs`定義行のタグ引数を書き換え、#16(`RelicCatalog.cs:213`の`RelicTag.LateGame`)は`RelicTag.General`（または新設の`RelicTag.None`扱い＋Tier2コメントのみ残す）に変更する。

### 「レリック自体を階層10クリア後に解放」という別方針について

これは今回読んだファイル群（Catalog/Definition/Effects/Config/DraftService/GameController）には実装の痕跡が無い。`RelicConfig.ShouldOfferDraft`(`RelicConfig.cs:75-84`)には階層10のロジックは存在せず、`GameController.StartNewRun`のスターターパーク解放も`_player.IsRelicUnlocked`という別の永続状態（`PlayerState`側）を参照している。この方針を実装する場合は、`OfferRelicDraft`呼び出しの前段（`GameController.cs:452`付近）に`Session.CurrentFloor(またはnextFloor) >= 10`のゲートを1行追加するのが最小差分になる。ただしこれは今回のスコープ（矛盾の洗い出し）を超えるため、着手は別タスクとして切り出すことを推奨する。

---

## 5. 参照した実コード行の索引

- 特殊ルール発動階層の正: `Floor.cs:35-41`, `GameConfig.cs:69-71`
- AI自然難易度・実効AI: `Floor.cs:44-89`, `GameConfig.cs:74-75`
- レリック定義18種: `RelicCatalog.cs:37-226`
- タグ定義: `RelicDefinition.cs:20-30`
- 効果フィールドと消費先: `RelicEffects.cs` 全体、消費箇所は`GameSession.cs`各所（行番号は§2の表に記載）
- ドラフト提示可否 (階層10未実装の確認含む): `RelicConfig.cs:56-84`
- 重み計算 (矛盾の主因): `RelicDraftService.cs:101-144`
- ドラフト呼び出し元・floor引数の受け渡し: `GameController.cs:445-482`
- ルート前倒しによる特殊ルールのズレ要因: `RouteChoice.cs:63-78`
