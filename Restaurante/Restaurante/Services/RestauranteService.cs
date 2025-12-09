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
        private readonly ConcurrentQueue<string> _motosQueue;
        private readonly SemaphoreSlim _readyMotosSemaphore;
        private const int maxMotos = 2; 

        public RestauranteService(ILogger<RestauranteService> logger)
        {
            _logger = logger;
            _ordersQueue = new ConcurrentQueue<Pedido>();
            _readyOrdersSemaphore = new SemaphoreSlim(0);
            _takeLock = new object();
            _orderID = 1;
            _motosQueue = new ConcurrentQueue<string>();
            _readyMotosSemaphore = new SemaphoreSlim(maxMotos);
            for (int i = 1; i <= maxMotos; i++)
            {
                _motosQueue.Enqueue($"Moto-{i}");
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

        public override async Task<PedidoResponse> TomarPedidoParaRepartir(Empty request, ServerCallContext context)
        {
            await _readyOrdersSemaphore.WaitAsync();
            //Ahora no sólo necesitas que haya pedidos, sino también motos disponibles
            await _readyMotosSemaphore.WaitAsync();

            Pedido pedidoAsignado = null!;
            string motoAsignada = string.Empty;

            try
            {
                lock (_takeLock)
                {   // Intentar extraer un pedido y una moto de las colas
                    if (_ordersQueue.TryDequeue(out Pedido pedido) && _motosQueue.TryDequeue(out string moto))
                    {
                        pedidoAsignado = pedido;
                        motoAsignada = moto;

                        pedidoAsignado.ActualizarEstado("En reparto");

                        _logger.LogInformation($"Pedido {pedidoAsignado.Id} tomado para reparto con {motoAsignada}.");
                    }
                    else
                    {

                        if (pedidoAsignado == null)
                        {
                            _readyOrdersSemaphore.Release();
                        }
                        // Si la moto no se extrajo, devolver el permiso de moto
                        if (string.IsNullOrEmpty(motoAsignada))
                        {
                            _readyMotosSemaphore.Release();
                        }

                        throw new RpcException(new Status(StatusCode.Unavailable, "Error de sincronización: Recurso no encontrado."));
                    }
                }
                // Simular el proceso de reparto asincrónicamente
                Task.Run(async () =>
                {
                    _logger.LogInformation($"Iniciando reparto de Pedido {pedidoAsignado.Id} con {motoAsignada}. T.E.: {pedidoAsignado.TiempoEstimado}s");
                    await Task.Delay(pedidoAsignado.TiempoEstimado * 1000); // Simular tiempo

                    // Devolver el ciclomotor y liberar el semáforo
                    _motosQueue.Enqueue(motoAsignada);
                    _readyMotosSemaphore.Release();
                    pedidoAsignado.ActualizarEstado("Entregado");
                    _logger.LogInformation($"Ciclomotor {motoAsignada} devuelto. Pedido {pedidoAsignado.Id} entregado.");
                });

                return new PedidoResponse
                {
                    IdPedido = int.Parse(pedidoAsignado.Id),
                    Mensaje = $"Pedido asignado para reparto con {motoAsignada}" // Mensaje mejorado
                };
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en TomarPedidoParaRepartir.");
                throw new RpcException(new Status(StatusCode.Internal, "Error interno del servidor."));
            }
        }

        public void Dispose()
        {
            _readyOrdersSemaphore?.Dispose();
            _readyMotosSemaphore?.Dispose(); 
        }
    
    }
}