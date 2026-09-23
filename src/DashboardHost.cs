using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public static class DashboardHost
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });
        builder.WebHost.UseUrls("http://127.0.0.1:5080");
        builder.Services.AddSingleton<AgentRunCoordinator>();
        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/api/agents", () => AgentCatalog.All);
        app.MapGet("/api/models", async (AgentRunCoordinator coordinator, CancellationToken cancellationToken) =>
        {
            try { return Results.Ok(await coordinator.GetModelsAsync(cancellationToken)); }
            catch (HttpRequestException exception) { return Results.Problem($"Ollama'ya bağlanılamadı: {exception.Message}", statusCode: StatusCodes.Status503ServiceUnavailable); }
        });
        app.MapGet("/api/state", (AgentRunCoordinator coordinator) => coordinator.GetState());
        app.MapGet("/api/events", (int? after, AgentRunCoordinator coordinator) => coordinator.GetEvents(after ?? 0));
        app.MapPost("/api/run", async (RunRequest request, AgentRunCoordinator coordinator, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Brief)) return Results.BadRequest(new { error = "Önce proje brief'ini yaz." });
            if (request.Brief.Length > 6000) return Results.BadRequest(new { error = "Brief en fazla 6000 karakter olabilir." });
            string[] installed;
            try { installed = await coordinator.GetModelsAsync(cancellationToken); }
            catch (HttpRequestException exception) { return Results.Problem($"Ollama'ya bağlanılamadı: {exception.Message}", statusCode: StatusCodes.Status503ServiceUnavailable); }
            if (installed.Length == 0) return Results.Problem("Ollama'da yerel model yok. Önce ollama pull <model> komutuyla ücretsiz bir model indir.", statusCode: StatusCodes.Status503ServiceUnavailable);

            var models = request.Models ?? [];
            var unknown = models.Values.Where(model => !installed.Contains(model, StringComparer.OrdinalIgnoreCase)).Distinct().ToArray();
            if (unknown.Length > 0) return Results.BadRequest(new { error = $"Bu modeller Ollama'da yüklü değil: {string.Join(", ", unknown)}" });
            foreach (var agent in AgentCatalog.All)
                if (!models.ContainsKey(agent.Id) || string.IsNullOrWhiteSpace(models[agent.Id])) return Results.BadRequest(new { error = $"{agent.Name} için yerel model seç." });

            if (!coordinator.TryStart(request, out var runId)) return Results.Conflict(new { error = "Brief boş/çok uzun veya başka bir ekip çalışıyor." });
            return Results.Accepted("/api/state", new { runId });
        });
        app.MapPost("/api/stop", (AgentRunCoordinator coordinator) => coordinator.Stop()
            ? Results.Accepted()
            : Results.Conflict(new { error = "Durdurulacak etkin bir çalışma yok." }));

        Console.WriteLine("Free Local AI Dev Team dashboard: http://127.0.0.1:5080");
        Console.WriteLine("Only this computer can connect. Press Ctrl+C here to exit.");
        await app.RunAsync();
    }
}
