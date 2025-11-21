using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

// Serializer
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaFunction;

public class Function
{
    public async Task<JsonElement> FunctionHandler(JsonElement input, ILambdaContext context)
    {
        // 1. Nombre del secreto (viene desde CDK)
        string secretName = Environment.GetEnvironmentVariable("SECRET_NAME")!;
        string region = Environment.GetEnvironmentVariable("AWS_REGION")!;

        // 2. Crear cliente de Secrets Manager
        var client = new AmazonSecretsManagerClient(
            Amazon.RegionEndpoint.GetBySystemName(region)
        );

        // 3. Obtener el secreto
        var response = await client.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = secretName
        });

        // 4. Convertir el JSON del secreto en un diccionario dinámico
        var secretDict = JsonSerializer.Deserialize<Dictionary<string, object>>(response.SecretString!)!;

        // 5. Construir connection string usando el formato estándar de RDS
        string connectionString =
            $"Host={secretDict["host"]};" +
            $"Port={secretDict["port"]};" +
            $"Username={secretDict["username"]};" +
            $"Password={secretDict["password"]};" +
            $"Database={secretDict["dbname"]};";

        // 6. Ejecutar query
        var resultados = new List<Dictionary<string, object?>>();

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("SELECT * FROM clientes_compras LIMIT 5", conn);
            var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string col = reader.GetName(i);
                    object? val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[col] = val;
                }

                resultados.Add(row);
            }
        }

        // 7. Devolver JSON
        string jsonOut = JsonSerializer.Serialize(resultados);
        return JsonSerializer.Deserialize<JsonElement>(jsonOut)!;
    }
}
