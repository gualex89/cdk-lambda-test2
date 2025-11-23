using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaFunction
{
    public class WriteFunction
    {
        public async Task<object> FunctionHandler(object input, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation("WriteFunction invoked.");

                // 1. Convertir input dinámico a Dictionary
                var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(input))!;

                if (!payload.ContainsKey("items"))
                    throw new Exception("El JSON de entrada no contiene 'items'");

                var items = payload["items"].EnumerateArray(); // obtener array de objetos

                // 2. Obtener secreto desde Secrets Manager
                string secretName = Environment.GetEnvironmentVariable("SECRET_NAME")!;
                string region = Environment.GetEnvironmentVariable("AWS_REGION")!;

                var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));

                var secretResponse = await client.GetSecretValueAsync(new GetSecretValueRequest
                {
                    SecretId = secretName
                });

                var secretDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(secretResponse.SecretString!)!;

                // 3. Construir connection string desde el secreto
                string connectionString =
                    $"Host={secretDict["host"].GetString()};" +
                    $"Port={secretDict["port"].GetInt32()};" +
                    $"Username={secretDict["username"].GetString()};" +
                    $"Password={secretDict["password"].GetString()};" +
                    $"Database={secretDict["dbname"].GetString()};";

                context.Logger.LogInformation("Connection string built successfully.");

                // 4. Insertar cada registro en PostgreSQL
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                int totalInserted = 0;

                foreach (var item in items)
                {
                    int id = item.GetProperty("id").GetInt32();
                    string nombre = item.GetProperty("nombre_solicitante").GetString()!;
                    int tipoSolicitud = item.GetProperty("tipo_solicitud").GetInt32();
                    string descripcion = item.GetProperty("descripcion").GetString()!;
                    string estado = item.GetProperty("estado").GetString()!;
                    int prioridad = item.GetProperty("prioridad").GetInt32();
                    DateTime fechaCreacion = item.GetProperty("fecha_creacion").GetDateTime();
                    DateTime fechaMaterializacion = item.GetProperty("fecha_materializacion").GetDateTime();

                    string query = @"
                        INSERT INTO solicitudes_2
                        (id, nombre_solicitante, tipo_solicitud, descripcion, estado, prioridad, fecha_creacion, fecha_materializacion, monto_solicitado, observaciones)
                        VALUES (@id, @nombre, @tipo, @descripcion, @estado, @prioridad, @creacion, @materializacion, @monto, @obs);
                    ";

                    await using var cmd = new NpgsqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@nombre", nombre);
                    cmd.Parameters.AddWithValue("@tipo", tipoSolicitud);
                    cmd.Parameters.AddWithValue("@descripcion", descripcion);
                    cmd.Parameters.AddWithValue("@estado", estado);
                    cmd.Parameters.AddWithValue("@prioridad", prioridad);
                    cmd.Parameters.AddWithValue("@creacion", fechaCreacion);
                    cmd.Parameters.AddWithValue("@materializacion", fechaMaterializacion);
                    cmd.Parameters.AddWithValue("@monto", item.GetProperty("monto_solicitado").GetDecimal());
                    cmd.Parameters.AddWithValue("@obs", item.GetProperty("observaciones").GetString()!);

                    int rows = await cmd.ExecuteNonQueryAsync();
                    totalInserted += rows;
                }

                context.Logger.LogInformation($"Total rows inserted: {totalInserted}");

                return new
                {
                    ok = true,
                    inserted = totalInserted
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error: {ex}");
                return new
                {
                    ok = false,
                    error = ex.Message
                };
            }
        }
    }
}
