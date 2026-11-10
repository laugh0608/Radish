using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NuGet.Packaging;

namespace Radish.HangfireMetadata;

internal static class Program
{
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static int Main(string[] args)
    {
        object report = args[0] switch
        {
            "packages" => Directory.GetFiles(args[1], "*.nupkg").Order().Select(Package).ToArray(),
            "assembly" => Inspect(args[1]),
            _ => throw new ArgumentException("Expected packages or assembly mode")
        };
        File.WriteAllText(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return 0;
    }

    private static object Package(string file)
    {
        using var package = new PackageArchiveReader(file);
        var identity = package.GetIdentity();
        return new { package = identity.Id.ToLowerInvariant(), version = identity.Version.ToNormalizedString(), contentHash = package.GetContentHash(CancellationToken.None), sha256 = Hash(File.ReadAllBytes(file)) };
    }

    private static object Inspect(string file)
    {
        using var stream = File.OpenRead(file);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();
        var names = new SignatureNames(reader);
        var api = new List<string>();
        var bodies = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = names.Type(handle);
            if (Visible(reader, handle))
            {
                api.Add($"type {name} {(int)type.Attributes} : {names.Type(type.BaseType)}");
                foreach (var item in type.GetInterfaceImplementations()) api.Add($"interface {name} : {names.Type(reader.GetInterfaceImplementation(item).Interface)}");
                foreach (var parameter in type.GetGenericParameters()) api.Add(Generic(reader, parameter, name, names));
            }
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                var signature = method.DecodeSignature(names, (object?)null);
                var methodName = $"{name}.{reader.GetString(method.Name)}`{signature.GenericParameterCount}({string.Join(",", signature.ParameterTypes)}):{signature.ReturnType}";
                if (Visible(reader, handle) && (method.Attributes & MethodAttributes.MemberAccessMask) is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem)
                {
                    api.Add($"method {methodName} {(int)method.Attributes} {(int)method.ImplAttributes}");
                    foreach (var parameterHandle in method.GetParameters())
                    {
                        var parameter = reader.GetParameter(parameterHandle);
                        var constant = parameter.GetDefaultValue();
                        api.Add($"parameter {methodName} #{parameter.SequenceNumber} {reader.GetString(parameter.Name)} {(int)parameter.Attributes} {Constant(reader, constant)}");
                    }
                    foreach (var parameter in method.GetGenericParameters()) api.Add(Generic(reader, parameter, methodName, names));
                }
                if (method.RelativeVirtualAddress != 0)
                {
                    var body = pe.GetMethodBody(method.RelativeVirtualAddress);
                    // Replay uses the same compiler/input order. Keep raw IL fingerprints distinct from API/resource proof.
                    if (!bodies.TryGetValue(methodName, out var hashes)) bodies[methodName] = hashes = [];
                    hashes.Add(Hash(body.GetILBytes() ?? []));
                }
            }
            if (!Visible(reader, handle)) continue;
            foreach (var fieldHandle in type.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                if ((field.Attributes & FieldAttributes.FieldAccessMask) is FieldAttributes.Public or FieldAttributes.Family or FieldAttributes.FamORAssem)
                    api.Add($"field {name}.{reader.GetString(field.Name)}:{field.DecodeSignature(names, (object?)null)} {(int)field.Attributes} {Constant(reader, field.GetDefaultValue())}");
            }
            foreach (var propertyHandle in type.GetProperties())
            {
                var property = reader.GetPropertyDefinition(propertyHandle);
                var accessors = property.GetAccessors();
                if (!PublicMethod(reader, accessors.Getter) && !PublicMethod(reader, accessors.Setter)) continue;
                var signature = property.DecodeSignature(names, (object?)null);
                api.Add($"property {name}.{reader.GetString(property.Name)}({string.Join(",", signature.ParameterTypes)}):{signature.ReturnType} {(int)property.Attributes} {Constant(reader, property.GetDefaultValue())}");
            }
            foreach (var eventHandle in type.GetEvents())
            {
                var item = reader.GetEventDefinition(eventHandle);
                var accessors = item.GetAccessors();
                if (PublicMethod(reader, accessors.Adder) || PublicMethod(reader, accessors.Remover)) api.Add($"event {name}.{reader.GetString(item.Name)}:{names.Type(item.Type)} {(int)item.Attributes}");
            }
        }
        var resources = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var handle in reader.ManifestResources)
        {
            var resource = reader.GetManifestResource(handle);
            if (!resource.Implementation.IsNil) throw new InvalidDataException("Linked resource requires explicit review");
            var data = pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress).GetReader((int)resource.Offset, sizeof(int));
            var size = data.ReadInt32();
            var bytes = pe.GetSectionData(pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress).GetContent((int)resource.Offset + 4, size).ToArray();
            resources.Add(reader.GetString(resource.Name), new { size, sha256 = Hash(bytes) });
        }
        var documents = new List<object>();
        var links = new List<string>();
        var debugEntries = pe.ReadDebugDirectory();
        void ReadPdb(MetadataReader pdb)
        {
            foreach (var handle in pdb.CustomDebugInformation)
            {
                var item = pdb.GetCustomDebugInformation(handle);
                if (pdb.GetGuid(item.Kind) == new Guid("cc110556-a091-4d38-9fec-25ab9a351a6a")) links.Add(Encoding.UTF8.GetString(pdb.GetBlobBytes(item.Value)));
            }
            foreach (var handle in pdb.Documents)
            {
                var document = pdb.GetDocument(handle);
                string? embeddedHash = null;
                foreach (var custom in pdb.GetCustomDebugInformation(handle))
                {
                    var item = pdb.GetCustomDebugInformation(custom);
                    if (pdb.GetGuid(item.Kind) != new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe")) continue;
                    var bytes = pdb.GetBlobBytes(item.Value);
                    using var input = new MemoryStream(bytes, 4, bytes.Length - 4);
                    using var output = new MemoryStream();
                    if (BitConverter.ToInt32(bytes, 0) == 0) input.CopyTo(output);
                    else { using var inflater = new DeflateStream(input, CompressionMode.Decompress); inflater.CopyTo(output); }
                    embeddedHash = Hash(output.ToArray());
                    if (BitConverter.ToInt32(bytes, 0) > 0 && output.Length != BitConverter.ToInt32(bytes, 0)) throw new InvalidDataException("Embedded source length mismatch");
                }
                documents.Add(new { path = pdb.GetString(document.Name), algorithm = document.HashAlgorithm.IsNil ? null : pdb.GetGuid(document.HashAlgorithm).ToString(), checksum = Convert.ToHexStringLower(pdb.GetBlobBytes(document.Hash)), embeddedSha256 = embeddedHash });
            }
        }
        foreach (var entry in debugEntries.Where(entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb))
        {
            using var provider = pe.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
            ReadPdb(provider.GetMetadataReader());
        }
        var adjacentPdb = Path.ChangeExtension(file, ".pdb");
        if (!debugEntries.Any(entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb) && File.Exists(adjacentPdb))
        {
            using var provider = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(adjacentPdb));
            var pdb = provider.GetMetadataReader();
            var id = pdb.DebugMetadataHeader!.Id;
            var entry = debugEntries.Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
            var codeView = pe.ReadCodeViewDebugDirectoryData(entry);
            if (id.Length != 20 || new Guid(id.AsSpan(0, 16)) != codeView.Guid || BitConverter.ToUInt32(id.AsSpan(16, 4)) != entry.Stamp)
                throw new InvalidDataException("Adjacent portable PDB identity mismatch");
            foreach (var checksum in debugEntries.Where(entry => entry.Type == DebugDirectoryEntryType.PdbChecksum))
            {
                var value = pe.ReadPdbChecksumDebugDirectoryData(checksum);
                // PE-COFF Portable PDB checksum zeroes the 20-byte PDB ID to avoid a circular hash.
                var content = File.ReadAllBytes(adjacentPdb);
                Array.Clear(content, pdb.DebugMetadataHeader.IdStartOffset, 20);
                if (value.AlgorithmName != "SHA256" || !SHA256.HashData(content).SequenceEqual(value.Checksum))
                    throw new InvalidDataException("Adjacent PDB checksum mismatch");
            }
            ReadPdb(pdb);
        }
        return new { file, name = reader.GetString(assembly.Name), version = assembly.Version.ToString(), publicKey = Convert.ToHexStringLower(reader.GetBlobBytes(assembly.PublicKey)), sha256 = Hash(File.ReadAllBytes(file)), mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid), references = reader.AssemblyReferences.Select(handle => { var item = reader.GetAssemblyReference(handle); return new { name = reader.GetString(item.Name), version = item.Version.ToString(), publicKeyToken = Convert.ToHexStringLower(reader.GetBlobBytes(item.PublicKeyOrToken)) }; }).ToArray(), api = api.Order(StringComparer.Ordinal).ToArray(), resources, bodies, sourceLinks = links, documents, debugTypes = debugEntries.Select(entry => entry.Type.ToString()).ToArray() };
    }

    private static bool Visible(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var visibility = type.Attributes & TypeAttributes.VisibilityMask;
        return visibility == TypeAttributes.Public || visibility is TypeAttributes.NestedPublic or TypeAttributes.NestedFamily or TypeAttributes.NestedFamORAssem && Visible(reader, type.GetDeclaringType());
    }
    private static bool PublicMethod(MetadataReader reader, MethodDefinitionHandle handle) => !handle.IsNil && (reader.GetMethodDefinition(handle).Attributes & MethodAttributes.MemberAccessMask) is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem;
    private static string Constant(MetadataReader reader, ConstantHandle handle) => handle.IsNil ? "none" : $"{reader.GetConstant(handle).TypeCode}:{Convert.ToHexStringLower(reader.GetBlobBytes(reader.GetConstant(handle).Value))}";
    private static string Generic(MetadataReader reader, GenericParameterHandle handle, string owner, SignatureNames names)
    {
        var item = reader.GetGenericParameter(handle);
        return $"generic {owner} #{item.Index} {(int)item.Attributes} {string.Join(",", item.GetConstraints().Select(constraint => names.Type(reader.GetGenericParameterConstraint(constraint).Type)).Order(StringComparer.Ordinal))}";
    }
}

