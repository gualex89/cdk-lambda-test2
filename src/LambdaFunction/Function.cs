using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

// Serializer
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaFunction;

public class Function
{
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
    {
        try
        {
            // 1. Nombre del secreto
            string secretName = Environment.GetEnvironmentVariable("SECRET_NAME")!;
            string region = Environment.GetEnvironmentVariable("AWS_REGION")!;

            // 2. Cliente secrets manager
            var client = new AmazonSecretsManagerClient(
                Amazon.RegionEndpoint.GetBySystemName(region)
            );

            // 3. Obtener el secreto
            var response = await client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretName
            });

            var secretDict = JsonSerializer.Deserialize<Dictionary<string, object>>(response.SecretString!)!;

            // 4. Connection string
            string connectionString =
                $"Host={secretDict["host"]};" +
                $"Port={secretDict["port"]};" +
                $"Username={secretDict["username"]};" +
                $"Password={secretDict["password"]};" +
                $"Database={secretDict["dbname"]};";

            // 5. Ejecutar query
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

            string jsonOut = JsonSerializer.Serialize(resultados);

            // 6. RESPUESTA VÁLIDA PARA API GATEWAY
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = jsonOut,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };
        }
        catch (Exception ex)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = $"{{\"error\": \"{ex.Message}\"}}",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };
        }
    }
}
