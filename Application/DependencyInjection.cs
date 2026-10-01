using Application.Common.Interfaces.Authentication;
using Application.Common.Services;
using Application.Services;
using FluentValidation;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        
        services.AddValidatorsFromAssembly(typeof(IAuthService).Assembly);
        services.AddScoped<IAuthService, AuthService>();
       services.AddScoped<IBidService, BidService>();
        services.AddScoped<IAuctionService, AuctionService>();

        return services;
    }
}