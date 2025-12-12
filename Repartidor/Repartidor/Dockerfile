# Imagen base para compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Repartidor.csproj", "./"]
RUN dotnet restore "Repartidor.csproj"

COPY . .

RUN dotnet publish "Repartidor.csproj" -c Release -o /app/publish

# Imagen final para ejecución
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Repartidor.dll"]