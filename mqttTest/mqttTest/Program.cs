using System;
using System.IO;
using uPLibrary.Networking.M2Mqtt;

namespace mqttTest
{
    class Program
    {
        static void Main(string[] args)
        {
            String ip = "10.3.208.89";
            String imgLocation = "C:\\test.jpg";

            Console.WriteLine("Sending img...");

            MqttClient client = new MqttClient(ip);
            client.Connect(Guid.NewGuid().ToString());
            client.Publish("face_recognition", ReadImageFile(imgLocation));

            Console.WriteLine("img sent");
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