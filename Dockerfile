FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish NordicBeesERP.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# .NET 10 aspnet base image is Ubuntu 24.04 (noble) — NOT Debian (Debian images are not shipped for .NET 10).
# Pin Ghostscript to the noble apt base version. Bumped 2026-08-29: ubuntu7 -> ubuntu7.8 (security update
# replaced the earlier pinned build in the noble repo, breaking the old pin). Bump again if this recurs.
RUN apt-get update && apt-get install -y ghostscript=10.02.1~dfsg1-0ubuntu7.8 && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NordicBeesERP.dll"]
