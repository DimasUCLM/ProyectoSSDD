using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using Restaurante.Protos;
using System.Collections.Concurrent;

namespace Restaurante.Services
{
    public class RestauranteService : Protos.RestauranteService.RestauranteServiceBase, IDisposable
    {
        private readonly ILogger<RestauranteService> _logger;
        private readonly ConcurrentQueue<Pedido> _ordersQueue;
        private const int MaxQueueCapacity = 10;
        private readonly SemaphoreSlim _readyOrdersSemaphore;
        private readonly object _takeLock;
        private int _orderID;

        public RestauranteService(ILogger<RestauranteService> logger)
        {
            _logger = logger;
            _ordersQueue = new ConcurrentQueue<Pedido>();
            _readyOrdersSemaphore = new SemaphoreSlim(0);
            _takeLock = new object();
            _orderID = 1;
        }

        public override Task<PedidoResponse> HacerPedido(PedidoRequest request, ServerCallContext context)
        {
            lock (_takeLock) 
            {
                if (_ordersQueue.Count < MaxQueueCapacity) 
                {
                    // ADAPTACIÓN: Usamos tu constructor en lugar de inicializador de objeto
                    // Nota: Convertimos request.Platos (RepeatedField) a List usando .ToList()
                    // Nota: Convertimos request.Distancia (float) a int
                    var pedido = new Pedido(
                        _orderID, 
                        request.Platos.ToList(), 
                        (int)request.Distancia
                    );

                    _ordersQueue.Enqueue(pedido);
                    _logger.LogInformation($"Nuevo pedido recibido: {pedido.Id}"); 
                    
                    // Guardamos el ID actual (entero) para devolverlo en la respuesta
                    int idRespuesta = _orderID;
                    _orderID++; 
                    _readyOrdersSemaphore.Release(); 

                    return Task.FromResult(new PedidoResponse
                    {
                        IdPedido = idRespuesta,
                        Mensaje = "Pedido recibido y en preparación"
                    }); 
                }
            }
            
            throw new RpcException(new Status(StatusCode.ResourceExhausted, "La cola está llena")); 
        }

        public override Task<EstadoResponse> ConsultarEstadoPedido(ConsultaRequest request, ServerCallContext context)
        {
            // ADAPTACIÓN: Convertimos el ID del request (int) a string para comparar con tu clase
            var pedido = _ordersQueue.FirstOrDefault(p => p.Id == request.IdPedido.ToString());

            if (pedido != null)
            {
                return Task.FromResult(new EstadoResponse
                {
                    Estado = pedido.Estado,
                    Mensaje = "Pedido encontrado",
                    // Usamos tu propiedad TiempoEstimado convertida a string
                    TiempoEstimado = $"{pedido.TiempoEstimado} min" 
                }); 
            }

            throw new RpcException(new Status(StatusCode.NotFound, $"El pedido {request.IdPedido} no existe.")); 
        }

        public override Task<PedidoResponse> ConsultarPlatosPedido(ConsultaRequest request, ServerCallContext context)
        {
            // ADAPTACIÓN: Comparación con ToString()
            var pedido = _ordersQueue.FirstOrDefault(p => p.Id == request.IdPedido.ToString());

            if (pedido != null)
            {
                string resumenPlatos = string.Join(", ", pedido.Platos.Select(p => $"{p.Cantidad}x {p.Nombre}"));

                return Task.FromResult(new PedidoResponse
                {
                    IdPedido = request.IdPedido, // Devolvemos el mismo ID int que nos pidieron
                    Mensaje = $"Platos: {resumenPlatos}"
                });
            }

            throw new RpcException(new Status(StatusCode.NotFound, "Pedido no encontrado"));
        }

        public override async Task<PedidoResponse> TomarPedidoParaRepartir(Empty request, ServerCallContext context)
        {
            await _readyOrdersSemaphore.WaitAsync(); 

            lock (_takeLock) 
            {
                if (_ordersQueue.TryDequeue(out Pedido pedido)) 
                {
                    // ADAPTACIÓN: Usamos tu método público para cambiar el estado (porque el set es private)
                    pedido.ActualizarEstado("En reparto"); 
                    
                    _logger.LogInformation($"Pedido {pedido.Id} tomado para reparto.");
                    
                    return new PedidoResponse
                    {
                        // Convertimos tu ID string de vuelta a int para el Proto
                        IdPedido = int.Parse(pedido.Id), 
                        Mensaje = "Pedido asignado para reparto"
                    }; 
                }
            }

            throw new RpcException(new Status(StatusCode.Unavailable, "No hay pedidos disponibles")); 
        }

        public void Dispose()
        {
            _readyOrdersSemaphore?.Dispose();
        }
    }
}