FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Mingle.Api/Mingle.Api.csproj Mingle.Api/
RUN dotnet restore Mingle.Api/Mingle.Api.csproj

COPY Mingle.Api/ Mingle.Api/
RUN dotnet publish Mingle.Api/Mingle.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
# Render supplies PORT=10000. Other hosts can omit it and use port 8080.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet Mingle.Api.dll"]
