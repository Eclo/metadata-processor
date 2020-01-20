﻿//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing strings list and writing this
    /// list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoStringTable : InanoTable
    {
        /// <summary>
        /// Default implementation of <see cref="ICustomStringSorter"/> interface.
        /// Do nothing and just returns original sequence of string literals.
        /// </summary>
        private sealed class EmptyStringSorter : ICustomStringSorter
        {
            /// <inheritdoc/>
            public IEnumerable<string> Sort(
                ICollection<string> strings)
            {
                return strings;
            }
        }

        /// <summary>
        /// Maps for each unique string and related identifier (offset in strings table).
        /// </summary>
        private readonly Dictionary<string, ushort> _idsByStrings =
            new Dictionary<string, ushort>(StringComparer.Ordinal);

        /// <summary>
        /// Concrete implementation of string literals sorting algorithm (used by UTs).
        /// </summary>
        private readonly ICustomStringSorter _stringSorter;

        /// <summary>
        /// Last available string identifier.
        /// </summary>
        private ushort _lastAvailableId;

        /// <summary>
        /// Creates new instance of <see cref="nanoStringTable"/> object.
        /// </summary>
        public nanoStringTable(
            ICustomStringSorter stringSorter = null)
        {
            GetOrCreateStringId(string.Empty); // First item in string table always empty string
            _stringSorter = stringSorter ?? new EmptyStringSorter();
        }

        /// <summary>
        /// Gets existing or creates new string reference identifier related to passed string value.
        /// </summary>
        /// <remarks>
        /// Identifier is offset in strings table or just number from table of pre-defined constants.
        /// </remarks>
        /// <param name="value">String value for obtaining identifier.</param>
        /// <param name="useConstantsTable">
        /// If <c>true</c> hard-coded string constants table will be used (should be <c>false</c>
        /// for byte code writer because only loader use this pre-defined string table optimization).
        /// </param>
        /// <returns>Existing identifier if string already in table or new one.</returns>
        public ushort GetOrCreateStringId(
            string value,
            bool useConstantsTable = true)
        {
            ushort id;
            if (useConstantsTable && nanoStringsConstants.TryGetStringIndex(value, out id))
            {
                return id;
            }
            if (!_idsByStrings.TryGetValue(value, out id))
            {
                id = _lastAvailableId;
                _idsByStrings.Add(value, id);
                var length = Encoding.UTF8.GetBytes(value).Length + 1;
                _lastAvailableId += (ushort)(length);
            }
            return id;
        }

        /// <summary>
        /// Try to get a string value from the table providing the reference identifier.
        /// </summary>
        /// <param name="id">Existing identifier in table.</param>
        /// <returns>The string value or a null if the identifier doesn't exist.</returns>
        public string TryGetString(ushort id)
        {
            // try to get string from id table
            var output = _idsByStrings.FirstOrDefault(s => s.Value == id).Key;

            if(output == null)
            {
                // try to find string in string constant table
                output = nanoStringsConstants.TryGetString(id);
            }

            return output;
        }

        /// <inheritdoc/>
        public void Write(
            nanoBinaryWriter writer)
        {
            foreach (var item in _idsByStrings
                .OrderBy(item => item.Value)
                .Select(item => item.Key))
            {
                writer.WriteString(item);
            }
        }

        /// <summary>
        /// Adds all string constants from <paramref name="fakeStringTable"/> table into this one.
        /// </summary>
        /// <param name="fakeStringTable">Additional string table for merging with this one.</param>
        internal void MergeValues(
            nanoStringTable fakeStringTable)
        {
            foreach (var item in _stringSorter.Sort(fakeStringTable._idsByStrings.Keys))
            {
                GetOrCreateStringId(item, false);
            }
        }

        public void RemoveUnusedItems(HashSet<MetadataToken> items)
        {
            var setAll = new HashSet<MetadataToken>();

            // build a collection of the existing tokens
            foreach (var a in _idsByStrings)
            {
                setAll.Add(new MetadataToken(TokenType.String, a.Value));
            }

            // remove the ones that are used
            foreach (var t in items)
            {
                setAll.Remove(t);
            }

            // the ones left are the ones not used, OK to remove them
            foreach (var t in setAll)
            {
                var itemToRemove = TryGetString((ushort)t.ToUInt32());
                _idsByStrings.Remove(itemToRemove);
            }
        }
    }
}
