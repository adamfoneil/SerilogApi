using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SerilogQueryApi;

public static class EndpointExtensions
{
    public static void MapLogQueryEndpoints(this IEndpointRouteBuilder routeBuilder, AuthorizationPolicy policy)
    {
        var grp = routeBuilder.MapGroup("/serilog/query").RequireAuthorization(policy);

        grp.MapPost("/errors/{timeExpression?}", async ([FromServices] ILogQuery query, string? timeExpression) =>
        {
            var results = await query.RecentErrorsAsync(timeExpression);
            return Results.Ok(results);
        });

        grp.MapPost("/trace/{requestId}", async ([FromServices] ILogQuery query, string requestId, [FromBody]JsonColumn[] columns) =>
        {
            var results = await query.TraceAsync(requestId, columns);
            return Results.Ok(results);
        });

        grp.MapPost("/", async ([FromServices] ILogQuery query, [FromBody] QueryRequest? request) =>
        {
            request ??= new QueryRequest(Criteria: new() { MaxResults = 50 });
            
            var results = await query.QueryAsync(request.Criteria, request.Columns ?? [], SortOptions.TimestampAsc);
            return Results.Ok(results);
        });
    }
}
