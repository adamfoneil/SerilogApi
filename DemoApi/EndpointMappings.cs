namespace DemoApi;

using Microsoft.EntityFrameworkCore;

public static class EndpointMappings
{
    public static WebApplication MapDemoEndpoints(this WebApplication app)
    {
        app.MapGet("/", (DemoDatabaseConnection connection) => Results.Ok(new
        {
            message = "Demo API is running.",
            itemsEndpoint = "/items",
            database = connection.IsDisposable ? "testcontainer" : "configured"
        }));

        var items = app.MapGroup("/items");

        items.MapGet("/", async (DemoDbContext db, ILogger<Program> logger) =>
        {
            logger.LogInformation("Listing items");
            var results = await db.Items.OrderBy(item => item.Id).ToListAsync();
            return Results.Ok(results);
        });

        items.MapGet("/{id:int}", async (int id, DemoDbContext db, ILogger<Program> logger) =>
        {
            logger.LogInformation("Retrieving item {ItemId}", id);
            var item = await db.Items.FindAsync(id);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        items.MapPost("/", async (ItemUpsertRequest request, DemoDbContext db, ILogger<Program> logger) =>
        {
            var validationProblem = Validate(request);
            if (validationProblem is not null)
            {
                return Results.ValidationProblem(validationProblem);
            }

            var item = CreateItem(request);

            db.Items.Add(item);
            await db.SaveChangesAsync();

            logger.LogInformation("Created item {ItemId} named {ItemName}", item.Id, item.Name);
            return Results.Created($"/items/{item.Id}", item);
        });

        items.MapPut("/{id:int}", async (int id, ItemUpsertRequest request, DemoDbContext db, ILogger<Program> logger) =>
        {
            var validationProblem = Validate(request);
            if (validationProblem is not null)
            {
                return Results.ValidationProblem(validationProblem);
            }

            var item = await db.Items.FindAsync(id);
            if (item is null)
            {
                return Results.NotFound();
            }

            ApplyRequest(item, request);

            await db.SaveChangesAsync();

            logger.LogInformation("Updated item {ItemId}", item.Id);
            return Results.Ok(item);
        });

        return app;
    }

    private static Dictionary<string, string[]>? Validate(ItemUpsertRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }

        if (request.Price < 0)
        {
            errors["price"] = ["Price must be zero or greater."];
        }

        return errors.Count == 0 ? null : errors;
    }

    private static Item CreateItem(ItemUpsertRequest request)
    {
        var item = new Item
        {
            CreatedUtc = DateTime.UtcNow
        };

        ApplyRequest(item, request);
        return item;
    }

    private static void ApplyRequest(Item item, ItemUpsertRequest request)
    {
        item.Name = request.Name.Trim();
        item.Description = request.Description?.Trim();
        item.Price = request.Price;
        item.UpdatedUtc = DateTime.UtcNow;
    }
}
