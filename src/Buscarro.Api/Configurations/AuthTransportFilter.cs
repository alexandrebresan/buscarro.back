using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Buscarro.Api.Configurations;

public sealed class AuthTransportFilter(AuthAntiforgeryService antiforgery) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.Pragma = "no-cache";
        if (HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            try
            {
                await antiforgery.ValidateAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "csrf_invalid",
                    Detail = "Requisicao de autenticacao nao autorizada.",
                    Extensions = { ["code"] = "csrf_invalid" }
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }
        await next();
    }
}
