# Etap 1 — build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KatalogCzesci/KatalogCzesci.csproj KatalogCzesci/
RUN dotnet restore KatalogCzesci/KatalogCzesci.csproj

COPY KatalogCzesci/ KatalogCzesci/

WORKDIR /src/KatalogCzesci
RUN dotnet publish KatalogCzesci.csproj -c Release -o /app/publish --no-restore

# Etap 2 — runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8050

ENTRYPOINT ["dotnet", "KatalogCzesci.dll"]
