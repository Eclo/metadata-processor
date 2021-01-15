﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Mustache;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    /// <summary>
    /// Dumps details for parsed assemblies and PE files.
    /// </summary>
    public sealed class nanoDumperGenerator
    {
        private readonly nanoTablesContext _tablesContext;
        private readonly string _path;

        public nanoDumperGenerator(
                    nanoTablesContext tablesContext,
                    string path)
        {
            _tablesContext = tablesContext;
            _path = path;
        }

        public void DumpAll()
        {
            var dumpTable = new DumpAllTable();

            DumpAssemblyReferences(dumpTable);
            //DumpModuleReferences(dumpTable);
            DumpTypeReferences(dumpTable);
            DumpTypeDefinitions(dumpTable);
            DumpTypeSpecifications(dumpTable);
            DumpCustomAttributes(dumpTable);
            DumpStringHeap(dumpTable);
            DumpUserStrings(dumpTable);

            FormatCompiler compiler = new FormatCompiler();
            Generator generator = compiler.Compile(DumpTemplates.DumpAllTemplate);

            using (var dumpFile = File.CreateText(_path))
            {
                var output = generator.Render(dumpTable);
                dumpFile.Write(output);
            }
        }

        private void DumpCustomAttributes(DumpAllTable dumpTable)
        {
            foreach (var a in _tablesContext.TypeDefinitionTable.Items.Where(td => td.HasCustomAttributes))
            {
                foreach (var ma in a.Methods)
                {
                    if (ma.HasCustomAttributes)
                    {
                        var attribute = new AttributeCustom()
                        {
                            Name = a.Module.Assembly.Name.Name,
                            ReferenceId = ma.MetadataToken.ToInt32().ToString("x8"),
                            TypeToken = ma.CustomAttributes[0].Constructor.MetadataToken.ToInt32().ToString("x8")
                        };

                        if (ma.CustomAttributes[0].HasConstructorArguments)
                        {
                            foreach (var value in ma.CustomAttributes[0].ConstructorArguments)
                            {
                                attribute.FixedArgs.AddRange(BuildFixedArgsAttribute(value));
                            }
                        }

                        dumpTable.Attributes.Add(attribute);
                    }
                }

                foreach (var fa in a.Fields)
                {
                    if(fa.HasCustomAttributes)
                    {
                        var attribute = new AttributeCustom()
                        {
                            Name = a.Module.Assembly.Name.Name,
                            ReferenceId = fa.MetadataToken.ToInt32().ToString("x8"),
                            TypeToken = fa.CustomAttributes[0].Constructor.MetadataToken.ToInt32().ToString("x8")
                        };

                        if (fa.CustomAttributes[0].HasConstructorArguments)
                        {
                            foreach (var value in fa.CustomAttributes[0].ConstructorArguments)
                            {
                                attribute.FixedArgs.AddRange(BuildFixedArgsAttribute(value));
                            }
                        }

                        dumpTable.Attributes.Add(attribute);
                    }
                }
            }
        }

        private List<AttFixedArgs> BuildFixedArgsAttribute(CustomAttributeArgument value)
        {
            if (value.Type.IsArray && value.Type.GetElementType().FullName == "System.Object")
            {
                var attArgs = new List<AttFixedArgs>();

                foreach (var attributeArgument in (CustomAttributeArgument[])value.Value)
                {
                    attArgs.AddRange(BuildFixedArgsAttribute((CustomAttributeArgument)attributeArgument.Value));
                }

                return attArgs;
            }

            var serializationType = value.Type.ToSerializationType();

            var newArg = new AttFixedArgs()
            {
                Options = ((byte)serializationType).ToString("X2"),
                Text = "",
            };

            switch (serializationType)
            {
                case nanoSerializationType.ELEMENT_TYPE_BOOLEAN:
                    newArg.Numeric = ((bool)value.Value) ? 1.ToString("X16") : 0.ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_STRING:
                    newArg.Text = (string)value.Value;
                    break;

                case nanoSerializationType.ELEMENT_TYPE_OBJECT:
                    newArg.Text = (string)value.Value;
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I1:
                    newArg.Numeric = ((sbyte)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I2:
                    newArg.Numeric = ((short)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I4:
                    newArg.Numeric = ((int)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I8:
                    newArg.Numeric = ((long)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U1:
                    newArg.Numeric = ((byte)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U2:
                    newArg.Numeric = ((ushort)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U4:
                    newArg.Numeric = ((uint)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U8:
                    newArg.Numeric = ((ulong)value.Value).ToString("X16");
                    break;

                default:
                    newArg.Text = value.Value.ToString();
                    break;
            }

            return new List<AttFixedArgs>() { newArg };
        }

        private void DumpStringHeap(DumpAllTable dumpTable)
        {
            foreach (var s in _tablesContext.StringTable.GetItems().OrderBy(i => i.Value))
            {
                // don't output the empty string
                if (s.Value == 0)
                {
                    continue;
                }

                dumpTable.StringHeap.Add(
                    new HeapString()
                    {
                        ReferenceId = s.Value.ToString("x8"),
                        Content = s.Key
                    });
            }
        }

        private void DumpUserStrings(DumpAllTable dumpTable)
        {
            foreach (var s in _tablesContext.StringTable.GetItems().OrderBy(i => i.Value).Where(i => i.Value > _tablesContext.StringTable.LastPreAllocatedId))
            {
                // don't output the empty string
                if (s.Value == 0)
                {
                    continue;
                }

                dumpTable.UserStrings.Add(
                    new UserString()
                    {
                        ReferenceId = new nanoMetadataToken(ClrTable.Strings, _tablesContext.StringTable.GetOrCreateStringId(s.Key, true)).ToString(),
                        Length = s.Key.Length.ToString("x2"),
                        Content = s.Key
                });
            }
        }

        private void DumpTypeDefinitions(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeDefinitionTable.Items.OrderBy(tr => tr.MetadataToken.ToInt32()))
            {
                string realToken = t.MetadataToken.ToInt32().ToString("x8");

                _tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(t, out ushort referenceId);

                // fill type definition
                var typeDef = new TypeDef()
                {
                   ReferenceId = $"[{new nanoMetadataToken(t.MetadataToken, referenceId)}] /*{realToken}*/"
                };

                if(t.IsNested)
                {
                    typeDef.Name = t.Name;
                }
                else
                {
                    typeDef.Name = t.FullName;
                }

                var typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(
                    t,
                    _tablesContext.MethodDefinitionTable);

                typeDef.Flags = typeFlags.ToString("x8");

                if (t.BaseType != null)
                {
                    realToken = t.BaseType.MetadataToken.ToInt32().ToString("x8");

                    _tablesContext.TypeReferencesTable.TryGetTypeReferenceId(t.BaseType, out referenceId);

                    typeDef.ExtendsType = $"{t.BaseType.FullName}[{new nanoMetadataToken(t.BaseType.MetadataToken, referenceId)}] /*{realToken}*/";
                }
                else
                {
                    typeDef.ExtendsType = "(none)";
                }
                
                if(t.DeclaringType != null)
                {
                    realToken = t.DeclaringType.MetadataToken.ToInt32().ToString("x8");

                    _tablesContext.TypeReferencesTable.TryGetTypeReferenceId(t.DeclaringType, out referenceId);

                    typeDef.EnclosedType = $"{t.DeclaringType.FullName}[{new nanoMetadataToken(t.DeclaringType.MetadataToken, referenceId)}] /*{realToken}*/";
                }
                else
                {
                    typeDef.EnclosedType = "(none)";
                }

                // list generic parameters
                foreach (var gp in t.GenericParameters)
                {
                    realToken = gp.MetadataToken.ToInt32().ToString("x8");
                    _tablesContext.GenericParamsTable.TryGetParameterId(gp, out referenceId);

                    var ownerRealToken = gp.Owner.MetadataToken.ToInt32().ToString("x8");
                    _tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(gp.Owner as TypeDefinition, out ushort ownerReferenceId);

                    var genericParam = new GenericParam()
                    {
                        Position = gp.Position.ToString(),
                        GenericParamToken = $"[{new nanoMetadataToken(gp.MetadataToken, referenceId)}] /*{realToken}*/",
                        Name = new string('!', gp.Position + 1) + gp.FullName,
                        Owner = $"[{ownerReferenceId:x4}] /*{ownerRealToken}*/",
                        Signature =  gp.DeclaringType.Name
                    };

                    typeDef.GenericParameters.Add(genericParam);
                }

                // list type fields
                foreach (var f in t.Fields)
                {
                    uint att = (uint)f.Attributes;

                    realToken = f.MetadataToken.ToInt32().ToString("x8");
                    _tablesContext.TypeReferencesTable.TryGetTypeReferenceId(f.FieldType, out referenceId);

                    var fieldDef = new FieldDef()
                    {
                        ReferenceId = $"[{new nanoMetadataToken(f.FieldType.MetadataToken, referenceId)}] /*{realToken}*/",
                        Name = f.Name,
                        Flags = att.ToString("x8"),
                        Attributes = att.ToString("x8"),
                        Signature = f.FieldType.TypeSignatureAsString()
                    };

                    typeDef.FieldDefinitions.Add(fieldDef);
                }

                // list type methods
                foreach (var m in t.Methods)
                {
                    realToken = m.MetadataToken.ToInt32().ToString("x8");
                    _tablesContext.MethodDefinitionTable.TryGetMethodReferenceId(m, out referenceId);

                    var methodDef = new MethodDef()
                    {
                        ReferenceId = $"[{new nanoMetadataToken(m.MetadataToken, referenceId)}] /*{realToken}*/",
                        Name = m.FullName(),
                        RVA = m.RVA.ToString("x8"),
                        Implementation = "00000000",
                        Signature = PrintSignatureForMethod(m)
                    };

                    var methodFlags = nanoMethodDefinitionTable.GetFlags(m);
                    methodDef.Flags = methodFlags.ToString("x8");

                    if (m.HasBody)
                    {
                        // locals
                        if (m.Body.HasVariables)
                        {
                            methodDef.Locals = $"{m.Body.Variables.Count}: {PrintSignatureForLocalVar(m.Body.Variables)}";
                        }

                        // exceptions
                        foreach (var eh in m.Body.ExceptionHandlers)
                        {
                            var h = new ExceptionHandler
                            {
                                Handler = $"{(int)eh.HandlerType:x2} " +
                                $"{eh.TryStart?.Offset.ToString("x8")}->{eh.TryEnd?.Offset.ToString("x8")} " +
                                $"{eh.HandlerStart?.Offset.ToString("x8")}->{eh.HandlerEnd?.Offset.ToString("x8")} "
                            };

                            if (eh.CatchType != null)
                            {
                                h.Handler += $"{eh.CatchType.MetadataToken.ToInt32():x8}";
                            }
                            else
                            {
                                h.Handler += "00000000";
                            }

                            methodDef.ExceptionHandlers.Add(h);
                        }

                        methodDef.ILCodeInstructionsCount = m.Body.Instructions.Count.ToString();

                        // IL code
                        foreach (var instruction in m.Body.Instructions)
                        {
                            if (instruction.OpCode.OperandType == OperandType.InlineMethod ||
                                instruction.OpCode.OperandType == OperandType.InlineField ||
                                instruction.OpCode.OperandType == OperandType.InlineTok ||
                                instruction.OpCode.OperandType == OperandType.InlineType)
                            {
                                realToken = ((IMetadataTokenProvider)instruction.Operand).MetadataToken.ToInt32().ToString("x8");
                            }

                            string typeName = string.Empty;

                            ILCode ilCode = new ILCode();

                            StringBuilder ilDescription = new StringBuilder();

                            ilDescription.Append($"IL_{instruction.Offset:x4}:  ");

                            ilDescription.Append(instruction.OpCode.Name.PadRight(12));

                            if (instruction.Operand != null)
                            {
                                if (instruction.OpCode.OperandType == OperandType.InlineMethod)
                                {
                                    typeName = (instruction.Operand as MethodReference).FullName;

                                    referenceId = _tablesContext.GetMethodReferenceId((MethodReference)instruction.Operand);

                                    // need to clear the encoded method mask
                                    unchecked
                                    {
                                        referenceId &= 0x7FFF;
                                    }

                                    ilDescription.Append($"{typeName} [{new nanoMetadataToken((instruction.Operand as MethodReference).MetadataToken, referenceId)}] /*{realToken}*/");
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineField)
                                {
                                    typeName = (instruction.Operand as FieldReference).FullName;

                                    referenceId = _tablesContext.GetFieldReferenceId((FieldReference)instruction.Operand);

                                    if((referenceId & 0x8000) != 0)
                                    {
                                        // need to clear the external ref mask
                                        unchecked
                                        {
                                            referenceId &= 0x7FFF;
                                        }

                                        ilDescription.Append($"{typeName} [{new nanoMetadataToken(ClrTable.FieldRef, referenceId)}] /*{realToken}*/");
                                    }
                                    else
                                    {
                                        ilDescription.Append($"{typeName} [{new nanoMetadataToken(ClrTable.FieldDef, referenceId)}] /*{realToken}*/");
                                    }
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineTok)
                                {
                                    var token = _tablesContext.GetMetadataToken((IMetadataTokenProvider)instruction.Operand);

                                    ilCode.IL += $"[{token:x8}] /*{realToken}*/";
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineType)
                                {
                                    referenceId = _tablesContext.GetTypeReferenceId((TypeReference)instruction.Operand);
                                    var nfToken = new nanoMetadataToken();

                                    // need to clear the encoded type mask
                                    referenceId &= (ushort)~nanoEncodedInlineType.EncodedInlineTypeMask;

                                    if (instruction.Operand is GenericParameter)
                                    {
                                        typeName = (instruction.Operand as GenericParameter).TypeSignatureAsString();
                                        nfToken = new nanoMetadataToken(ClrTable.GenericParam, referenceId);
                                    }
                                    else if (instruction.Operand is TypeSpecification)
                                    {
                                        typeName = (instruction.Operand as TypeSpecification).FullName;
                                        nfToken = new nanoMetadataToken(ClrTable.TypeSpec, referenceId);
                                    }
                                    else if (instruction.Operand is TypeReference)
                                    {
                                        typeName = (instruction.Operand as TypeReference).FullName;
                                        nfToken = new nanoMetadataToken(ClrTable.TypeRef, referenceId);
                                    }
                                    else if (instruction.Operand is TypeDefinition)
                                    {
                                        typeName = (instruction.Operand as TypeDefinition).FullName;
                                        nfToken = new nanoMetadataToken(ClrTable.TypeDef, referenceId);
                                    }

                                    else
                                    {
                                        Debug.Fail("Can't find operand type.");
                                    }

                                    ilDescription.Append($"{typeName}[{nfToken}] /*{realToken}*/");
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineString)
                                {
                                    // strings need a different processing
                                    // get string ID from table
                                    referenceId = _tablesContext.StringTable.GetOrCreateStringId((string)instruction.Operand, true);

                                    ilDescription.Append($"\"{instruction.Operand}\" [{new nanoMetadataToken(ClrTable.Strings, referenceId)}]");
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineSig)
                                {
                                    Debug.Fail("Check this");
                                    ilDescription.Append($"InlineSig ???? /*{realToken}*/");
                                }
                            }

                            // clean-up trailing spaces
                            ilCode.IL = ilDescription.ToString().TrimEnd();

                            methodDef.ILCode.Add(ilCode);
                        }
                    }

                    typeDef.MethodDefinitions.Add(methodDef);
                }

                // list interface implementations
                foreach (var i in t.Interfaces)
                {
                    string realInterfaceToken = i.MetadataToken.ToInt32().ToString("x8");
                    string realInterfaceTypeToken = i.InterfaceType.MetadataToken.ToInt32().ToString("x8");

                    // TODO
                    //_tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(i as TypeDefinition, out ushort interfaceTypeReferenceId);
                    _tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(i.InterfaceType as TypeDefinition, out ushort interfaceTypeReferenceId);
                    ushort interfaceReferenceId = 0xFFFF;

                    typeDef.InterfaceDefinitions.Add(
                        new InterfaceDef()
                        {
                            ReferenceId = $"[{interfaceReferenceId:x4}] /*{realInterfaceToken}*/",
                            Interface = $"[{interfaceTypeReferenceId:x4}] /*{realInterfaceTypeToken}*/"
                        });
                }

                dumpTable.TypeDefinitions.Add(typeDef);
            }
        }

        private void DumpTypeReferences(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeReferencesTable.Items.OrderBy(tr => tr.MetadataToken.ToInt32()))
            {
                string realToken = t.MetadataToken.ToInt32().ToString("x8");

                var typeRef = new TypeRef()
                {
                    Name = t.FullName,
                    // need to add 1 to match the index on the old MDP
                    Scope = $"[{_tablesContext.TypeReferencesTable.GetScope(t):x4}] /*{realToken}*/"
                };

                _tablesContext.TypeReferencesTable.TryGetTypeReferenceId(t, out ushort referenceId);
                typeRef.ReferenceId = $"[{new nanoMetadataToken(t.MetadataToken, referenceId)}] /*{realToken}*/";

                // list member refs               
                foreach(var m in _tablesContext.MethodReferencesTable.Items.Where(mr => mr.DeclaringType == t))
                {
                    var memberRef = new MemberRef()
                    {
                        Name = m.Name
                    };

                    realToken = m.MetadataToken.ToInt32().ToString("x8");

                    _tablesContext.MethodReferencesTable.TryGetMethodReferenceId(m, out referenceId);
                    
                    memberRef.ReferenceId = $"[{new nanoMetadataToken(m.MetadataToken, referenceId)}] /*{realToken}*/";
                    memberRef.Signature = PrintSignatureForMethod(m);

                    typeRef.MemberReferences.Add(memberRef);
                }

                dumpTable.TypeReferences.Add(typeRef);
            }
        }

        private void DumpModuleReferences(DumpAllTable dumpTable)
        {
            throw new NotImplementedException();
        }

        private void DumpAssemblyReferences(DumpAllTable dumpTable)
        {
            foreach(var a in _tablesContext.AssemblyReferenceTable.Items)
            {
                string realToken = a.MetadataToken.ToInt32().ToString("x8");

                var referenceId = _tablesContext.AssemblyReferenceTable.GetReferenceId(a);

                dumpTable.AssemblyReferences.Add(new AssemblyRef()
                {
                    Name = a.Name,
                    // need to add 1 to match the index on the old MDP
                    ReferenceId = $"[{new nanoMetadataToken(a.MetadataToken, referenceId)}] /*{realToken}*/",
                    Flags = "00000000"
                });
            }
        }

        private void DumpTypeSpecifications(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeSpecificationsTable.GetItems())
            {
                string realToken = t.Key.MetadataToken.ToInt32().ToString("x8");

                _tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(t.Key, out ushort referenceId);

                var typeSpec = new TypeSpec();

                // build name
                StringBuilder typeSpecName = new StringBuilder();

                if(t.Key is GenericParameter)
                {
                    var genericParam = t.Key as GenericParameter;

                    typeSpecName.Append(t.Key.MetadataType);

                    if (genericParam.Owner is TypeDefinition)
                    {
                        typeSpecName.Append("!");
                    }
                    if (genericParam.Owner is MethodDefinition)
                    {
                        typeSpecName.Append("!!");
                    }

                    typeSpecName.Append(genericParam.Owner.GenericParameters.IndexOf(genericParam));

                    typeSpecName.Append($" ({genericParam.FullName})");

                    typeSpec.Name = typeSpecName.ToString();

                    // get token
                    if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(t.Key, out ushort refId))
                    {
                        typeSpec.ReferenceId = $"[{new nanoMetadataToken(t.Key.MetadataToken, referenceId)}] /*{realToken}*/";
                    }
                }
                else if (t.Key is GenericInstanceType)
                {
                    // type is a GenericInstance
                    // can't compare with Cecil MetadataToken because the tables have been cleaned-up and re-indexed

                    typeSpec.Name = t.Key.FullName;

                    foreach (var mr in _tablesContext.MethodReferencesTable.Items)
                    {
                        if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(mr.DeclaringType, out referenceId) &&
                            referenceId == t.Value)
                        {
                            // update typeSpec, if needed
                            if (string.IsNullOrEmpty(typeSpec.ReferenceId))
                            {
                                realToken = mr.DeclaringType.MetadataToken.ToInt32().ToString("x8");
                                typeSpec.ReferenceId = $"[{new nanoMetadataToken(mr.DeclaringType.MetadataToken, referenceId)}] /*{realToken}*/";
                            }

                            var memberRef = new MemberRef()
                            {
                                Name = mr.Name
                            };

                            if (_tablesContext.MethodReferencesTable.TryGetMethodReferenceId(mr, out ushort methodRefId))
                            {
                                realToken = mr.MetadataToken.ToInt32().ToString("x8");

                                memberRef.ReferenceId = $"[{new nanoMetadataToken(ClrTable.MethodRef, referenceId)}] /*{realToken}*/";
                                memberRef.Signature = PrintSignatureForMethod(mr);
                            }

                            typeSpec.MemberReferences.Add(memberRef);
                        }
                    }

                    foreach (var ms in _tablesContext.MethodSpecificationTable.Items)
                    {
                        if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(ms.DeclaringType, out referenceId) &&
                            referenceId == t.Value)
                        {
                            // update typeSpec, if needed
                            if (string.IsNullOrEmpty(typeSpec.ReferenceId))
                            {
                                realToken = ms.DeclaringType.MetadataToken.ToInt32().ToString("x8");
                                typeSpec.ReferenceId = $"[{new nanoMetadataToken(ms.DeclaringType.MetadataToken, referenceId)}] /*{realToken}*/";
                            }

                            var memberRef = new MemberRef()
                            {
                                Name = ms.Name
                            };

                            if (_tablesContext.MethodSpecificationTable.TryGetMethodSpecificationId(ms, out ushort methodSpecId))
                            {
                                realToken = ms.MetadataToken.ToInt32().ToString("x8");

                                memberRef.ReferenceId = $"[{new nanoMetadataToken(ms.MetadataToken, referenceId)}] /*{realToken}*/"; ;
                                memberRef.Signature = PrintSignatureForMethod(ms);
                            }

                            typeSpec.MemberReferences.Add(memberRef);
                        }
                    }

                    Debug.Assert(typeSpec.MemberReferences.Count > 0, $"Couldn't find any MethodRef for TypeSpec[{t.Value}] {t.Key.FullName}");
                }

                dumpTable.TypeSpecifications.Add(typeSpec);
            }
        }

        private string PrintSignatureForMethod(MethodReference method)
        {
            var sig = new StringBuilder(method.ReturnType.TypeSignatureAsString());

            sig.Append("( ");

            foreach(var p in method.Parameters)
            {
                sig.Append(p.ParameterType.TypeSignatureAsString());
                sig.Append(", ");
            }

            // remove trailing", "
            if (method.Parameters.Count > 0)
            {
                sig.Remove(sig.Length - 2, 2);
            }
            else
            {
                sig.Append(" ");
            }

            sig.Append(" )");

            return sig.ToString();
        }

        private string PrintSignatureForLocalVar(Collection<VariableDefinition> variables)
        {
            StringBuilder sig = new StringBuilder();
            sig.Append("( ");

            foreach (var l in variables)
            {
                sig.Append($"[{l.Index}] ");
                sig.Append(l.VariableType.TypeSignatureAsString());
                sig.Append(", ");
            }

            // remove trailing", "
            if (variables.Count > 0)
            {
                sig.Remove(sig.Length - 2, 2);
            }
            else
            {
                sig.Append(" ");
            }

            sig.Append(" )");

            return sig.ToString();
        }
    }
}
