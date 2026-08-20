using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AutoTest.MessageScanner;

public sealed record MessageEntry(
    string Key,
    string Module,
    string? Vietnamese,
    string? English,
    IReadOnlyList<string> SourceFiles);

public sealed record MessageScanResult(
    string SourceDirectory,
    IReadOnlyList<MessageEntry> Messages,
    int ScannedFileCount,
    IReadOnlyList<string> SkippedFiles);

public sealed class SourceMessageScanner
{
    private static readonly Regex MessageRegex = new(
        @"\bMes\.[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex GeneratedMessageRegex = new(
        @"Messages\s*<\s*(?<type>[A-Za-z_][A-Za-z0-9_]*(?:<[^>]+>)?)\s*>\s*\.\s*(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>[^;]*?)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex DisplayNameRegex = new(
        @"\[\s*MessageDisplay\s*\(\s*""(?<name>[^""]+)""\s*\)\s*\]\s*(?:public\s+|internal\s+)?(?:sealed\s+|abstract\s+|partial\s+)*(?:class|record)\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", ".vscode", "bin", "obj", "node_modules", "packages",
        "dist", "build", "coverage", "test-results", ".next", ".nuxt", "vendor"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cshtml", ".razor", ".json", ".resx", ".xml", ".yml", ".yaml",
        ".ts", ".tsx", ".js", ".jsx", ".vue", ".properties", ".txt", ".md"
    };

    public MessageScanResult Scan(string sourceDirectory, CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục source: {root}");

        var occurrences = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var translations = new Dictionary<string, Translation>(StringComparer.Ordinal);
        var sourceTexts = new List<(string File, string Relative, string Text)>();
        var skipped = new List<string>();
        int scanned = 0;

        foreach (string file in EnumerateFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                if (info.Length > 10 * 1024 * 1024)
                {
                    skipped.Add(Relative(root, file));
                    continue;
                }

                string text = File.ReadAllText(file);
                scanned++;
                string relative = Relative(root, file);
                sourceTexts.Add((file, relative, text));
                foreach (Match match in MessageRegex.Matches(text))
                {
                    if (!occurrences.TryGetValue(match.Value, out var files))
                        occurrences[match.Value] = files = new(StringComparer.OrdinalIgnoreCase);
                    files.Add(relative);
                }

                ReadTranslations(file, text, translations);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                skipped.Add(Relative(root, file));
            }
        }

        var displayNames = ReadDisplayNames(sourceTexts);
        foreach (var source in sourceTexts.Where(item => Path.GetExtension(item.File).Equals(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (string key in InferGeneratedMessages(source.Text, displayNames))
            {
                if (!occurrences.TryGetValue(key, out var files))
                    occurrences[key] = files = new(StringComparer.OrdinalIgnoreCase);
                files.Add(source.Relative);
            }
        }

        var keys = occurrences.Keys.Concat(translations.Keys).Distinct(StringComparer.Ordinal);
        var messages = keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key =>
            {
                translations.TryGetValue(key, out var translation);
                occurrences.TryGetValue(key, out var files);
                return new MessageEntry(
                    key,
                    GetModule(key),
                    translation?.Vietnamese,
                    translation?.English,
                    (files ?? []).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
            })
            .ToArray();

        return new(root, messages, scanned, skipped);
    }

    private static Dictionary<string, string> ReadDisplayNames(IEnumerable<(string File, string Relative, string Text)> sources)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
            foreach (Match match in DisplayNameRegex.Matches(source.Text))
                names[match.Groups["type"].Value] = match.Groups["name"].Value;
        return names;
    }

    private static IEnumerable<string> InferGeneratedMessages(string text, IReadOnlyDictionary<string, string> displayNames)
    {
        foreach (Match match in GeneratedMessageRegex.Matches(text))
        {
            string rawType = match.Groups["type"].Value;
            string type = rawType.Split('<')[0];
            string module = displayNames.TryGetValue(type, out string? displayed) ? displayed : type;
            string method = match.Groups["method"].Value;
            string arguments = match.Groups["args"].Value.Trim();
            string? suffix = method switch
            {
                "Create" or "Update" or "Detail" or "List" or "Search" or "Delete" or "Import" or
                "Assign" or "Unassign" or "SendMail" or "View" => InferOperationSuffix(method, arguments),
                "Action" => InferActionSuffix(arguments),
                _ => InferValidationSuffix(method, arguments)
            };
            if (!string.IsNullOrWhiteSpace(suffix)) yield return $"Mes.{module}.{suffix}";
        }
    }

    private static string InferOperationSuffix(string method, string arguments)
    {
        bool failed = Regex.IsMatch(arguments, @"(?:^|,)\s*false\s*(?:,|$)", RegexOptions.IgnoreCase);
        string action = method;
        if (method == "View")
        {
            string? explicitAction = ExtractName(arguments);
            action = explicitAction ?? method;
        }
        else
        {
            string? extra = ExtractName(arguments.Split(',')[0]);
            if (extra is not null && !extra.Equals("true", StringComparison.OrdinalIgnoreCase) && !extra.Equals("false", StringComparison.OrdinalIgnoreCase))
                action += extra;
        }
        return $"{action}.{(failed ? "Failed" : "Successfully")}";
    }

