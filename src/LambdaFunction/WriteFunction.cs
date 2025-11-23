using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

namespace LambdaFunction
{
    public class WriteFunction
    {
        public async Task<object> FunctionHandler(object input, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation("WriteFunction invoked.");

                // 1. Convertir input dinámico (JsonElement) a un diccionario
                var jsonInput = JsonSerializer.Serialize(input);
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonInput);

                int id = data["id"].GetInt32();
                string nombre = data["nombre_solicitante"].GetString();
                int tipoSolicitud = data["tipo_solicitud"].GetInt32();
                string descripcion = data["descripcion"].GetString();
                string estado = data["estado"].GetString();
                int prioridad = data["prioridad"].GetInt32();
                DateTime fechaCreacion = data["fecha_creacion"].GetDateTime();
                DateTime fechaMaterializacion = data["fecha_materializacion"].GetDateTime();

                // 2. Obtener secreto desde Secrets Manager
                string secretName = Environment.GetEnvironmentVariable("SECRET_NAME")!;
                string region = Environment.GetEnvironmentVariable("AWS_REGION")!;

                var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));

                var secretResponse = await client.GetSecretValueAsync(new GetSecretValueRequest
                {
                    SecretId = secretName
                });

                var secretDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(secretResponse.SecretString!)!;

                // 3. Construir connectionString desde el secreto
                string connectionString =
                    $"Host={secretDict["host"]};" +
                    $"Port={secretDict["port"]};" +
                    $"Username={secretDict["username"]};" +
                    $"Password={secretDict["password"]};" +
                    $"Database={secretDict["dbname"]};";

                context.Logger.LogInformation("Connection string built successfully.");

                // 4. Insertar en PostgreSQL
                using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO solicitudes.solicitud 
                    (id, nombre_solicitante, tipo_solicitud, descripcion, estado, prioridad, fecha_creacion, fecha_materializacion)
                    VALUES (@id, @nombre, @tipo, @descripcion, @estado, @prioridad, @creacion, @materializacion);
                ";

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@nombre", nombre);
                cmd.Parameters.AddWithValue("@tipo", tipoSolicitud);
                cmd.Parameters.AddWithValue("@descripcion", descripcion);
                cmd.Parameters.AddWithValue("@estado", estado);
                cmd.Parameters.AddWithValue("@prioridad", prioridad);
                cmd.Parameters.AddWithValue("@creacion", fechaCreacion);
                cmd.Parameters.AddWithValue("@materializacion", fechaMaterializacion);

                int rows = await cmd.ExecuteNonQueryAsync();

                context.Logger.LogInformation($"Rows inserted: {rows}");

                return new
                {
                    ok = true,
                    inserted = rows
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
