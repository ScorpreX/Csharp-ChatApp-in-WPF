using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ChatCommon.Model;
using ChatCommon.DTO;
using ChatCommon.Mapper;


namespace ChatServer
{
    /// <summary>
    /// Interaction logic for ServerWindow.xaml
    /// </summary>
    public partial class ServerWindow : Window
    {
        private readonly TcpListener _server;
        private List<Clientinfo> _onlineUsers = new();
        private List<User> _registeredUsers = new();
        private Dispatcher dispatcher { get; set; }
        public ServerWindow()
        {
            InitializeComponent();
            _server = new TcpListener(IPAddress.Any, 5000);
            dispatcher = Dispatcher.CurrentDispatcher;
        }

        public async void Window_loaded(object sender, RoutedEventArgs e)
        {
            _server.Start();
            try
            {
                CancellationTokenSource cts = new();

                while (!cts.IsCancellationRequested)
                {
                    var client = await _server.AcceptTcpClientAsync();
                    _ = Task.Run(async () => await HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private async Task HandleClientAsync(TcpClient client)
        {
            using NetworkStream? stream = client.GetStream();
            while (client.Connected)
            {
                List<byte> rawMessage = (await ReceiveMessage(stream)).ToList();
                var messageType = (MessageType)rawMessage[0]; // üzenet típusa
                var message = rawMessage.Skip(1); // az üzenet típusát kivesszük, és csak az üzenet marad
                string feedbackMessage = string.Empty;
                switch (messageType)
                {
                    case MessageType.Register:
                        {
                            RegisterMessage registerMessage =
                                await ByteSerializer.GetDTOAssync<RegisterMessage>(message);

                            if (string.IsNullOrWhiteSpace(registerMessage.registerName) && string.IsNullOrWhiteSpace(registerMessage.registerPassword)) break;
                            User user = new User(registerMessage.registerName, registerMessage.registerPassword);
                            bool isRegistered = await IsRegistered(user);
                            if (!isRegistered)
                            {
                                _registeredUsers.Add(user);
                                await MyJsonSerializer<User>.StoreUsersAsync(_registeredUsers);

                                feedbackMessage = $"Sikeres regisztráció";
                                await SendFeedBack(MessageType.Accept, feedbackMessage, client);
                            }
                            else
                            {
                                feedbackMessage = $"Sikertelen regisztráció";
                                await SendFeedBack(MessageType.Decline, feedbackMessage, client);
                            }
                            break;
                        }

                    case MessageType.Login:
                        {
                            LoginMessage loginMessage =
                                await ByteSerializer.GetDTOAssync<LoginMessage>(message);

                            if (string.IsNullOrEmpty(loginMessage.loginName) || string.IsNullOrEmpty(loginMessage.loginPassword)) break;

                            User user = new(loginMessage.loginName, loginMessage.loginPassword);
                            Clientinfo clientinfo = new(client, loginMessage.loginName);

                            bool isRegistered = await IsRegistered(user);
                            bool isLogedIn = await IsLogedIn(clientinfo);
                            bool isAuthenticated = await IsAuthenticated(user);

                            if (isRegistered && !isLogedIn && isAuthenticated)
                            {
                                feedbackMessage = $"Sikeres bejelentkezés";
                                await SendFeedBack(MessageType.Accept, feedbackMessage, client);
                                _onlineUsers.Add(clientinfo);
                                await updateUserlist();

                                LogMessage($"{loginMessage.loginName} bejelentkezett");
                            }
                            else
                            {
                                feedbackMessage = "Sikertelen bejelentkezés";
                                await SendFeedBack(MessageType.Decline, feedbackMessage, client);
                            }

                            break;
                        }

                    case MessageType.Disconnected:
                        {
                            DisconnectMessage disconnectMessage =
                                await ByteSerializer.GetDTOAssync<DisconnectMessage>(message);

                            if (string.IsNullOrEmpty(disconnectMessage.loginName)) break;

                            Clientinfo clientinfo = new(client, disconnectMessage.loginName);
                            bool isLogedIn = await IsLogedIn(clientinfo);

                            if (!isLogedIn) break;
                            var clientToRemove = _onlineUsers.FirstOrDefault(liu => liu.TcpClient == client);
                            if (clientToRemove is not null) _onlineUsers.Remove(clientinfo);

                            string messageToSend = $"{disconnectMessage.loginName}";
                            //await SendBroadcastMessage(MessageType.Disconnected, messageToSend);

                            foreach (var user in _onlineUsers)
                            {
                                await SendMessage(MessageType.Disconnected, messageToSend, user.TcpClient);
                            }

                            // await updateUserlist();

                            LogMessage($"{disconnectMessage.loginName} kijelentkezett");
                            break;
                        }

                    case MessageType.UnicastMessage:
                        {
                            var unicastMessage = await ByteSerializer.GetDTOAssync<UnicastMessage>(message);
                            if (string.IsNullOrEmpty(unicastMessage.SenderName)) break;
                            if (string.IsNullOrEmpty(unicastMessage.ReceiverName)) break;
                            if (string.IsNullOrEmpty(unicastMessage.UserMessage)) break;


                            var clientInfo = new Clientinfo(client, unicastMessage.SenderName);
                            bool isLogedIn = await IsLogedIn(clientInfo);

                            var receiverClientInfo = _onlineUsers.FirstOrDefault(liu => liu.UserName == unicastMessage.ReceiverName);
                            bool isReceiverLogedIn = await IsLogedIn(receiverClientInfo);

                            if (!isLogedIn || !isReceiverLogedIn || receiverClientInfo is null) break;

                            string messageToSend = $"{unicastMessage.SenderName}: {unicastMessage.UserMessage}";
                            await SendMessage(MessageType.UnicastMessage, messageToSend, receiverClientInfo.TcpClient);
                            LogMessage($"{unicastMessage.SenderName} privát üzenetet küldött {unicastMessage.ReceiverName}-nek");
                            break;
                        }

                    case MessageType.BroadcastMessage:
                        {
                            var broadcastMessage = await ByteSerializer.GetDTOAssync<BroadcastMessage>(message);
                            if (string.IsNullOrEmpty(broadcastMessage.SenderName)) break;

                            var clientInfo = new Clientinfo(client, broadcastMessage.SenderName!);
                            bool isLogedIn = await IsLogedIn(clientInfo);
                            if (!isLogedIn) break;
                            if (string.IsNullOrEmpty(broadcastMessage.UserMessage)) break;

                            string messageToSend = $"{broadcastMessage.SenderName}: {broadcastMessage.UserMessage}";
                            await SendBroadcastMessage(messageToSend);
                            LogMessage($"{broadcastMessage.SenderName} broadcast üzenetet küldött");
                            break;
                        }
                }
            }
            client.Dispose();
        }

        private async Task SendMessage(MessageType messageType, string message, TcpClient client)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            var networkStream = client.GetStream();
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            int totalByteCount = Encoding.UTF8.GetByteCount(message);
            int chunkSize = 1022;

            for (int offset = 0; offset < totalByteCount; offset += chunkSize)
            {
                int remainingBytes = totalByteCount - offset;
                int realChunkSize = remainingBytes < chunkSize ? remainingBytes : chunkSize;
                byte[] buffer = new byte[realChunkSize + 2];
                buffer[0] = (byte)messageType;
                buffer[1] = (byte)(offset + 1022 > messageBytes.Length - 1 ? FieldType.End : FieldType.Data);

                Array.Copy(messageBytes, offset, buffer, 2, realChunkSize);

                await networkStream.WriteAsync(buffer);
            }

        }

        private async Task updateUserlist()
        {
            await Parallel.ForEachAsync(_onlineUsers, async (client, _) =>
            {
                var filteredNames = _onlineUsers.Where(x => x.UserName != client.UserName);

                foreach (var clientInfo in filteredNames)
                {
                    await SendMessage(MessageType.Connected, $"{clientInfo.UserName}", client.TcpClient);
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            });
        }

        private async Task SendBroadcastMessage(string message)
        {
            await Parallel.ForEachAsync(_onlineUsers, async (client, _) =>
            {
                await SendMessage(MessageType.BroadcastMessage, message, client.TcpClient);
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            });
        }

        private static async Task<IEnumerable<byte>> ReceiveMessage(NetworkStream stream)
        {
            List<byte> message = new();
            byte[] buffer = new byte[1024];

            do
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // Client disconnected
                }
                message.AddRange(buffer.Skip(2).Take(bytesRead - 2));
            } while (buffer[1] != (byte)FieldType.End);

            message.Insert(0, buffer[0]); // üzenet típúsa
            return message; // az üzenet típusa és az üzenet, lényegében kivettük a mező típusát
        }


        private async Task SendFeedBack(MessageType messageType, string message, TcpClient client)
        {
            await SendMessage(messageType, message, client);
        }

        private async Task<bool> IsRegistered(User user)
        {
            _registeredUsers = (List<User>)await MyJsonSerializer<User>.LoadUsersAsync();

            return _registeredUsers.Count(ru => ru.UserName == user.UserName) == 1;
        }

        private async Task<bool> IsLogedIn(Clientinfo clientinfo)
        {
            return await Task.Run(() => _onlineUsers.Count(lu => lu.UserName == clientinfo.UserName) == 1);
        }

        private async Task<bool> IsAuthenticated(User user)
        {
            _registeredUsers = (List<User>)await MyJsonSerializer<User>.LoadUsersAsync();

            bool isUsernameMatched = _registeredUsers.Any(au => au.UserName == user.UserName);
            bool isPasswordMatched = _registeredUsers.Any(au => au.UserPassword == user.UserPassword);
            return isUsernameMatched && isPasswordMatched;
        }

        private async void OnClosing(object? sender, CancelEventArgs e)
        {
            await Parallel.ForEachAsync(_onlineUsers, async (client, _) =>
            {
                await SendMessage(MessageType.ServerStopped, "A szerver leállt.", client.TcpClient);
                client.TcpClient.Dispose();
            });
        }

        private void LogMessage(string message)
        {
            string separator = "───────────────────────────────────────────────────────────────────";
            dispatcher.Invoke(() =>
            {
                logBox.Items.Add($"{message}{Environment.NewLine}{separator}");
            });
        }
    }
}
