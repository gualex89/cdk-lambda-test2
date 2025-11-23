using Amazon.Lambda.Core;
using Npgsql;
using System;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.


namespace LambdaFunction
{
    public class WriteFunction
    {
        private readonly string _connectionString =
            Environment.GetEnvironmentVariable("DB_CONNECTION") ??
            throw new Exception("DB_CONNECTION not configured");

        public object FunctionHandler(JsonElement input, ILambdaContext context)
        {
            try
            {
                // Extraer valores de forma segura
                string nombreSolicitante = ExtractString(input, "nombre_solicitante");
                int tipoSolicitud = ExtractInt(input, "tipo_solicitud");
                string descripcion = ExtractString(input, "descripcion");
                string estado = ExtractString(input, "estado");
                int prioridad = ExtractInt(input, "prioridad");
                DateTime fechaCreacion = ExtractDate(input, "fecha_creacion");
                DateTime fechaMaterializacion = ExtractDate(input, "fecha_materializacion");

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    string sql = @"
                        INSERT INTO solicitudes.solicitud
                        (nombre_solicitante, tipo_solicitud, descripcion, estado, prioridad, fecha_creacion, fecha_materializacion)
                        VALUES (@nombre, @tipo, @descripcion, @estado, @prioridad, @creacion, @materializacion)
                        RETURNING id;
                    ";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@nombre", nombreSolicitante);
                        cmd.Parameters.AddWithValue("@tipo", tipoSolicitud);
                        cmd.Parameters.AddWithValue("@descripcion", descripcion);
                        cmd.Parameters.AddWithValue("@estado", estado);
                        cmd.Parameters.AddWithValue("@prioridad", prioridad);
                        cmd.Parameters.AddWithValue("@creacion", fechaCreacion);
                        cmd.Parameters.AddWithValue("@materializacion", fechaMaterializacion);

                        int id = Convert.ToInt32(cmd.ExecuteScalar());

                        return new { success = true, id };
                    }
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"ERROR Write Lambda: {ex}");
                return new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                };
            }
        }

        // ----------------------
        // MÉTODOS UTILITARIOS
        // ----------------------

        private string ExtractString(JsonElement input, string prop)
        {
            if (!input.TryGetProperty(prop, out JsonElement el))
                throw new Exception($"Falta el campo requerido: {prop}");

            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                _ => throw new Exception($"El campo '{prop}' debe ser string")
            };
        }

        private int ExtractInt(JsonElement input, string prop)
        {
            if (!input.TryGetProperty(prop, out JsonElement el))
                throw new Exception($"Falta el campo requerido: {prop}");

            return el.ValueKind switch
            {
                JsonValueKind.Number => el.GetInt32(),
                JsonValueKind.String when int.TryParse(el.GetString(), out int v) => v,
                _ => throw new Exception($"El campo '{prop}' debe ser número (int)")
            };
        }

        private DateTime ExtractDate(JsonElement input, string prop)
        {
            if (!input.TryGetProperty(prop, out JsonElement el))
                throw new Exception($"Falta el campo requerido: {prop}");

            if (el.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(el.GetString(), out DateTime dt))
            {
                return dt;
            }

            throw new Exception($"El campo '{prop}' debe ser una fecha válida");
        }
    }
}
