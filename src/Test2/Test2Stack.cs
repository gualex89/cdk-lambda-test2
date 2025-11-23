using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.SecretsManager;
using Constructs;
using System.Collections.Generic;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;

namespace NuevoApiProyecto
{
    public class lambdaConStepF : Stack
    {
        internal lambdaConStepF(Construct scope, string id, IStackProps? props = null)
            : base(scope, id, props)
        {
            var lambdaPath = "src/publish";

            // 1) SECRET
            var secret = Secret.FromSecretNameV2(
                this,
                "RdsSecretReference",
                "test2/rds-credentials"
            );

            // 2) LAMBDA QUE LEE
            var readLambda = new Function(this, "ReadTableLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "LambdaFunction::LambdaFunction.ReadFunction::FunctionHandler",
                Code = Code.FromAsset(lambdaPath),
                Timeout = Duration.Seconds(30),
                Environment = new Dictionary<string, string>
                {
                    { "SECRET_NAME", "test2/rds-credentials" }
                }
            });

            // 3) LAMBDA QUE ESCRIBE
            var writeLambda = new Function(this, "WriteTableLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "LambdaFunction::LambdaFunction.WriteFunction::FunctionHandler",
                Code = Code.FromAsset(lambdaPath),
                Timeout = Duration.Seconds(30),
                Environment = new Dictionary<string, string>
                {
                    { "SECRET_NAME", "test2/rds-credentials" }
                }
            });

            // 4) PERMISOS
            secret.GrantRead(readLambda);
            secret.GrantRead(writeLambda);

            // 5) STEP FUNCTION TASKS
            var readTask = new LambdaInvoke(this, "ReadFromDB", new LambdaInvokeProps
            {
                LambdaFunction = readLambda,
                OutputPath = "$.Payload"
            });

            var writeTask = new LambdaInvoke(this, "WriteToDB", new LambdaInvokeProps
            {
                LambdaFunction = writeLambda,
                Payload = TaskInput.FromObject(new Dictionary<string, object?>
                {
                    { "items", JsonPath.StringAt("$.items") }
                })
            });




            // 6) STATE MACHINE
            var stateMachine = new StateMachine(this, "DbCopyStateMachine", new StateMachineProps
            {
                DefinitionBody = DefinitionBody.FromChainable(
                    readTask.Next(writeTask)
                )
            });

        }
    }
}
