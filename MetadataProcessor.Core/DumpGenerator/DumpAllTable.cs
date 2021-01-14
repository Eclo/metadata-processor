﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class DumpAllTable
    {
        public List<AssemblyRef> AssemblyReferences = new List<AssemblyRef>();
        public List<TypeRef> TypeReferences = new List<TypeRef>();
        public List<TypeDef> TypeDefinitions = new List<TypeDef>();
        public List<TypeSpec> TypeSpecifications = new List<TypeSpec>();
        public List<MethodDef> MethodDefinitions = new List<MethodDef>();
        public List<InterfaceDef> InterfaceDefinitions = new List<InterfaceDef>();
        public List<AttributeCustom> Attributes = new List<AttributeCustom>();
        public List<HeapString> StringHeap = new List<HeapString>();
        public List<UserString> UserStrings = new List<UserString>();
    }
}
