# DEVELOPMENT.md - Escape Nine: Endless

## 📘 開発者向けドキュメント

このドキュメントはCursorでの開発を効率化するための技術仕様書です。

---

## 目次
1. [プロジェクト構成](#プロジェクト構成)
2. [技術スタック](#技術スタック)
3. [セットアップ手順](#セットアップ手順)
4. [ディレクトリ構造](#ディレクトリ構造)
5. [コーディング規約](#コーディング規約)
6. [音楽同期システム](#音楽同期システム)
7. [コンポーネント設計](#コンポーネント設計)
8. [状態管理](#状態管理)
9. [データモデル](#データモデル)
10. [実装優先順位](#実装優先順位)

---

## プロジェクト構成

### 基本情報
- **プロジェクト名**: escape-nine-endless
- **パッケージ名**: com.souatou.escapenine
- **最小iOS**: 14.0
- **開発言語**: TypeScript
- **フレームワーク**: React Native + Expo

### ゲームの核心
- **音楽同期**: ビートに合わせた移動判定が最重要
- **エンドレス**: 100階層までのスコアアタック
- **シンプル**: スタミナ・コイン・複雑な要素なし

---

## 技術スタック

### フロントエンド
- **React Native**: 0.73.x
- **Expo**: SDK 50.x
- **TypeScript**: 5.x
- **React Navigation**: 6.x
- **React Native Paper**: 5.x
- **React Native Reanimated**: 3.x

### 音楽・音声
- **Expo AV**: 音楽再生、BPM同期
- **React Native Sound**: 効果音(軽量)

### 状態管理
- **Zustand**: 4.x

### バックエンド
- **Firebase**:
  - Authentication
  - Firestore (ランキング)
  - Analytics
- **Google AdMob**: 広告
- **Expo In-App Purchases**: 課金
- **Game Center**: ランキング

---

## セットアップ手順

```bash
# プロジェクト作成
npx create-expo-app escape-nine --template blank-typescript
cd escape-nine

# 依存パッケージ
npm install @react-navigation/native @react-navigation/stack
npm install react-native-paper
npm install zustand
npm install firebase
npm install @react-native-async-storage/async-storage
npm install expo-av
npm install react-native-reanimated

# Expo関連
npx expo install expo-dev-client
npx expo install react-native-safe-area-context react-native-screens
npx expo install expo-game-center

# 開発サーバー起動
npm start
```

---

## ディレクトリ構造

```
escape-nine/
├── src/
│   ├── screens/
│   │   ├── HomeScreen.tsx
│   │   ├── GameScreen.tsx          # メイン画面
│   │   ├── ResultScreen.tsx
│   │   ├── RankingScreen.tsx
│   │   ├── ShopScreen.tsx
│   │   ├── PracticeScreen.tsx
│   │   └── SettingsScreen.tsx
│   │
│   ├── components/
│   │   ├── Grid/
│   │   │   ├── GridBoard.tsx       # 9マス盤面
│   │   │   ├── GridCell.tsx
│   │   │   └── BeatIndicator.tsx  # ビート表示
│   │   ├── Character/
│   │   │   ├── CharacterSprite.tsx
│   │   │   └── CharacterSelect.tsx
│   │   ├── UI/
│   │   │   ├── Button.tsx
│   │   │   ├── Header.tsx
│   │   │   └── Modal.tsx
│   │   └── Ad/
│   │       ├── BannerAd.tsx
│   │       └── InterstitialAd.tsx
│   │
│   ├── stores/
│   │   ├── gameStore.ts            # ゲーム状態
│   │   ├── playerStore.ts          # プレイヤーデータ
│   │   └── rankingStore.ts         # ランキング
│   │
│   ├── hooks/
│   │   ├── useBeatSync.ts          # 音楽同期(最重要)
│   │   ├── useGame.ts
│   │   └── useRanking.ts
│   │
│   ├── services/
│   │   ├── GameEngine.ts           # ゲームロジック
│   │   ├── AIEngine.ts             # AI制御
│   │   ├── BeatEngine.ts           # 音楽同期エンジン(最重要)
│   │   ├── StageManager.ts         # 階層管理
│   │   └── RankingService.ts       # ランキング
│   │
│   ├── utils/
│   │   ├── constants.ts
│   │   ├── types.ts
│   │   └── helpers.ts
│   │
│   ├── config/
│   │   ├── firebase.ts
│   │   ├── admob.ts
│   │   └── gameConfig.ts
│   │
│   ├── assets/
│   │   ├── images/characters/
│   │   ├── sounds/bgm/             # BPM別BGM
│   │   ├── sounds/sfx/
│   │   └── fonts/
│   │
│   └── navigation/
│       └── AppNavigator.tsx
│
├── App.tsx
├── app.json
└── package.json
```

---

## コーディング規約

### TypeScript規約

```typescript
// ✅ Good: 明示的な型定義
interface Character {
  id: string;
  name: string;
  type: 'hero' | 'thief' | 'wizard' | 'elf';
  skill: Skill;
  spriteUrl: string;
}

// ✅ Good: Enum使用
enum AILevel {
  EASY = 'easy',
  NORMAL = 'normal',
  HARD = 'hard',
}

// ✅ Good: Optional chaining
const skillCount = character?.skill?.usageCount;
```

### 命名規則

```typescript
// コンポーネント: PascalCase
GridBoard.tsx
BeatIndicator.tsx

// 関数・変数: camelCase
const checkBeatTiming = () => {};
const currentBpm = 120;

// 定数: UPPER_SNAKE_CASE
const MAX_FLOORS = 100;
const GRID_SIZE = 9;

// 型: PascalCase
interface GameState {}
type SkillType = 'dash' | 'diagonal' | 'invisible' | 'bind';
```

---

## 音楽同期システム

### 最重要: BeatEngine.ts

```typescript
import { Audio } from 'expo-av';

export class BeatEngine {
  private sound: Audio.Sound | null = null;
  private bpm: number = 60;
  private beatInterval: number = 1000; // ミリ秒
  private lastBeatTime: number = 0;
  private beatCallbacks: Array<() => void> = [];

  /**
   * BGMを読み込み、BPMを設定
   */
  async loadMusic(bpm: number) {
    this.bpm = bpm;
    this.beatInterval = (60 / bpm) * 1000; // BPMからビート間隔を計算

    // BGMファイル読み込み
    const { sound } = await Audio.Sound.createAsync(
      require(`@/assets/sounds/bgm/bgm_${bpm}.mp3`)
    );
    this.sound = sound;
  }

  /**
   * BGM再生開始
   */
  async play() {
    if (this.sound) {
      await this.sound.playAsync();
      this.startBeatDetection();
    }
  }

  /**
   * BGM停止
   */
  async stop() {
    if (this.sound) {
      await this.sound.stopAsync();
    }
  }

  /**
   * ビート検出を開始
   */
  private startBeatDetection() {
    this.lastBeatTime = Date.now();

    setInterval(() => {
      const now = Date.now();
      const elapsed = now - this.lastBeatTime;

      if (elapsed >= this.beatInterval) {
        this.onBeat();
        this.lastBeatTime = now;
      }
    }, 10); // 10msごとにチェック
  }

  /**
   * ビートコールバックを登録
   */
  onBeatCallback(callback: () => void) {
    this.beatCallbacks.push(callback);
  }

  /**
   * ビートが来た時の処理
   */
  private onBeat() {
    this.beatCallbacks.forEach(cb => cb());
  }

  /**
   * タイミング判定(プレイヤーの入力が正しいか)
   */
  checkTiming(inputTime: number): boolean {
    const timeDiff = Math.abs(inputTime - this.lastBeatTime);
    const tolerance = this.beatInterval * 0.15; // ±15%の誤差を許容

    return timeDiff <= tolerance;
  }

  /**
   * BPMを変更(階層が上がった時)
   */
  changeBPM(newBpm: number) {
    this.bpm = newBpm;
    this.beatInterval = (60 / newBpm) * 1000;
    // BGMも切り替える必要あり
  }
}
```

### useBeatSync.ts (カスタムフック)

```typescript
import { useEffect, useState } from 'react';
import { BeatEngine } from '@/services/BeatEngine';

export const useBeatSync = (bpm: number) => {
  const [beatEngine] = useState(() => new BeatEngine());
  const [currentBeat, setCurrentBeat] = useState(0);

  useEffect(() => {
    // BPMでBGM読み込み
    beatEngine.loadMusic(bpm);

    // ビートコールバック登録
    beatEngine.onBeatCallback(() => {
      setCurrentBeat(prev => prev + 1);
    });

    // BGM再生
    beatEngine.play();

    return () => {
      beatEngine.stop();
    };
  }, [bpm]);

  /**
   * プレイヤーが移動した時のタイミングチェック
   */
  const checkMoveTimin = () => {
    const inputTime = Date.now();
    const isCorrectTiming = beatEngine.checkTiming(inputTime);

    if (!isCorrectTiming) {
      // タイミングが外れた = ゲームオーバー
      return false;
    }

    return true;
  };

  return {
    currentBeat,
    checkMoveTiming,
  };
};
```

---

## コンポーネント設計

### GameScreen.tsx (最重要)

```typescript
import React, { useState, useEffect } from 'react';
import { View, StyleSheet, TouchableOpacity } from 'react-native';
import { GridBoard } from '@/components/Grid/GridBoard';
import { BeatIndicator } from '@/components/Grid/BeatIndicator';
import { useBeatSync } from '@/hooks/useBeatSync';
import { useGameStore } from '@/stores/gameStore';

export const GameScreen: React.FC = () => {
  const {
    currentFloor,
    playerPosition,
    enemyPosition,
    gameStatus,
    moveCharacter,
    endGame,
  } = useGameStore();

  // 階層に応じたBPM計算
  const bpm = calculateBPM(currentFloor);
  const { currentBeat, checkMoveTiming } = useBeatSync(bpm);

  /**
   * マスタップ時の処理
   */
  const handleCellPress = (position: number) => {
    // タイミングチェック
    const isCorrectTiming = checkMoveTiming();
    if (!isCorrectTiming) {
      endGame('lose'); // タイミングミスで即死
      return;
    }

    // 移動実行
    moveCharacter(position);
  };

  return (
    <View style={styles.container}>
      {/* 階層表示 */}
      <Text style={styles.floor}>Floor: {currentFloor}</Text>

      {/* ビートインジケーター */}
      <BeatIndicator currentBeat={currentBeat} />

      {/* 9マスグリッド */}
      <GridBoard
        playerPosition={playerPosition}
        enemyPosition={enemyPosition}
        onCellPress={handleCellPress}
      />
    </View>
  );
};

/**
 * 階層からBPMを計算
 */
const calculateBPM = (floor: number): number => {
  if (floor <= 10) return 60;
  if (floor <= 20) return 80;
  if (floor <= 30) return 100;
  if (floor <= 40) return 120;
  if (floor <= 50) return 140;
  if (floor <= 60) return 160;
  if (floor <= 70) return 180;
  if (floor <= 80) return 200;
  if (floor <= 90) return 220;
  return 240;
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#1a1a2e',
    alignItems: 'center',
    justifyContent: 'center',
  },
  floor: {
    fontSize: 32,
    color: '#eaeaea',
    marginBottom: 20,
  },
});
```

### BeatIndicator.tsx (ビート表示)

```typescript
import React, { useEffect } from 'react';
import { View, StyleSheet, Animated } from 'react-native';

interface BeatIndicatorProps {
  currentBeat: number;
}

export const BeatIndicator: React.FC<BeatIndicatorProps> = ({ currentBeat }) => {
  const scaleAnim = new Animated.Value(1);

  useEffect(() => {
    // ビートごとにアニメーション
    Animated.sequence([
      Animated.timing(scaleAnim, {
        toValue: 1.3,
        duration: 100,
        useNativeDriver: true,
      }),
      Animated.timing(scaleAnim, {
        toValue: 1,
        duration: 100,
        useNativeDriver: true,
      }),
    ]).start();
  }, [currentBeat]);

  return (
    <View style={styles.container}>
      <Animated.View
        style={[
          styles.indicator,
          { transform: [{ scale: scaleAnim }] },
        ]}
      />
      <Text style={styles.text}>♪ Beat: {currentBeat}</Text>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    marginBottom: 20,
  },
  indicator: {
    width: 60,
    height: 60,
    borderRadius: 30,
    backgroundColor: '#667eea',
  },
  text: {
    color: '#eaeaea',
    fontSize: 18,
    marginTop: 10,
  },
});
```

---

## 状態管理

### gameStore.ts

```typescript
import { create } from 'zustand';

interface GameState {
  // ゲーム状態
  currentFloor: number;
  turnCount: number;
  maxTurns: number;
  playerPosition: number;
  enemyPosition: number;
  gameStatus: 'playing' | 'win' | 'lose' | 'paused';
  aiLevel: 'easy' | 'normal' | 'hard';
  specialRule: 'none' | 'fog' | 'disappear' | 'fog_disappear';
  
  // スキル
  selectedCharacter: CharacterType;
  skillUsageCount: number;
  maxSkillUsage: number;
  
  // アクション
  startGame: (aiLevel: AILevel) => void;
  moveCharacter: (newPosition: number) => void;
  moveEnemy: () => void;
  useSkill: () => void;
  nextFloor: () => void;
  endGame: (result: 'win' | 'lose') => void;
}

export const useGameStore = create<GameState>((set, get) => ({
  // 初期値
  currentFloor: 1,
  turnCount: 0,
  maxTurns: 10,
  playerPosition: 1,
  enemyPosition: 9,
  gameStatus: 'playing',
  aiLevel: 'normal',
  specialRule: 'none',
  selectedCharacter: 'hero',
  skillUsageCount: 0,
  maxSkillUsage: 5,

  startGame: (aiLevel) => {
    set({
      currentFloor: 1,
      turnCount: 0,
      gameStatus: 'playing',
      aiLevel,
      playerPosition: Math.floor(Math.random() * 9) + 1,
      enemyPosition: Math.floor(Math.random() * 9) + 1,
      skillUsageCount: 0,
    });
  },

  moveCharacter: (newPosition) => {
    const { playerPosition, enemyPosition } = get();

    // 移動可能かチェック
    if (!GameEngine.isValidMove(playerPosition, newPosition)) {
      return;
    }

    set({ playerPosition: newPosition });

    // 同じマスに入ったらゲームオーバー
    if (newPosition === enemyPosition) {
      get().endGame('lose');
      return;
    }

    // ターン進行
    set(state => ({ turnCount: state.turnCount + 1 }));

    // 敵の移動
    get().moveEnemy();

    // 10ターン逃げ切ったら次の階層へ
    if (get().turnCount >= get().maxTurns) {
      get().nextFloor();
    }
  },

  moveEnemy: () => {
    const { enemyPosition, playerPosition, aiLevel } = get();
    const nextPosition = AIEngine.calculateNextMove(
      enemyPosition,
      playerPosition,
      aiLevel
    );

    set({ enemyPosition: nextPosition });

    if (nextPosition === get().playerPosition) {
      get().endGame('lose');
    }
  },

  useSkill: () => {
    const { selectedCharacter, skillUsageCount, maxSkillUsage } = get();

    if (skillUsageCount >= maxSkillUsage) {
      return; // スキル回数上限
    }

    // スキル効果を適用
    SkillManager.applySkill(selectedCharacter);

    set(state => ({ skillUsageCount: state.skillUsageCount + 1 }));
  },

  nextFloor: () => {
    const newFloor = get().currentFloor + 1;

    if (newFloor > 100) {
      get().endGame('win'); // 100階層クリア
      return;
    }

    // 階層が変わると特殊ルールも変わる
    const newRule = StageManager.getSpecialRule(newFloor);

    set({
      currentFloor: newFloor,
      turnCount: 0,
      specialRule: newRule,
      playerPosition: Math.floor(Math.random() * 9) + 1,
      enemyPosition: Math.floor(Math.random() * 9) + 1,
      skillUsageCount: 0, // スキルリセット
    });
  },

  endGame: (result) => {
    set({ gameStatus: result });

    if (result === 'win' || result === 'lose') {
      // ランキングに記録
      RankingService.submitScore(get().currentFloor);
    }
  },
}));
```

### playerStore.ts

```typescript
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';

interface PlayerState {
  highestFloor: number;
  unlockedCharacters: CharacterType[];
  selectedCharacter: CharacterType;
  adRemoved: boolean;
  
  unlockCharacter: (character: CharacterType) => void;
  selectCharacter: (character: CharacterType) => void;
  updateHighestFloor: (floor: number) => void;
  removeAds: () => void;
}

export const usePlayerStore = create<PlayerState>()(
  persist(
    (set, get) => ({
      highestFloor: 0,
      unlockedCharacters: ['hero'],
      selectedCharacter: 'hero',
      adRemoved: false,

      unlockCharacter: (character) => {
        set(state => ({
          unlockedCharacters: [...state.unlockedCharacters, character],
        }));
      },

      selectCharacter: (character) => {
        set({ selectedCharacter: character });
      },

      updateHighestFloor: (floor) => {
        const current = get().highestFloor;
        if (floor > current) {
          set({ highestFloor: floor });

          // 階層10で盗賊解放
          if (floor >= 10 && !get().unlockedCharacters.includes('thief')) {
            get().unlockCharacter('thief');
          }
        }
      },

      removeAds: () => {
        set({ adRemoved: true });
      },
    }),
    {
      name: 'player-storage',
      storage: AsyncStorage,
    }
  )
);
```

---

## データモデル

### types.ts

```typescript
// キャラクター
export type CharacterType = 'hero' | 'thief' | 'wizard' | 'elf';

export interface Character {
  id: string;
  type: CharacterType;
  name: string;
  skill: Skill;
  isFree: boolean;
  price?: number;
  spriteUrl: string;
}

// スキル
export type SkillType = 'dash' | 'diagonal' | 'invisible' | 'bind';

export interface Skill {
  type: SkillType;
  name: string;
  description: string;
  maxUsage: number;
}

// AI難易度
export type AILevel = 'easy' | 'normal' | 'hard';

// 特殊ルール
export type SpecialRule = 'none' | 'fog' | 'disappear' | 'fog_disappear';

// ゲーム状態
export interface GameState {
  currentFloor: number;
  turnCount: number;
  maxTurns: number;
  playerPosition: number;
  enemyPosition: number;
  gameStatus: 'playing' | 'win' | 'lose' | 'paused';
  aiLevel: AILevel;
  specialRule: SpecialRule;
  bpm: number;
}
```

---

## 実装優先順位

### フェーズ1: 音楽同期システム(最優先)
```typescript
// 1. BeatEngineの実装
- [ ] BeatEngine.ts 作成
- [ ] BGM読み込み・再生
- [ ] ビート検出
- [ ] タイミング判定

// 2. useBeatSyncフックの実装
- [ ] カスタムフック作成
- [ ] ビートコールバック
- [ ] タイミングチェック関数
```

### フェーズ2: 基本ゲームロジック
```typescript
// 3. GameEngineの実装
- [ ] 9マス移動ロジック
- [ ] 勝敗判定
- [ ] 階層進行

// 4. AIEngineの実装
- [ ] Easy AI
- [ ] Normal AI
- [ ] Hard AI
```

### フェーズ3: UI実装
```typescript
// 5. 画面実装
- [ ] HomeScreen
- [ ] GameScreen
- [ ] ResultScreen

// 6. コンポーネント実装
- [ ] GridBoard
- [ ] BeatIndicator
```

---

## 開発Tips

### Cursor活用

```
# 良い指示例
「BeatEngine.tsを実装してください。
- Expo AVを使用してBGM再生
- BPMから ビート間隔を計算
- ビートコールバックを実装
- タイミング判定(±15%の誤差許容)
TypeScriptで記述」
```

---

## 次のステップ

1. 音楽同期システムの実装(最優先)
2. 基本ゲームロジック
3. UI実装
4. テストプレイ・調整

---

**Escape Nine: Endless**
開発開始日: 2025-11-13
開発者: Souatou

Happy Coding! 🎮🎵
