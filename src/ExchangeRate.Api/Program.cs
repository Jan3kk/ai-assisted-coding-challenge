using ExchangeRate.Api.Endpoints;
using ExchangeRate.Api.Extensions;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExchangeRateConfiguration(builder.Configuration);
builder.Services.AddExchangeRateProviders();
builder.Services.AddExchangeRateInfrastructure();

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = feature?.Error;

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Detail = exception?.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.MapExchangeRateEndpoints();

app.Run();

// Make Program accessible to test project (WebApplicationFactory<Program>)
public partial class Program { }
