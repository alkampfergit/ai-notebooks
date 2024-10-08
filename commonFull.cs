using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public static class Common
{
    public static DumpLoggingProvider DumpLoggingProvider = new DumpLoggingProvider();

    public static Kernel GetKernel(
        bool enableLogging, 
        bool enableDumpProvider = false) 
    {
        
        var kernelBuilder = ConfigureKernelBuilder(enableLogging, enableDumpProvider);
        var kernel = kernelBuilder.Build();

        return kernel;
    }

    public static IKernelBuilder ConfigureKernelBuilder(bool enableLogging, bool enableDumpProvider)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        kernelBuilder.Services.AddLogging(config =>
        {
            config
                .SetMinimumLevel(LogLevel.Trace)
                .AddDebug();
            if (enableDumpProvider)
            {
                config.AddProvider(DumpLoggingProvider);
            }
            if (enableLogging)
            {
                config.AddConsole();
            }
        });

        if (enableDumpProvider)
        {
            kernelBuilder.Services.ConfigureHttpClientDefaults(c => c
                .AddLogger(s => DumpLoggingProvider.CreateHttpRequestBodyLogger(s.GetRequiredService<ILogger<DumpLoggingProvider>>())));
        }

        kernelBuilder.Services.AddAzureOpenAIChatCompletion(
            "GPT4o", //"GPT35_2",//"GPT42",
            Dotenv.Get("OPENAI_API_BASE"),
            Dotenv.Get("OPENAI_API_KEY"),
            serviceId: "default",
            modelId: "gpt4o");

        return kernelBuilder;
    }
}

public static class LogFactory
{
    private static readonly ILoggerFactory _loggerFactory;

    static LogFactory()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddDebug()
                .AddConsole()
                .SetMinimumLevel(LogLevel.Trace);
        });
    }

    public static ILogger CreateLogger(string category)
    {
        return _loggerFactory.CreateLogger(category);
    }   

    public static ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    } 
}

public class LLMCall
{
    public  string Url { get; set; }

    public string CorrelationKey { get; set; }

    public string Prompt { get; set; }

    /// <summary>
    /// Full RAW request made by semantic kernel.
    /// </summary>
    public string FullRequest { get; set; }

    public string PromptFunctions { get; set; }

    public string Response { get; set; }

    public string ResponseFunctionCall { get; set; }

    public string ResponseFunctionCallParameters { get; set; }

    public DateTime CallStart { get; set; }

    public DateTime CallEnd { get; set; }

    public TimeSpan CallDuration => CallEnd - CallStart;    

    public string Dump()
    {
        if (string.IsNullOrEmpty(PromptFunctions))
            return
                $"Prompt: {Prompt}\n" +
                $"Response: {Response}\n" +
                $"ResponseFunctionCall: {ResponseFunctionCall}\n";

        return $"Ask to LLM: {Prompt} -> Call function {ResponseFunctionCall} with arguments {ResponseFunctionCallParameters}";
    }
}

internal class DumpLoggingProvider : ILoggerProvider
{
    private readonly AccumulatorLogger _logger;
    private RequestBodyLogger _httpRequestBodyLogger;

    private static DumpLoggingProvider Instance;

    public DumpLoggingProvider()
    {
        _logger = new AccumulatorLogger();
        Instance = this;
    }

    public IHttpClientAsyncLogger CreateHttpRequestBodyLogger(ILogger logger) =>
        _httpRequestBodyLogger = new RequestBodyLogger(logger);

    public ILogger CreateLogger(string categoryName)
        {
        return _logger;
    }

    public void Dispose()
    {
    }

    internal IEnumerable<LLMCall> GetLLMCalls()
    {
        return _logger.GetLLMCalls();
    }

    class AccumulatorLogger : ILogger
    {
        private readonly List<string> _logs;
        private readonly List<LLMCall> _llmCalls;

        public AccumulatorLogger()
        {
            _logs = new List<string>();
            _llmCalls = new List<LLMCall>();
        }

        public IEnumerable<LLMCall> GetLLMCalls() => _llmCalls; 

        public void AddLLMCall(LLMCall lLMCall)
        {
            _llmCalls.Add(lLMCall);
        }

        internal LLMCall CompleteLLMCall(string correlationId, string function, string arguments, string response)
        {
            for (int i = _llmCalls.Count -1; i >= 0; i--)
            {
                var llmCall = _llmCalls[i];
                if (llmCall.CorrelationKey == correlationId) 
                {
                    llmCall.Response = response;
                    llmCall.ResponseFunctionCall = function;
                    llmCall.ResponseFunctionCallParameters = arguments;
                    llmCall.CallEnd = DateTime.UtcNow;
                    return llmCall;
                }
            }

            return null;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return new LogScope(state);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var stateDictionary = ExtractDictionaryFromState(state);
            var interfaces = state.GetType().GetInterfaces().Select(i => i.Name).ToList();
            _logs.Add(formatter(state, exception));
        }

        private static Dictionary<string, object> ExtractDictionaryFromState<TState>(TState state)
        {
            Dictionary<string, object> retValue = new();
            if (state is IEnumerable en)
            {
                foreach (var element in en)
                {
                    if (element is KeyValuePair<string, object> stateValue)
                    {
                        retValue[stateValue.Key] = stateValue.Value;
                    }
                }
            }
            return retValue;
        }

