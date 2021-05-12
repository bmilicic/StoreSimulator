using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Communication;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace OrdersHandler
{
    internal sealed class OrdersHandler : StatelessService, IOrdersHandler
    {
        private readonly int _partitionsCount = 4;
        private int _numOfOrdersPerSec;

        public OrdersHandler(StatelessServiceContext context)
            : base(context)
        {
            _numOfOrdersPerSec = 0;
        }

        public async Task<string> PlaceOrder(int itemId, int quantity)
        {
            int partitionKey = itemId % _partitionsCount;

            var inventoryProxy = ServiceProxy.Create<IInventoryHandler>(
                new Uri("fabric:/StoreSimulator/InventoryHandler"),
                new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(partitionKey));

            var purchasedQuantity = await inventoryProxy.ProcessOrder(itemId, quantity);

            ++_numOfOrdersPerSec;
            return $"Order finished: item No. {itemId}; quantity: {purchasedQuantity}";
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                this.Partition.ReportLoad(new List<LoadMetric> { new LoadMetric("OrdersPerSec", _numOfOrdersPerSec) });

                _numOfOrdersPerSec = 0;

                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            }
        }
    }
}
