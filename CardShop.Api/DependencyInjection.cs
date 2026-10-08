using CardShop.Api.ExceptionHandling;
using System.Text.Json.Serialization;

namespace CardShop.Api
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApiServices(
            this IServiceCollection services)
        {
            services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
    });

            services.AddProblemDetails();

            services.AddExceptionHandler<GlobalExceptionHandler>();

            return services;
        }
    }
}
