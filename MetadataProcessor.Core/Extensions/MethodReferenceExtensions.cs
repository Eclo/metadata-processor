//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Linq;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class MethodReferenceExtensions
    {
         public static ushort ToEncodedNanoMethodToken(this MethodReference value)
        {
            // implements .NET nanoFramework encoding for MethodToken
            // encodes Method to be decoded with CLR_UncompressMethodToken
            // CLR tables are
            // 0: TBL_MethodDef
            // 1: TBL_MethodRef

            return nanoTokenHelpers.EncodeTableIndex(value.TonanoClrTable(), nanoTokenHelpers.NanoMethodTokenTables);
        }

        public static ClrTable TonanoClrTable(this MethodReference value)
        {
            // this one has to be before the others because generic parameters are also "other" types
            if (value is MethodDefinition)
            {
                return ClrTable.TBL_MethodDef;
            }
            else if (value is MethodReference)
            {
                return ClrTable.TBL_MethodRef;
            }
            else
            {
                throw new ArgumentException("Unknown conversion to ClrTable.");
            }
        }
    }
}