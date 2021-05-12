using Communication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Threading.Tasks;

namespace SimulatorHandler.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SimulatorHandlerController : ControllerBase
    {
        private readonly int _ordersGeneratorPartitionId = 0;
        private readonly int _partitionsCount = 4;

        [HttpGet]
        [Route("order")]
        public async Task<string> IssueOrder(
            [FromQuery] int id, [FromQuery] int qnty)
        {
            var ordersHandlerProxy = ServiceProxy.Create<IOrdersHandler>(
                new Uri("fabric:/StoreSimulator/OrdersHandler"));

            var placedOrder = await ordersHandlerProxy.PlaceOrder(id, qnty);

            return placedOrder;
        }

        [HttpGet]
        [Route("getFreq")]
        public async Task<string> GetCurrentFrequency()
        {
            var ordersGeneratorProxy = ServiceProxy.Create<IOrdersGenerator>(
                new Uri("fabric:/StoreSimulator/OrdersGenerator"),
                new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(_ordersGeneratorPartitionId));

            var currentFrequency = await ordersGeneratorProxy.GetCurrentFrequency();

            return $"Current orders frequency: {currentFrequency} orders/sec";
        }

        [HttpGet]
        [Route("setfreq")]
        public async Task<string> SetFrequency(
            [FromQuery] int newFreq)
        {
            var ordersGeneratorProxy = ServiceProxy.Create<IOrdersGenerator>(
                new Uri("fabric:/StoreSimulator/OrdersGenerator"),
                new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(_ordersGeneratorPartitionId));

            var setFrequencyResult = await ordersGeneratorProxy.SetFrequency(newFreq);

            return setFrequencyResult;
        }

        [HttpGet]
        [Route("getQnty")]
        public async Task<string> GetItemQuantity(
            [FromQuery] int id)
        {
            int partitionId = id % _partitionsCount;
            var inventoryProxy = ServiceProxy.Create<IInventoryHandler>(
                new Uri("fabric:/StoreSimulator/InventoryHandler"),
                new Microsoft.ServiceFabric.Services.Client.ServicePartitionKey(partitionId));

            var itemQuantity = await inventoryProxy.FetchItemQuantity(id);

            if (itemQuantity != -1)
            {
                return $"Inventory state: Item No. {id}, quantity: {itemQuantity}";
            }
            else
            {
                return $"Inventory state: Item No. {id} not found in inventory!";
            }
        }
    }
}
