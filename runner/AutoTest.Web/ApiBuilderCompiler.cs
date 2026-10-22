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
        JsonObject request = new()
        {
            ["method"] = step.Method,
            ["path"] = Path(step)
        };
        JsonObject body = new();
        foreach (ApiBuilderField field in step.RequestFields.Where(field => !step.Path.Contains($"{{{field.Name}}}", StringComparison.OrdinalIgnoreCase)))
            body[field.Name] = field.Value;
        if (body.Count > 0)
            request["body"] = body;

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

    private static string Path(ApiBuilderStep step)
    {
        string path = step.Path;
        foreach (ApiBuilderField field in step.RequestFields)
            path = path.Replace($"{{{field.Name}}}", field.Value, StringComparison.OrdinalIgnoreCase);
        return path;
    }

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
