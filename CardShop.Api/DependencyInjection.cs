using CardShop.Api.ExceptionHandling;
using CardShop.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;

namespace CardShop.Api
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApiServices(
            this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSection = configuration.GetSection(
    JwtSettings.SectionName);

            var issuer = jwtSection["Issuer"]
                ?? throw new InvalidOperationException("JWT issuer missing.");

            var audience = jwtSection["Audience"]
                ?? throw new InvalidOperationException("JWT audience missing.");

            var secret = jwtSection["SecretKey"]
                ?? throw new InvalidOperationException("JWT secret missing.");

            byte[] keyBytes;

            try
            {
                keyBytes = Convert.FromBase64String(secret);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "JWT secret must be Base64 encoded.",
                    exception);
            }

            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT signing key must contain at least 32 bytes.");
            }

            var signingKey = new SymmetricSecurityKey(keyBytes);

            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter your JWT access token.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
            });

            services.AddAuthentication(
                JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = issuer,

                            ValidateAudience = true,
                            ValidAudience = audience,

                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = signingKey,

                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.Zero,

                            RoleClaimType = ClaimTypes.Role,
                            NameClaimType = "sub"
                        };
                });

            services.AddAuthorization();
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
