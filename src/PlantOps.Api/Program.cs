using PlantOps.Infrastructure;
using PlantOps.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Composition root: the only place that knows about every layer.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.Services.InitializeDevelopmentDatabaseAsync();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
