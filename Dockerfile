FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore NordicBeesERP.csproj
RUN dotnet publish NordicBeesERP.csproj -c Release -o /app/publish --no-restore
RUN test -f /app/publish/wwwroot/_framework/blazor.web.js || (echo "blazor.web.js missing from publish output" && exit 1)

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# .NET 10 aspnet base image is Ubuntu 24.04 (noble). Ghostscript pinned to 10.02.1, any Ubuntu security revision.
RUN apt-get update && apt-get install -y 'ghostscript=10.02.1~dfsg1-0ubuntu7*' && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NordicBeesERP.dll"]
