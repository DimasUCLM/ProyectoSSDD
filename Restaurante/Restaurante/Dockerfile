# Imagen base para compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiamos el csproj y restauramos dependencias
COPY ["Restaurante.csproj", "./"]
RUN dotnet restore "Restaurante.csproj"

# Copiamos todo el resto de archivos y construimos
COPY . .

# Construimos y publicamos
RUN dotnet publish "Restaurante.csproj" -c Release -o /app/publish

# Imagen final para ejecución
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Exponer puertos
EXPOSE 5000
ENTRYPOINT ["dotnet", "Restaurante.dll"]