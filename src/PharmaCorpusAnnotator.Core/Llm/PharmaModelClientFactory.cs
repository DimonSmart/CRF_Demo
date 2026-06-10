using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using PharmaCorpusAnnotator.Core.Interfaces;
using PharmaCorpusAnnotator.Core.Mapping;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace PharmaCorpusAnnotator.Core.Llm;

public static class PharmaModelClientFactory
{
    public static IPharmaAnnotationModelClient Create(
        LlmOptions options,
        ILoggerFactory loggerFactory)
    {
        var chatClient = BuildChatClient(options, loggerFactory);
        var agentOptions = new ChatClientAgentOptions
        {
            Name = "pharma-annotation-agent",
            Description = "Annotates Spanish pharmaceutical product lines for a sequence labeling corpus.",
            UseProvidedChatClientAsIs = true,
        };
        var agent = new ChatClientAgent(chatClient, agentOptions, loggerFactory);
        var promptBuilder = new PharmaAnnotationPromptBuilder();
        var spanValidator = new PharmaSpanAnnotationValidator();
        var spanMapper = new SpanAnnotationMapper();
        var validator = new PharmaAnnotationValidator();

        return new PharmaAnnotationModelClient(
            agent,
            promptBuilder,
            spanValidator,
            spanMapper,
            validator,
            options.RetryCount,
            options.AttemptsOutputPath,
            loggerFactory);
    }

    private static IChatClient BuildChatClient(LlmOptions options, ILoggerFactory loggerFactory)
    {
        var openAiOptions = new OpenAIClientOptions
        {
            Endpoint = options.BaseEndpoint,
            NetworkTimeout = options.Timeout,
        };

        if (options.IgnoreSslErrors || options.Username is not null)
        {
            var handler = new HttpClientHandler();
            if (options.IgnoreSslErrors)
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            var httpClient = new HttpClient(handler) { Timeout = options.Timeout };

            if (options.Username is not null)
            {
                var credentials = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{options.Username}:{options.Password ?? ""}"));
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            }

            openAiOptions.Transport = new HttpClientPipelineTransport(httpClient);
        }

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            openAiOptions);

        return openAiClient
            .GetChatClient(options.ModelId)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory)
            .Build(null);
    }
}
