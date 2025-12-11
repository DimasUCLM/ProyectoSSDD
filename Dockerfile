# Etapa de construcción
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar el archivo de proyecto y restaurar dependencias
COPY ["Restaurante.csproj", "./"]
RUN dotnet restore "./Restaurante.csproj"

# Copiar todo el código fuente
COPY . .

# Construir y publicar
RUN dotnet build "Restaurante.csproj" -c Release -o /app/build
RUN dotnet publish "Restaurante.csproj" -c Release -o /app/publish

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copiar los archivos publicados
COPY --from=build /app/publish .

# Exponer solo el puerto 5000 (HTTP/2 para gRPC)
EXPOSE 5000

# Comando de inicio
ENTRYPOINT ["dotnet", "Restaurante.dll"]