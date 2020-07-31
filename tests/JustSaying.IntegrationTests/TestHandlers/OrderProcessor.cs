using System.Threading.Tasks;
using JustSaying.Messaging.MessageHandling;

namespace JustSaying.IntegrationTests.TestHandlers
{
    public class OrderProcessor : IHandlerAsync<OrderPlaced>
    {
        public OrderProcessor(Future<OrderPlaced> future)
        {
            Future = future;
        }

        public async Task<bool> Handle(OrderPlaced message)
        {
            await Future.Complete(message).ConfigureAwait(false);
            return true;
        }

        public Future<OrderPlaced> Future { get; }
    }
}
