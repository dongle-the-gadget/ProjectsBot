#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/runtime:6.0-alpine3.14 AS base
WORKDIR /app

FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:6.0-alpine3.14 AS build
WORKDIR /src
COPY "FCProjectBot.csproj" .
RUN dotnet restore "FCProjectBot.csproj"
COPY . .
RUN dotnet build "FCProjectBot.csproj" -c Release -o /app/build

FROM base AS final
WORKDIR /app
COPY --from=build /app/build .
ENTRYPOINT ["dotnet", "FCProjectBot.dll"]
