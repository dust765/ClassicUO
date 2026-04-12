using System;
using System.Net;
using System.Net.Sockets;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Network.Socket
{
    internal sealed class TcpSocketWrapper : SocketWrapper
    {
        private TcpClient _socket;
        private NetworkStream _cachedStream;

        private const int ConnectTimeoutMs = 10000;

        public override bool IsConnected => _socket?.Client?.Connected ?? false;

        public override EndPoint LocalEndPoint => _socket?.Client?.LocalEndPoint;

        public override void Connect(Uri uri)
        {
            if (IsConnected)
            {
                return;
            }

            _socket = new TcpClient();
            _socket.NoDelay = true;
            _socket.ReceiveBufferSize = 262144;
            _socket.SendBufferSize = 65536;

            try
            {
                IAsyncResult result = _socket.BeginConnect(uri.Host, uri.Port, null, null);
                if (!result.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                {
                    Disconnect();
                    Log.Error($"connection timeout to {uri.Host}:{uri.Port}");
                    InvokeOnError(SocketError.TimedOut);
                    return;
                }

                _socket.EndConnect(result);

                if (!IsConnected)
                {
                    InvokeOnError(SocketError.NotConnected);
                    return;
                }

                _cachedStream = _socket.GetStream();
                InvokeOnConnected();
            }
            catch (SocketException socketEx)
            {
                Log.Error($"error while connecting {socketEx}");
                InvokeOnError(socketEx.SocketErrorCode);
            }
            catch (Exception ex)
            {
                Log.Error($"error while connecting {ex}");
                InvokeOnError(SocketError.SocketError);
            }
        }

        public override void Send(byte[] buffer, int offset, int count)
        {
            NetworkStream stream = _cachedStream;
            if (stream == null)
            {
                return;
            }

            stream.Write(buffer, offset, count);
            stream.Flush();
        }

        public override int Read(byte[] buffer)
        {
            if (!IsConnected)
            {
                return 0;
            }

            int available = _socket.Available;
            if (available == 0)
            {
                return -1;
            }

            available = Math.Min(buffer.Length, available);
            int done = 0;
            NetworkStream stream = _cachedStream;

            while (done < available)
            {
                int toRead = Math.Min(buffer.Length, available - done);
                int read = stream.Read(buffer, done, toRead);

                if (read <= 0)
                {
                    InvokeOnDisconnected();
                    Disconnect();
                    return 0;
                }

                done += read;
            }

            return done;
        }

        public override void Disconnect()
        {
            TcpClient s = _socket;
            _socket = null;
            _cachedStream = null;
            if (s == null)
            {
                return;
            }

            try
            {
                if (s.Client?.Connected == true)
                {
                    s.Close();
                }
            }
            catch { }

            try
            {
                s.Dispose();
            }
            catch { }
        }

        public override void Dispose()
        {
            Disconnect();
        }
    }
}
