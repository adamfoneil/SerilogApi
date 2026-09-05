using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SerilogApi;

namespace SerilogQueryApi;

public static class EndpointExtensions
{
    public static void MapLogLevelEndpoints(this IEndpointRouteBuilder routeBuilder, AuthorizationPolicy policy)
    {
        var grp = routeBuilder.MapGroup("/serilog").RequireAuthorization(policy);

        grp.MapGet("/errors/{timeExpression?}", async (ILogQuery query, string? timeExpression) =>
        {
            var results = await query.RecentErrorsAsync(timeExpression);
            return Results.Ok(results);
        });

        grp.MapGet("/trace/{requestId}", async (ILogQuery query, string requestId) =>
        {
            var results = await query.QueryAsync(new()
            {
                Properties = new()
                {
                    ["RequestId"] = requestId
                }
            });
        });

        grp.MapPost("/query", async (ILogQuery query, LogCriteria criteria) =>
        {
            var results = await query.QueryAsync(criteria);
            return Results.Ok(results);
        });
    }
}
