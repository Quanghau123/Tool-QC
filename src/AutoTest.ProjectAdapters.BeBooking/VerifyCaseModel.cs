namespace AutoTest.ProjectAdapters.BeBooking;

public sealed record VerifyCaseModel(
    string Id,
    string Title,
    IReadOnlyList<VerifyCampaignModel> Campaigns,
    VerifyExpectation Expectation,
    string SyncDirection,
    int SourceRow,
    VerifySourceManifest? Source = null,
    IReadOnlyList<VerifyCaseDiagnostic>? Diagnostics = null);

public sealed record VerifySourceManifest(
    string Title,
    string Range,
    IReadOnlyList<VerifyDayTableManifest> DayTables,
    string? Direction,
    string SemanticClass);

public sealed record VerifyDayTableManifest(
    string DateKey,
    int StartRow,
    int EndRow,
    IReadOnlyList<string> FrameLabels);

public sealed record VerifyCaseDiagnostic(string Code, string Field, string Message, string Range);

public sealed record VerifyCampaignModel(
    string Name,
    string Kind,
    int CreationOrder,
    IReadOnlyList<VerifyDailySchedule> DailySchedules);

public sealed record VerifyDailySchedule(
    string DateKey,
    int Requested,
    IReadOnlyList<string> FrameLabels,
    IReadOnlyDictionary<string, int> PerFrame,
    IReadOnlyList<VerifyDurationGroup> Groups);

public sealed record VerifyDurationGroup(
    int Requested,
    int? Maximum,
    IReadOnlyList<string> FrameLabels,
    IReadOnlyDictionary<string, int> PerFrame);

public sealed record VerifyExpectation(bool IsValid, string? TesterResult, bool IsCandidateSpot);
