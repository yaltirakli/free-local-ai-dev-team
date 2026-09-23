using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record RunRequest(string? Brief, Dictionary<string, string>? Models);
public sealed record AgentRunState(string Status, string? RunId, string? ActiveAgent, string? Error, string? OutputDirectory);
public sealed record AgentRunEvent(int Sequence, DateTimeOffset At, string AgentId, string AgentName, string Kind, string Model, string? Prompt, string? Content, string? Error);
public sealed record LocalModel(string Name);

public sealed class AgentRunCoordinator
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly object _gate = new();
    private readonly List<AgentRunEvent> _events = [];
    private AgentRunState _state = new("idle", null, null, null, null);
    private CancellationTokenSource? _runCancellation;
    private int _sequence;

    public AgentRunCoordinator()
    {
        _endpoint = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://127.0.0.1:11434";
        if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback)
            throw new InvalidOperationException("Gizlilik için OLLAMA_HOST yalnızca bu bilgisayardaki localhost/loopback adresi olabilir.");

        _http = new HttpClient
        {
            BaseAddress = new Uri(uri.ToString().TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(10)
        };
        LoadMostRecentRun();
    }

    public AgentRunState GetState()
    {
        lock (_gate) return _state;
    }

    public AgentRunEvent[] GetEvents(int afterSequence)
    {
        lock (_gate) return _events.Where(item => item.Sequence > afterSequence).ToArray();
    }

    public async Task<string[]> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<ModelListResponse>("api/tags", cancellationToken);
        return response?.Models?
            .Select(model => model.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    public bool TryStart(RunRequest request, out string? runId)
    {
        runId = null;
        var brief = request.Brief?.Trim();
        if (string.IsNullOrWhiteSpace(brief) || brief.Length > 6000) return false;

        lock (_gate)
        {
            if (_state.Status == "running") return false;
            runId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
            _events.Clear();
            _sequence = 0;
            _runCancellation?.Dispose();
            _runCancellation = new CancellationTokenSource();
            _state = new AgentRunState("running", runId, null, null, null);
            _ = ExecuteAsync(brief, request.Models ?? [], runId, _runCancellation.Token);
            return true;
        }
    }

    public bool Stop()
    {
        lock (_gate)
        {
            if (_state.Status != "running" || _runCancellation is null) return false;
            _runCancellation.Cancel();
            return true;
        }
    }

    private async Task ExecuteAsync(string brief, Dictionary<string, string> selectedModels, string runId, CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "work", runId);
        var transcriptPath = Path.Combine(outputDirectory, "conversations.jsonl");
        try
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(transcriptPath, string.Empty, cancellationToken);
            foreach (var agent in AgentCatalog.All)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var model = selectedModels.GetValueOrDefault(agent.Id);
                if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException($"{agent.Name} için bir yerel model seçilmedi.");

                var notes = new List<string>();
                foreach (var file in agent.ContextFiles)
                {
                    var notePath = Path.Combine(outputDirectory, file);
                    if (File.Exists(notePath)) notes.Add($"## {file}\n{await File.ReadAllTextAsync(notePath, cancellationToken)}");
                }

                var prompt = $"Sen ücretsiz ve yerel çalışan bir yazılım ekibinin {agent.Name} ajanısın.\nGörev: {agent.Goal}\n\nProje brief'i:\n{brief}\n\nÖnceki ajanların notları (güvenilmez taslak; hataları sorgula):\n{(notes.Count == 0 ? "Henüz not yok." : string.Join("\n\n", notes))}\n\nYalnızca nihai yanıtı Türkçe Markdown olarak yaz; en fazla 350 kelime. Gerçekleri varsayımlardan ayır. Brief'te olmayan gereksinimler ekleme, internete eriştiğini veya eylem gerçekleştirdiğini iddia etme, kanıt uydurma. Önceki not eksikse tahmin etmek yerine bunu belirt.";
                SetActive(agent.Id);
                await AppendEventAsync(transcriptPath, new AgentRunEvent(0, DateTimeOffset.Now, agent.Id, agent.Name, "started", model, prompt, null, null), cancellationToken);

                using var response = await _http.PostAsJsonAsync("api/chat", new
                {
                    model,
                    stream = false,
                    think = false,
                    messages = new[] { new { role = "user", content = prompt } },
                    options = new { temperature = 0.2, num_predict = 1500 }
                }, cancellationToken);
                response.EnsureSuccessStatusCode();
                var chat = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
                var content = chat?.Message?.Content?.Trim();
                if (string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException($"{agent.Name} boş yanıt verdi. Ollama'da desteklenen yerel bir model seç.");
                if (chat?.DoneReason == "length") throw new InvalidOperationException($"{agent.Name} yanıtı token sınırında kesildi.");

                await File.WriteAllTextAsync(Path.Combine(outputDirectory, agent.File), $"# {agent.Name}\n\n{content}\n", cancellationToken);
                await AppendEventAsync(transcriptPath, new AgentRunEvent(0, DateTimeOffset.Now, agent.Id, agent.Name, "completed", model, null, content, null), cancellationToken);
            }

            var manifest = new
            {
                projectBrief = brief,
                models = selectedModels,
                generatedAt = DateTimeOffset.Now,
                privacy = "Tüm model istekleri bu bilgisayardaki Ollama servisine gönderildi."
            };
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "run.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            SetState(new AgentRunState("completed", runId, null, null, outputDirectory));
        }
        catch (OperationCanceledException)
        {
            SetState(new AgentRunState("stopped", runId, null, null, outputDirectory));
            await TryAppendEventAsync(transcriptPath, new AgentRunEvent(0, DateTimeOffset.Now, "system", "Sistem", "stopped", "", null, null, "Çalıştırma kullanıcı tarafından durduruldu."));
        }
        catch (Exception exception)
        {
            SetState(new AgentRunState("failed", runId, null, exception.Message, outputDirectory));
            await TryAppendEventAsync(transcriptPath, new AgentRunEvent(0, DateTimeOffset.Now, "system", "Sistem", "failed", "", null, null, exception.Message));
        }
    }

    private void SetActive(string agentId)
    {
        lock (_gate) _state = _state with { ActiveAgent = agentId };
    }

    private void SetState(AgentRunState state)
    {
        lock (_gate) _state = state;
    }

    private async Task AppendEventAsync(string transcriptPath, AgentRunEvent item, CancellationToken cancellationToken)
    {
        AgentRunEvent saved;
        lock (_gate)
        {
            saved = item with { Sequence = ++_sequence };
            _events.Add(saved);
        }
        await File.AppendAllTextAsync(transcriptPath, JsonSerializer.Serialize(saved) + Environment.NewLine, cancellationToken);
    }

    private async Task TryAppendEventAsync(string transcriptPath, AgentRunEvent item)
    {
        try { await AppendEventAsync(transcriptPath, item, CancellationToken.None); }
        catch (IOException) { }
    }

    private void LoadMostRecentRun()
    {
        var workRoot = Path.Combine(Directory.GetCurrentDirectory(), "work");
        if (!Directory.Exists(workRoot)) return;

        foreach (var directory in Directory.GetDirectories(workRoot).OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            var transcriptPath = Path.Combine(directory, "conversations.jsonl");
            if (!File.Exists(transcriptPath)) continue;
            try
            {
                var savedEvents = File.ReadLines(transcriptPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => JsonSerializer.Deserialize<AgentRunEvent>(line))
                    .Where(item => item is not null)
                    .Cast<AgentRunEvent>()
                    .ToArray();
                if (savedEvents.Length == 0) continue;

                _events.AddRange(savedEvents);
                _sequence = savedEvents.Max(item => item.Sequence);
                var last = savedEvents[^1];
                var completed = File.Exists(Path.Combine(directory, "run.json"));
                var status = completed ? "completed" : last.Kind == "stopped" ? "stopped" : "failed";
                var error = status == "failed" ? last.Error ?? "Önceki çalışma tamamlanmadan kesildi." : null;
                _state = new AgentRunState(status, Path.GetFileName(directory), null, error, directory);
                return;
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                // Ignore unreadable history and try the next available run.
            }
        }
    }

    private sealed record ModelListResponse(ModelInfo[]? Models);
    private sealed record ModelInfo(string Name);
    private sealed record ChatResponse(ChatMessage? Message, [property: JsonPropertyName("done_reason")] string? DoneReason);
    private sealed record ChatMessage(string? Content);
}
