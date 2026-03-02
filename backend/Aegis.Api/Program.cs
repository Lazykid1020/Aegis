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

// ── Microsoft Semantic Kernel (Azure AI Foundry / OpenAI) ────
var azureEndpoint = builder.Configuration["AzureOpenAI:Endpoint"] ??
    Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
    "https://taxsource-api-resource.services.ai.azure.com/";
var azureApiKey = builder.Configuration["AzureOpenAI:ApiKey"] ??
    Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? "";
var azureDeployment = builder.Configuration["AzureOpenAI:Deployment"] ??
    Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-5-mini";

builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: azureDeployment,
        endpoint: azureEndpoint,
        apiKey: azureApiKey);

    // Register the IT Operations plugin
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    kernelBuilder.Plugins.AddFromObject(new ITOperationsPlugin(httpClientFactory), "ITOperations");

    return kernelBuilder.Build();
});

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
