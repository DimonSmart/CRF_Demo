using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Core.Interfaces;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Core.Llm;

public sealed class PharmaAnnotationModelClient : IPharmaAnnotationModelClient
{
    private readonly ChatClientAgent _agent;
    private readonly PharmaAnnotationPromptBuilder _promptBuilder;
    private readonly PharmaAnnotationValidator _validator;
    private readonly int _retryCount;
    private readonly ILogger<PharmaAnnotationModelClient> _logger;

    public PharmaAnnotationModelClient(
        ChatClientAgent agent,
        PharmaAnnotationPromptBuilder promptBuilder,
        PharmaAnnotationValidator validator,
        int retryCount,
        ILoggerFactory loggerFactory)
    {
        _agent = agent;
        _promptBuilder = promptBuilder;
        _validator = validator;
        _retryCount = retryCount;
        _logger = loggerFactory.CreateLogger<PharmaAnnotationModelClient>();
    }

    public async Task<PharmaAnnotationResponse> AnnotateAsync(
        PharmaAnnotationModelRequest request,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, _promptBuilder.GetSystemPrompt()),
            new(ChatRole.User, _promptBuilder.BuildUserPrompt(request)),
        };

        string lastError = "";

        for (int attempt = 0; attempt < _retryCount; attempt++)
        {
            if (attempt > 0)
            {
                messages.Add(new ChatMessage(ChatRole.User,
                    $"The previous response was invalid for PharmaAnnotationResponse.\n" +
                    $"Previous error: {lastError}\n" +
                    $"Return exactly one structured response that matches the requested schema and fixes the contract error."));
            }

            try
            {
                var agentResponse = await _agent.RunAsync<PharmaAnnotationResponse>(
                    messages,
                    session: null,
                    serializerOptions: JsonSerializerOptions.Web,
                    options: null,
                    cancellationToken);
                var response = agentResponse.Result;
                var validation = _validator.Validate(request, response);

                if (validation.IsValid)
                    return response;

                lastError = string.Join("; ", validation.Errors);
                _logger.LogWarning("Attempt {Attempt}/{Max} validation failed for {Key}:{Row}: {Error}",
                    attempt + 1, _retryCount, request.SourceKey, request.RowNumber, lastError);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "Attempt {Attempt}/{Max} call failed for {Key}:{Row}",
                    attempt + 1, _retryCount, request.SourceKey, request.RowNumber);
            }
        }

        throw new AnnotationFailedException(
            request.SourceKey,
            request.RowNumber,
            request.Text,
            lastError,
            _retryCount);
    }
}
