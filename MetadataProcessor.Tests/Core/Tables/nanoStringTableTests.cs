// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoStringTableTests
    {
        private static nanoTablesContext CreateContext()
        {
            var assembly = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("Strings", new Version(1, 0)), "Strings", ModuleKind.Dll);
            return new nanoTablesContext(assembly, null, null, false, false, false);
        }

        private static byte[] Write(nanoStringTable table)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                table.Write(nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer));
                return stream.ToArray();
            }
        }

        [TestMethod]
        public void FrozenPoolSharesUtf8SuffixesAndKeepsEmptyAtZero()
        {
            var table = CreateContext().StringTable;
            table.BeginCollection();
            table.GetOrCreateStringLiteralId("\u00e9_tail");
            table.GetOrCreateStringLiteralId("Object");
            table.GetOrCreateStringId("prefix_\u00e9_tail", false);
            table.Freeze();

            byte[] data = Write(table);
            Assert.AreEqual((ushort)0, table.GetOrCreateStringId(string.Empty));
            Assert.AreEqual((byte)0, data[0]);
            ushort root = table.GetOrCreateStringId("prefix_\u00e9_tail", false);
            ushort suffix = table.GetOrCreateStringLiteralId("\u00e9_tail");
            Assert.AreEqual(root + Encoding.UTF8.GetByteCount("prefix_"), (int)suffix);
            Assert.AreEqual("\u00e9_tail", Encoding.UTF8.GetString(data, suffix, data.Length - suffix - 1));
            Assert.AreEqual(Encoding.UTF8.GetByteCount("prefix_\u00e9_tail") + 2, data.Length);
            Assert.IsTrue(table.IsUserString("\u00e9_tail"));
            Assert.IsFalse(table.IsUserString("prefix_\u00e9_tail"));
            Assert.IsTrue(table.GetUserStrings().Single(s => s.Key == "Object").Value >= nanoStringsConstants.FirstId);
        }

        [TestMethod]
        public void SuffixSharingDoesNotSplitSurrogatePairs()
        {
            var table = CreateContext().StringTable;
            table.BeginCollection();
            table.GetOrCreateStringId("\uD83D\uDE00_tail", false);
            table.GetOrCreateStringId("\uDE00_tail", false);
            table.Freeze();
            byte[] data = Write(table);
            foreach (string value in new[] { "\uD83D\uDE00_tail", "\uDE00_tail" })
            {
                ushort offset = table.GetOrCreateStringId(value, false);
                int length = Array.IndexOf(data, (byte)0, offset) - offset;
                CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(value), data.Skip(offset).Take(length).ToArray());
            }
        }

        [TestMethod]
        public void FrozenPoolIsIndependentOfCollectionOrderAndRejectsLateStrings()
        {
            var first = CreateContext().StringTable;
            var second = CreateContext().StringTable;
            string[] strings = { "tail", "other_tail", "some_other_tail", "equal_length" };
            foreach (var table in new[] { first, second })
            {
                table.BeginCollection();
            }
            foreach (string value in strings)
            {
                first.GetOrCreateStringId(value, false);
            }
            foreach (string value in strings.Reverse())
            {
                second.GetOrCreateStringId(value, false);
            }
            first.Freeze();
            second.Freeze();
            CollectionAssert.AreEqual(Write(first), Write(second));
            foreach (string value in strings)
            {
                Assert.AreEqual(first.GetOrCreateStringId(value, false), second.GetOrCreateStringId(value, false));
            }
            Assert.ThrowsExactly<InvalidOperationException>(() => first.GetOrCreateStringId("late_string"));
            Assert.ThrowsExactly<InvalidOperationException>(() => first.GetOrCreateStringId("Object"));
        }

        [TestMethod]
        public void AssemblyNamesRemainLocalWhenTheyAreAlsoSharedStrings()
        {
            var table = CreateContext().StringTable;
            table.BeginCollection();
            table.GetOrCreateAssemblyNameId("System");
            table.GetOrCreateStringLiteralId("System");
            table.GetOrCreateStringId(".cctor");
            table.Freeze();
            ushort name = table.GetOrCreateAssemblyNameId("System");
            Assert.IsTrue(name < nanoStringsConstants.FirstId);
            Assert.AreEqual(name, table.GetOrCreateStringLiteralId("System"));
            Assert.AreEqual("System", table.TryGetString(name));
            Assert.AreNotEqual(ushort.MaxValue, table.GetOrCreateStringId(".cctor"));
        }

        [TestMethod]
        public void CapacityIsCheckedAfterPoolingAndBeforeSharedIdRange()
        {
            var table = CreateContext().StringTable;
            table.BeginCollection();
            string root = new string('x', nanoStringsConstants.FirstId - 2);
            table.GetOrCreateStringId(root, false);
            table.GetOrCreateStringId(root.Substring(1), false);
            table.Freeze();
            Assert.AreEqual((int)nanoStringsConstants.FirstId, Write(table).Length);

            var overflowing = CreateContext().StringTable;
            overflowing.BeginCollection();
            overflowing.GetOrCreateStringId(root, false);
            overflowing.GetOrCreateStringId("y", false);
            Assert.ThrowsExactly<InvalidOperationException>(() => overflowing.Freeze());
        }

        [TestMethod]
        public void DefaultFixtureUsesFrozenCatalogAndProducesDeterministicPeAndDebugSymbols()
        {
            using (var firstAssembly = TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints())
            using (var secondAssembly = TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints())
            {
                var first = new nanoAssemblyBuilder(firstAssembly, false);
                var second = new nanoAssemblyBuilder(secondAssembly, false);
                byte[] firstPe = Build(first);
                byte[] secondPe = Build(second);

                Assert.AreEqual(first.LastNativeMethodsChecksum, second.LastNativeMethodsChecksum);
                CollectionAssert.AreEqual(firstPe, secondPe);
                Assert.AreEqual((ushort)2, BitConverter.ToUInt16(firstPe, 34));
                Assert.IsTrue(first.TablesContext.StringTable.IsFrozen);
                Assert.IsTrue(first.TablesContext.GenericParamsTable.Items.Any());
                int genericTable = (int)BitConverter.ToUInt32(firstPe, 36 + 7 * 4);
                first.TablesContext.GenericParamsTable.ForEachItems((index, parameter) =>
                {
                    ushort name = BitConverter.ToUInt16(firstPe, genericTable + (int)index * 10 + 8);
                    Assert.AreEqual(parameter.Name, first.TablesContext.StringTable.TryGetString(name));
                });

                int headerSize = (int)BitConverter.ToUInt32(firstPe, 36);
                Assert.AreEqual(BitConverter.ToUInt32(firstPe, 12), Crc32.Compute(firstPe.Skip(headerSize).ToArray()));
                byte[] header = firstPe.Take(headerSize).ToArray();
                Array.Clear(header, 8, 4);
                Assert.AreEqual(BitConverter.ToUInt32(firstPe, 8), Crc32.Compute(header));

                string firstPdbx = Path.GetTempFileName();
                string secondPdbx = Path.GetTempFileName();
                try
                {
                    first.Write(firstPdbx);
                    second.Write(secondPdbx);
                    CollectionAssert.AreEqual(File.ReadAllBytes(firstPdbx), File.ReadAllBytes(secondPdbx));
                }
                finally
                {
                    File.Delete(firstPdbx);
                    File.Delete(secondPdbx);
                }
            }
        }

        [TestMethod]
        public void CollectorUsesSerializedNestedTypeReferenceNames()
        {
            var context = CreateContext();
            var scope = new AssemblyNameReference("External", new Version(1, 0));
            var outer = new TypeReference("Example", "Outer", context.AssemblyDefinition.MainModule, scope);
            var nested = new TypeReference(string.Empty, "Inner", context.AssemblyDefinition.MainModule, scope)
            {
                DeclaringType = outer
            };
            var holder = new TypeDefinition("Example", "Holder", TypeAttributes.Public);
            holder.Fields.Add(new FieldDefinition("Value", FieldAttributes.Public, nested));
            context.AssemblyDefinition.MainModule.Types.Add(holder);
            using (var stream = new MemoryStream())
            {
                context.AssemblyDefinition.Write(stream);
                stream.Position = 0;
                using (var assembly = AssemblyDefinition.ReadAssembly(stream))
                {
                    var loaded = new nanoTablesContext(assembly, null, null, false, false, false);
                    loaded.PrepareStringTable();
                    Assert.AreEqual(nested.FullName,
                        loaded.StringTable.TryGetString(loaded.StringTable.GetOrCreateStringId(nested.FullName)));
                }
            }
        }

        private static byte[] Build(nanoAssemblyBuilder builder)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var binary = nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);
                builder.Write(binary);
                builder.Minimize();
                stream.SetLength(0);
                stream.Position = 0;
                builder.Write(binary);
                return stream.ToArray();
            }
        }
    }
}
