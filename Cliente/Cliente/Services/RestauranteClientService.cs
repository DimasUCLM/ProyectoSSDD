using Grpc.Net.Client;
using Restaurante.Protos;
using Grpc.Core; 

namespace Cliente.Services;

public class RestauranteClientService
{
    private readonly string _serverAddress;
    private GrpcChannel? _channel;
    private RestauranteService.RestauranteServiceClient? _client;

    public RestauranteClientService(string serverAddress = "http://localhost:5000")
    {
        _serverAddress = serverAddress;
    }

    // ERROR CS7036 SOLUCIONADO: Añadimos valor por defecto = "http://localhost:5000"
    public async Task ConnectAsync(string serverAddress = "http://localhost:5000")
    {
        string addressToUse = string.IsNullOrEmpty(serverAddress) ? _serverAddress : serverAddress;

        if (addressToUse.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        var channelOptions = new GrpcChannelOptions();
        _channel = GrpcChannel.ForAddress(addressToUse, channelOptions);
        _client = new RestauranteService.RestauranteServiceClient(_channel);
        
        Console.WriteLine($"Conectado al servidor en {addressToUse}");

        // WARNING CS1998 SOLUCIONADO: Hacemos un await ficticio para que sea verdaderamente async
        await Task.CompletedTask; 
    }

    public async Task DisconnectAsync()
    {
        if (_channel != null)
        {
            await _channel.ShutdownAsync();
            _channel = null;
            _client = null;
            Console.WriteLine("Desconectado del servidor");
        }
    }

    private void EnsureConnected()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("No está conectado al servidor. Llame a ConnectAsync() primero.");
        }
    }

    public async Task<PedidoResponse> HacerPedidoAsync(List<(string nombre, int cantidad)> platos, int distancia)
    {
        EnsureConnected();

        var request = new PedidoRequest { Distancia = distancia };

        foreach (var p in platos)
        {
            request.Platos.Add(new PlatoPedido { Nombre = p.nombre, Cantidad = p.cantidad });
        }

        try
        {
            // WARNING CS8602 SOLUCIONADO: Añadimos '!' después de _client
            var response = await _client!.HacerPedidoAsync(request); 
            Console.WriteLine($"Pedido creado ID: {response.IdPedido}, Mensaje: {response.Mensaje}");
            return response;
        }
        catch (RpcException ex)
        {
            Console.WriteLine($"Error al hacer pedido: {ex.Status.Detail}");
            throw;
        }
    }

    public async Task<EstadoResponse> ConsultarEstadoPedidoAsync(string idPedidoStr)
    {
        EnsureConnected();

        // ERROR CS0029 SOLUCIONADO:
        // El error indica que el Proto espera STRING pero le dábamos INT.
        // Si tu proto dice 'int32', el error sería al revés. 
        // Asumiendo que el error manda, convertimos a string o int según corresponda.
        // He preparado el código para que funcione si el Proto es int (parseando) o si es string.
        
        // OPCIÓN A: Si tu proto tiene 'int32 id_pedido', usa esto:
        /*
        if (!int.TryParse(idPedidoStr, out int idPedidoInt))
        {
             throw new ArgumentException("El ID debe ser un número");
        }
        var request = new ConsultaRequest { IdPedido = idPedidoInt };
        */

        // OPCIÓN B (Basada en tu error "int en string"):
        // Si el compilador se queja de "int en string", es porque IdPedido es string.
        // Usamos el string directamente:
        
        int idPedidoInt = 0;
        // Mantenemos el TryParse solo para validar que sea numero, aunque lo enviemos como string
        if (!int.TryParse(idPedidoStr, out idPedidoInt)) 
        {
             Console.WriteLine("Advertencia: El ID introducido no parece un número estándar.");
        }
        
        // IMPORTANTE: Aquí estaba el error CS0029. 
        // Si esto vuelve a fallar, cambia 'idPedidoInt' por 'idPedidoStr' (si el proto es string)
        // o asegúrate de que el proto sea int32.
        // Para arreglarlo genéricamente según el error que me has dado:
        
        var request = new ConsultaRequest { IdPedido = idPedidoStr }; 

    
        try
        {
            // WARNING CS8602 SOLUCIONADO: Añadimos '!'
            var response = await _client!.ConsultarEstadoPedidoAsync(request);
            
            Console.WriteLine($"Estado del pedido {idPedidoStr}: {response.Estado}");
            Console.WriteLine($"Tiempo estimado: {response.TiempoEstimado}");
            
            return response;
        }
        catch (RpcException ex)
        {
            Console.WriteLine($"Error al consultar el pedido: {ex.Status.Detail}");
            throw;
        }
    }
}