using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Net;
using System.Security.Authentication;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred. CorrelationId: {CorrelationId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, ex, _env);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, IHostEnvironment env)
    {
        context.Response.ContentType = "application/problem+json";

        context.Response.StatusCode = exception switch
        {
            DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict,
            DbUpdateException dbUpdateException when IsUniqueConstraintViolation(dbUpdateException)
                => (int)HttpStatusCode.Conflict,
            InvalidStateTransitionException => (int)HttpStatusCode.Conflict,
            AuthenticationException => (int)HttpStatusCode.Unauthorized,
            UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            System.ComponentModel.DataAnnotations.ValidationException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = GetTitleForStatusCode(context.Response.StatusCode),
            Detail = GetSafeMessage(exception, context.Response.StatusCode),
            Instance = context.Request.Path
        };

        problemDetails.Extensions["correlationId"] = context.TraceIdentifier;

        return context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static string GetSafeMessage(Exception exception, int statusCode)
    {
        if (statusCode == 500)
            return "An internal server error occurred. Please try again later.";

        return exception switch
        {
            DbUpdateConcurrencyException => "The resource was modified by another operation. Please retry.",
            DbUpdateException dbUpdateException when IsUniqueConstraintViolation(dbUpdateException)
                => "A record with the same unique key already exists.",
            AuthenticationException => "Invalid email or password.",
            UnauthorizedAccessException => "You are not authorized to perform this action.",
            KeyNotFoundException => "The requested resource was not found.",
            ArgumentException => "Invalid request parameters provided.",
            InvalidStateTransitionException => exception.Message,
            System.ComponentModel.DataAnnotations.ValidationException => exception.Message,
            _ => "An error occurred processing your request."
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is DbException dbException && dbException.SqlState == "23505")
                return true;
        }

        return false;
    }

    private static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Internal Server Error"
    };
}