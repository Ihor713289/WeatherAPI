FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy project files first so 'dotnet restore' is cached until a project file changes.
COPY ["ImageStarter/ImageStarter.csproj", "ImageStarter/"]
COPY ["WeatherAPI.Shared/WeatherAPI.Shared.csproj", "WeatherAPI.Shared/"]
RUN dotnet restore "ImageStarter/ImageStarter.csproj"
COPY . .
WORKDIR "/src/ImageStarter"
RUN dotnet publish "./ImageStarter.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ImageStarter.dll"]
