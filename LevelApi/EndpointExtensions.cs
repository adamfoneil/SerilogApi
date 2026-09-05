using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Serilog.Events;

namespace SerilogLevelApi;

public static class EndpointExtensions
{
    public record LogLevelDto(string Category, string Level, DateTime? ExpiresAfter);

    public static void MapLogLevelEndpoints(this IEndpointRouteBuilder routeBuilder, AuthorizationPolicy policy)
    {
        var grp = routeBuilder.MapGroup("/serilog").RequireAuthorization(policy);

        grp.MapGet("/levels", async (ILogLevelOverrides overrides) =>
        {
            var levels = await overrides.GetAsync();
            var dto = levels.Select(l => new LogLevelDto(l.Key, l.Value.Level.ToString(), l.Value.ExpiresUtc)).ToArray();
            return Results.Ok(dto);
        });

        grp.MapPut("/debug/{category:alpha}", async (ILogLevelOverrides overrides, string? category) =>
        {            
            await overrides.SetAsync(category ?? "Default", LogEventLevel.Debug, TimeSpan.FromMinutes(10));
        });        
    }
}
