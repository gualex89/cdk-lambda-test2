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
        // Log para debugging
        context.Logger.Log("INPUT RAW → " + JsonSerializer.Serialize(input));

        // 1. Obtener lista de items directamente
        var itemsJson = (JsonElement)input["items"];
        var items = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(itemsJson.GetRawText())!;

        // 2. Secret
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

        // 3. Insert
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        foreach (var row in items)
        {
            var cmd = new NpgsqlCommand(
                @"INSERT INTO solicitudes_2
                (nombre_solicitante, tipo_solicitud, descripcion, estado, prioridad, fecha_creacion, fecha_materializacion, monto_solicitado, observaciones)
                VALUES (@c1, @c2, @c3, @c4, @c5, @c6, @c7, @c8, @c9)",
                conn
            );

            cmd.Parameters.AddWithValue("c1", row["nombre_solicitante"].GetString()!);
            cmd.Parameters.AddWithValue("c2", row["tipo_solicitud"].GetInt32());
            cmd.Parameters.AddWithValue("c3", row["descripcion"].GetString()!);
            cmd.Parameters.AddWithValue("c4", row["estado"].GetString()!);
            cmd.Parameters.AddWithValue("c5", row["prioridad"].GetInt32());
            cmd.Parameters.AddWithValue("c6", row["fecha_creacion"].GetDateTime());
            cmd.Parameters.AddWithValue("c7", row["fecha_materializacion"].GetDateTime());
            cmd.Parameters.AddWithValue("c8", row["monto_solicitado"].GetInt32());
            cmd.Parameters.AddWithValue("c9", row["observaciones"].GetString()!);

            await cmd.ExecuteNonQueryAsync();
        }

        return new { status = "ok" };
    }
}
