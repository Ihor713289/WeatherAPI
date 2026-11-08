FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
EXPOSE 8080
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy project files first so 'dotnet restore' is cached until a project file changes.
COPY ["WeatherAPI/WeatherAPI.csproj", "WeatherAPI/"]
COPY ["WeatherAPI.Shared/WeatherAPI.Shared.csproj", "WeatherAPI.Shared/"]
RUN dotnet restore "WeatherAPI/WeatherAPI.csproj"
COPY . .
WORKDIR "/src/WeatherAPI"
RUN dotnet publish "./WeatherAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WeatherAPI.dll"]
