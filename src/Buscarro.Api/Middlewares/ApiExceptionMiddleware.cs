using Buscarro.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Buscarro.Api.Configurations;

namespace Buscarro.Api.Middlewares;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
        }
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (ValidationException ex) { await Write(context,400,"validation_error",ex.Message); }
        catch (ConflictException ex) { await Write(context,409,"conflict",ex.Message); }
        catch (UniqueConstraintException) { await Write(context,409,"conflict","Operacao concorrente conflitou com um registro existente."); }
        catch (AuthenticationFailedException) { await Write(context,401,"authentication_failed","Credenciais invalidas."); }
        catch (AuthFlowException ex)
        {
            if (ex.StatusCode == 401 && context.Request.Path.Equals(new PathString("/api/auth/refresh")))
                AuthCookies.ClearRefresh(context);
            await Write(context,ex.StatusCode,ex.Code,"Nao foi possivel concluir a autenticacao.");
        }
        catch (AuthUnavailableException ex)
        {
            logger.LogWarning(ex,"Authentication dependency unavailable for {Path}.",context.Request.Path);
            await Write(context,503,ex.Code,"Autenticacao temporariamente indisponivel.");
        }
        catch (TransactionCommitOutcomeUnknownException ex)
        {
            logger.LogError(ex,"Transaction commit outcome unknown for {Path}.",context.Request.Path);
            if (context.Request.Path.StartsWithSegments("/api/auth")) AuthCookies.ClearRefresh(context);
            await Write(context,503,"session_state_unknown","Resultado da operacao desconhecido. Entre novamente.");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.NpgsqlException postgres && postgres.IsTransient)
        {
            logger.LogWarning(ex,"Persistence temporarily unavailable for {Path}.",context.Request.Path);
            await Write(context,503,"authentication_unavailable","Servico temporariamente indisponivel.");
        }
        catch (Npgsql.NpgsqlException ex) when (ex.IsTransient)
        {
            logger.LogWarning(ex,"PostgreSQL temporarily unavailable for {Path}.",context.Request.Path);
            await Write(context,503,"authentication_unavailable","Servico temporariamente indisponivel.");
        }
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
