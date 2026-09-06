using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Serilog.Events;

namespace SerilogLevelApi;

public static class EndpointExtensions
{
    public record LogLevelDto(string Category, string Level, DateTime? ExpiresAfter);

    public static void MapLogLevelEndpoints(this IEndpointRouteBuilder routeBuilder, AuthorizationPolicy policy)
    {
        var grp = routeBuilder.MapGroup("/serilog").RequireAuthorization(policy);

        grp.MapGet("/overrides", async (ILogLevelOverrides overrides) =>
        {
            var levels = await overrides.GetAsync();
            var dto = levels.Select(l => new LogLevelDto(l.Key, l.Value.Level.ToString(), l.Value.ExpiresUtc)).ToArray();
            return Results.Ok(dto);
        });

        grp.MapPut("/debug", async (ILogLevelOverrides overrides, [FromQuery] string? category, [FromQuery] int? expiresIn) =>
        {
            await overrides.SetAsync(category ?? "Default", LogEventLevel.Debug, TimeSpan.FromMinutes(expiresIn ?? 10));
        });

        grp.MapPost("/overrides/clear", async (ILogLevelOverrides overrides) =>
        {
            await overrides.ClearAsync();
        });
    }
}
