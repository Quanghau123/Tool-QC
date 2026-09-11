using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace AutoTest.Generator;

// Phân tích cú pháp C# để lấy contract có bằng chứng trực tiếp từ controller, DTO và validator.
public sealed class SourceContractAnalyzer
{
    private static readonly HashSet<string> HttpAttributes = new(StringComparer.OrdinalIgnoreCase) { "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch" };

    public GeneratorAnalysis Analyze(string sourceDirectory)
    {
        string root = Path.GetFullPath(sourceDirectory);
        string[] files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(path => IsSourceFile(root, path)).ToArray();
        List<string> warnings = [];
        List<SyntaxTree> trees = [];
        List<CompilationUnitSyntax> roots = [];
        foreach (string file in files)
        {
            try { SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file); trees.Add(tree); roots.Add(tree.GetCompilationUnitRoot()); }
            catch (Exception exception) { warnings.Add($"Cannot parse {Path.GetRelativePath(root, file)}: {exception.Message}"); }
        }

        CSharpCompilation compilation = CreateCompilation(trees);
        Dictionary<string, TypeContract> types = ReadTypes(roots, root);
        Dictionary<string, List<ValidationContract>> validations = ReadValidations(roots, compilation, root);
        Dictionary<string, string> inheritedRoutes = ReadInheritedRoutes(roots);
        Dictionary<string, List<ClassDeclarationSyntax>> controllerParts = new(StringComparer.OrdinalIgnoreCase);
        foreach (CompilationUnitSyntax syntax in roots)
            foreach (ClassDeclarationSyntax controller in syntax.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(IsController))
            {
                string key = ControllerKey(controller);
                if (!controllerParts.TryGetValue(key, out List<ClassDeclarationSyntax>? parts)) controllerParts[key] = parts = [];
                parts.Add(controller);
            }

