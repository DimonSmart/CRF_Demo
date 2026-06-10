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
    private readonly PharmaLabelAnnotationValidator _labelValidator;
    private readonly LabelAnnotationMapper _labelMapper;
    private readonly PharmaAnnotationValidator _validator;
    private readonly int _retryCount;
    private readonly string? _attemptsOutputPath;
    private readonly ILogger<PharmaAnnotationModelClient> _logger;

    public PharmaAnnotationModelClient(
        ChatClientAgent agent,
        PharmaAnnotationPromptBuilder promptBuilder,
        PharmaLabelAnnotationValidator labelValidator,
        LabelAnnotationMapper labelMapper,
        PharmaAnnotationValidator validator,
        int retryCount,
        string? attemptsOutputPath,
        ILoggerFactory loggerFactory)
    {
        _agent = agent;
        _promptBuilder = promptBuilder;
        _labelValidator = labelValidator;
        _labelMapper = labelMapper;
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
                    "\n\nReturn corrected JSON object:\n" +
                    "{ \"labels\": [...] }\n\n" +
                    $"Rules:\n- labels.length must be exactly {request.Tokens.Count}.\n" +
                    "- Use only allowed labels.\n" +
                    "Do not return tokens.\n" +
                    "Do not return confidence."));
            }

            string? rawResponse = null;

            try
            {
                var agentResponse = await _agent.RunAsync<PharmaLabelAnnotationResponse>(
                    messages,
                    session: null,
                    serializerOptions: JsonSerializerOptions.Web,
                    options: null,
                    cancellationToken);

                rawResponse = agentResponse.Text;
                LogRawResponse(request, attempt + 1, rawResponse);

                var labelResponse = agentResponse.Result;
                var labelValidation = _labelValidator.Validate(request, labelResponse);

                if (!labelValidation.IsValid)
                {
                    lastErrors = labelValidation.Errors;
                    await WriteAttemptAsync(request, attempt + 1, false, lastErrors, null, rawResponse, cancellationToken);

                    _logger.LogWarning("Attempt {Attempt}/{Max} label validation failed for {Key}:{Row}: {Error}",
                        attempt + 1, _retryCount, request.SourceKey, request.RowNumber, string.Join("; ", lastErrors));
                    continue;
                }

                var response = _labelMapper.Map(request, labelResponse);
                var validation = _validator.Validate(request, response);

                if (validation.IsValid)
                {
                    await WriteAttemptAsync(request, attempt + 1, true, [], null, rawResponse, cancellationToken);
                    return response;
                }

                lastErrors = validation.Errors;
                await WriteAttemptAsync(request, attempt + 1, false, lastErrors, null, rawResponse, cancellationToken);

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
                await WriteAttemptAsync(request, attempt + 1, false, [], ex.Message, rawResponse, cancellationToken);
                _logger.LogWarning("Attempt {Attempt}/{Max} call failed for {Key}:{Row}: {Error}",
                    attempt + 1, _retryCount, request.SourceKey, request.RowNumber, ex.Message);
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
        string? rawResponse,
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
            RawResponse = rawResponse,
        }, JsonSerializerOptions.Web);

        await File.AppendAllTextAsync(_attemptsOutputPath, line + Environment.NewLine, cancellationToken);
    }

    private void LogRawResponse(PharmaAnnotationModelRequest request, int attempt, string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            _logger.LogWarning("LLM response JSON for {Key}:{Row} attempt {Attempt}/{Max}: <empty>",
                request.SourceKey, request.RowNumber, attempt, _retryCount);
            return;
        }

        _logger.LogInformation("LLM response JSON for {Key}:{Row} attempt {Attempt}/{Max}:{NewLine}{Json}",
            request.SourceKey,
            request.RowNumber,
            attempt,
            _retryCount,
            Environment.NewLine,
            rawResponse);
    }
}
