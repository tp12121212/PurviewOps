using System.Text.Json;
using System.Text.Json.Serialization;
using PurviewOps.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.WriteIndented = false;
});

builder.Services.AddSingleton<IOperationCatalogService, OperationCatalogService>();
builder.Services.AddSingleton<IJobStore, InMemoryJobStore>();
builder.Services.AddSingleton<IAuditService, InMemoryAuditService>();
builder.Services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var provider = context.RequestServices.GetRequiredService<ICorrelationIdProvider>();
    var correlationId = provider.GetOrCreate();
    context.Response.Headers["x-correlation-id"] = correlationId;
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
