# Imagen base para compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Cliente.csproj", "./"]
RUN dotnet restore "Cliente.csproj"

COPY . .

RUN dotnet publish "Cliente.csproj" -c Release -o /app/publish

# Imagen final para ejecución
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# NOTA: EJECUTAR CON -it
ENTRYPOINT ["dotnet", "Cliente.dll"]