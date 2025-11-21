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
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // 0. Leer JSON del body
            var input = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Body!);

            int tipoSolicitud = int.Parse(input["tipo_solicitud"].ToString()!);
            int prioridad = int.Parse(input["prioridad"].ToString()!);

            // estado es opcional
            string? estado = input.ContainsKey("estado")
                ? input["estado"]?.ToString()
                : null;

            // 1. Obtener secretos
            string secretName = Environment.GetEnvironmentVariable("SECRET_NAME")!;
            string region = Environment.GetEnvironmentVariable("AWS_REGION")!;

            var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));
            var secretResponse = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });

            var secretDict = JsonSerializer.Deserialize<Dictionary<string, object>>(secretResponse.SecretString!)!;

            string connectionString =
                $"Host={secretDict["host"]};" +
                $"Port={secretDict["port"]};" +
                $"Username={secretDict["username"]};" +
                $"Password={secretDict["password"]};" +
                $"Database={secretDict["dbname"]};";

            // 2. Crear query dinámica
            var resultados = new List<Dictionary<string, object?>>();

            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // Query base
                string sql = @"
                    SELECT * 
                    FROM solicitudes 
                    WHERE tipo_solicitud = @tipo 
                      AND prioridad = @prio";

                // Agregar estado si viene
                if (!string.IsNullOrEmpty(estado))
                {
                    sql += " AND estado = @estado";
                }

                sql += " LIMIT 50;";

                var cmd = new NpgsqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("tipo", tipoSolicitud);
                cmd.Parameters.AddWithValue("prio", prioridad);

                if (!string.IsNullOrEmpty(estado))
                {
                    cmd.Parameters.AddWithValue("estado", estado);
                }

                var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i)
                            ? null
                            : reader.GetValue(i);
                    }

                    resultados.Add(row);
                }
            }

            string jsonOut = JsonSerializer.Serialize(resultados);

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
