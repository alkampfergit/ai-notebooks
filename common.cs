// Now configure kernel memory
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class Common
{
    public static string GetDeployment()
    {
        return Dotenv.Get("OPENAI_DEFAULT_DEPLOYMENT");
    }

    public static Kernel GetKernel(bool enableLogging) 
    {
        var kernelBuilder = Kernel.CreateBuilder();
        if (enableLogging)
        {
            kernelBuilder.Services.AddLogging(l => l
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole()
                .AddDebug()
            );
        }

        var deployment = Dotenv.Get("OPENAI_DEFAULT_DEPLOYMENT");
        kernelBuilder.Services.AddAzureOpenAIChatCompletion(
            deployment, //"GPT35_2",//"GPT42",
            Dotenv.Get("OPENAI_API_BASE"),
            Dotenv.Get("OPENAI_API_KEY"),
            serviceId: "default",
            modelId: deployment);

        return kernelBuilder.Build();
    }
}
