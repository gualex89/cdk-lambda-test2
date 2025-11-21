using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace CdkLambdaTest
{
    public class CdkLambdaTest2Stack : Stack
    {
        internal CdkLambdaTest2Stack(Construct scope, string id, IStackProps? props = null)
            : base(scope, id, props)
        {
            var lambdaPath = "src/publish";

            var lambdaFunction = new Function(this, "Test2Lambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "LambdaFunction::LambdaFunction.Function::FunctionHandler",
                Code = Code.FromAsset(lambdaPath),
                Timeout = Duration.Seconds(10),
                MemorySize = 256
            });
        }
    }
}
