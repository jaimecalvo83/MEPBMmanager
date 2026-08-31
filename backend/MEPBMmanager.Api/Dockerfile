# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MEPBMmanager.Api/MEPBMmanager.Api.csproj MEPBMmanager.Api/
COPY MEPBMmanager.Domain/MEPBMmanager.Domain.csproj MEPBMmanager.Domain/
COPY MEPBMmanager.Infrastructure/MEPBMmanager.Infrastructure.csproj MEPBMmanager.Infrastructure/
RUN dotnet restore MEPBMmanager.Api/MEPBMmanager.Api.csproj

COPY MEPBMmanager.Api/ MEPBMmanager.Api/
COPY MEPBMmanager.Domain/ MEPBMmanager.Domain/
COPY MEPBMmanager.Infrastructure/ MEPBMmanager.Infrastructure/
RUN dotnet publish MEPBMmanager.Api/MEPBMmanager.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5171
ENV ASPNETCORE_URLS=http://+:5171
ENTRYPOINT ["dotnet", "MEPBMmanager.Api.dll"]
