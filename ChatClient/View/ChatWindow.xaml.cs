using ChatCommon.DTO;
using ChatCommon.Mapper;
using ChatCommon.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
using System.Xml.Linq;

namespace ChatClient.View
{
    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        private TcpClient _client { get; init; }
        private string _userName { get; init; }

        private readonly NetworkStream _stream;
        private Dispatcher _dispatcher { get; set; }
        private string messagePlaceholder { get; init; } = "Enter your message...";
        public ChatWindow(TcpClient client, string userName, NetworkStream stream)
        {
            InitializeComponent();
            _client = client;
            _userName = userName;
            _dispatcher = Dispatcher.CurrentDispatcher;
            tbName.Text = userName;
            _stream = stream;
            Task.Run(async () => await HandleMessage(_client));
        }


        private async Task HandleMessage(TcpClient client)
        {
            while (client.Connected)
            {
                var rawMessage = (await ReceiveMessage()).ToList();

                var messageType = (MessageType)rawMessage[0]; // üzenet típusa
                var message = Encoding.UTF8.GetString(rawMessage.Skip(1).ToArray());

                switch (messageType)
                {
                    case MessageType.BroadcastMessage:
                        {
                            _dispatcher.Invoke(() => broadcastMessageList.Items.Add(message));
                            break;
                        }
                    case MessageType.UnicastMessage:
                        {
                            _dispatcher.Invoke(() => unicastMessageList.Items.Add(message));
                            break;
                        }
                    case MessageType.Connected:
                        {
                            _dispatcher.Invoke(() => 
                            { 
                                var username = message.Trim('\"');
                                if (!onlineUserList.Items.Contains(username))
                                    onlineUserList.Items?.Add(message.Trim('\"'));
                            });
                            break;
                        }
                    case MessageType.Disconnected:
                        {
                            _dispatcher.Invoke(() => onlineUserList.Items?.Remove(message.Trim('\"')));
                            break;
                        }
                    case MessageType.ServerStopped:
                        {
                            Environment.Exit(1);
                            break;
                        }
                }
            }
        }

        private async void SendMessage(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(inputMessage.Text)) return;

            List<byte> toSend = new();

            if (onlineUserList.SelectedIndex <= 0 || onlineUserList.SelectedItems is null)
            {
                BroadcastMessage message = new();
                message.SenderName = _userName;
                message.UserMessage = inputMessage.Text;

                toSend = (List<byte>)await ByteSerializer.SaveDTOAsync(message);
                toSend.Insert(0, (byte)MessageType.BroadcastMessage);
                toSend.Insert(1, (byte)FieldType.End);

                await _stream.WriteAsync(toSend.ToArray(), 0, toSend.Count);
            }

            else
            {
                UnicastMessage message = new();
                message.SenderName = _userName;
                message.UserMessage = inputMessage.Text;
                message.ReceiverName = onlineUserList.SelectedItem.ToString();

                toSend = (List<byte>)await ByteSerializer.SaveDTOAsync(message);
                toSend.Insert(0, (byte)FieldType.End);
                toSend.Insert(0, (byte)MessageType.UnicastMessage);

                string messageToAdd = $"{message.SenderName}: {message.UserMessage}";
                _dispatcher.Invoke(() => unicastMessageList.Items.Add(messageToAdd));
                await _stream.WriteAsync(toSend.ToArray());
            }

            inputMessage.Text = messagePlaceholder;

        }

        private async Task<IEnumerable<byte>> ReceiveMessage()
        {
            List<byte> message = new();
            byte[] buffer = new byte[1024];

            do
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                }
                message.AddRange(buffer.Skip(2).Take(bytesRead - 2));

            } while (buffer[1] != (byte)FieldType.End);

            message.Insert(0, buffer[0]); // üzenet típúsa

            return message;
        }


        private void MessageBoxOnFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (textBox.Text == messagePlaceholder)
            {
                textBox.Text = string.Empty;
            }
        }

        private void MessageBoxLostFocus(object sender, RoutedEventArgs e)
        {
            TextBox messageBox = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(messageBox.Text))
            {
                messageBox.Text = messagePlaceholder;
            }
        }


        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void btnMinimizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnMaximizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private async void btnCloseWindow(object sender, RoutedEventArgs e)
        {
            DisconnectMessage message = new();
            message.loginName = _userName;
            List<byte> toSend = (List<byte>)await ByteSerializer.SaveDTOAsync(message);
            toSend.Insert(0, (byte)MessageType.Disconnected);
            toSend.Insert(1, (byte)FieldType.End);

            NetworkStream stream = _client.GetStream();
            await stream.WriteAsync(toSend.ToArray());

            _client.Close();
            Environment.Exit(0);
        }

        private async void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            DisconnectMessage message = new();
            message.loginName = _userName;
            List<byte> toSend = (List<byte>)await ByteSerializer.SaveDTOAsync(message);
            toSend.Insert(0, (byte)MessageType.Disconnected);
            toSend.Insert(1, (byte)FieldType.End);

            NetworkStream stream = _client.GetStream();
            await stream.WriteAsync(toSend.ToArray());
            await stream.FlushAsync();

            _client.Close();
            Environment.Exit(0);
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _client.Dispose();
        }
    }
}
