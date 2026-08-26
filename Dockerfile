FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish NordicBeesERP.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# .NET 10 aspnet base image is Ubuntu 24.04 (noble) — NOT Debian (Debian images are not shipped for .NET 10).
# Pin Ghostscript to the noble apt base version. Security updates may provide 10.02.1~dfsg1-0ubuntu7.8 (bump if needed).
RUN apt-get update && apt-get install -y ghostscript=10.02.1~dfsg1-0ubuntu7 && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NordicBeesERP.dll"]
