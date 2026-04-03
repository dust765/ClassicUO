using System;
using System.Buffers;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;
using TcpSocket = System.Net.Sockets.Socket;

namespace ClassicUO.Network.Socket
{
    internal sealed class WebSocketWrapper : SocketWrapper
    {
        private const int MAX_RECEIVE_BUFFER_SIZE = 1024 * 1024;
        private const int WS_KEEP_ALIVE_INTERVAL = 5;

        private ClientWebSocket _webSocket;
        private TcpSocket _rawSocket;
        private CancellationTokenSource _tokenSource = new();
        private CircularBuffer _receiveStream;

        public override bool IsConnected =>
            _webSocket?.State is WebSocketState.Connecting or WebSocketState.Open;

        public override EndPoint LocalEndPoint => _rawSocket?.LocalEndPoint;

        private bool IsCanceled => _tokenSource.IsCancellationRequested;

        public override void Connect(Uri uri) => ConnectAsync(uri, _tokenSource).GetAwaiter().GetResult();

        public override void Send(byte[] buffer, int offset, int count)
        {
            byte[] copy = ArrayPool<byte>.Shared.Rent(count);
            Buffer.BlockCopy(buffer, offset, copy, 0, count);
            SendCopyAsync(copy, count);
        }

        private async void SendCopyAsync(byte[] copy, int count)
        {
            try
            {
                await _webSocket
                    .SendAsync(copy.AsMemory(0, count), WebSocketMessageType.Binary, true, _tokenSource.Token)
                    .ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copy);
            }
        }

        public override int Read(byte[] buffer)
        {
            if (!IsConnected)
            {
                return 0;
            }

            CircularBuffer rs = _receiveStream;
            if (rs == null)
            {
                return -1;
            }

            lock (rs)
            {
                if (rs.Length == 0)
                {
                    return -1;
                }

                return rs.Dequeue(buffer, 0, buffer.Length);
            }
        }

        private async Task ConnectAsync(Uri uri, CancellationTokenSource tokenSource = null)
        {
            if (IsConnected)
            {
                return;
            }

            _tokenSource = tokenSource ?? new CancellationTokenSource();
            _receiveStream = new CircularBuffer();

            try
            {
                await ConnectWebSocketAsyncCore(uri).ConfigureAwait(false);

                if (IsConnected)
                {
                    InvokeOnConnected();
                }
                else
                {
                    InvokeOnError(SocketError.NotConnected);
                }
            }
            catch (WebSocketException ex)
            {
                SocketError error = ex.InnerException?.InnerException switch
                {
                    SocketException socketException => socketException.SocketErrorCode,
                    _ => SocketError.SocketError
                };

                Log.Error($"Error {ex.GetType().Name} {error} while connecting to {uri} {ex}");
                InvokeOnError(error);
            }
            catch (Exception ex)
            {
                Log.Error($"Unknown Error {ex.GetType().Name} while connecting to {uri} {ex}");
                InvokeOnError(SocketError.SocketError);
            }
        }

        private async Task ConnectWebSocketAsyncCore(Uri uri)
        {
            _rawSocket = new TcpSocket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            _webSocket = new ClientWebSocket();
            _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(WS_KEEP_ALIVE_INTERVAL);

            using var httpClient = new HttpClient(
                new SocketsHttpHandler
                {
                    ConnectCallback = async (context, token) =>
                    {
                        try
                        {
                            await _rawSocket.ConnectAsync(context.DnsEndPoint, token).ConfigureAwait(false);

                            return new NetworkStream(_rawSocket, ownsSocket: true);
                        }
                        catch
                        {
                            _rawSocket?.Dispose();
                            _rawSocket = null;
                            _webSocket?.Dispose();
                            _webSocket = null;

                            throw;
                        }
                    }
                }
            );

            await _webSocket.ConnectAsync(uri, httpClient, _tokenSource.Token).ConfigureAwait(false);

            Log.Trace($"Connected WebSocket: {uri}");

            _ = StartReceiveAsync();
        }

        private async Task StartReceiveAsync()
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
            Memory<byte> memory = buffer.AsMemory();
            int position = 0;

            try
            {
                while (IsConnected)
                {
                    GrowReceiveBufferIfNeeded(ref buffer, ref memory);

                    var receiveResult = await _webSocket
                        .ReceiveAsync(memory.Slice(position), _tokenSource.Token)
                        .ConfigureAwait(false);

                    if (receiveResult.MessageType == WebSocketMessageType.Binary)
                    {
                        position += receiveResult.Count;
                    }

                    if (!receiveResult.EndOfMessage)
                    {
                        continue;
                    }

                    lock (_receiveStream)
                    {
                        _receiveStream.Enqueue(buffer.AsSpan(0, position));
                    }

                    position = 0;
                }
            }
            catch (OperationCanceledException)
            {
                Log.Trace(
                    "WebSocket OperationCanceledException on websocket "
                        + (IsCanceled ? "(was requested)" : "(remote cancelled)")
                );
            }
            catch (Exception e)
            {
                Log.Trace($"WebSocket error in StartReceiveAsync {e}");
                InvokeOnError(SocketError.SocketError);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (!IsCanceled)
            {
                InvokeOnError(SocketError.ConnectionReset);
            }
        }

        private void GrowReceiveBufferIfNeeded(ref byte[] buffer, ref Memory<byte> memory)
        {
            if (_rawSocket == null || _rawSocket.Available <= buffer.Length)
            {
                return;
            }

            if (_rawSocket.Available > MAX_RECEIVE_BUFFER_SIZE)
            {
                throw new SocketException(
                    (int)SocketError.MessageSize,
                    $"WebSocket message frame too large: {_rawSocket.Available} > {MAX_RECEIVE_BUFFER_SIZE}"
                );
            }

            Log.Trace($"WebSocket growing receive buffer {buffer.Length} bytes to {_rawSocket.Available} bytes");

            ArrayPool<byte>.Shared.Return(buffer);
            buffer = ArrayPool<byte>.Shared.Rent(_rawSocket.Available);
            memory = buffer.AsMemory();
        }

        public override void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                _ = _webSocket
                    ?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None)
                    .ContinueWith(_ => _tokenSource?.Cancel(), TaskScheduler.Default);
            }
            catch
            {
                _tokenSource?.Cancel();
            }
        }

        public override void Dispose() { }
    }
}
