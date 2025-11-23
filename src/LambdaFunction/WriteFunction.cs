using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

namespace LambdaFunction;

public class WriteFunction
{
    public async Task<object> FunctionHandler(Dictionary<string, object> input, ILambdaContext context)
    {
        context.Logger.Log($"INPUT RAW: {JsonSerializer.Serialize(input)}");

        // Parse payload correctamente
        var payloadJson = JsonSerializer.Serialize(input);
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson)!;

        var items = payload["items"].EnumerateArray().ToList();

        // Obtener secretos
        string secretName = Environment.GetEnvironmentVariable("SECRET_NAME")!;
        string region = Environment.GetEnvironmentVariable("AWS_REGION")!;

        var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));
        var secretResponse = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });

        var secretDict = JsonSerializer.Deserialize<Dictionary<string, string>>(secretResponse.SecretString!)!;

        string connectionString =
            $"Host={secretDict["host"]};" +
            $"Port={secretDict["port"]};" +
            $"Username={secretDict["username"]};" +
            $"Password={secretDict["password"]};" +
            $"Database={secretDict["dbname"]};";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        foreach (var row in items)
        {
            // Helpers robustos
            int GetInt(JsonElement el) =>
                el.ValueKind switch
                {
                    JsonValueKind.Number => el.GetInt32(),
                    JsonValueKind.String => int.Parse(el.GetString()!),
                    _ => throw new Exception($"Valor inválido para int: {el}")
                };

            string GetString(JsonElement el) =>
                el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString()!,
                    JsonValueKind.Number => el.ToString(),
                    _ => throw new Exception($"Valor inválido para string: {el}")
                };

            DateTime GetDate(JsonElement el) =>
                DateTime.Parse(GetString(el));

            var cmd = new NpgsqlCommand(
                @"INSERT INTO solicitudes_2
                    (nombre_solicitante, tipo_solicitud, descripcion, estado, prioridad, 
                     fecha_creacion, fecha_materializacion, monto_solicitado, observaciones)
                  VALUES
                    (@c1, @c2, @c3, @c4, @c5, @c6, @c7, @c8, @c9)",
                conn);

            cmd.Parameters.AddWithValue("c1", GetString(row.GetProperty("nombre_solicitante")));
            cmd.Parameters.AddWithValue("c2", GetInt(row.GetProperty("tipo_solicitud")));
            cmd.Parameters.AddWithValue("c3", GetString(row.GetProperty("descripcion")));
            cmd.Parameters.AddWithValue("c4", GetString(row.GetProperty("estado")));
            cmd.Parameters.AddWithValue("c5", GetInt(row.GetProperty("prioridad")));
            cmd.Parameters.AddWithValue("c6", GetDate(row.GetProperty("fecha_creacion")));
            cmd.Parameters.AddWithValue("c7", GetDate(row.GetProperty("fecha_materializacion")));
            cmd.Parameters.AddWithValue("c8", GetInt(row.GetProperty("monto_solicitado")));
            cmd.Parameters.AddWithValue("c9", GetString(row.GetProperty("observaciones")));

            await cmd.ExecuteNonQueryAsync();
        }

        return new { status = "ok" };
    }
}
