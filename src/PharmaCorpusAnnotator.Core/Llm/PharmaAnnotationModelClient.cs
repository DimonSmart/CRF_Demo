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
        IReadOnlyList<string> lastErrors = [];
        for (int attempt = 0; attempt < _retryCount; attempt++)
        {
            var messages = _promptBuilder.BuildMessages(request, lastErrors);

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
                LogTypedResult(request, labelResponse);
                var labelValidation = _labelValidator.Validate(request, labelResponse);

                if (!labelValidation.IsValid)
                {
                    lastErrors = labelValidation.Errors;
                    await WriteAttemptAsync(request, attempt + 1, false, lastErrors, null, rawResponse, cancellationToken);

                    LogLabelValidationFailure(request, labelResponse, lastErrors);
                    _logger.LogWarning("Attempt {Attempt}/{Max} LLM labels validation failed for {Key}:{Row}: {Error}",
                        attempt + 1, _retryCount, request.SourceKey, request.RowNumber, string.Join("; ", lastErrors));
                    continue;
                }

                _logger.LogDebug("LLM labels validation passed for {SourceKey}:{RowNumber}",
                    request.SourceKey, request.RowNumber);

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

    private void LogRawResponse(PharmaAnnotationModelRequest request, int attempt, string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            _logger.LogWarning("LLM raw response text is empty for {Key}:{Row} attempt {Attempt}/{Max}",
                request.SourceKey, request.RowNumber, attempt, _retryCount);
            return;
        }

        _logger.LogDebug("LLM raw response text for {Key}:{Row} attempt {Attempt}/{Max}:{NewLine}{Json}",
            request.SourceKey,
            request.RowNumber,
            attempt,
            _retryCount,
            Environment.NewLine,
            rawResponse);
    }

    private void LogTypedResult(
        PharmaAnnotationModelRequest request,
        PharmaLabelAnnotationResponse? response)
    {
        if (response is null)
        {
            _logger.LogWarning("LLM typed result is null for {SourceKey}:{RowNumber}",
                request.SourceKey, request.RowNumber);
            return;
        }

        if (response.Labels is null)
        {
            _logger.LogWarning("LLM labels array is null for {SourceKey}:{RowNumber}",
                request.SourceKey, request.RowNumber);
            return;
        }

        if (response.Labels.Length == 0)
        {
            _logger.LogWarning("LLM labels array is empty for {SourceKey}:{RowNumber}",
                request.SourceKey, request.RowNumber);
            return;
        }

        _logger.LogDebug(
            "LLM parsed labels for {SourceKey}:{RowNumber}: {Labels}",
            request.SourceKey,
            request.RowNumber,
            string.Join("|", response.Labels));
    }

    private void LogLabelValidationFailure(
        PharmaAnnotationModelRequest request,
        PharmaLabelAnnotationResponse? response,
        IReadOnlyList<string> errors)
    {
        if (response?.Labels is not null && response.Labels.Length != request.Tokens.Count)
        {
            _logger.LogWarning(
                "LLM labels count mismatch for {SourceKey}:{RowNumber}: expected {Expected}, actual {Actual}",
                request.SourceKey,
                request.RowNumber,
                request.Tokens.Count,
                response.Labels.Length);
        }

        _logger.LogWarning(
            "LLM labels validation failed for {SourceKey}:{RowNumber}: {Errors}",
            request.SourceKey,
            request.RowNumber,
            string.Join("; ", errors));
    }
}
