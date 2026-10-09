using LedgerApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Retrieve connection string and register Db context.
var connectionString = builder.Configuration.GetConnectionString("Ledger");
builder.Services.AddDbContext<LedgerDbContext>(ctx => ctx.UseSqlite(connectionString));

var app = builder.Build();

// Migrate the Db.
using (var scope = app.Services.CreateScope())
	scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.Migrate();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{

}
