using Amazon.Lambda.Core;
using System.Text.Json;

// Serializer
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaFunction;

public class Function
{
    public string FunctionHandler(JsonElement input, ILambdaContext context)
    {
        string json = input.GetRawText();
        return $"Hola Briggithe desde test2! Recibí JSON: {json}";
    }
}