        List<EndpointContract> endpoints = [];
        foreach (List<ClassDeclarationSyntax> parts in controllerParts.Values)
        {
            string prefix = parts.Select(ControllerRoute).FirstOrDefault(route => route is not null) ?? InheritedRoute(parts, inheritedRoutes) ?? string.Empty;
            bool authorize = parts.Any(part => HasAttribute(part.AttributeLists, "Authorize"));
            foreach (ClassDeclarationSyntax controller in parts) endpoints.AddRange(ReadController(root, controller, validations, prefix, authorize, compilation));
        }
        EndpointContract[] ordered = endpoints.OrderBy(endpoint => endpoint.Path).ThenBy(endpoint => endpoint.Method).ToArray();
        return new(root, files.Length, ordered, types, warnings, DiscoverAuthentication(ordered, types));
    }

    private static IEnumerable<EndpointContract> ReadController(string root, ClassDeclarationSyntax controller, Dictionary<string, List<ValidationContract>> validations, string prefix, bool controllerAuthorize, CSharpCompilation compilation)
    {
        string controllerName = controller.Identifier.ValueText;
        foreach (MethodDeclarationSyntax action in controller.Members.OfType<MethodDeclarationSyntax>())
        {
            AttributeSyntax? http = action.AttributeLists.SelectMany(list => list.Attributes).FirstOrDefault(attribute => HttpAttributes.Contains(AttributeName(attribute)));
            if (http is null) continue;
            string method = AttributeName(http)[4..].ToUpperInvariant();
            string path = CombineRoute(prefix, FirstStringArgument(http) ?? string.Empty, controllerName);
            bool anonymous = HasAttribute(action.AttributeLists, "AllowAnonymous");
            bool authorize = !anonymous && (controllerAuthorize || HasAttribute(action.AttributeLists, "Authorize") || HasAttribute(action.AttributeLists, "HasPermission"));
            string[] permissions = action.AttributeLists.SelectMany(list => list.Attributes).Where(attribute => AttributeName(attribute).Equals("HasPermission", StringComparison.OrdinalIgnoreCase)).Select(attribute => attribute.ToString()).ToArray();
            int[] statuses = action.AttributeLists.SelectMany(list => list.Attributes).Where(attribute => AttributeName(attribute).Equals("ProducesResponseType", StringComparison.OrdinalIgnoreCase)).Select(ParseStatus).Where(status => status > 0).Distinct().Order().ToArray();
            ParameterContract[] parameters = action.ParameterList.Parameters.Where(parameter => !IsInfrastructureParameter(parameter)).Select(ReadParameter).ToArray();
            List<ValidationContract> rules = parameters.Where(parameter => parameter.Source is "body" or "query").SelectMany(parameter => validations.GetValueOrDefault(CleanType(parameter.Type)) ?? []).Distinct().ToList();
            FileLinePositionSpan span = action.SyntaxTree.GetLineSpan(action.Identifier.Span);
            SemanticModel semanticModel = compilation.GetSemanticModel(action.SyntaxTree, ignoreAccessibility: true);
            ServiceCallContract[] serviceCalls = ReadServiceCalls(root, action, semanticModel);
            BranchContract[] branches = ReadBranches(root, action, semanticModel);
            string? responseType = UnwrapResponseType(action.ReturnType.ToString());
            string role = DetectAuthenticationRole(method, path, controllerName, action.Identifier.ValueText, anonymous, authorize, parameters, responseType);
            yield return new(Slug($"{method}-{path}"), controllerName, action.Identifier.ValueText, method, path, Path.GetRelativePath(root, action.SyntaxTree.FilePath), span.StartLinePosition.Line + 1, authorize, anonymous, role, permissions, statuses, responseType, parameters, rules, serviceCalls, branches);
        }
    }

    private static AuthenticationDiscovery DiscoverAuthentication(IReadOnlyList<EndpointContract> endpoints, IReadOnlyDictionary<string, TypeContract> types)
    {
        AuthEndpointCandidate[] logins = endpoints.Where(endpoint => endpoint.AuthenticationRole == "login").Select(endpoint => Candidate(endpoint, types)).OrderByDescending(candidate => candidate.Confidence).ToArray();
        AuthEndpointCandidate[] users = endpoints.Where(endpoint => endpoint.AuthenticationRole == "user-create").Select(endpoint => Candidate(endpoint, types)).OrderByDescending(candidate => candidate.Confidence).ToArray();
        AuthConfigurationSuggestion? suggestion = logins.FirstOrDefault() is { } login ? Suggest(login) : null;
        return new(logins, users, suggestion);
    }

    private static AuthEndpointCandidate Candidate(EndpointContract endpoint, IReadOnlyDictionary<string, TypeContract> types)
    {
        ParameterContract? body = endpoint.Parameters.FirstOrDefault(parameter => parameter.Source == "body");
        string[] credentials = body is not null && types.TryGetValue(TypeKey(body.Type), out TypeContract? request)
            ? request.Properties.Where(property => CredentialKind(property.JsonName) is not null).Select(property => property.JsonName).ToArray() : [];
        string? token = endpoint.ResponseType is not null && types.TryGetValue(TypeKey(endpoint.ResponseType), out TypeContract? response)
            ? response.Properties.FirstOrDefault(property => IsTokenField(property.JsonName))?.JsonName : null;
        int confidence = (endpoint.AllowsAnonymous ? 20 : 0)
            + (credentials.Any(field => CredentialKind(field) == "password") ? 35 : 0)
            + (credentials.Any(field => CredentialKind(field) is "username" or "email") ? 30 : 0)
            + (credentials.Any(field => CredentialKind(field) == "code") ? 20 : 0)
            + (token is not null ? 20 : 0);
        return new(endpoint.Id, endpoint.Method, endpoint.Path, body?.Type ?? string.Empty, endpoint.ResponseType, credentials, token, confidence);
    }

    private static AuthConfigurationSuggestion Suggest(AuthEndpointCandidate login)
    {
        Dictionary<string, string> body = new(StringComparer.OrdinalIgnoreCase);
        List<string> variables = [];
        foreach (string field in login.CredentialFields)
        {
            string variable = CredentialKind(field) switch { "password" => "AUTH_PASSWORD", "code" => "AUTH_CODE", "email" => "AUTH_EMAIL", _ => "AUTH_USERNAME" };
            body[field] = $"${{env:{variable}}}";
            if (!variables.Contains(variable, StringComparer.OrdinalIgnoreCase)) variables.Add(variable);
        }
        string tokenPath = string.IsNullOrWhiteSpace(login.TokenField) ? "$.accessToken" : $"$.data.{login.TokenField}";
        return new("login", login.Path, login.Method, body, tokenPath, "Authorization", "Bearer", variables);
    }

    private static string DetectAuthenticationRole(string method, string path, string controller, string action, bool anonymous, bool authorize, IReadOnlyList<ParameterContract> parameters, string? responseType)
    {
        string text = $"{path} {controller} {action} {responseType}";
        bool loginName = Regex.IsMatch(text, @"\b(auth|authenticate|authentication|login|sign[- ]?in|token)\b", RegexOptions.IgnoreCase);
        bool createUserName = method == "POST" && Regex.IsMatch(text, @"\b(user|users|account|accounts|member|members)\b", RegexOptions.IgnoreCase) && Regex.IsMatch(action, "create|register|signup|signUp", RegexOptions.IgnoreCase);
        string parameterText = string.Join(' ', parameters.Select(parameter => parameter.Type));
        bool credentialRequest = Regex.IsMatch(parameterText, "Auth|Login|Credential|SignIn", RegexOptions.IgnoreCase);
        if (method == "POST" && anonymous && (loginName || credentialRequest)) return "login";
        if (method == "POST" && authorize && createUserName) return "user-create";
        return "business";
    }

    private static string? CredentialKind(string name)
    {
        if (name.Contains("password", StringComparison.OrdinalIgnoreCase) || name.Contains("secret", StringComparison.OrdinalIgnoreCase)) return "password";
        if (name.Equals("code", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Code", StringComparison.OrdinalIgnoreCase)) return "code";
        if (name.Contains("email", StringComparison.OrdinalIgnoreCase)) return "email";
        if (name.Contains("username", StringComparison.OrdinalIgnoreCase) || name.Contains("userName", StringComparison.OrdinalIgnoreCase) || name.Equals("login", StringComparison.OrdinalIgnoreCase)) return "username";
        return null;
    }

    private static bool IsTokenField(string name) => name.Equals("accessToken", StringComparison.OrdinalIgnoreCase) || name.Equals("token", StringComparison.OrdinalIgnoreCase) || name.EndsWith("AccessToken", StringComparison.OrdinalIgnoreCase);

    private static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees)
    {
        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).ToArray();
        return CSharpCompilation.Create("ToolQcSourceAnalysis", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ServiceCallContract[] ReadServiceCalls(string root, MethodDeclarationSyntax action, SemanticModel semanticModel)
    {
        List<ServiceCallContract> calls = [];
        foreach (InvocationExpressionSyntax invocation in action.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            IMethodSymbol? symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol is null || symbol.ContainingType is null) continue;
            string containingType = symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (containingType.StartsWith("System.", StringComparison.Ordinal) || containingType.StartsWith("Microsoft.", StringComparison.Ordinal)) continue;
            FileLinePositionSpan line = invocation.SyntaxTree.GetLineSpan(invocation.Span);
            string source = symbol.Locations.FirstOrDefault(location => location.IsInSource)?.SourceTree?.FilePath is { } path ? Path.GetRelativePath(root, path) : Path.GetRelativePath(root, invocation.SyntaxTree.FilePath);
            calls.Add(new(containingType, symbol.Name, symbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), source, line.StartLinePosition.Line + 1));
        }
        return calls.Distinct().ToArray();
    }

    private static BranchContract[] ReadBranches(string root, MethodDeclarationSyntax action, SemanticModel semanticModel)
    {
        string file = Path.GetRelativePath(root, action.SyntaxTree.FilePath);
        List<BranchContract> branches = [];
        foreach (IfStatementSyntax branch in action.DescendantNodes().OfType<IfStatementSyntax>()) branches.Add(Branch("if", branch.Condition, branch, file, action.Identifier.ValueText));
        foreach (SwitchExpressionArmSyntax arm in action.DescendantNodes().OfType<SwitchExpressionArmSyntax>()) branches.Add(Branch("switch-arm", arm.Pattern, arm, file, action.Identifier.ValueText));
        foreach (SwitchSectionSyntax section in action.DescendantNodes().OfType<SwitchSectionSyntax>()) foreach (SwitchLabelSyntax label in section.Labels) branches.Add(Branch("switch-case", label, label, file, action.Identifier.ValueText));
        foreach (CatchClauseSyntax clause in action.DescendantNodes().OfType<CatchClauseSyntax>()) branches.Add(Branch("catch", clause.Declaration ?? (CSharpSyntaxNode)clause, clause, file, action.Identifier.ValueText));
        foreach (ConditionalExpressionSyntax condition in action.DescendantNodes().OfType<ConditionalExpressionSyntax>()) branches.Add(Branch("conditional", condition.Condition, condition, file, action.Identifier.ValueText));
        foreach (InvocationExpressionSyntax invocation in action.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            IMethodSymbol? symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol?.Name.StartsWith("ThrowIf", StringComparison.Ordinal) == true || symbol?.ContainingType.Name.Contains("Guard", StringComparison.OrdinalIgnoreCase) == true) branches.Add(Branch("guard", invocation, invocation, file, action.Identifier.ValueText));
        }
        return branches.Distinct().ToArray();
    }

    private static BranchContract Branch(string kind, SyntaxNode condition, SyntaxNode location, string file, string owner)
    {
        FileLinePositionSpan line = location.SyntaxTree.GetLineSpan(location.Span);
        return new(kind, Regex.Replace(condition.ToString(), @"\s+", " ").Trim(), file, line.StartLinePosition.Line + 1, owner);
    }

    private static Dictionary<string, TypeContract> ReadTypes(IEnumerable<CompilationUnitSyntax> roots, string root)
    {
        Dictionary<string, TypeContract> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (CompilationUnitSyntax syntax in roots)
        {
            string relative = Path.GetRelativePath(root, syntax.SyntaxTree.FilePath);
            foreach (EnumDeclarationSyntax declaration in syntax.DescendantNodes().OfType<EnumDeclarationSyntax>())
                result[declaration.Identifier.ValueText] = new(declaration.Identifier.ValueText, "enum", 0, [], [], [], declaration.Members.Select(member => member.Identifier.ValueText).ToArray(), relative);
            foreach (TypeDeclarationSyntax declaration in syntax.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                List<PropertyContract> properties = [];
                if (declaration is RecordDeclarationSyntax record && record.ParameterList is not null) properties.AddRange(record.ParameterList.Parameters.Select(ReadRecordProperty));
                properties.AddRange(declaration.Members.OfType<PropertyDeclarationSyntax>().Where(property => property.AccessorList is not null).Select(ReadProperty));
                string[] typeParameters = declaration.TypeParameterList?.Parameters.Select(parameter => parameter.Identifier.ValueText).ToArray() ?? [];
                string name = TypeKey(declaration.Identifier.ValueText, typeParameters.Length);
                string[] baseTypes = (declaration.BaseList?.Types ?? []).Select(type => TypeKey(type.Type.ToString())).Where(type => type is not "object").ToArray();
                TypeContract candidate = new(name, declaration.Kind().ToString().Contains("Record", StringComparison.OrdinalIgnoreCase) ? "record" : "class", typeParameters.Length, typeParameters, baseTypes, properties.GroupBy(property => property.JsonName, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray(), [], relative);
                if (!result.TryGetValue(name, out TypeContract? existing) || IsPreferred(candidate, existing)) result[name] = candidate;
            }
        }
        return result;
    }

    private static bool IsPreferred(TypeContract candidate, TypeContract existing) => candidate.Properties.Count > existing.Properties.Count || candidate.TypeParameters.Count > existing.TypeParameters.Count || (candidate.BaseTypes.Count > existing.BaseTypes.Count && existing.Properties.Count == 0);

    private static PropertyContract ReadProperty(PropertyDeclarationSyntax property)
    {
        string type = property.Type.ToString(); string name = property.Identifier.ValueText; bool nullable = type.EndsWith('?');
        (bool collection, string? itemType) = Collection(type);
        return new(name, AttributeString(property.AttributeLists, "JsonPropertyName") ?? Camel(name), type, property.Modifiers.Any(SyntaxKind.RequiredKeyword) || HasAttribute(property.AttributeLists, "Required") || (!nullable && IsValueType(type)), nullable, collection, itemType);
    }

    private static PropertyContract ReadRecordProperty(ParameterSyntax property)
    {
        string type = property.Type?.ToString() ?? "object"; string name = property.Identifier.ValueText; bool nullable = type.EndsWith('?');
        (bool collection, string? itemType) = Collection(type);
        return new(name, AttributeString(property.AttributeLists, "JsonPropertyName") ?? Camel(name), type, HasAttribute(property.AttributeLists, "Required") || (!nullable && IsValueType(type)), nullable, collection, itemType);
    }

    private static Dictionary<string, List<ValidationContract>> ReadValidations(IEnumerable<CompilationUnitSyntax> roots, CSharpCompilation compilation, string root)
    {
        Dictionary<string, List<ValidationContract>> result = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> supported = new(StringComparer.OrdinalIgnoreCase) { "NotEmpty", "NotNull", "EmailAddress", "MaximumLength", "MinimumLength", "Length", "GreaterThanOrEqualTo", "GreaterThan", "LessThanOrEqualTo", "LessThan", "InclusiveBetween", "ExclusiveBetween", "IsInEnum" };
        foreach (CompilationUnitSyntax syntax in roots)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree, ignoreAccessibility: true);
            foreach (ClassDeclarationSyntax validator in syntax.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                BaseTypeSyntax? validatorBase = validator.BaseList?.Types.FirstOrDefault(type => type.Type.ToString().Contains("AbstractValidator<", StringComparison.Ordinal));
                if (validatorBase is null) continue;
                string requestType = GenericArguments(validatorBase.Type.ToString()).FirstOrDefault() ?? string.Empty;
                requestType = CleanType(requestType);
                if (!result.TryGetValue(requestType, out List<ValidationContract>? list)) result[requestType] = list = [];
                foreach (InvocationExpressionSyntax invocation in validator.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string method = InvocationName(invocation);
                    if (!supported.Contains(method)) continue;
                    InvocationExpressionSyntax? ruleFor = invocation.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault(candidate => InvocationName(candidate).Equals("RuleFor", StringComparison.OrdinalIgnoreCase));
                    if (ruleFor?.ArgumentList.Arguments.FirstOrDefault()?.Expression is not SimpleLambdaExpressionSyntax lambda || lambda.Body is not MemberAccessExpressionSyntax member) continue;
                    string? value = invocation.ArgumentList.Arguments.Count == 0 ? null : string.Join(',', invocation.ArgumentList.Arguments.Select(argument => ResolveConstant(argument.Expression, semanticModel)));
                    list.Add(new(member.Name.Identifier.ValueText, method, NullIfEmpty(value ?? string.Empty), Path.GetRelativePath(root, syntax.SyntaxTree.FilePath)));
                }
            }
        }
        return result;
    }

    private static string ResolveConstant(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        Optional<object?> constant = semanticModel.GetConstantValue(expression);
        return constant.HasValue ? Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture) ?? expression.ToString() : expression.ToString();
    }

    private static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch { MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText, IdentifierNameSyntax identifier => identifier.Identifier.ValueText, _ => string.Empty };

    private static Dictionary<string, string> ReadInheritedRoutes(IEnumerable<CompilationUnitSyntax> roots)
    {
        Dictionary<string, string> routes = new(StringComparer.OrdinalIgnoreCase);
        foreach (CompilationUnitSyntax syntax in roots) foreach (ClassDeclarationSyntax declaration in syntax.DescendantNodes().OfType<ClassDeclarationSyntax>()) if (ControllerRoute(declaration) is { } route) routes[declaration.Identifier.ValueText] = route;
        return routes;
    }

    private static string? InheritedRoute(IEnumerable<ClassDeclarationSyntax> parts, Dictionary<string, string> routes) => parts.SelectMany(part => part.BaseList?.Types ?? []).Select(type => CleanType(type.Type.ToString())).Select(type => routes.GetValueOrDefault(type)).FirstOrDefault(route => route is not null);
    private static bool IsController(ClassDeclarationSyntax type) => type.Identifier.ValueText.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) || HasAttribute(type.AttributeLists, "ApiController");
    private static string ControllerKey(ClassDeclarationSyntax controller) => $"{controller.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name}.{controller.Identifier.ValueText}";
    private static string? ControllerRoute(ClassDeclarationSyntax controller) => controller.AttributeLists.SelectMany(list => list.Attributes).Where(attribute => AttributeName(attribute).Equals("Route", StringComparison.OrdinalIgnoreCase)).Select(FirstStringArgument).FirstOrDefault(value => value is not null);
    private static ParameterContract ReadParameter(ParameterSyntax parameter) { string type = parameter.Type?.ToString() ?? "object"; string source = ParameterSource(parameter); bool nullable = type.EndsWith('?') || parameter.Default is not null; return new(parameter.Identifier.ValueText, type, source, !nullable, nullable); }
    private static string ParameterSource(ParameterSyntax parameter) { if (HasAttribute(parameter.AttributeLists, "FromBody")) return "body"; if (HasAttribute(parameter.AttributeLists, "FromRoute")) return "route"; if (HasAttribute(parameter.AttributeLists, "FromQuery")) return "query"; return IsSimple(parameter.Type?.ToString() ?? string.Empty) ? "route-or-query" : "body"; }
    private static bool IsInfrastructureParameter(ParameterSyntax parameter) => CleanType(parameter.Type?.ToString() ?? string.Empty) is "CancellationToken" or "HttpContext" or "ClaimsPrincipal";
    private static int ParseStatus(AttributeSyntax attribute) { foreach (AttributeArgumentSyntax argument in attribute.ArgumentList?.Arguments ?? default) { Match match = Regex.Match(argument.ToString(), @"(?:StatusCodes\.Status)?(?<status>[1-5]\d{2})"); if (match.Success) return int.Parse(match.Groups["status"].Value); } return 0; }
    private static string? UnwrapResponseType(string type) { string clean = type.Replace("global::", string.Empty).Replace("?", string.Empty); string[] wrappers = ["Task", "ValueTask", "ActionResult", "SuccessResultWrapper", "ResultWrapper"]; bool changed; do { changed = false; foreach (string wrapper in wrappers) if (clean.StartsWith(wrapper + "<", StringComparison.Ordinal)) { clean = Inner(clean); changed = true; break; } } while (changed); return clean is "IActionResult" or "ActionResult" or "void" or "Task" ? null : clean.Trim(); }
    private static string Inner(string type) { int start = type.IndexOf('<'); return start < 0 ? type : type[(start + 1)..^1]; }
    private static string[] GenericArguments(string type) { int start = type.IndexOf('<'); if (start < 0 || !type.EndsWith('>')) return []; string inner = type[(start + 1)..^1]; List<string> result = []; int depth = 0; int segmentStart = 0; for (int index = 0; index < inner.Length; index++) { if (inner[index] == '<') depth++; else if (inner[index] == '>') depth--; else if (inner[index] == ',' && depth == 0) { result.Add(inner[segmentStart..index].Trim()); segmentStart = index + 1; } } result.Add(inner[segmentStart..].Trim()); return result.ToArray(); }
    private static string CleanType(string type) { string clean = type.Trim().TrimEnd('?'); int generic = clean.IndexOf('<'); if (generic >= 0) clean = clean[..generic]; return clean.Split('.').Last(); }
    private static string TypeKey(string type) { string clean = type.Trim().TrimEnd('?'); int start = clean.IndexOf('<'); if (start < 0) return CleanType(clean); return TypeKey(CleanType(clean), GenericArguments(clean).Length); }
    private static string TypeKey(string name, int arity) => arity == 0 ? CleanType(name) : $"{CleanType(name)}`{arity}";
    private static bool IsSimple(string type) => IsValueType(type) || type.Contains("string", StringComparison.OrdinalIgnoreCase) || type.Contains("Guid", StringComparison.OrdinalIgnoreCase);
    private static bool IsValueType(string type) => Regex.IsMatch(type.TrimEnd('?'), @"^(bool|byte|short|int|long|float|double|decimal|DateTime|DateTimeOffset|Guid)$", RegexOptions.IgnoreCase);
    private static (bool, string?) Collection(string type) { Match match = Regex.Match(type, @"(?:IEnumerable|IReadOnlyList|ICollection|List|HashSet|IList)<(?<item>.+)>|(?<array>.+)\[\]"); return match.Success ? (true, CleanType(match.Groups["item"].Success ? match.Groups["item"].Value : match.Groups["array"].Value)) : (false, null); }
    private static string AttributeName(AttributeSyntax attribute) => attribute.Name.ToString().Split('.').Last().Replace("Attribute", string.Empty);
    private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string name) => lists.SelectMany(list => list.Attributes).Any(attribute => AttributeName(attribute).Equals(name, StringComparison.OrdinalIgnoreCase));
    private static string? AttributeString(SyntaxList<AttributeListSyntax> lists, string name) => lists.SelectMany(list => list.Attributes).Where(attribute => AttributeName(attribute).Equals(name, StringComparison.OrdinalIgnoreCase)).Select(FirstStringArgument).FirstOrDefault(value => value is not null);
    private static string? FirstStringArgument(AttributeSyntax attribute) { string text = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString() ?? string.Empty; Match match = Regex.Match(text, "\"(?<value>.*)\""); return match.Success ? match.Groups["value"].Value : null; }
    private static string CombineRoute(string prefix, string suffix, string controller) { string path = $"{prefix.TrimEnd('/')}/{suffix.TrimStart('/')}".Replace("[controller]", controller.Replace("Controller", string.Empty), StringComparison.OrdinalIgnoreCase); path = Regex.Replace(path, "/+", "/"); path = path.Length > 1 ? path.TrimEnd('/') : path; return path.StartsWith('/') ? path : "/" + path; }
    private static string Slug(string value) => Regex.Replace(value.Trim('/').ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    private static string Camel(string value) => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool IsSourceFile(string root, string path) { string relative = Path.GetRelativePath(root, path); string[] ignored = ["bin", "obj", ".git", ".claude", ".codex", ".agents", "node_modules", ".artifacts", "test-results"]; return !relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => ignored.Contains(segment, StringComparer.OrdinalIgnoreCase)); }
}
