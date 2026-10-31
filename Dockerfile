
# ========================================
# STAGE 1: BUILD
# ========================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy the project files first.
COPY ["CardShop.Api/CardShop.Api.csproj", "CardShop.Api/"]
COPY ["CardShop.Application/CardShop.Application.csproj", "CardShop.Application/"]
COPY ["CardShop.Domain/CardShop.Domain.csproj", "CardShop.Domain/"]
COPY ["CardShop.Infrastructure/CardShop.Infrastructure.csproj", "CardShop.Infrastructure/"]

# Restore NuGet dependencies.
RUN dotnet restore "CardShop.Api/CardShop.Api.csproj"

# Copy the remaining source files.
COPY . .

# Publish the application.
RUN dotnet publish "CardShop.Api/CardShop.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ========================================
# STAGE 2: RUNTIME
# ========================================

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

# Copy published files from build stage.
COPY --from=build /app/publish .

# ASP.NET Core listens on port 8080.
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

# Start CardShop.
ENTRYPOINT ["dotnet", "CardShop.Api.dll"]
