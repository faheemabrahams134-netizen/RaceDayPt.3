# syntax=docker/dockerfile:1
# RaceDay API (Part 2 + Blob Storage). Build context: Part2_RaceDayApi/

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY RaceDay.Api/RaceDay.Api.csproj RaceDay.Api/
RUN dotnet restore RaceDay.Api/RaceDay.Api.csproj
COPY RaceDay.Api/ RaceDay.Api/
RUN dotnet publish RaceDay.Api/RaceDay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 UseHttpsRedirection=false
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
