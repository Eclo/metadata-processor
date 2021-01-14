﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class MemberRef
    {
        public string ReferenceId;

        public string Name;

        public string Signature;

        public List<string> Arguments;
    }
}
