#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0.1-alpine3.14 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0.1-alpine3.14 AS build
WORKDIR /src
COPY ["FCProjectBot/FCProjectBot.csproj", "FCProjectBot/"]
RUN dotnet restore "FCProjectBot/FCProjectBot.csproj"
COPY . .
WORKDIR "/src/FCProjectBot"
RUN dotnet build "FCProjectBot.csproj" -c Release -o /app/build

FROM base AS final
WORKDIR /app
COPY --from=build /app/build .
ENTRYPOINT ["dotnet", "FCProjectBot.dll"]
