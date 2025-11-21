using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.SecretsManager;  // <-- importante
using Constructs;
using System.Collections.Generic;
using Amazon.CDK.AWS.APIGateway;


namespace CdkLambdaTest
{
    public class CdkLambdaTest2Stack : Stack
    {
        internal CdkLambdaTest2Stack(Construct scope, string id, IStackProps? props = null)
            : base(scope, id, props)
        {
            var lambdaPath = "src/publish";

            // 1. Referenciar el secreto creado manualmente en Secrets Manager
            var secret = Secret.FromSecretNameV2(
                this,
                "RdsSecretReference",
                "test2/rds-credentials" // <-- el nombre EXACTO del secreto
            );

            // 2. Crear la Lambda e inyectar els nombre del secreto como variable de ambiente
            var lambdaFunction = new Function(this, "Test2Lambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "LambdaFunction::LambdaFunction.Function::FunctionHandler",
                Code = Code.FromAsset(lambdaPath),
                Timeout = Duration.Seconds(10),
                MemorySize = 256,
                Environment = new Dictionary<string, string>
                {
                    { "SECRET_NAME", "test2/rds-credentials" }
                }
            });
            var api = new LambdaRestApi(this, "Test2Api", new LambdaRestApiProps
            {
                Handler = lambdaFunction,
                Proxy = false
            });
            var clientes = api.Root.AddResource("solicitudes");

            // Método GET
            clientes.AddMethod("POST");

            // 3. Permitir que la Lambda LEA el secreto
            secret.GrantRead(lambdaFunction);
        }
    }
}