internal sealed class SignatureNames(MetadataReader reader) : ISignatureTypeProvider<string, object?>
{
    public string Type(EntityHandle handle) => handle.Kind switch
    {
        _ when handle.IsNil => "none",
        HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
        HandleKind.TypeSpecification => GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0),
        _ => throw new InvalidDataException("Unexpected type handle " + handle.Kind)
    };
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte kind)
    {
        var item = r.GetTypeDefinition(h);
        return item.GetDeclaringType().IsNil ? r.GetString(item.Namespace) + "." + r.GetString(item.Name) : Type(item.GetDeclaringType()) + "+" + r.GetString(item.Name);
    }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte kind)
    {
        var item = r.GetTypeReference(h);
        var scope = item.ResolutionScope.Kind == HandleKind.AssemblyReference ? r.GetString(r.GetAssemblyReference((AssemblyReferenceHandle)item.ResolutionScope).Name) : item.ResolutionScope.Kind == HandleKind.TypeReference ? Type(item.ResolutionScope) : "module";
        return $"[{scope}]{r.GetString(item.Namespace)}.{r.GetString(item.Name)}";
    }
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
    public string GetArrayType(string element, ArrayShape shape) => $"{element}[rank={shape.Rank};sizes={string.Join(',', shape.Sizes)};lower={string.Join(',', shape.LowerBounds)}]";
    public string GetByReferenceType(string element) => element + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => $"fn({string.Join(',', signature.ParameterTypes)}):{signature.ReturnType}";
    public string GetGenericInstantiation(string type, ImmutableArray<string> arguments) => $"{type}<{string.Join(',', arguments)}>";
    public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? context, int index) => "!" + index;
    public string GetModifiedType(string modifier, string type, bool required) => $"{type} mod{(required ? "req" : "opt")}({modifier})";
    public string GetPinnedType(string type) => type + " pinned";
    public string GetPointerType(string type) => type + "*";
    public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
    public string GetSZArrayType(string type) => type + "[]";
}
