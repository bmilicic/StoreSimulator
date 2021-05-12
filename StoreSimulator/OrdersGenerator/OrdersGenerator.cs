using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Communication;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace OrdersGenerator
{
    internal sealed class OrdersGenerator : StatefulService, IOrdersGenerator
    {
        private readonly string _propertyDictName = "propertyDict";
        private readonly string _frequencyValEntry = "frequency";
        private readonly string _storeInitiated_entry = "storeInit";
        private readonly int _defaultFrequency = 10;
        private readonly int _defaultNumberOfItems = 25;
        private readonly int _maxItemQuantity = 15;
        private readonly Random _random = new Random();
        private int _currentFrequency;

        private bool _storeInitiated = false;
        private bool _frequencyLoaded = false;
        private readonly int _inventoryHandlerParitionsCount = 4;

        public OrdersGenerator(StatefulServiceContext context)
            : base(context)
        { }

        public async Task<int> GetCurrentFrequency()
        {
            return _currentFrequency;
        }

        public async Task<string> SetFrequency(int newFreq)
        {
            _currentFrequency = newFreq;

            var propertyDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, int>>(_propertyDictName);

            using (var tx = this.StateManager.CreateTransaction())
            {
                await propertyDict.AddOrUpdateAsync(tx, _frequencyValEntry, _currentFrequency, (key, value) => _currentFrequency);

                await tx.CommitAsync();
            }

            return $"Frequency set to {_currentFrequency} orders/sec";
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return this.CreateServiceRemotingReplicaListeners();
        }

        private async Task GenerateOrders()
        {
            int itemId, quantity;

            var ordersHandlerProxy = ServiceProxy.Create<IOrdersHandler>(
                new Uri("fabric:/StoreSimulator/OrdersHandler"));

            for (int i = 0; i < _currentFrequency; ++i)
            {

                itemId = _random.Next(_defaultNumberOfItems);
                quantity = _random.Next(1, _maxItemQuantity);

                var placedOrder = await ordersHandlerProxy.PlaceOrder(itemId, quantity);

                ServiceEventSource.Current.ServiceMessage(this.Context, placedOrder);
            }
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_storeInitiated)
                {
                    await InitStore();
                    _storeInitiated = true;
                }

                if (!_frequencyLoaded)
                {
                    await LoadFrequency();
                    _frequencyLoaded = true;
                }

                await GenerateOrders();

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        private async Task LoadFrequency()
        {
            var propertyDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, int>>(_propertyDictName);

            using (var tx = this.StateManager.CreateTransaction())
            {
                var persistedFreqVal = await propertyDict.TryGetValueAsync(tx, _frequencyValEntry);

                if (!persistedFreqVal.HasValue)
                {
                    await propertyDict.SetAsync(tx, _frequencyValEntry, _defaultFrequency);

                    await tx.CommitAsync();

                    _currentFrequency = _defaultFrequency;
                }
                else
                {
                    _currentFrequency = persistedFreqVal.Value;
                }
            }
        }

        private async Task InitStore()
        {
            var propertyDict = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, int>>(_propertyDictName);

            using (var tx = this.StateManager.CreateTransaction())
            {
                var persistedStoreInit = await propertyDict.TryGetValueAsync(tx, _storeInitiated_entry);

                if (!persistedStoreInit.HasValue)
                {
                    //StringBuilder result = new StringBuilder();
                    string result = "Init store: \n";

                    for (int partitionId = 0; partitionId < _inventoryHandlerParitionsCount; ++partitionId)
                    {
                        var inventoryProxy = ServiceProxy.Create<IInventoryHandler>(
                            new Uri("fabric:/StoreSimulator/InventoryHandler"),
                            new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(partitionId));
                        var initResponse = await inventoryProxy.InitInventory(partitionId);

                        //result.Append(initResponse).Append("\n");
                        result.Concat(initResponse).Concat("\n");
                    }

                    await propertyDict.SetAsync(tx, _storeInitiated_entry, 1);

                    //ServiceEventSource.Current.ServiceMessage(this.Context, result.ToString());
                    ServiceEventSource.Current.ServiceMessage(this.Context, result);

                    await tx.CommitAsync();
                }
            }

        }
    }
}
