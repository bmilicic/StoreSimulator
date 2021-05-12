using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading.Tasks;

namespace Communication
{
    public interface IOrdersHandler : IService
    {
        public Task<string> PlaceOrder(int itemId, int quantity);
    }
}
