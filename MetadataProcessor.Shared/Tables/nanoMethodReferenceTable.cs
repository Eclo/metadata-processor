﻿//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing methods references list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoMethodReferenceTable :
        nanoReferenceTableBase<MethodReference>
    {
        private const int sizeOf_CLR_RECORD_METHODREF = 6;

        /// <summary>
        /// Helper class for comparing two instances of <see cref="MethodReference"/> objects
        /// using <see cref="MethodReference.FullName"/> property as unique key for comparison.
        /// </summary>
        private sealed class MemberReferenceComparer : IEqualityComparer<MethodReference>
        {
            /// <inheritdoc/>
            public bool Equals(MethodReference lhs, MethodReference rhs)
            {
                return string.Equals(lhs.FullName, rhs.FullName, StringComparison.Ordinal);
            }

            /// <inheritdoc/>
            public int GetHashCode(MethodReference that)
            {
                return that.FullName.GetHashCode();
            }
        }

        public nanoClrTable TableIndex => nanoClrTable.TBL_MethodRef;

        /// <summary>
        /// Creates new instance of <see cref="nanoMethodReferenceTable"/> object.
        /// </summary>
        /// <param name="items">List of member references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoMethodReferenceTable(
            IEnumerable<MethodReference> items,
            nanoTablesContext context)
            : base(items, new MemberReferenceComparer(), context)
        {
        }

        /// <summary>
        /// Gets method reference ID if possible (if method is external and stored in this table).
        /// </summary>
        /// <param name="methodReference">Method reference metadata in Mono.Cecil format.</param>
        /// <param name="referenceId">Method reference ID in .NET nanoFramework format.</param>
        /// <returns>Returns <c>true</c> if reference found, otherwise returns <c>false</c>.</returns>
        public bool TryGetMethodReferenceId(
            MethodReference methodReference,
            out ushort referenceId)
        {
            return TryGetIdByValue(methodReference, out referenceId);
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            MethodReference item)
        {
            if (!_context.MinimizeComplete)
            {
                return;
            }

            var writerStartPosition = writer.BaseStream.Position;

            if ((item.DeclaringType is TypeSpecification ||
                 item.DeclaringType is GenericParameter) &&
                _context.TypeSpecificationsTable.TryGetTypeReferenceId(item.DeclaringType, out ushort referenceId))
            {
                // methodReference is TypeSpecification
            }
            else if (_context.TypeReferencesTable.TryGetTypeReferenceId(item.DeclaringType, out referenceId))
            {
                // methodReference is TypeReference
            }
            else
            {
                throw new ArgumentException($"Can't find a type reference for {item.DeclaringType}.");
            }

            // Name
            WriteStringReference(writer, item.Name);

            // Container
            writer.WriteUInt16((ushort)(item.DeclaringType.ToEncodedNanoTypeToken() | referenceId));

            // Signature
            writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(item));

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_METHODREF);

        }
    }
}
