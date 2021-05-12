using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Threading.Tasks;

namespace Communication
{
    public interface IInventoryHandler : IService
    {

        Task<int> ProcessOrder(int itemId, int quantity);
        Task<string> InitInventory(int partitionId);

        Task<int> FetchItemQuantity(int itemId);
    }
}
