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
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(input.ToString()!)!;
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(payload["items"].ToString()!)!;

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

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            foreach (var row in items)
            {
                var cmd = new NpgsqlCommand(
                    "INSERT INTO solicitudes_2(nombre_solicitante, tipo_solicitud, descripcion, estado, prioridad, fecha_creacion, fecha_materializacion, monto_solicitado, observaciones) VALUES (@c1, @c2, @c3, @c4, @c5, @c6, @c7, @c8, @c9)", conn);

                cmd.Parameters.AddWithValue("c1", row["nombre_solicitante"].ToString());
                cmd.Parameters.AddWithValue("c2", row["tipo_solicitud"].ToString());
                cmd.Parameters.AddWithValue("c2", row["descripcion"].ToString());
                cmd.Parameters.AddWithValue("c2", row["estado"].ToString());
                cmd.Parameters.AddWithValue("c2", row["prioridad"].ToString());
                cmd.Parameters.AddWithValue("c2", row["fecha_creacion"].ToString());
                cmd.Parameters.AddWithValue("c2", row["fecha_materializacion"].ToString());
                cmd.Parameters.AddWithValue("c2", row["monto_solicitado"].ToString());
                cmd.Parameters.AddWithValue("c2", row["observaciones"].ToString());
                


                await cmd.ExecuteNonQueryAsync();
            }
        }

        return new { status = "ok" };
    }
}
