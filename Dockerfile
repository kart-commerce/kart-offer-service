FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartOfferService.sln Directory.Build.props nuget.config ./
COPY packages/ packages/
COPY src/Api/KartOfferService.Api.csproj src/Api/
COPY src/Application/KartOfferService.Application.csproj src/Application/
COPY src/Domain/KartOfferService.Domain.csproj src/Domain/
COPY src/Infrastructure/KartOfferService.Infrastructure.csproj src/Infrastructure/
RUN dotnet restore src/Api/KartOfferService.Api.csproj

COPY src/ src/
COPY contracts/ contracts/
RUN dotnet publish src/Api/KartOfferService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KartOfferService.Api.dll"]
