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
        
        int idPedidoInt = 0;
        if (!int.TryParse(idPedidoStr, out idPedidoInt)) 
        {
             Console.WriteLine("Advertencia: El ID introducido no parece un número estándar.");
        }
        

        
        var request = new ConsultaRequest { IdPedido = idPedidoInt }; 

    
        try
        {
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