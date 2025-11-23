using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaFunction;

public class ReadFunction
{
    public async Task<object> FunctionHandler(object input, ILambdaContext context)
    {
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

        var data = new List<Dictionary<string, object>>();

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("select * from solicitudes where tipo_solicitud = 90", conn);
            var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.GetValue(i);

                data.Add(row);
            }
        }

        return new { items = data };
    }
}