        public List<string> GetLogs()
        {
            return _logs;
        }
    }

    private class LogScope : IDisposable
    {
        private object _state;

        public LogScope(object state)
        {
            _state = state;
        }

        public void Dispose()
        {
        }
    }

    sealed class RequestBodyLogger(ILogger logger) : IHttpClientAsyncLogger
    {
        public async ValueTask<object?> LogRequestStartAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var requestContent = await request.Content!.ReadAsStringAsync(cancellationToken);
            StringBuilder sb = new();

            if (request.RequestUri.Host.Contains("openai"))
            {
                // I need to pase the request content as json object to extract some informations.
                var jsonObject = JsonDocument.Parse(requestContent).RootElement;
                var messages = jsonObject.GetProperty("messages");

                sb.AppendLine($"Call LLM: {request.RequestUri}");

                foreach (var message in messages.EnumerateArray())
                {
                    var content = message.GetProperty("content").GetString();
                    sb.AppendLine($"{message.GetProperty("role").GetString()}: {content}");
                }

                if (jsonObject.TryGetProperty("tools", out var tools))
                {

                    sb.AppendLine("Functions:");
                    foreach (JsonElement tool in tools.EnumerateArray())
                    {
                        // Extracting function object
                        JsonElement function = tool.GetProperty("function");

                        // Extracting function name and description
                        string functionName = function.GetProperty("name").GetString();
                        string functionDescription = function.GetProperty("description").GetString();

                        sb.AppendLine($"Function Name: {functionName}");
                        sb.AppendLine($"Description: {functionDescription}");

                        // Extracting parameters
                        JsonElement parameters = function.GetProperty("parameters");
                        foreach (JsonProperty parameter in parameters.EnumerateObject())
                        {
                            sb.AppendLine($"Parameter name {parameter.Name} Value; {parameter.Value}");
                        }
                        sb.AppendLine();
                    }
                }
                foreach (var header in request.Headers)
                {
                    if (!header.Key.Contains("key", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"{header.Key}: {header.Value.First()}");
                    }
                }

                LLMCall lLMCall = new LLMCall()
                {
                    Url = request.RequestUri.ToString(),    
                    CorrelationKey = request.Headers.GetValues("x-ms-client-request-id").First(),
                    Prompt = jsonObject.GetProperty("messages").ToString(),
                    FullRequest = jsonObject.ToString(),
                    PromptFunctions = tools.ToString(),
                    CallStart = DateTime.UtcNow
                };
                DumpLoggingProvider.Instance._logger.AddLLMCall(lLMCall);
            }
            else
            {
                sb.AppendLine($"Call HTTP: {request.RequestUri}");
                sb.AppendLine("CONTENT:");
                sb.AppendLine(requestContent);
            }

            logger.LogTrace(sb.ToString());
            return default;
        }

        public object? LogRequestStart(HttpRequestMessage request)
        {
            var requestContent = request.Content!.ReadAsStringAsync().Result;

            logger.LogTrace("Request: {Request}", request);
            logger.LogTrace("Request content: {Content}", requestContent);
            return default;
        }

        public void LogRequestStop(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed)
        {
            var responseContent = response.Content.ReadAsStringAsync().Result;
            logger.LogTrace("Response: {Response}", response);
            logger.LogTrace("Response content: {Content}", responseContent);
        }
        public ValueTask LogRequestStopAsync(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed, CancellationToken cancellationToken = default)
        {
            var responseContent = response.Content.ReadAsStringAsync().Result;

            var sb = new StringBuilder();
            var functions = GetFunctionInformation(responseContent);
            if (functions.Function != null)
            {
                sb.AppendLine($"Call function {functions.Function} with arguments {functions.Arguments}");
            }

            sb.AppendLine($"Response: {response}");
            sb.AppendLine($"Response content: {responseContent}");

            if (request.RequestUri.Host.Contains("openai"))
            {
                var correlationId = response.Headers.GetValues("x-ms-client-request-id").First();
                var llmCall = DumpLoggingProvider.Instance._logger.CompleteLLMCall(correlationId, functions.Function, functions.Arguments, responseContent);
                if (llmCall != null) 
                {
                    logger.LogTrace(llmCall.Dump());
                    return ValueTask.CompletedTask;
                }
            }

            logger.LogTrace(sb.ToString());
            return ValueTask.CompletedTask;
        }

        private static (string Function, string Arguments) GetFunctionInformation(string responseContent)
        {
            try
            {
                var root = JsonDocument.Parse(responseContent);
                var functionInfo = root.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("tool_calls")[0]
                    .GetProperty("function");

                string functionName = functionInfo.GetProperty("name").GetString();
                string arguments = functionInfo.GetProperty("arguments").GetString();

                return (functionName, arguments);
            }
            catch (Exception)
            {
                return (null, null);
            }
        }

        public void LogRequestFailed(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception, TimeSpan elapsed) { }
        public ValueTask LogRequestFailedAsync(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception, TimeSpan elapsed, CancellationToken cancellationToken = default) => default;
    }
}