    private static string? InferActionSuffix(string arguments)
    {
        string[] parts = arguments.Split(',', 2);
        string? action = ExtractName(parts[0]);
        if (action is null) return null;
        if (parts.Length == 1) return action;
        bool success = !parts[1].Trim().StartsWith("false", StringComparison.OrdinalIgnoreCase);
        return $"{action}.{(success ? "Successfully" : "Failed")}";
    }

    private static string? InferValidationSuffix(string method, string arguments)
    {
        var validationMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "NotAllowed", "Blocked", "WasUsed", "NotFound", "Repeated", "Invalid", "MustBeEmpty",
            "Required", "OverLength", "NotEnoughLength", "NotWhiteSpace", "NotSpecialCharacter",
            "AlreadyExist", "Expired", "NotEqual"
        };
        if (!validationMethods.Contains(method)) return null;
        string? property = ExtractName(arguments);
        return property is null ? method : $"{method}.{property}";
    }

    private static string? ExtractName(string expression)
    {
        expression = expression.Trim();
        if (expression.Length == 0) return null;
        Match literal = Regex.Match(expression, "\\\"(?<value>[^\\\"]+)\\\"");
        if (literal.Success) return literal.Groups["value"].Value.Split('.').Last();
        Match nameOf = Regex.Match(expression, @"nameof\s*\(\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)*(?<value>[A-Za-z_][A-Za-z0-9_]*)\s*\)");
        if (nameOf.Success) return nameOf.Groups["value"].Value;
        Match lambda = Regex.Match(expression, @"=>\s*[A-Za-z_][A-Za-z0-9_]*\.(?<value>[A-Za-z_][A-Za-z0-9_]*)");
        if (lambda.Success) return lambda.Groups["value"].Value;
        Match member = Regex.Match(expression, @"(?:ControllerActions|MessagesType)\.(?<value>[A-Za-z_][A-Za-z0-9_]*)");
        return member.Success ? member.Groups["value"].Value : null;
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            IEnumerable<string> directories;
            IEnumerable<string> files;
            try
            {
                directories = Directory.EnumerateDirectories(directory).ToArray();
                files = Directory.EnumerateFiles(directory).ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string child in directories)
                if (!IgnoredDirectories.Contains(Path.GetFileName(child)))
                    pending.Push(child);

            foreach (string file in files)
                if (TextExtensions.Contains(Path.GetExtension(file)))
                    yield return file;
        }
    }

    private static void ReadTranslations(
        string file,
        string text,
        Dictionary<string, Translation> translations)
    {
        string extension = Path.GetExtension(file);
        string language = DetectLanguage(file);
        if (extension.Equals(".resx", StringComparison.OrdinalIgnoreCase))
        {
            var document = XDocument.Parse(text, LoadOptions.None);
            foreach (var data in document.Descendants("data"))
            {
                string? key = data.Attribute("name")?.Value;
                string? value = data.Element("value")?.Value;
                SetTranslation(key, value, language, translations);
            }
            return;
        }

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            ReadJson(document.RootElement, null, language, translations);
            return;
        }

        if (extension.Equals(".properties", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                int separator = trimmed.IndexOf('=');
                if (separator > 0)
                    SetTranslation(trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim(), language, translations);
            }
        }
    }

    private static void ReadJson(
        JsonElement element,
        string? prefix,
        string language,
        Dictionary<string, Translation> translations)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string key = prefix is null ? property.Name : $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.String)
                SetTranslation(key, property.Value.GetString(), language, translations);
            else if (property.Value.ValueKind == JsonValueKind.Object)
                ReadJson(property.Value, key, language, translations);
        }
    }

    private static void SetTranslation(
        string? key,
        string? value,
        string language,
        Dictionary<string, Translation> translations)
    {
        if (key is null || value is null || !MessageRegex.IsMatch(key)) return;
        translations.TryGetValue(key, out var current);
        current ??= new();
        if (language == "vi") current.Vietnamese ??= value;
        else if (language == "en") current.English ??= value;
        translations[key] = current;
    }

    private static string DetectLanguage(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        if (Regex.IsMatch(name, @"(^|[._-])(vi|vi-vn)([._-]|$)") || normalized.Contains("/vi/")) return "vi";
        if (Regex.IsMatch(name, @"(^|[._-])(en|en-us)([._-]|$)") || normalized.Contains("/en/")) return "en";
        return "unknown";
    }

    private static string GetModule(string key)
    {
        string[] parts = key.Split('.');
        return parts.Length > 1 ? parts[1] : "Other";
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');

    private sealed class Translation
    {
        public string? Vietnamese { get; set; }
        public string? English { get; set; }
    }
}
