using DemoApi;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
var database = await DemoDatabaseConnection.CreateAsync(builder.Configuration);

var dbContextOptions = new DbContextOptionsBuilder<DemoDbContext>()
    .UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString))
    .Options;

await using (var db = new DemoDbContext(dbContextOptions))
{
    await db.Database.EnsureCreatedAsync();
}

builder.Services.AddSingleton(database);
builder.Services.AddDbContext<DemoDbContext>(options =>
    options.UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString)));

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.RequestQuery |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;
});

builder.Host.UseSerilog((_, _, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Sink(new MySqlLogSink(database.ConnectionString));
});

var app = builder.Build();

app.Lifetime.ApplicationStopped.Register(() => database.DisposeAsync().AsTask().GetAwaiter().GetResult());

app.UseHttpLogging();

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

    var item = new Item
    {
        Name = request.Name.Trim(),
        Description = request.Description?.Trim(),
        Price = request.Price,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow
    };

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

    item.Name = request.Name.Trim();
    item.Description = request.Description?.Trim();
    item.Price = request.Price;
    item.UpdatedUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();

    logger.LogInformation("Updated item {ItemId}", item.Id);
    return Results.Ok(item);
});

app.Run();

static Dictionary<string, string[]>? Validate(ItemUpsertRequest request)
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

public partial class Program;
