using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
IConfiguration configuration = builder.Configuration;
IWebHostEnvironment environment = builder.Environment;

// --- Services ---
builder.Services.AddControllers();

// Minio Configuration Injection
builder.Services.AddTransient<MinioProvider.Helper.MinioConfigurations>
    (conf => new EC.Core.Minio.MinioConfigurations(
        Environment.GetEnvironmentVariable("MINIO_URL"),
        Environment.GetEnvironmentVariable("MINIO_USERNAME"),
        Environment.GetEnvironmentVariable("MINIO_PASSWORD")));

// Minio Services Injection - See DependencyInjection.cs
builder.Services.InjectMinioServices();

// Enable CORS (optional: adjust origin as needed)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "My API",
        Version = "v1",
        Description = "API boilerplate for Minio Provider Test"
    });
});

// --- Build ---
var app = builder.Build();

// --- Middleware ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
        c.RoutePrefix = string.Empty; // Swagger at root
    });
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("DefaultPolicy");

app.UseAuthorization();

app.MapControllers();

app.Run();
