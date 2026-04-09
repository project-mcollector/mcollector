FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Identity.Api/Identity.Api.csproj", "src/Identity.Api/"]
COPY ["src/BuildingBlocks/Utilities/Utilities.csproj", "src/BuildingBlocks/Utilities/"]
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "src/BuildingBlocks/Contracts/"]
COPY ["src/BuildingBlocks/Infrastructure/Infrastructure.csproj", "src/BuildingBlocks/Infrastructure/"]

RUN dotnet restore "src/Identity.Api/Identity.Api.csproj"

COPY . .
WORKDIR "/src/src/Identity.Api"
RUN dotnet publish "Identity.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
