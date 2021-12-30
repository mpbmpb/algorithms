FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["algorithms/algorithms.csproj", "algorithms/"]
RUN dotnet restore "algorithms/algorithms.csproj"
COPY . .
WORKDIR "/src/algorithms"
RUN dotnet build "algorithms.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "algorithms.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "algorithms.dll"]
