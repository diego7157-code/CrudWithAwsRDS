using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Data.SqlClient;

public static class Startup
{
    // TODO: Avaliar cache organizacional compartilhado (ex.: Redis) para disponibilizar o segredo
    // entre instâncias da Lambda e reduzir chamadas ao Secrets Manager; proteger o cache com
    // criptografia, controle de acesso, TTL e estratégia de invalidação após rotação do segredo.
    private static readonly Lazy<Task<string>> ConnectionString =
        new(GetConnectionStringCoreAsync);

    public static Task<string> GetConnectionStringAsync() => ConnectionString.Value;

    private static async Task<string> GetConnectionStringCoreAsync()
    {
        var secretId = Environment.GetEnvironmentVariable("DB_SECRET_ARN");
        if (string.IsNullOrWhiteSpace(secretId))
            throw new InvalidOperationException("A variável de ambiente DB_SECRET_ARN não foi definida.");

        using var client = new AmazonSecretsManagerClient();
        var response = await client.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = secretId
        });

        if (string.IsNullOrWhiteSpace(response.SecretString))
            throw new InvalidOperationException($"O segredo '{secretId}' não contém um valor de texto.");

        var secret = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString)
            ?? throw new InvalidOperationException($"O segredo '{secretId}' não contém um objeto JSON válido.");

        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"{GetRequiredSecret(secret, "host")},1433",
            InitialCatalog = GetRequiredSecret(secret, "dbname"),
            UserID = GetRequiredSecret(secret, "username"),
            Password = GetRequiredSecret(secret, "password"),
            Encrypt = true,
            TrustServerCertificate = false,
            MinPoolSize = 1,
            MaxPoolSize = 10,
            ConnectTimeout = 15
        };

        return connectionString.ConnectionString;
    }

    private static string GetRequiredSecret(
        IReadOnlyDictionary<string, string> secret,
        string key)
    {
        if (!secret.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"O segredo do banco de dados não contém o campo '{key}'.");

        return value;
    }
}
