# String tables

## Frozen string layout

The pooled layout is mandatory. The initial sizing write and minimization still run;
after the retained metadata tables have been normalized, `PrepareStringTable`
collects their serialized names, attributes, IL literals and resource filenames.
Collection uses symbolic placeholders, not usable offsets. `Freeze` assigns the
final offsets before signatures, bytecode and resource-file records are emitted.
No new string may be discovered after freezing, including a shared string.
The initial sizing pass retains the existing provisional string-table capacity limit.

The local pool starts with the empty string at offset zero. Nonempty strings are
packed by descending UTF-8 byte length, then ordinal value. A string ending a
previous root reuses that root's UTF-8 suffix offset; otherwise it becomes a new
NUL-terminated root. This preserves direct runtime flash pointers without RAM
allocation or decompression. Final bytes must not enter the reserved shared-ID
range. Embedded NULs retain the format's existing limitations.

Assembly and AssemblyRef names always have local copies, even if they also appear
in the shared dictionary. References to those names reuse the local copy.
IL literal provenance is tracked explicitly: the old "offset after metadata"
heuristic is invalid once suffixes and metadata share storage.

TypeRef serialization uses the full name for nested types, not their simple name.
Collect the exact writer representation; names needed for binding, reflection,
debugging, generic parameters and native contracts must not be stripped.

## Shared dictionary contract

`assets/compact-string-table.json` is the authoritative frozen version-2 catalog.
`tools/Generate-StringTable.ps1` generates `Utility/nanoStringsConstants.cs` and
nanoCLR's pooled dictionary/lookup array in `src/CLR/Core/StringTableData.cpp`.
Changing entries or IDs requires
a new string-table version and coordinated firmware/package rebuilds. Do not
regenerate the catalog for individual applications or boards.

Version 2 is the only supported contract. It uses 697 strings with IDs descending from
`0xFFFE`; `0xFFFF` remains the null-attribute-string sentinel. Local strings cover
arbitrary future application names and literals. A firmware build includes only
the frozen dictionary and rejects every other header string-table version.

The manifest's `corpus` pins the latest published previews of 26 libraries, including
package and device-DLL SHA-256 hashes and source commits where NuGet provides them.
There are no stable-package fallbacks. Device assets are taken from `netnano1.0`
or a package's legacy `lib` root, never its desktop `net472` reference assembly.
Generic collections are in the System.Collections preview, not a separate package.

Selection considers every typed metadata, IL and attribute string reference,
including local strings absent from the legacy dictionary. Assembly names remain
local. The initial benefit is `(assemblyCount - 1) * (UTF8Length + 1) - 2`, counting
distinct assemblies needing displaced copies, not reference frequency. This selects
667 entries. All observed public generic type/parameter names and supported generic
reflection names are retained as an explicit coverage floor (`genericCoverage`),
adding 30 entries at a modeled combined corpus cost of 58 bytes.

This covers `List<T>`, `Dictionary<TKey,TValue>`, `KeyValuePair<TKey,TValue>`,
generic collection interfaces, Action/Func/EventHandler delegates,
`Nullable<T>`, `Span<T>`, `ReadOnlySpan<T>`, nested
enumerator names, T/T1/T2/TKey/TValue/TResult/TEventArgs and generic Type reflection.
Closed type arguments and VAR/MVAR indices are encoded in signatures, not synthesized
display strings. Application-defined generic names still fall back to local strings.
All 79 retained generic parameters and their PDBX names are preserved across the
26-assembly migration. Do not select only from the legacy constants: it excludes
new generic names by construction.

The full referenced union is 4,584 entries and 68,141 pooled data bytes, exceeding
the native lookup's 16-bit offset range. The selected dictionary is 8,591 data +
1,394 lookup bytes (9,985 total), down from 13,048. The aligned corpus PEs shrink
11,964 bytes, for 15,027 bytes of modeled combined saving. Profile estimates are
1,968 bytes for core/GPIO/threading, 9,471 for networking/MQTT, and 3,584 for graphics.
This is not an embedded link-map measurement; code size, alignment and the deployed
assembly set affect the result.

The generator pins the version-2 SHA-256 independently of the manifest:
`67cb81422fbcc6f5050d41afae2e44c9f939c9534bc6449fe9c6a858ada7c643`.
It hashes UTF-8 `version + LF + lastId + LF + join(strings, LF) + LF` and rejects
catalog changes even when asked to regenerate. The pre-freeze prototype catalog
is superseded; rebuild every prototype compact PE and the matching firmware.
