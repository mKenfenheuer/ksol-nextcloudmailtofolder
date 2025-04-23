#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["KSol.NextCloudMailToFolder.csproj", "./"]
RUN dotnet restore "KSol.NextCloudMailToFolder.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "KSol.NextCloudMailToFolder.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KSol.NextCloudMailToFolder.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_HTTP_PORTS=80
ENTRYPOINT ["dotnet", "KSol.NextCloudMailToFolder.dll"]