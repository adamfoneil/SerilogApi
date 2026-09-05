using DemoApi;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
var database = await DemoDatabaseConnection.CreateAsync(builder.Configuration);
var mySqlLogSink = new MySqlLogSink(database.ConnectionString);

var dbContextOptions = new DbContextOptionsBuilder<DemoDbContext>()
    .UseMySql(database.ConnectionString, ServerVersion.AutoDetect(database.ConnectionString))
    .Options;

await using (var db = new DemoDbContext(dbContextOptions))
{
    await db.Database.MigrateAsync();
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

builder.Services.AddOpenApi();

builder.Host.UseSerilog((_, _, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Sink(mySqlLogSink);
});

var app = builder.Build();

app.UseHttpLogging();

app.MapOpenApi();
app.MapScalarApiReference("/scalar/v1");

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

try
{
    await app.RunAsync();
}
finally
{
    await mySqlLogSink.DisposeAsync();
    await database.DisposeAsync();
}

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

static Item CreateItem(ItemUpsertRequest request)
{
    var item = new Item
    {
        CreatedUtc = DateTime.UtcNow
    };

    ApplyRequest(item, request);
    return item;
}

static void ApplyRequest(Item item, ItemUpsertRequest request)
{
    item.Name = request.Name.Trim();
    item.Description = request.Description?.Trim();
    item.Price = request.Price;
    item.UpdatedUtc = DateTime.UtcNow;
}

public partial class Program;
