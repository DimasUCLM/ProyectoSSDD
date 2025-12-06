using Restaurante.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => 
{ 
    options.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http2); 
}); 


builder.Services.AddGrpc(); 

builder.Services.AddSingleton<RestauranteService>(); 

var app = builder.Build();

app.MapGrpcService<RestauranteService>(); 


app.MapGet("/", () => "Servidor gRPC del Restaurante ejecutándose..."); 

Console.WriteLine("Iniciando servidor gRPC del Restaurante..."); 
Console.WriteLine("Presiona Ctrl+C para salir"); 

app.Run();
