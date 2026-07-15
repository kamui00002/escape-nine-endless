// SwiftSaveConverterTests.cs
// Swift 版セーブデータ移行 (B-1) の純ロジック部分 (Core/SwiftSaveConverter.cs) のテスト。
// UnityEngine 非依存のため unity/verify/Core.Tests の dotnet test に自動包含される。

using System.Collections.Generic;
using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class SwiftSaveConverterTests
    {
        // MARK: - ParseJsonStringArray

        [Test]
        public void ParseJsonStringArray_Null_ReturnsEmptyList()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray(null);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseJsonStringArray_EmptyString_ReturnsEmptyList()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseJsonStringArray_EmptyArray_ReturnsEmptyList()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("[]");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseJsonStringArray_FlatStrings_ParsesInOrder()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("[\"a\",\"b\",\"c\"]");
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result);
        }

        [Test]
        public void ParseJsonStringArray_WithWhitespace_Parses()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("[ \"a\" , \"b\" ]");
            CollectionAssert.AreEqual(new[] { "a", "b" }, result);
        }

        [Test]
        public void ParseJsonStringArray_JapaneseValues_Parses()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("[\"初勝利\",\"階層10到達\"]");
            CollectionAssert.AreEqual(new[] { "初勝利", "階層10到達" }, result);
        }

        [Test]
        public void ParseJsonStringArray_EscapedQuote_Unescapes()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("[\"a\\\"b\"]");
            CollectionAssert.AreEqual(new[] { "a\"b" }, result);
        }

        [Test]
        public void ParseJsonStringArray_NotAnArray_ReturnsEmptyList()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("{\"a\":1}");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseJsonStringArray_UnterminatedString_ReturnsEmptyList()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("[\"a");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseJsonStringArray_NonStringElement_ReturnsEmptyList()
        {
            var result = SwiftSaveConverter.ParseJsonStringArray("[1,2,3]");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseJsonStringArray_GarbageInput_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SwiftSaveConverter.ParseJsonStringArray("not json at all }{["));
        }

        // MARK: - MapAchievementsToEnumCsv

        [Test]
        public void MapAchievementsToEnumCsv_Null_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, SwiftSaveConverter.MapAchievementsToEnumCsv(null));
        }

        [Test]
        public void MapAchievementsToEnumCsv_AllNineRawValues_MapToEnumNames()
        {
            var raw = new List<string>
            {
                "初勝利", "階層10到達", "階層25到達", "階層50到達", "階層75到達",
                "階層100到達", "素手の達人", "スピードランナー", "生存者"
            };
            var csv = SwiftSaveConverter.MapAchievementsToEnumCsv(raw);
            Assert.AreEqual(
                "FirstWin,Floor10,Floor25,Floor50,Floor75,Floor100,NoSkillWin,SpeedRunner,Survivor",
                csv);
        }

        [Test]
        public void MapAchievementsToEnumCsv_UnknownValue_IsSkipped()
        {
            var csv = SwiftSaveConverter.MapAchievementsToEnumCsv(new List<string> { "初勝利", "未知の実績" });
            Assert.AreEqual("FirstWin", csv);
        }

        [Test]
        public void MapAchievementsToEnumCsv_Duplicate_IsDeduped()
        {
            var csv = SwiftSaveConverter.MapAchievementsToEnumCsv(new List<string> { "初勝利", "初勝利" });
            Assert.AreEqual("FirstWin", csv);
        }

        [Test]
        public void MapAchievementsToEnumCsv_EndToEnd_FromJsonArray()
        {
            var parsed = SwiftSaveConverter.ParseJsonStringArray("[\"初勝利\",\"階層10到達\"]");
            var csv = SwiftSaveConverter.MapAchievementsToEnumCsv(parsed);
            Assert.AreEqual("FirstWin,Floor10", csv);
        }

        // MARK: - NormalizeCharactersCsv

        [Test]
        public void NormalizeCharactersCsv_Null_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, SwiftSaveConverter.NormalizeCharactersCsv(null));
        }

        [Test]
        public void NormalizeCharactersCsv_KnownValues_ArePreserved()
        {
            Assert.AreEqual("hero,thief,wizard,elf,knight",
                SwiftSaveConverter.NormalizeCharactersCsv("hero,thief,wizard,elf,knight"));
        }

        [Test]
        public void NormalizeCharactersCsv_UnknownToken_IsSkipped()
        {
            Assert.AreEqual("hero", SwiftSaveConverter.NormalizeCharactersCsv("hero,bogus"));
        }

        [Test]
        public void NormalizeCharactersCsv_Duplicate_IsDeduped()
        {
            Assert.AreEqual("hero,thief", SwiftSaveConverter.NormalizeCharactersCsv("hero,thief,hero"));
        }

        // MARK: - NormalizeProductIdsCsv

        [Test]
        public void NormalizeProductIdsCsv_Null_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, SwiftSaveConverter.NormalizeProductIdsCsv(null));
        }

        [Test]
        public void NormalizeProductIdsCsv_KnownProductIds_ArePreserved()
        {
            var ids = new List<string>
            {
                "com.escapenine.endless.character.wizard",
                "com.escapenine.endless.removeads"
            };
            Assert.AreEqual(
                "com.escapenine.endless.character.wizard,com.escapenine.endless.removeads",
                SwiftSaveConverter.NormalizeProductIdsCsv(ids));
        }

        [Test]
        public void NormalizeProductIdsCsv_UnknownPrefix_IsSkipped()
        {
            var ids = new List<string> { "com.escapenine.endless.removeads", "com.other.app.product" };
            Assert.AreEqual("com.escapenine.endless.removeads", SwiftSaveConverter.NormalizeProductIdsCsv(ids));
        }

        [Test]
        public void NormalizeProductIdsCsv_EndToEnd_FromJsonArray()
        {
            var parsed = SwiftSaveConverter.ParseJsonStringArray(
                "[\"com.escapenine.endless.character.elf\",\"com.escapenine.endless.removeads\"]");
            var csv = SwiftSaveConverter.NormalizeProductIdsCsv(parsed);
            Assert.AreEqual("com.escapenine.endless.character.elf,com.escapenine.endless.removeads", csv);
        }
    }
}
