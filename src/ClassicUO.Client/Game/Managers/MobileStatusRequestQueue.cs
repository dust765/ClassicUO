using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ClassicUO.Game.Managers
{
    internal class MobileStatusRequestQueue
    {
        private readonly ConcurrentQueue<uint> _requestedSerials = new ConcurrentQueue<uint>();
        private static MobileStatusRequestQueue instance;
        private Task _queueProcessor;

        public static MobileStatusRequestQueue Instance
        {
            get
            {
                instance ??= new MobileStatusRequestQueue();
                return instance;
            }
        }

        private MobileStatusRequestQueue() { }

        public void RequestMobileStatus(uint serial)
        {
            _requestedSerials.Enqueue(serial);

            if (_queueProcessor == null || _queueProcessor.IsCompleted)
            {
                _queueProcessor = Task.Run(async () =>
                {
                    while (_requestedSerials.TryDequeue(out uint s))
                    {
                        // GameActions must run on main thread
                        MainThreadQueue.EnqueueAction(() =>
                        {
                            GameActions.RequestMobileStatus(s);
                        });
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                });
            }
        }
    }
}
