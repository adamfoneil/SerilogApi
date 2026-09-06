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
            var useCategory = category ?? "Default";
            try
            {
                await overrides.SetAsync(useCategory, LogEventLevel.Debug, TimeSpan.FromMinutes(expiresIn ?? 10));
                return Results.Ok();
            }
            catch (Exception exc)
            {                
                return Results.Problem(exc.Message);
            }
        });

        grp.MapPut("/override/{category:alpha}/{level:alpha}", async (ILogLevelOverrides overrides, string category, string level, [FromQuery] int? expiresIn) =>
        {
            if (Enum.TryParse<LogEventLevel>(level, out var levelVal))
            {
                try
                {
                    var expireMinutes = expiresIn.HasValue ? TimeSpan.FromMinutes(expiresIn.Value) : default;
                    await overrides.SetAsync(category, levelVal, expireMinutes);
                    return Results.Ok();
                }
                catch (Exception exc)
                {                    
                    return Results.Problem(exc.Message);
                }
            }

            return Results.BadRequest($"Couldn't parse level value: {level}");
        });
        
        grp.MapDelete("/overrides/remove", async (ILogLevelOverrides overrides) => await overrides.RemoveTemporaryAsync());
    }
}
