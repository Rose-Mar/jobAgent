using JobAgent.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Bind the "RegonApi" section from appsettings.Development.json to the RegonSettings class
builder.Services.Configure<RegonSettings>(builder.Configuration.GetSection("RegonApi"));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
