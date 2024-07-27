// Now configure kernel memory
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class Common
{
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

        kernelBuilder.Services.AddAzureOpenAIChatCompletion(
            "GPT4o", //"GPT35_2",//"GPT42",
            Dotenv.Get("OPENAI_API_BASE"),
            Dotenv.Get("OPENAI_API_KEY"),
            serviceId: "default",
            modelId: "gpt4o");

        return kernelBuilder.Build();
    }
}
