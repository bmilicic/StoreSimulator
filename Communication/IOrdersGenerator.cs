using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading.Tasks;

namespace Communication
{
    public interface IOrdersGenerator : IService
    {
        Task<string> SetFrequency(int newFreq);

        Task<int> GetCurrentFrequency();
    }
}
