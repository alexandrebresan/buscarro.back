using Buscarro.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Buscarro.Api.Middlewares;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (ValidationException ex) { await Write(context,400,"validation_error",ex.Message); }
        catch (ConflictException ex) { await Write(context,409,"conflict",ex.Message); }
        catch (CatalogUnavailableException) { await Write(context,503,"catalog_unavailable",CatalogUnavailableException.PublicMessage); }
        catch (Exception ex)
        {
            logger.LogError(ex,"Unhandled API error for {Method} {Path}.",context.Request.Method,context.Request.Path);
            await Write(context,500,"internal_error","Ocorreu um erro interno.");
        }
    }
    private static async Task Write(HttpContext context,int status,string code,string detail)
    {
        context.Response.StatusCode=status;
        context.Response.ContentType="application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status=status,Title=code,Detail=detail,Extensions={ ["code"]=code } });
    }
}
