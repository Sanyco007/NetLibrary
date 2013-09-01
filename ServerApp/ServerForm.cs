using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using NetLibrary;

namespace ServerApp
{
    public partial class ServerForm : Form
    {
        private Server _server;
        private Socket _socket;

        public ServerForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _server = new Server(11000);
            _server.OnConnect += new ConnectEventHandler(_server_OnConnect);
            _server.Start();
            timer1.Start();
        }

        void _server_OnConnect(Socket socket)
        {
            _socket = socket;
        }

        static private Bitmap GetImage()
        {
            var image = new Bitmap(
                Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            using (var g = Graphics.FromImage(image))
            {
                g.CopyFromScreen(0, 0, 0, 0, image.Size);
            }
            return image;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var image = GetImage();
            if (_socket != null) _server.SendObject(_socket, image);
        }

    }
}
