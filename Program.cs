using CoberPush.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCoberPush(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCoberPush();

app.Run();

/// <summary>Expuesta para las pruebas de integración (WebApplicationFactory).</summary>
public partial class Program;
