using Grpc.Net.Client;
using Grpc.Core; // Necesario para manejar RpcException
using Google.Protobuf.WellKnownTypes; // Necesario para 'Empty'
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

        Console.WriteLine("Cliente gRPC del Repartidor");
        Console.WriteLine("1. Tomar pedido para repartir");
        Console.WriteLine("2. Salir");

        while (true)
        {
            Console.Write("\nElige una opción (1-2): ");
            var opcion = Console.ReadLine();

            switch (opcion)
            {
                case "1":
                    Console.WriteLine("Esperando pedidos disponibles..."); 
                    await TomarPedidoParaRepartir(client);
                    break;
                case "2":
                    return;
                default:
                    Console.WriteLine("Opción no válida");
                    break;
            }
        }
    }

    static async Task TomarPedidoParaRepartir(RestauranteService.RestauranteServiceClient client)
    {
        try
        {

            var response = await client.TomarPedidoParaRepartirAsync(new Empty());

            Console.WriteLine($"\n>>> ¡Pedido Asignado!");
            Console.WriteLine($"ID del Pedido: {response.IdPedido}");
            Console.WriteLine($"Mensaje del Servidor: {response.Mensaje}");
            
        }
        catch (RpcException ex)
        {
            // Mostrar en pantalla el mensaje de error si falla
            Console.WriteLine($"\nError al intentar tomar pedido: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError inesperado: {ex.Message}");
        }
    }
}