using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using NetLibrary;

namespace ClientApp
{
    public partial class Form1 : Form
    {
        private Client _cl;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _cl = new Client(IPAddress.Parse("127.0.0.1"), 11000);
            _cl.Receive += cl_Receive;
            _cl.ConnectToServer();
        }

        void cl_Receive(byte[] data)
        {
            lock (pImage)
            {
                var bf = new BinaryFormatter();
                var ms = new MemoryStream(data);
                var image = (Bitmap) bf.Deserialize(ms);
                ms.Dispose();
                if (pImage.Image != null) pImage.Image.Dispose();
                pImage.Image = image;
                pImage.Invalidate();
            }
        }

    }
}
