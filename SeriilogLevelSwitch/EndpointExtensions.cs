using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Serilog.Events;

namespace SeriilogLevelSwitch;

public static class EndpointExtensions
{
    public static void MapLogLevelEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        routeBuilder.MapPut("/serilog/level/{level:alpha}", async (ILogLevelStore levelStore, string level) =>
        {
            var levelVal = Enum.Parse<LogEventLevel>(level, true);
            await levelStore.SetLevelAsync("Default", levelVal, TimeSpan.FromMinutes(10));
        });
    }
}
