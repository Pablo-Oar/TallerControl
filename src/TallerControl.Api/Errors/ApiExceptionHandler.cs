using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallerControl.Application.Common;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.Errors;

/// <summary>
/// Traduce las excepciones a respuestas RFC 7807 (doc 05 §1). Nunca devuelve el mensaje ni la traza
/// de una excepción inesperada: el detalle queda en el log, enlazado por <c>traceId</c>.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // El cliente cortó la conexión: no hay a quién responder.
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var problem = Map(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Error no controlado al procesar la solicitud.");
        }
        else
        {
            _logger.LogInformation(
                "Solicitud rechazada con {StatusCode}: {ExceptionType}",
                problem.Status,
                exception.GetType().Name);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        // Un recurso de otro taller responde igual que uno inexistente.
        NotFoundException or TenantViolationException => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "No encontrado",
            Detail = "El recurso no existe.",
        },

        BusinessRuleException rule => new HttpValidationProblemDetails(
            rule.Errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "No se puede completar la operación",
            Detail = rule.Message,
        },

        ConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflicto",
            Detail = conflict.Message,
        },

        // Concurrencia optimista (xmin / If-Match): otro usuario modificó el recurso.
        DbUpdateConcurrencyException => new ProblemDetails
        {
            Status = StatusCodes.Status412PreconditionFailed,
            Title = "El recurso cambió",
            Detail = "Otra persona modificó este dato. Recargá y volvé a intentar.",
        },

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto",
                Detail = "Ya existe un registro con esos datos.",
            },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno",
            Detail = "Ocurrió un error inesperado. Si persiste, informá el código de seguimiento.",
        },
    };
}
