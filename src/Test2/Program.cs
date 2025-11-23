using Amazon.CDK;

namespace NuevoApiProyecto
{
    sealed class Program
    {
        public static Amazon.CDK.Environment MakeEnv(string account, string region)
        {
            return new Amazon.CDK.Environment
            {
                Account = account,
                Region = region
            };
        }

        public static void Main(string[] args)
        {
            var app = new App();

            string? account = System.Environment.GetEnvironmentVariable("AWS_ACCOUNT") ?? "";
            string? region = System.Environment.GetEnvironmentVariable("AWS_REGION") ?? "";

            var env = MakeEnv(account, region);

            new apiStackDistinto(app, "apiStackDistinto2989", new StackProps
            {
                Env = env,
                Description = "API creada desde plantilla"
            });

            app.Synth();
        }
    }
}
