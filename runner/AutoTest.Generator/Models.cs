using System.Text.Json;

namespace AutoTest.Generator;

public sealed record GeneratorAnalysis(string SourceDirectory, int ScannedFileCount, IReadOnlyList<EndpointContract> Endpoints, IReadOnlyDictionary<string, TypeContract> Types, IReadOnlyList<string> Warnings);
public sealed record EndpointContract(string Id, string Controller, string Action, string Method, string Path, string SourceFile, int SourceLine, bool RequiresAuthentication, bool AllowsAnonymous, IReadOnlyList<string> Permissions, IReadOnlyList<int> ResponseStatuses, string? ResponseType, IReadOnlyList<ParameterContract> Parameters, IReadOnlyList<ValidationContract> Validations, IReadOnlyList<ServiceCallContract> ServiceCalls, IReadOnlyList<BranchContract> Branches);
public sealed record ParameterContract(string Name, string Type, string Source, bool Required, bool Nullable);
public sealed record ValidationContract(string Property, string Rule, string? Value, string SourceFile);
public sealed record TypeContract(string Name, string Kind, int GenericArity, IReadOnlyList<string> TypeParameters, IReadOnlyList<string> BaseTypes, IReadOnlyList<PropertyContract> Properties, IReadOnlyList<string> EnumValues, string SourceFile);
public sealed record PropertyContract(string Name, string JsonName, string Type, bool Required, bool Nullable, bool IsCollection, string? ItemType);
public sealed record ServiceCallContract(string ContainingType, string Method, string ReturnType, string SourceFile, int SourceLine);
public sealed record BranchContract(string Kind, string Condition, string SourceFile, int SourceLine, string OwnerMethod, bool Covered = false);
public sealed record CoverageAnalysis(string ReportPath, int CoveredLines, int TotalLines, decimal LineRate, int CoveredBranches, int TotalBranches, decimal BranchRate, IReadOnlyList<CoverageGap> Gaps);
public sealed record CoverageGap(string SourceFile, string Method, int Line, string Kind);
public sealed record GeneratedSuite(int SchemaVersion, string Project, bool GeneratedDraft, bool Disabled, IReadOnlyList<object> Cases);

public static class GeneratorJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
