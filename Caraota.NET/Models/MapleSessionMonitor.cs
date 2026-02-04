using Caraota.NET.Events;

namespace Caraota.NET.Models
{
    public class MapleSessionMonitor
    {
        public event DisconnectedEventDelegate? OnDisconnected;

        public delegate Task DisconnectedEventDelegate();

        public long LastPacketInterceptedTime;

        private MapleSession? _session;

        public void Start(MapleSession session)
        {
            _session = session;

            var checkAliveThread = new Thread(CheckAlive)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            checkAliveThread.Start();
        }

        private void CheckAlive()
        {
            while (true)
            {
                Thread.Sleep(500);

                if (_session == null || !_session.SessionSuccess)
                    continue;

                if ((Environment.TickCount64 - LastPacketInterceptedTime) >= 8000)
                {
                    OnDisconnected?.Invoke();

                    _session.SessionSuccess = false;

                    break;
                }
            }
        }
    }
}
