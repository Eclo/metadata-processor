//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor
{
    internal partial class DumpTemplates
    {
        internal const string DumpAllTemplate =
@"{{#each AssemblyReferences}}
AssemblyRefProps [{{ReferenceId}}]: Flags: {{Flags}} '{{Name}}'{{#newline}}
{{/each}}
{{#if AssemblyReferences}}{{#newline}}{{/if}}

{{#each TypeReferences}}
TypeRefProps [{{ReferenceId}}]: Scope: {{Scope}} '{{Name}}'{{#newline}}
{{#each MemberReferences}}
    MemberRefProps [{{ReferenceId}}]: '{{Name}}' [{{Signature}}]{{#newline}}
{{/each}}
{{/each}}
{{#if TypeReferences}}{{#newline}}{{/if}}

{{#each TypeDefinitions}}
TypeDefProps [{{ReferenceId}}]: Flags: {{Flags}} Extends: {{ExtendsType}} Enclosed: {{EnclosedType}} '{{Name}}'{{#newline}}
{{#each GenericParameters}}
    GenericParam [{{GenericParamToken}}]: Position: ({{Position}}) '{{Name}}' Owner: {{Owner}} [{{Signature}}]{{#newline}}
{{/each}}

{{#each TypeSpecifications}}
TypeSpec [{{ReferenceId}}]: '{{Name}}'{{#newline}}
{{#each MemberReferences}}
    MemberRef [{{ReferenceId}}]: '{{Name}}'{{#newline}}
{{#if Arguments}}
        Argument: {{Arguments}}{{#newline}}
{{/else}}
    No arguments
{{/if}}
{{/each}}
{{/each}}
{{#if TypeSpecifications}}{{#newline}}{{/if}}

{{#each FieldDefinitions}}
    FieldDefProps [{{ReferenceId}}]: Attr: {{Attributes}} Flags: {{Flags}} '{{Name}}' [{{Signature}}]{{#newline}}
{{/each}}

{{#each MethodDefinitions}}
    MethodDefProps [{{ReferenceId}}]: Flags: {{Flags}} Impl: {{Implementation}} RVA: {{RVA}} '{{Name}}' [{{Signature}}]{{#newline}}
{{#if Locals}}
        Locals {{Locals}}{{#newline}}
{{/if}}
{{#each ExceptionHandlers}}
        EH: {{Handler}}{{#newline}}
{{/each}}
{{#if ILCodeInstructionsCount}}
        IL count: {{ILCodeInstructionsCount}}{{#newline}}
{{/if}}
{{#each ILCode}}
        {{IL}}{{#newline}}
{{/each}}
{{/each}}

{{#each InterfaceDefinitions}}
    InterfaceImplProps [{{ReferenceId}}]: Itf: {{Interface}}{{#newline}}
{{/each}}
{{/each}}
{{#if TypeDefinitions}}{{#newline}}{{/if}}

{{#each Attributes}}
Attribute: {{Name}}::[{{ReferenceId}} {{TypeToken}}]{{#newline}}
{{#if FixedArgs}}Fixed Arguments:{{#newline}}{{#else}}{{#newline}}{{/if}}
{{#each FixedArgs}}
{{Options}} {{Numeric}}{{Text}}{{#newline}}
{{#newline}}
{{/each}}
{{/each}}
{{#if Attributes}}{{#newline}}{{/if}}
String Heap{{#newline}}
-----------{{#newline}}
{{#each StringHeap}}
{{ReferenceId}}: {{Content}}{{#newline}}
{{/each}}
{{#newline}}

User Strings{{#newline}}
------------{{#newline}}
{{#each UserStrings}}
{{ReferenceId}} : ({{Length}}) ""{{Content}}""{{#newline}}
{{/each}}
";
    }
}
