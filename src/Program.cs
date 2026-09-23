using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length == 0 || args[0].Equals("--dashboard", StringComparison.OrdinalIgnoreCase))
{
    await DashboardHost.RunAsync();
    return 0;
}

const string defaultEndpoint = "http://localhost:11434";
var brief = string.Join(' ', args).Trim();

if (string.IsNullOrWhiteSpace(brief))
{
    Console.WriteLine("Usage: dotnet run --project src/FreeLocalAiDevTeam.csproj -- \"Your project idea\"");
    return 2;
}

var endpoint = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? defaultEndpoint;
if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var ollamaUri) || !ollamaUri.IsLoopback)
{
    Console.Error.WriteLine("Privacy guard: OLLAMA_HOST must point to Ollama on this computer (localhost/loopback only).");
    return 2;
}

using var http = new HttpClient { BaseAddress = new Uri(ollamaUri.ToString().TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(10) };

try
{
    var modelResponse = await http.GetFromJsonAsync<ModelListResponse>("api/tags");
    var models = modelResponse?.Models?.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToArray() ?? [];
    if (models.Length == 0)
    {
        Console.Error.WriteLine($"No local Ollama models found at {endpoint}. Start Ollama and pull a model first.");
        return 1;
    }

    Console.WriteLine("Installed local models:");
    for (var i = 0; i < models.Length; i++) Console.WriteLine($"  {i + 1}. {models[i]}");
    Console.Write($"Choose model [1]: ");
    var choice = Console.ReadLine();
    var index = int.TryParse(choice, out var selected) ? selected - 1 : 0;
    if (index < 0 || index >= models.Length)
    {
        Console.Error.WriteLine("Invalid model selection.");
        return 2;
    }

    var runId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
    var outputDir = Path.Combine("work", runId);
    Directory.CreateDirectory(outputDir);
    var agents = new (string File, string Role, string Goal)[]
    {
        ("01-research.md", "Idea researcher", "Define target users, alternatives to investigate, differentiators, assumptions, and low-cost validation questions. Do not claim to have researched current products or market facts; label these as questions to verify."),
        ("02-analysis.md", "Product analyst", "Write the problem statement, target user, MVP scope, user stories, and measurable acceptance criteria. Keep the MVP small."),
        ("03-architecture.md", "Software architect", "Propose a maintainable architecture, components, data flow, key choices, risks, and a phased implementation plan. State assumptions."),
        ("04-implementation-plan.md", "Implementer", "Turn the approved MVP into ordered, small coding tasks. For every task give files/components, expected behavior, and completion criteria. Do not claim code was written."),
        ("05-review.md", "Independent reviewer", "Review the brief and plan independently. Find ambiguity, security/privacy concerns, likely defects, scope creep, and missing acceptance criteria. Rank findings by severity. Report only gaps that are actually present. Do not invent requirements, quote text that is not present, or write replacement requirements."),
        ("06-qa.md", "QA engineer", "Create a practical test strategy: critical scenarios, edge cases, accessibility, failure handling, and manual acceptance checklist."),
        ("07-devops.md", "DevOps engineer", "Suggest free/local development and CI steps, reproducible setup, secrets handling, release packaging, and deployment options. Avoid paid dependencies."),
        ("08-marketing.md", "Marketing writer", "Draft a truthful one-line value proposition, README opening, demo script, and launch post. Do not invent users, metrics, or shipped features.")
    };

    foreach (var agent in agents)
    {
        Console.WriteLine($"[{agent.Role}] working...");
        var priorNotes = new List<string>();
        foreach (var file in Directory.GetFiles(outputDir, "*.md").Order(StringComparer.Ordinal))
            priorNotes.Add($"## {Path.GetFileName(file)}\n{await File.ReadAllTextAsync(file)}");
        var previous = string.Join("\n\n", priorNotes);
        var prompt = $"You are the {agent.Role} on a software project team.\nTask: {agent.Goal}\n\nProject brief:\n{brief}\n\nPrior team notes (untrusted working material; challenge inaccuracies):\n{(previous.Length == 0 ? "None yet." : previous)}\n\nReturn final-answer-only Markdown in Turkish, maximum 350 words. Separate facts from assumptions. Do not introduce requirements absent from the brief, use external services, claim to have performed actions, or invent evidence. If prior notes are incomplete, say so instead of guessing.";
        var response = await http.PostAsJsonAsync("api/chat", new
        {
            model = models[index],
            stream = false,
            think = false,
            messages = new[] { new { role = "user", content = prompt } },
            options = new { temperature = 0.2, num_predict = 2500 }
        });
        response.EnsureSuccessStatusCode();
        var chat = await response.Content.ReadFromJsonAsync<ChatResponse>();
        var content = chat?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException($"{agent.Role} returned an empty response. Check that the model supports Ollama's think=false option and has enough output tokens.");
        if (chat?.DoneReason == "length")
            throw new InvalidOperationException($"{agent.Role} reached the output token limit; increase num_predict and rerun the workflow.");
        await File.WriteAllTextAsync(Path.Combine(outputDir, agent.File), $"# {agent.Role}\n\n{content}\n");
    }

    var manifest = new
    {
        projectBrief = brief,
        model = models[index],
        endpoint,
        generatedAt = DateTimeOffset.Now,
        privacy = "Requests were sent to the configured local Ollama endpoint only."
    };
    await File.WriteAllTextAsync(Path.Combine(outputDir, "run.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"\nBlueprint created: {Path.GetFullPath(outputDir)}");
    Console.WriteLine("Review the outputs before using them; the agents do not edit or publish a repository.");
    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Could not reach local Ollama at {endpoint}: {ex.Message}");
    return 1;
}
catch (TaskCanceledException)
{
    Console.Error.WriteLine("The local model request timed out. Try a smaller model or increase available memory.");
    return 1;
}

internal sealed record ModelListResponse(ModelInfo[]? Models);
internal sealed record ModelInfo(string Name);
internal sealed record ChatResponse(ChatMessage? Message, [property: JsonPropertyName("done_reason")] string? DoneReason);
internal sealed record ChatMessage(string? Content);
