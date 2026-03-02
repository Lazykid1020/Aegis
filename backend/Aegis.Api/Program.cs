using Microsoft.SemanticKernel;
using Aegis.Api.Services;
using Aegis.Api.Plugins;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers & OpenAPI ────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── CORS (allow frontend to connect) ─────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ── HttpClient for mock APIs (points to self) ────────────────
builder.Services.AddHttpClient("MockApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});

// ── Microsoft Semantic Kernel (Google Gemini) ────────────────
var geminiApiKey = builder.Configuration["Gemini:ApiKey"] ??
    Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
var geminiModel = builder.Configuration["Gemini:Model"] ?? "gemini-2.0-flash";

#pragma warning disable SKEXP0070 // Google connector is experimental
builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddGoogleAIGeminiChatCompletion(
        modelId: geminiModel,
        apiKey: geminiApiKey);

    // Register the IT Operations plugin
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    kernelBuilder.Plugins.AddFromObject(new ITOperationsPlugin(httpClientFactory), "ITOperations");

    return kernelBuilder.Build();
});
#pragma warning restore SKEXP0070

// ── Register our Agent Services ──────────────────────────────
builder.Services.AddSingleton<KnowledgeBaseService>();
builder.Services.AddSingleton<BaseAgent>();
builder.Services.AddSingleton<OrchestratorService>();

var app = builder.Build();

// ── Initialize Knowledge Base ────────────────────────────────
var kb = app.Services.GetRequiredService<KnowledgeBaseService>();
kb.InitializeAsync().Wait();

// ── Middleware Pipeline ──────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
