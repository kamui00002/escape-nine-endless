// IRandomSource.cs
// Swift 版 AIEngine は Double.random / randomElement のグローバル乱数を使用。
// C# 移植では乱数源を注入可能にし、テストで決定論的に検証できるようにする
// (移植方針の意図的な改善。既定挙動は System.Random でグローバル乱数相当)。

namespace EscapeNine.Core
{
    public interface IRandomSource
    {
        /// <summary>[0.0, 1.0) の乱数。Swift の Double.random(in: 0...1) 相当 (上端の扱いのみ微差)。</summary>
        double NextDouble();

        /// <summary>[0, maxExclusive) の整数。randomElement のインデックス選択に使用。</summary>
        int NextInt(int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly System.Random _rng;

        /// <param name="seed">指定するとテスト等で決定論的に再現可能。省略時は時刻シード。</param>
        public SystemRandomSource(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public double NextDouble() => _rng.NextDouble();

        public int NextInt(int maxExclusive) => maxExclusive <= 0 ? 0 : _rng.Next(maxExclusive);
    }
}
