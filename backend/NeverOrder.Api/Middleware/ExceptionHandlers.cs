using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using NeverOrder.Application.Auth;
using NeverOrder.Application.Common;
using NeverOrder.Domain.Carts;
using NeverOrder.Domain.Catalog;
using NeverOrder.Domain.Common;
using NeverOrder.Domain.Orders;

namespace NeverOrder.Api.Middleware;

/// <summary>Maps expected rule violations onto status codes without leaking internals.</summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public DomainExceptionHandler(IProblemDetailsService problemDetails) => _problemDetails = problemDetails;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        var status = domainException switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            AuthenticationFailedException => StatusCodes.Status401Unauthorized,
            InvalidOrderTransitionException => StatusCodes.Status409Conflict,
            RegistrationFailedException => StatusCodes.Status400BadRequest,
            CheckoutValidationException => StatusCodes.Status400BadRequest,
            InvalidCartOperationException => StatusCodes.Status400BadRequest,
            ProductUnavailableException => StatusCodes.Status400BadRequest,
            EmptyOrderException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(status),
            Detail = domainException.Message
        };

        if (domainException is RegistrationFailedException registration)
        {
            problemDetails.Extensions["errors"] = registration.Errors;
        }

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = domainException,
            ProblemDetails = problemDetails
        });
    }

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "Authentication failed",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Request could not be completed"
    };
}

/// <summary>Last resort: log the detail, return only a trace id.</summary>
public sealed class UnhandledExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<UnhandledExceptionHandler> _logger;

    public UnhandledExceptionHandler(IProblemDetailsService problemDetails, ILogger<UnhandledExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.Features.Get<IHttpActivityFeature>()?.Activity.Id
            ?? httpContext.TraceIdentifier;

        _logger.LogError(exception, "Unhandled exception for {Path} (trace {TraceId})", httpContext.Request.Path, traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong",
                Detail = "An unexpected error occurred. Quote the trace id when reporting this.",
                Extensions = { ["traceId"] = traceId }
            }
        });
    }
}
