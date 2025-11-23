using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

namespace LambdaFunction;

public class WriteFunction
{
    public async Task<object> FunctionHandler(object input, ILambdaContext context)
    {
        //
        // 1. Parseo correcto del input
        //
        var json = input.ToString()!;
        var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        var items = root["items"].Deserialize<List<Dictionary<string, JsonElement>>>()!;

        //
        // 2. Secrets Manager
        //
        string secretName = Environment.GetEnvironmentVariable("SECRET_NAME")!;
        string region = Environment.GetEnvironmentVariable("AWS_REGION")!;

        var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));
        var secretResponse = await client.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = secretName
        });

        var secretDict = JsonSerializer.Deserialize<Dictionary<string, string>>(
            secretResponse.SecretString!)!;

        string connectionString =
            $"Host={secretDict["host"]};" +
            $"Port={secretDict["port"]};" +
            $"Username={secretDict["username"]};" +
            $"Password={secretDict["password"]};" +
            $"Database={secretDict["dbname"]};";

        //
        // 3. Conexión y escritura
        //
        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            foreach (var row in items)
            {
                var cmd = new NpgsqlCommand(
                @"INSERT INTO solicitudes_2(
                    nombre_solicitante,
                    tipo_solicitud,
                    descripcion,
                    estado,
                    prioridad,
                    fecha_creacion,
                    fecha_materializacion,
                    monto_solicitado,
                    observaciones
                )
                VALUES (
                    @c1, @c2, @c3, @c4, @c5, @c6, @c7, @c8, @c9
                )", conn);

                cmd.Parameters.AddWithValue("c1", GetString(row["nombre_solicitante"]));
                cmd.Parameters.AddWithValue("c2", GetInt(row["tipo_solicitud"]));
                cmd.Parameters.AddWithValue("c3", GetString(row["descripcion"]));
                cmd.Parameters.AddWithValue("c4", GetString(row["estado"]));
                cmd.Parameters.AddWithValue("c5", GetInt(row["prioridad"]));
                cmd.Parameters.AddWithValue("c6", GetDate(row["fecha_creacion"]));
                cmd.Parameters.AddWithValue("c7", GetDate(row["fecha_materializacion"]));
                cmd.Parameters.AddWithValue("c8", GetDecimal(row["monto_solicitado"]));
                cmd.Parameters.AddWithValue("c9", GetString(row["observaciones"]));

                await cmd.ExecuteNonQueryAsync();
            }
        }

        return new { status = "ok" };
    }

    // ---------------------------
    // HELPERS para JsonElement
    // ---------------------------

    private string GetString(JsonElement el)
        => el.ValueKind == JsonValueKind.Null ? "" : el.GetString() ?? "";

    private int GetInt(JsonElement el)
        => el.ValueKind == JsonValueKind.Number ? el.GetInt32() : int.Parse(el.GetString()!);

    private decimal GetDecimal(JsonElement el)
        => el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : decimal.Parse(el.GetString()!);

    private DateTime GetDate(JsonElement el)
        => DateTime.Parse(el.GetString()!);
}
