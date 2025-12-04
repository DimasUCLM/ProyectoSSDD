using Restaurante.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configurar Kestrel para permitir HTTP/2 sin TLS 
builder.WebHost.ConfigureKestrel(options => 
{ 
    // Configurar el servidor para escuchar en HTTP/2 sin TLS en el puerto 5000 
    options.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http2); 
}); 

// Agregar servicios gRPC 
builder.Services.AddGrpc(); 

// Registrar RestauranteService como Singleton para mantener el estado 
builder.Services.AddSingleton<RestauranteService>(); 

var app = builder.Build();

// Registra el servicio gRPC para que pueda recibir y gestionar invocaciones remotas. 
app.MapGrpcService<RestauranteService>(); 

// Configurar una página de inicio simple 
app.MapGet("/", () => "Servidor gRPC del Restaurante ejecutándose..."); 

Console.WriteLine("Iniciando servidor gRPC del Restaurante..."); 
Console.WriteLine("Presiona Ctrl+C para salir"); 

// Ejecutar la aplicación
app.Run();
