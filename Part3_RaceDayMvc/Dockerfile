# syntax=docker/dockerfile:1
# RaceDay MVC web app. Build context: Part3_RaceDayMvc/

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached unless the .csproj changes)
COPY RaceDay.Web/RaceDay.Web.csproj RaceDay.Web/
RUN dotnet restore RaceDay.Web/RaceDay.Web.csproj

COPY RaceDay.Web/ RaceDay.Web/
RUN dotnet publish RaceDay.Web/RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Listen on plain HTTP inside the container; the API address is supplied at run time.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    UseHttpsRedirection=false \
    Api__BaseUrl=http://host.docker.internal:5100/
EXPOSE 8080

# Run as the non-root user that ships with the .NET 8 images
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
