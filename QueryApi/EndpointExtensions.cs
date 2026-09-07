using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SerilogQueryApi;

public record QueryRequest(
    LogCriteria Criteria,
    JsonColumn[]? Columns);

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

        grp.MapPost("/trace/{requestId}", async (ILogQuery query, string requestId, [FromBody]JsonColumn[] columns) =>
        {
            var results = await query.TraceAsync(requestId, columns);
            return Results.Ok(results);
        });

        grp.MapPost("/query", async (ILogQuery query, QueryRequest request) =>
        {
            var results = await query.QueryAsync(request.Criteria, request.Columns ?? []);
            return Results.Ok(results);
        });
    }
}
