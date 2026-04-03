using System;
using System.Net;
using System.Net.Sockets;

namespace ClassicUO.Network.Socket
{
    public enum SocketWrapperType
    {
        TcpSocket,
        WebSocket
    }

    internal abstract class SocketWrapper : IDisposable
    {
        public abstract bool IsConnected { get; }

        public abstract EndPoint LocalEndPoint { get; }

        public event EventHandler OnConnected, OnDisconnected;
        public event EventHandler<SocketError> OnError;

        public abstract void Connect(Uri uri);
        public abstract void Send(byte[] buffer, int offset, int count);

        public abstract int Read(byte[] buffer);

        public abstract void Disconnect();

        public abstract void Dispose();

        protected void InvokeOnConnected()
        {
            OnConnected?.Invoke(this, EventArgs.Empty);
        }

        protected void InvokeOnDisconnected()
        {
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        protected void InvokeOnError(SocketError e)
        {
            OnError?.Invoke(this, e);
        }
    }
}
