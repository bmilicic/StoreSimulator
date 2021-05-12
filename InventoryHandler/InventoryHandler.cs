using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Communication;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace InventoryHandler
{
    internal sealed class InventoryHandler : StatefulService, IInventoryHandler
    {
        private readonly int _partitionsCount = 4;
        private readonly int _defaultNumberOfItems = 25;
        private readonly int _defaultItemQuantity = 120;
        private readonly int _defaultQuantityIncrement = 200;
        private readonly int _minimumQuantityThreshold = 100;
        private readonly string _storageName = "storage";

        public InventoryHandler(StatefulServiceContext context)
            : base(context)
        { }

        public async Task<int> FetchItemQuantity(int itemId)
        {
            var inventory = await StateManager.GetOrAddAsync<IReliableDictionary<int, int>>(_storageName);

            using (var tx = this.StateManager.CreateTransaction())
            {
                var itemEntry = inventory.TryGetValueAsync(tx, itemId);

                if (itemEntry.Result.HasValue)
                {
                    return itemEntry.Result.Value;
                }
                else
                {
                    return -1;
                }
            }
        }

        public async Task<string> InitInventory(int partitionId)
        {
            var inventory = await StateManager.GetOrAddAsync<IReliableDictionary<int, int>>(_storageName);
            
            using (var tx = this.StateManager.CreateTransaction())
            {
                for (int itemId = partitionId; itemId < _defaultNumberOfItems; itemId += _partitionsCount)
                {
                    await inventory.AddAsync(tx, itemId, _defaultItemQuantity);
                }

                await tx.CommitAsync();
            }
            return $"Partition No. {this.Context.PartitionId} finished init\n";
        }

        public async Task<int> ProcessOrder(int itemId, int quantity)
        {
            int purchasedQuantity, remainingQuantity;

            var inventory = await StateManager.GetOrAddAsync<IReliableDictionary<int, int>>(_storageName);

            using (var tx = this.StateManager.CreateTransaction())
            {
                var itemEntry = inventory.TryGetValueAsync(tx, itemId);
                
                if (itemEntry.Result.Value < quantity)
                {
                    purchasedQuantity = itemEntry.Result.Value;
                    remainingQuantity = 0;
                }
                else
                {
                    purchasedQuantity = quantity;
                    remainingQuantity = itemEntry.Result.Value - quantity;
                }
                
                await inventory.SetAsync(tx, itemId, remainingQuantity);
                await tx.CommitAsync();
            }
            return purchasedQuantity;
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            var inventory = await StateManager.GetOrAddAsync<IReliableDictionary<int, int>>(_storageName);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await CheckAndUpdateInventory(inventory, cancellationToken);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        private async Task CheckAndUpdateInventory(IReliableDictionary<int, int> inventory, CancellationToken cancellationToken)
        {
            int updatedQuantity;

            using (var tx = this.StateManager.CreateTransaction())
            {
                var list = await inventory.CreateEnumerableAsync(tx);
                var enumerator = list.GetAsyncEnumerator();

                while (await enumerator.MoveNextAsync(cancellationToken))
                {
                    if (enumerator.Current.Value < _minimumQuantityThreshold)
                    {
                        updatedQuantity = enumerator.Current.Value + _defaultQuantityIncrement;
                        await inventory.SetAsync(tx, enumerator.Current.Key, updatedQuantity);

                        string info = $"InventoryHandler: Item No. {enumerator.Current.Key} updated quantity to {updatedQuantity}";
                        ServiceEventSource.Current.ServiceMessage(this.Context, info);
                    }
                }

                await tx.CommitAsync();
            }
        }
    }
}
