using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows.Threading;

namespace NetLibrary
{
    public delegate void ConnectedEventHandler();
    public delegate void ClientReceiveEventHandler(byte[] data);

    public class Client : DispatcherObject
    {
        public event ConnectedEventHandler Connect;
        public event ClientReceiveEventHandler Receive;

        protected virtual void OnReceive(byte[] data)
        {
            ClientReceiveEventHandler handler = Receive;
            if (handler != null) handler(data);
        }

        protected virtual void OnConnect()
        {
            ConnectedEventHandler handler = Connect;
            if (handler != null) handler();
        }

        private readonly Socket _socket;
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly Stack<byte[]> _stack;

        private readonly bool _run;

        public Client(IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _stack = new Stack<byte[]>();
            _run = true;
            _port = port;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                ProtocolType.Tcp);
        }

        public void SendObject(object data)
        {
            var ms = new MemoryStream();
            var bf = new BinaryFormatter();
            bf.Serialize(ms, data);
            var buffer = ms.ToArray();
            ms.Dispose();
            lock (_stack)
            {
                _stack.Push(buffer);
            }
        }

        public void ConnectToServer()
        {
            new Thread(Connection) {IsBackground = true}.Start();
        }

        private void Connection()
        {
            var endPoint = new IPEndPoint(_ipAddress, _port);
            _socket.Connect(endPoint);
            if (_socket.Connected)
            {
                Dispatcher.Invoke((Action)(OnConnect));
                new Thread(Communication) { IsBackground = true }.Start();
            }
        }

        public void Communication()
        {
            while (_run)
            {
                if (!_socket.Connected)
                {
                    Thread.Sleep(100);
                    continue;
                }
                if (_socket.Poll(10, SelectMode.SelectRead))
                {
                    if (_socket.Available == -1)
                    {
                        //server disconnect
                    }
                    if (_socket.Available > 0)
                    {
                        const int size = 512;
                        var buffer = new byte[size];
                        int offset = 0;
                        while (_socket.Available > 0)
                        {
                            offset += _socket.Receive(buffer, offset, size, SocketFlags.None);
                            if (offset == buffer.Length)
                            {
                                Array.Resize(ref buffer, offset + size);
                            }
                        }
                        Array.Resize(ref buffer, offset);
                        try
                        {
                            Dispatcher.Invoke((Action) (() => OnReceive(buffer)));
                        }
                        catch
                        {
                        }

                    }
                }
                lock (_stack)
                {
                    if (_socket.Poll(10, SelectMode.SelectWrite) && _stack.Count > 0)
                    {
                        var data = _stack.Pop();
                        _socket.SendTimeout = data.Length / 10 + 20;
                        _socket.Send(data);
                    }
                }
                if (_socket.Poll(10, SelectMode.SelectError))
                {
                    //server error
                }
                Thread.Sleep(100);
            }
        }
    }
}
