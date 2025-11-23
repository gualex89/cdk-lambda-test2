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
        var root = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

        var itemsJson = root["items"].ToString()!;
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itemsJson)!;

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

        var secretDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
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

                // STRING
                cmd.Parameters.AddWithValue("c1", row["nombre_solicitante"]?.ToString() ?? "");

                // INT
                cmd.Parameters.AddWithValue("c2", Convert.ToInt32(row["tipo_solicitud"]));
                cmd.Parameters.AddWithValue("c5", Convert.ToInt32(row["prioridad"]));

                // STRING
                cmd.Parameters.AddWithValue("c3", row["descripcion"]?.ToString() ?? "");
                cmd.Parameters.AddWithValue("c4", row["estado"]?.ToString() ?? "");

                // DATE
                cmd.Parameters.AddWithValue("c6", DateTime.Parse(row["fecha_creacion"]!.ToString()!));
                cmd.Parameters.AddWithValue("c7", DateTime.Parse(row["fecha_materializacion"]!.ToString()!));

                // NUMERIC
                cmd.Parameters.AddWithValue("c8", Convert.ToDecimal(row["monto_solicitado"]));

                // STRING
                cmd.Parameters.AddWithValue("c9", row["observaciones"]?.ToString() ?? "");

                await cmd.ExecuteNonQueryAsync();
            }
        }

        return new { status = "ok" };
    }
}
