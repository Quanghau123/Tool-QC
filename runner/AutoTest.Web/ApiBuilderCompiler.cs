using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoTest.Web;

public static class ApiBuilderCompiler
{
    public static string Compile(ApiBuilderWorkspace workspace, string caseId)
    {
        if (workspace.Steps.Count == 0)
            throw new InvalidDataException("Workspace chưa có step để biên dịch.");

        JsonArray steps = new();
        foreach (ApiBuilderStep step in workspace.Steps)
            steps.Add(CompileStep(workspace, step));

        JsonObject suite = new()
        {
            ["schemaVersion"] = 2,
            ["project"] = workspace.Project,
            ["cases"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = caseId,
                    ["name"] = "Chuỗi API tạo từ API Builder",
                    ["tags"] = new JsonArray("api-builder"),
                    ["destructive"] = workspace.Steps.Any(IsMutation),
                    ["coverage"] = Coverage(workspace, workspace.Steps.Select(step => step.Name).ToArray()),
                    ["steps"] = steps
                }
            }
        };
        return suite.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    private static JsonObject CompileStep(ApiBuilderWorkspace workspace, ApiBuilderStep step)
    {
        Dictionary<string, string> values = Values(workspace, step);
        JsonObject request = new()
        {
            ["method"] = step.Method,
            ["path"] = Path(step, values)
        };
        JsonObject body = new();
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (ApiBuilderField field in step.RequestFields)
        {
            string location = Location(step, field);
            string value = values[field.Id ?? field.Name];
            if (location == "body")
                body[field.Name] = value;
            else if (location == "header")
                headers[field.Name] = value;
        }
        if (body.Count > 0)
            request["body"] = body;
        if (headers.Count > 0)
            request["headers"] = JsonSerializer.SerializeToNode(headers);

        Dictionary<string, string> saved = workspace.Variables
            .Where(variable => variable.SourceStep.Equals(step.Name, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(variable => variable.Name, variable => variable.JsonPath, StringComparer.OrdinalIgnoreCase);
        foreach (string jsonPath in saved.Values)
            if (!step.ResponseFields.Any(field => field.JsonPath.Equals(jsonPath, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Biến của step '{step.Name}' không khớp field phản hồi.");

        JsonObject result = new()
        {
            ["name"] = step.Name,
            ["request"] = request,
            ["expect"] = new JsonObject { ["status"] = 200 }
        };
        if (saved.Count > 0)
            result["save"] = JsonSerializer.SerializeToNode(saved);
        return result;
    }

    private static Dictionary<string, string> Values(ApiBuilderWorkspace workspace, ApiBuilderStep step)
    {
        Dictionary<string, string> values = step.RequestFields.ToDictionary(field => field.Id ?? field.Name, field => field.Value, StringComparer.Ordinal);
        foreach (ApiBuilderBinding binding in workspace.Bindings?.Where(binding => binding.TargetStep.Equals(step.Name, StringComparison.OrdinalIgnoreCase)) ?? [])
            values[binding.TargetFieldId] = $"${{{binding.SourceVariable}}}";
        return values;
    }

    private static string Path(ApiBuilderStep step, IReadOnlyDictionary<string, string> values)
    {
        string path = step.Path;
        foreach (ApiBuilderField field in step.RequestFields.Where(field => Location(step, field) == "path"))
            path = path.Replace($"{{{field.Name}}}", values[field.Id ?? field.Name], StringComparison.OrdinalIgnoreCase);
        string query = string.Join("&", step.RequestFields.Where(field => Location(step, field) == "query").Select(field => $"{field.Name}={values[field.Id ?? field.Name]}"));
        return query.Length == 0 ? path : $"{path}?{query}";
    }

    private static string Location(ApiBuilderStep step, ApiBuilderField field)
        => field.Location?.ToLowerInvariant() ?? (step.Path.Contains($"{{{field.Name}}}", StringComparison.OrdinalIgnoreCase) ? "path" : "body");

    private static JsonObject Coverage(ApiBuilderWorkspace workspace, string[] stepNames)
    {
        return new JsonObject
        {
            ["sourceVerification"] = new JsonObject
            {
                ["files"] = new JsonArray(workspace.SourceFingerprint ?? "API Builder chưa có source provenance"),
                ["verifiedAt"] = workspace.UpdatedAt.ToString("O"),
                ["contractVersion"] = "api-builder-v1"
            },
            ["contract"] = Covered(stepNames),
            ["validation"] = Blocked("Cần bổ sung testcase validation từ contract đã kiểm chứng."),
            ["authentication"] = Blocked("Cần chọn auth profile và testcase xác thực."),
            ["scope"] = Blocked("Cần fixture scope khác để xác nhận phân quyền."),
            ["integrity"] = workspace.Steps.Any(IsMutation) ? Blocked("Mutation cần assertion side-effect trước khi publish.") : NotApplicable("Chuỗi chỉ đọc."),
            ["lifecycle"] = NotApplicable("API Builder chưa có metadata lifecycle."),
            ["concurrency"] = NotApplicable("Chuỗi chạy tuần tự."),
            ["reporting"] = Covered(stepNames)
        };
    }

    private static JsonObject Covered(string[] stepNames) => new()
    {
        ["status"] = "covered",
        ["evidenceSteps"] = JsonSerializer.SerializeToNode(stepNames)
    };
    private static JsonObject Blocked(string reason) => new() { ["status"] = "blocked", ["reason"] = reason };
    private static JsonObject NotApplicable(string reason) => new() { ["status"] = "notApplicable", ["reason"] = reason };
    private static bool IsMutation(ApiBuilderStep step) => step.Method is not "GET" and not "HEAD";
}
