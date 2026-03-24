FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy just the project files for all layers to facilitate caching
COPY ["src/Analytics.Api/Analytics.Api.csproj", "src/Analytics.Api/"]
COPY ["src/BuildingBlocks/Shared/Shared.csproj", "src/BuildingBlocks/Shared/"]
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "src/BuildingBlocks/Contracts/"]
COPY ["src/BuildingBlocks/Infrastructure/Infrastructure.csproj", "src/BuildingBlocks/Infrastructure/"]

# Restore dependencies for the specific project
RUN dotnet restore "src/Analytics.Api/Analytics.Api.csproj"

# Copy the rest of the source code
COPY . .

# Build and publish
WORKDIR "/src/src/Analytics.Api"
RUN dotnet publish "Analytics.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Analytics.Api.dll"]
