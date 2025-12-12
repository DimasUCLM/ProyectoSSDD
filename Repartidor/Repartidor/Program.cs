using Grpc.Net.Client;
using Grpc.Core; 
using Google.Protobuf.WellKnownTypes; 
using Restaurante.Protos; 

namespace Repartidor;

class Program
{
    static async Task Main(string[] args)
    {
        var httpHandler = new HttpClientHandler();
        httpHandler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        var channel = GrpcChannel.ForAddress("http://localhost:5000",
            new GrpcChannelOptions { HttpHandler = httpHandler });

        var client = new RestauranteService.RestauranteServiceClient(channel);

        Console.Title = "Repartidor - Sistema Distribuido";
        Console.WriteLine("--- CLIENTE REPARTIDOR ---");
        Console.WriteLine("1. Entrar en turno (Empezar a repartir)");
        Console.WriteLine("2. Salir");

        while (true)
        {
            Console.Write("\nElige una opción (1-2): ");
            var opcion = Console.ReadLine();

            switch (opcion)
            {
                case "1":
                    await RealizarReparto(client);
                    break;
                case "2":
                    return;
                default:
                    Console.WriteLine("Opción no válida");
                    break;
            }
        }
    }

    static async Task RealizarReparto(RestauranteService.RestauranteServiceClient client)
    {
        try
        {
            // --- PASO 1: ESPERAR COMIDA ---
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\nEsperando en ventanilla por un pedido...");
            
            var pedidoInfo = await client.CogerPedidoAsync(new Empty());

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"--> ¡Tengo el pedido ID: {pedidoInfo.IdPedido}!");
            Console.WriteLine($"    Destino a {pedidoInfo.Distancia} km.");


            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Buscando ciclomotor disponible...");

            var motoInfo = await client.CogerMotoAsync(new Empty());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"--> ¡Moto conseguida! (ID: {motoInfo.IdMoto})");
            

            Console.WriteLine($"\n[3/3] Saliendo a repartir...");
            Console.WriteLine("      Viajando...");

            await Task.Delay(pedidoInfo.Distancia * 1000); 

            Console.WriteLine("      ¡Pedido entregado! Volviendo al restaurante...");
            
            await client.ConfirmarEntregaAsync(new EntregaRequest { 
                IdPedido = pedidoInfo.IdPedido,
                IdMoto = motoInfo.IdMoto 
            });
            
            Console.ResetColor();
            Console.WriteLine("--> Regreso completado. Moto devuelta y lista.");
            
        }
        catch (RpcException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError de comunicación: {ex.Status.Detail}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError inesperado: {ex.Message}");
            Console.ResetColor();
        }
    }
}