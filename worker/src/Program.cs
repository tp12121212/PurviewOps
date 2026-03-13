using PurviewOps.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<AdapterRegistry>();
builder.Services.AddHostedService<JobWorker>();

var host = builder.Build();
host.Run();
