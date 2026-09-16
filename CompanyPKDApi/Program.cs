using CompanyPKDApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Company PKD API",
        Version = "v1",
        Description = "API do wyszukiwania firm i ich stron WWW"
    });
});

builder.Services.AddSingleton<CompanyService>();
builder.Services.AddScoped<GUSService>();
builder.Services.AddScoped<KRSService>();
builder.Services.AddScoped<MasterWebsiteFinder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();