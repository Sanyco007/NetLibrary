using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows.Threading;

namespace NetLibrary
{
    public delegate void ReceiveEventHandler(Socket socket, byte[] data);
    public delegate void ConnectEventHandler(Socket socket);
    public delegate void DisconnectEventHandler(Socket socket);

    public class Server : DispatcherObject
    {
        /// <summary>
        /// Событие получения данных
        /// </summary>
        public event ReceiveEventHandler OnReceive;

        /// <summary>
        /// Событие подключения клиента
        /// </summary>
        public event ConnectEventHandler OnConnect;

        /// <summary>
        /// Событие отключения клиента
        /// </summary>
        public event DisconnectEventHandler OnDisconnect;

        //Метод для вызова события получения данных
        protected virtual void CallOnReceive(Socket socket, byte[] data)
        {
            var handler = OnReceive;
            if (handler != null) handler(socket, data);
        }

        //Метод для вызова события подключения клиента
        protected virtual void CallOnConnect(Socket socket)
        {
            var handler = OnConnect;
            if (handler != null) handler(socket);
        }

        //Метод для вызова события отключения клиента
        protected virtual void CallOnDisconnect(Socket socket)
        {
            var handler = OnDisconnect;
            _hash.Remove(socket);
            if (handler != null) handler(socket);
        }

        //Сокет для подтверждения подключений
        private readonly Socket _server;

        //Спиок подключенных клиентов
        private List<Socket> _clients;

        //Флаг активности потоков
        private bool _run;

        //Данные для отправки клиентам
        private Hashtable _hash;

        /// <summary>
        /// Создание обьекта сервера
        /// </summary>
        /// <param name="port">Номер порта, связанный с прложением, 
        /// или 0 для указания любого доступного порта.</param>
        public Server(int port)
        {
            _run = false;
            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, 
                ProtocolType.Tcp);
            _server.Bind(new IPEndPoint(IPAddress.Any, port));
            _server.Listen(10);
        }

        /// <summary>
        /// Запуск сервера на прослушивание и чтение входящих данных
        /// </summary>
        public void Start()
        {
            if (_run) return;
            _run = true;
            _hash = new Hashtable();
            _clients = new List<Socket>();
            new Thread(Listen) {IsBackground = true} .Start();
            new Thread(Communication) {IsBackground = true} .Start();
        }

        /// <summary>
        /// Метод для подтверждения входящих подключений
        /// </summary>
        private void Listen()
        {
            while (_run)
            {
                var client = _server.Accept();
                lock (_clients)
                {
                    _clients.Add(client);
                    _hash.Add(client, new Stack<byte[]>());
                }
                Dispatcher.Invoke((Action)(() => CallOnConnect(client)));
            }
        }

        /// <summary>
        /// Отправка клиенту socket обьекта
        /// </summary>
        /// <param name="socket">Сокет клиента для приема объекта</param>
        /// <param name="data">Ссылка на сериализуемый объект</param>
        public void SendObject(Socket socket, object data)
        {
            var ms = new MemoryStream();
            var bf = new BinaryFormatter();
            bf.Serialize(ms, data);
            byte[] buffer = ms.ToArray();
            ms.Dispose();
            lock (_hash)
            {
                var stack = (Stack<byte[]>)_hash[socket];
                if (stack != null) stack.Push(buffer);
            }
        }

        /// <summary>
        /// Отправка всем клиентам объект
        /// </summary>
        /// <param name="data">Объект для отправки</param>
        public void SendObjectToAll(object data)
        {
            var ms = new MemoryStream();
            var bf = new BinaryFormatter();
            bf.Serialize(ms, data);
            byte[] buffer = ms.ToArray();
            ms.Dispose();
            lock (_hash)
            {
                lock (_clients)
                {
                    foreach (var item in _clients)
                    {
                        var stack = (Stack<byte[]>)_hash[item];
                        if (stack != null) stack.Push(buffer);
                    }
                }
            }
        }

