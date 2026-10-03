using Amazon.Lambda.AspNetCoreServer;
using ClienteApi.Application.Services;
using ClienteApi.Domain.Interfaces;
using ClienteApi.Infrastructure.DbContext;
using ClienteApi.Infrastructure.Repositories;
using ClienteApi.Lambda.Json;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = IsRunningInLambda()
    ? await Startup.GetConnectionStringAsync()
    : GetLocalConnectionString(builder.Configuration);

builder.Services.AddSingleton<IDbConnectionFactory>(
    new DbConnectionFactory(connectionString));
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new BrazilianDateTimeConverter()));
builder.Services.AddOpenApi(options =>
    options.AddSchemaTransformer((schema, context, _) =>
    {
        if (context.JsonPropertyInfo?.Name.Equals("numero", StringComparison.OrdinalIgnoreCase) == true)
            schema.Type = JsonSchemaType.String;

        return Task.CompletedTask;
    }));
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/swagger";
        settings.SwaggerRoutes.Add(new NSwag.AspNetCore.SwaggerUiRoute(
            "v1",
            "/openapi/v1.json"));
    });
}

app.MapControllers();

app.Run();

static bool IsRunningInLambda() =>
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

static string GetLocalConnectionString(IConfiguration configuration)
{
    var connectionString = configuration["SQLSERVER_CONNECTION_STRING"]
        ?? configuration.GetConnectionString("SqlServer");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Defina SQLSERVER_CONNECTION_STRING ou ConnectionStrings:SqlServer para executar localmente.");
    }

    return connectionString;
}
