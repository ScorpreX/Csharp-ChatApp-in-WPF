using ChatCommon.DTO;
using ChatCommon.Mapper;
using ChatCommon.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

namespace ChatClient.View
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private TcpClient _client;
        IPEndPoint? ipEndPoint;
        private bool _isLogin = false;
        NetworkStream _stream;
        Dispatcher _dispatcher;
        public LoginWindow()
        {
            InitializeComponent();
            _client = new();
            ipEndPoint = new IPEndPoint(IPAddress.Loopback, 5000);
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _client.ConnectAsync(ipEndPoint!);
                _stream = _client.GetStream();
                //CancellationToken ctr = new();
                //while (!ctr.IsCancellationRequested)
                //{
                //    var rawMessage = (await ReceiveMessage()).ToList();
                //    handleFeedback(rawMessage);
                //    _isLogin = false;

                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _client?.Dispose();
                _client = null;
            }
        }

        private async Task SendMessage(List<byte> message)
        {
            try
            {
                await _client.GetStream().WriteAsync(message.ToArray());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task<IEnumerable<byte>> ReceiveMessage()
        {
            List<byte> message = new();
            byte[] buffer = new byte[1024];
            do
            {
                int readBytes = await _stream.ReadAsync(buffer);
                message.AddRange(buffer.Skip(2).Take(readBytes - 2));
            } while (buffer[1] != (byte)FieldType.End);
            message.Insert(0, buffer[0]);

            return message;
        }

        public async void Login(object sender, RoutedEventArgs e)
        {
            btnLogin.IsEnabled = false;
            try
            {
                if (IsFieldsEmpty()) return;

                _isLogin = true;
                LoginMessage loginMessage = new()
                {
                    loginName = textBoxUserName.Text,
                    loginPassword = textBoxPassword.Password
                };

                List<byte> toSend = (List<byte>)await ByteSerializer.SaveDTOAsync(loginMessage);
                GenerateMessage(toSend, MessageType.Login);
                await SendMessage(toSend);

                var rawMessage = (await ReceiveMessage()).ToList();
                handleFeedback(rawMessage);
            }
            catch(Exception ex) 
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                _client?.Dispose();
                _client = null;
                btnLogin.IsEnabled = true;
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        public async void Register(object sender, RoutedEventArgs e)
        {
            _isLogin = false;
            btnRegister.IsEnabled = false;
            try
            {
                if (IsFieldsEmpty()) return;

                RegisterMessage loginMessage = new()
                {
                    registerName = textBoxUserName.Text,
                    registerPassword = textBoxPassword.Password
                };

                List<byte> toSend = (List<byte>)await ByteSerializer.SaveDTOAsync(loginMessage);
                GenerateMessage(toSend, MessageType.Register);
                await SendMessage(toSend);

                var rawMessage = (await ReceiveMessage()).ToList();
                handleFeedback(rawMessage);
            }
            finally
            {
                btnRegister.IsEnabled = true;
            }
        }

        private bool IsFieldsEmpty()
        {
            alertBox.Foreground = Brushes.Red;
            alertBox.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(textBoxUserName.Text))
            {
                alertBox.Text = $"A 'név' mező nem lehet üres!";
                return true;
            }

            if (string.IsNullOrEmpty(textBoxPassword.Password))
            {
                alertBox.Text = $"A 'jelszó' mező nem lehet üres!";
                return true;
            }

            return false;
        }

        private void handleFeedback(List<byte> rawMessage)
        {
            try
            {
                var messageType = (MessageType)rawMessage[0];
                var message = Encoding.UTF8.GetString(rawMessage.Skip(1).ToArray());

                alertBox.Visibility = Visibility.Visible;
                alertBox.Foreground = messageType == MessageType.Accept ? Brushes.Green : Brushes.Red;

                if (messageType == MessageType.Accept && _isLogin)
                {
                    _dispatcher.Invoke(() =>
                    {
                        var chatWindow = new ChatWindow(_client, textBoxUserName.Text, _stream);
                        chatWindow.Show();
                        _isLogin = false;
                        this.Close();
                    });
                }

                alertBox.Text = $"{message}";
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _client?.Dispose();
                _client = null;
            }
        }

        private void GenerateMessage(List<byte> toSend, MessageType messageType)
        {
            toSend.Insert(0, (byte)messageType);
            toSend.Insert(1, (byte)(FieldType.End));
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

        private void btnCloseWindow(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
