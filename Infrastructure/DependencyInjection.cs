using Application.Common.Interfaces;
using Application.Common.Interfaces.Authentication;
using Application.Common.Interfaces.Repositories;
using Infrastructure.Data;
using Infrastructure.BackgroundServices;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                   .UseSnakeCaseNamingConvention());
        services.AddHttpContextAccessor();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAuctionReadQueries, AuctionReadQueries>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
     services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuctionAuditLogRepository, AuctionAuditLogRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IIdempotencyRecordRepository, IdempotencyRecordRepository>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuctionRepository, AuctionRepository>();
        services.AddScoped<IBidRepository, BidRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
      services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IOutboxSink, LoggerOutboxSink>();
       services.AddHostedService<OutboxBackgroundService>();
        
        return services;
    }
}