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

        // Modificación ciclomotores
        private readonly ConcurrentQueue<Ciclomotor> _motosQueue;
        private readonly SemaphoreSlim _readyMotosSemaphore;
        private const int maxMotos = 2; 

        public RestauranteService(ILogger<RestauranteService> logger)
        {
            _logger = logger;
            _ordersQueue = new ConcurrentQueue<Pedido>();
            _readyOrdersSemaphore = new SemaphoreSlim(0);
            _takeLock = new object();
            _orderID = 1;
            _motosQueue = new ConcurrentQueue<Ciclomotor>();
            _readyMotosSemaphore = new SemaphoreSlim(maxMotos);
            for (int i = 1; i <= maxMotos; i++)
            {
                _motosQueue.Enqueue(new Ciclomotor(i));
            }
        }

        public override Task<PedidoResponse> HacerPedido(PedidoRequest request, ServerCallContext context)
        {
            lock (_takeLock)
            {
                if (_ordersQueue.Count < MaxQueueCapacity)
                {
                    var pedido = new Pedido(
                        _orderID,
                        request.Platos.ToList(),
                        (int)request.Distancia
                    );

                    _ordersQueue.Enqueue(pedido);
                    _logger.LogInformation($"Nuevo pedido recibido: {pedido.Id}");

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
            var pedido = _ordersQueue.FirstOrDefault(p => p.Id == request.IdPedido.ToString());

            if (pedido != null)
            {
                return Task.FromResult(new EstadoResponse
                {
                    Estado = pedido.Estado,
                    Mensaje = "Pedido encontrado",
                    TiempoEstimado = $"{pedido.TiempoEstimado} min" 
                }); 
            }

            throw new RpcException(new Status(StatusCode.NotFound, $"El pedido {request.IdPedido} no existe.")); 
        }

        public override Task<PedidoResponse> ConsultarPlatosPedido(ConsultaRequest request, ServerCallContext context)
        {
            var pedido = _ordersQueue.FirstOrDefault(p => p.Id == request.IdPedido.ToString());

            if (pedido != null)
            {
                string resumenPlatos = string.Join(", ", pedido.Platos.Select(p => $"{p.Cantidad}x {p.Nombre}"));

                return Task.FromResult(new PedidoResponse
                {
                    IdPedido = request.IdPedido, 
                    Mensaje = $"Platos: {resumenPlatos}"
                });
            }

            throw new RpcException(new Status(StatusCode.NotFound, "Pedido no encontrado"));
        }

        // Archivo: RestauranteService.cs (MÉTODO RPC)

    public override async Task<PedidoResponse> TomarPedidoParaRepartir(Empty request, ServerCallContext context)
    {
        // 1. ESPERAR POR PEDIDO DISPONIBLE
        await _readyOrdersSemaphore.WaitAsync(); 

        Ciclomotor motoAsignada;
        Pedido pedidoAsignado;
    
        try
        {
            motoAsignada = await TomarCiclomotorParaRepartirAsync();
        }
        catch (InvalidOperationException)
        {
            // Si fallamos en tomar la moto, liberamos el permiso de pedido que ya tomamos
            _readyOrdersSemaphore.Release(); 
            throw new RpcException(new Status(StatusCode.Unavailable, "Error al sincronizar con flota de motos."));
        }

        lock (_takeLock) // Usamos el lock para el acceso concurrente a la cola de pedidos
        {
            if (_ordersQueue.TryDequeue(out Pedido pedido)) 
            {
                pedidoAsignado = pedido;
                pedidoAsignado.ActualizarEstado("En reparto");
                _logger.LogInformation($"Pedido {pedidoAsignado.Id} tomado con {motoAsignada.Id}.");
            }
            else
            {
                // Si la cola de pedidos falla: 
                // 1. Devolver la moto que ya aseguramos.
                motoAsignada.Devolver();
                _motosQueue.Enqueue(motoAsignada);
                _readyMotosSemaphore.Release();
            
                // 2. Devolver el permiso de pedido.
                _readyOrdersSemaphore.Release(); 
                throw new RpcException(new Status(StatusCode.Unavailable, "Cola de pedidos vacía inesperadamente.")); 
            }
    }
    
        Task.Run(async () =>
        {
            await Task.Delay(pedidoAsignado.TiempoEstimado * 1000); 

            // Devolver el ciclomotor
            motoAsignada.Devolver();
            _motosQueue.Enqueue(motoAsignada);
            _readyMotosSemaphore.Release(); // Libera el permiso
        
            pedidoAsignado.ActualizarEstado("Entregado");
            _logger.LogInformation($"Ciclomotor {motoAsignada.Id} devuelto. Pedido {pedidoAsignado.Id} entregado.");
        });
    
        return new PedidoResponse
        {
            IdPedido = int.Parse(pedidoAsignado.Id), 
            Mensaje = $"Pedido asignado para reparto con {motoAsignada.Id}" 
        }; 
}

        private async Task<Ciclomotor> TomarCiclomotorParaRepartirAsync()
        {
            // Esperar por un permiso de semáforo
            await _readyMotosSemaphore.WaitAsync();

             // Sección crítica para extraer la moto de la cola
            lock (_takeLock) // Usamos el mismo lock que para los pedidos, aunque lo ideal es uno para motos
            {
            if (_motosQueue.TryDequeue(out Ciclomotor moto))
            {
                moto.Asignar();
                return moto;
            }
        }
    
        // Error de sincronización: devolver el permiso
        _readyMotosSemaphore.Release(); 
        throw new InvalidOperationException("Error de sincronización: Ciclomotor no encontrado en cola.");
        }

        public void Dispose()
        {
            _readyOrdersSemaphore?.Dispose();
            _readyMotosSemaphore?.Dispose(); 
        }
    
    }
}