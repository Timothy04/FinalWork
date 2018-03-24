using System;
using System.IO;
using uPLibrary.Networking.M2Mqtt;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FacialRecognitionDB
{
    public partial class Form1 : Form
    {
        String ip = "192.168.1.21";
        String imageName = "";

        public Form1()
        {
            InitializeComponent();
        }

        private void btnSelectImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog fileDialog = new OpenFileDialog())
            {
                fileDialog.Title = "Select image";
                fileDialog.Filter = "JPG images (*.jpg)|*.jpg";

                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    imageName = fileDialog.FileName;
                    picImage.Image = Image.FromFile(imageName);
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            bool allFilled = true;

            if (txtFirstName.Text == "")
            {
                allFilled = false;
                txtFirstName.BackColor = Color.FromArgb(255, 255, 0, 0);
            }

            if (txtLastName.Text == "")
            {
                allFilled = false;
                txtLastName.BackColor = Color.FromArgb(255, 255, 0, 0);
            }

            if (picImage.Image == null || picImage.Image.Equals(Image.FromFile("C:\\Users\\timot\\source\\repos\\FacialRecognitionDB\\FacialRecognitionDB\\Resources\\no_img.jpg")))
            {
                allFilled = false;
                btnSelectImage.BackColor = Color.FromArgb(255, 255, 0, 0);
            }

            if (allFilled)
            {
                MqttClient client = new MqttClient(ip);
                client.Connect(Guid.NewGuid().ToString());

                string json = "{";

                json += "\"first_name\": \"";
                json += txtFirstName.Text;
                json += "\",\"last_name\": \"";
                json += txtLastName.Text;
                json += "\",\"school\": \"";
                json += txtSchool.Text;
                json += "\",\"year\": \"";
                json += numYear.Text;
                json += "\",\"course\": \"";
                json += txtCourse.Text;
                json += "\",\"image\": \"";
                json += Convert.ToBase64String(ReadImageFile(imageName));
                json += "\",\"image_name\": \"";
                json += txtFirstName.Text;
                json += "\"}";

                var msg = Encoding.UTF8.GetBytes(json);

                client.Publish("face/add/query", msg);
            }
        }

        public static byte[] ReadImageFile(string imageLocation)
        {
            byte[] imageData = null;
            FileInfo fileInfo = new FileInfo(imageLocation);
            long imageFileLength = fileInfo.Length;
            FileStream fs = new FileStream(imageLocation, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            imageData = br.ReadBytes((int)imageFileLength);
            return imageData;
        }
    }
}