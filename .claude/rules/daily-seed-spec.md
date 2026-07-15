# デイリーシード決定論仕様（凍結）⭐️

デイリーチャレンジは「同じ日付なら iOS でも Unity でも同一チャレンジ」が成立要件。以下は両実装で **bit 単位一致を確認済み**（2026-07-13 監査）の正本仕様。**この定数・式を変える変更は原則却下**（変えると当日中のプラットフォーム間で別チャレンジになり、過去のシェア記録とも矛盾する）。

## 正本仕様

- **シード**: UTC の日付文字列 `"YYYY-MM-DD"` の Unicode スカラー値の総和
- **PRNG**: 31bit マスク LCG
  `state = (state * 1664525 + 1013904223) & 0x7fffffff`（乗数・加数は Numerical Recipes 定番。マスク済みなので常に非負）
- **実装**: iOS `Services/DailyChallengeService.swift:132-146`（`SeededRNG`、`&*`/`&+` のオーバーフロー許容演算）↔ Unity `Core/SeededRng.cs`（**`long` 状態で桁溢れを回避**しつつ同一剰余系列を再現。この `long` が仕様の一部）
- **条件生成**: 条件数 `(next % 2) + 1`、種別 `next % 4`（characterLock / noSkillAllowed / forcedAI / startFloor）、重複排除、forcedAI は **Easy/Normal のみ**（Hard 除外）、キャラ配列順 Hero→Thief→Wizard→Elf→Knight — `Core/DailyChallengeGenerator.cs` が Swift と 1:1

## 番犬テスト（消さない・弱めない）

- `SeededRngTests.NextInt_FirstValueForSeed486` — seed 486 → 1822863373 の系列固定（Swift と一致する具体値）
- `SeededRngTests.SeedFromDateString_SumsCharCodes` / `NextInt_IsDeterministicForSameSeed` / `NextInt_AlwaysNonNegativeAndBounded`
- `DailyChallengeGeneratorTests.SameDate_ProducesIdenticalChallenge` / `ForcedAI_IsEasyOrNormalOnly` ほか

## 乱数の使い分けルール

- **ゲームロジック（Core / GameController のロジック経路）**: `Core/IRandomSource.cs` 経由のみ。デイリー中はシード付き実装が注入される
- **`UnityEngine.Random` はゲームロジック禁止**。許可は演出専用ディレクトリのみ: `Runtime/Stage/`（シェイク等）と `Runtime/UI/Fx/`(パーティクル等)
- 前科: `GameController.cs:585` は元 UnityEngine.Random 直呼びで、シード再現性を壊すため IRandomSource 経由に是正済み（`RelicDraftService.cs:29-31` に禁止理由コメント）
- hook `post_write_cs_check.py` が **Core での違反を exit 2 でブロック**、Runtime 非演出層は警告する
