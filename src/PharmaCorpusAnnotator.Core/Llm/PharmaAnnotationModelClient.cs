using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using PharmaCorpusAnnotator.Core.Interfaces;
using PharmaCorpusAnnotator.Core.Mapping;
using PharmaCorpusAnnotator.Core.Models;
using PharmaCorpusAnnotator.Core.Validation;

namespace PharmaCorpusAnnotator.Core.Llm;

public sealed class PharmaAnnotationModelClient : IPharmaAnnotationModelClient
{
    private readonly ChatClientAgent _agent;
    private readonly PharmaAnnotationPromptBuilder _promptBuilder;
    private readonly PharmaSpanAnnotationValidator _spanValidator;
    private readonly SpanAnnotationMapper _spanMapper;
    private readonly PharmaAnnotationValidator _validator;
    private readonly int _retryCount;
    private readonly string? _attemptsOutputPath;
    private readonly ILogger<PharmaAnnotationModelClient> _logger;

    public PharmaAnnotationModelClient(
        ChatClientAgent agent,
        PharmaAnnotationPromptBuilder promptBuilder,
        PharmaSpanAnnotationValidator spanValidator,
        SpanAnnotationMapper spanMapper,
        PharmaAnnotationValidator validator,
        int retryCount,
        string? attemptsOutputPath,
        ILoggerFactory loggerFactory)
    {
        _agent = agent;
        _promptBuilder = promptBuilder;
        _spanValidator = spanValidator;
        _spanMapper = spanMapper;
        _validator = validator;
        _retryCount = retryCount;
        _attemptsOutputPath = attemptsOutputPath;
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

        IReadOnlyList<string> lastErrors = [];

        for (int attempt = 0; attempt < _retryCount; attempt++)
        {
            if (attempt > 0)
            {
                messages.Add(new ChatMessage(ChatRole.User,
                    "Previous response was invalid:\n" +
                    string.Join("\n", lastErrors.Select(e => $"- {e}")) +
                    "\n\nReturn corrected span JSON only.\n" +
                    "Do not return tokens.\n" +
                    "Do not return BIO labels."));
            }

            try
            {
                var agentResponse = await _agent.RunAsync<PharmaSpanAnnotationResponse>(
                    messages,
                    session: null,
                    serializerOptions: JsonSerializerOptions.Web,
                    options: null,
                    cancellationToken);
                var spanResponse = agentResponse.Result;
                var spanValidation = _spanValidator.Validate(request, spanResponse);

                if (!spanValidation.IsValid)
                {
                    lastErrors = spanValidation.Errors;
                    await WriteAttemptAsync(request, attempt + 1, false, lastErrors, null, cancellationToken);

                    _logger.LogWarning("Attempt {Attempt}/{Max} span validation failed for {Key}:{Row}: {Error}",
                        attempt + 1, _retryCount, request.SourceKey, request.RowNumber, string.Join("; ", lastErrors));
                    continue;
                }

                var response = _spanMapper.Map(request, spanResponse);
                var validation = _validator.Validate(request, response);

                if (validation.IsValid)
                {
                    await WriteAttemptAsync(request, attempt + 1, true, [], null, cancellationToken);
                    return response;
                }

                lastErrors = validation.Errors;
                await WriteAttemptAsync(request, attempt + 1, false, lastErrors, null, cancellationToken);

                _logger.LogWarning("Attempt {Attempt}/{Max} final validation failed for {Key}:{Row}: {Error}",
                    attempt + 1, _retryCount, request.SourceKey, request.RowNumber, string.Join("; ", lastErrors));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastErrors = [ex.Message];
                await WriteAttemptAsync(request, attempt + 1, false, [], ex.Message, cancellationToken);
                _logger.LogWarning(ex, "Attempt {Attempt}/{Max} call failed for {Key}:{Row}",
                    attempt + 1, _retryCount, request.SourceKey, request.RowNumber);
            }
        }

        throw new AnnotationFailedException(
            request.SourceKey,
            request.RowNumber,
            request.Text,
            string.Join("; ", lastErrors),
            _retryCount);
    }

    private async Task WriteAttemptAsync(
        PharmaAnnotationModelRequest request,
        int attempt,
        bool success,
        IReadOnlyList<string> validationErrors,
        string? exception,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_attemptsOutputPath))
            return;

        var directory = Path.GetDirectoryName(_attemptsOutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var line = JsonSerializer.Serialize(new
        {
            request.SourceKey,
            request.RowNumber,
            Attempt = attempt,
            Success = success,
            ValidationErrors = validationErrors,
            Exception = exception,
        }, JsonSerializerOptions.Web);

        await File.AppendAllTextAsync(_attemptsOutputPath, line + Environment.NewLine, cancellationToken);
    }
}