        /// <summary>
        /// Отправка клиентам. кроме noSendSocket, обьекта
        /// </summary>
        /// <param name="noSendSocket">Клиент, которому не отправляется сообщение</param>
        /// <param name="data">Объект для отправки</param>
        public void SendObjectToAllWithoutOne(Socket noSendSocket, object data)
        {
            var ms = new MemoryStream();
            var bf = new BinaryFormatter();
            bf.Serialize(ms, data);
            byte[] buffer = ms.ToArray();
            ms.Dispose();
            lock (_hash)
            {
                lock (_clients)
                {
                    foreach (var item in _clients)
                    {
                        if (item != noSendSocket)
                        {
                            var stack = (Stack<byte[]>)_hash[item];
                            if (stack != null) stack.Push(buffer);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Отправка клиенту socket массива байт
        /// </summary>
        /// <param name="socket">Сокет клиента для приема данных</param>
        /// <param name="array">Массив байт для отправки</param>
        public void SendBytes(Socket socket, byte[] array)
        {
            lock (_hash)
            {
                var stack = (Stack<byte[]>)_hash[socket];
                if (stack != null) stack.Push(array);
            }
        }

        /// <summary>
        /// Отправка всем клиентам массива байт
        /// </summary>
        /// <param name="array">Массив байт для отправки</param>
        public void SendBytesToAll(byte[] array)
        {
            lock (_hash)
            {
                lock (_clients)
                {
                    foreach (var item in _clients)
                    {
                        var stack = (Stack<byte[]>)_hash[item];
                        if (stack != null) stack.Push(array);
                    }
                }
            }
        }

        /// <summary>
        /// Отправка клиентам, кроме noSendSocket, массива байт
        /// </summary>
        /// <param name="noSendSocket">Сокет, которому данные не передаются</param>
        /// <param name="array">Массив байт для отправки</param>
        public void SendBytesToAllWithoutOne(Socket noSendSocket, byte[] array)
        {
            lock (_hash)
            {
                lock (_clients)
                {
                    foreach (var item in _clients)
                    {
                        if (item != noSendSocket)
                        {
                            var stack = (Stack<byte[]>) _hash[item];
                            if (stack != null) stack.Push(array);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Остановка сервера
        /// </summary>
        public void Stop()
        {
            _run = false;
        }

        /// <summary>
        /// Поток для отправки и получения данных
        /// </summary>
        private void Communication()
        {
            while (_run)
            {
                List<Socket> read, write, error;
                lock (_clients)
                {
                    if (_clients.Count == 0)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    read = new List<Socket>(_clients);
                    write = new List<Socket>(_clients);
                    error = new List<Socket>(_clients);
                    Socket.Select(read, write, error, 50);
                }
                foreach (var item in read)
                {
                    if (item.Available <= 0)
                    {
                        _clients.Remove(item);
                        Socket socket = item;
                        Dispatcher.Invoke((Action)(() => CallOnDisconnect(socket)));
                    }
                    if (item.Available > 0)
                    {
                        const int size = 512;
                        var buffer = new byte[size];
                        int offset = 0;
                        while (item.Available > 0)
                        {
                            offset += item.Receive(buffer, offset, size, SocketFlags.None);
                            if (offset == buffer.Length)
                            {
                                Array.Resize(ref buffer, offset + size);
                            }
                        }
                        Array.Resize(ref buffer, offset);
                        Socket socket = item;
                        Dispatcher.Invoke((Action)(() => CallOnReceive(socket, buffer)));
                    }
                }
                lock (_hash)
                {
                    foreach (var item in write)
                    {
                        if (!_hash.ContainsKey(item)) continue;
                        var stack = (Stack<byte[]>) _hash[item];
                        if (stack.Count > 0)
                        {
                            var data = stack.Pop();
                            item.SendTimeout = data.Length / 10 + 20;
                            item.Send(data);
                        }
                    }
                }
                foreach (var item in error)
                {
                    _clients.Remove(item);
                    Socket socket = item;
                    Dispatcher.Invoke((Action)(() => CallOnDisconnect(socket)));
                }
                Thread.Sleep(100);
            }
        }
    }
}
