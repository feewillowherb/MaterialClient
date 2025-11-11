using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore.SwaggerUI;
using Volo.Abp.AspNetCore;
using Volo.Abp.Modularity;
using Volo.Abp;
using MaterialClient.Common;
using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;

namespace MaterialClient.HttpHost;

[DependsOn(
    typeof(MaterialClientCommonModule),
    typeof(AbpAspNetCoreModule)
)]
public class MaterialClientHttpHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var configuration = context.Services.GetConfiguration();

        // Configure controllers
        services.AddControllers();
        
        // Configure Swagger/OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MaterialClient API",
                Version = "v1",
                Description = "API for MaterialClient hardware interfaces testing"
            });
        });

        // Configure CORS if needed (minimal for now)
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        // Auto-migrate database on startup
        using (var scope = context.ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MaterialClientDbContext>();
            dbContext.Database.Migrate();
        }

        // Configure Swagger
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "MaterialClient API v1");
            options.RoutePrefix = "swagger";
        });

        // Configure routing
        app.UseRouting();
        
        // Configure CORS
        app.UseCors();
        
        // Map controllers
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}

