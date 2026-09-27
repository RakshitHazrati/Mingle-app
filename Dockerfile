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
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Mingle.Api.dll"]
