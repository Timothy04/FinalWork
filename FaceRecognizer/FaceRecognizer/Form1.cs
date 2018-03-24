using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Common.Contract;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FaceRecognizer
{
    public partial class Form1 : Form
    {
        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient("d66655560dea41ea923ed8c771acd67a", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");
        private string _imagePath = "C:\\test.jpg";
        string _groupId = "mygroup";

        public Form1()
        {
            InitializeComponent();

            imgBox.Load(_imagePath);
        }

        private async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        {
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    var faces = await faceServiceClient.DetectAsync(imageFileStream,
                      true,
                      true,
                      new FaceAttributeType[] {
                          FaceAttributeType.Gender,
                          FaceAttributeType.Age,
                          FaceAttributeType.Emotion
                      });
                    return faces.ToArray();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return new Face[0];
            }
        }

        private async void btnProcess_Click(object sender, EventArgs e)
        {
            DateTime time = DateTime.Now;

            Face[] faces = await UploadAndDetectFaces(_imagePath);

            if (faces.Length > 0)
            {
                var faceBitmap = new Bitmap(imgBox.Image);

                using (var g = Graphics.FromImage(faceBitmap))
                {
                    // Alpha-black rectangle on entire image
                    g.FillRectangle(new SolidBrush(Color.FromArgb(200, 0, 0, 0)), g.ClipBounds);

                    var br = new SolidBrush(Color.FromArgb(200, Color.LightGreen));

                    // Loop each face recognized
                    foreach (var face in faces)
                    {
                        var fr = face.FaceRectangle;
                        var fa = face.FaceAttributes;

                        // Get original face image (color) to overlap the grayed image
                        var faceRect = new Rectangle(fr.Left, fr.Top, fr.Width, fr.Height);
                        g.DrawImage(imgBox.Image, faceRect, faceRect, GraphicsUnit.Pixel);
                        g.DrawRectangle(Pens.LightGreen, faceRect);

                        // Loop face.FaceLandmarks properties for drawing landmark spots
                        var pts = new List<Point>();
                        Type type = face.FaceLandmarks.GetType();
                        foreach (PropertyInfo property in type.GetProperties())
                        {
                            g.DrawRectangle(Pens.LightGreen, GetRectangle((FeatureCoordinate)property.GetValue(face.FaceLandmarks, null)));
                        }

                        // Calculate where to position the detail rectangle
                        int rectTop = fr.Top + fr.Height + 10;
                        if (rectTop + 45 > faceBitmap.Height) rectTop = fr.Top - 30;

                        // Draw detail rectangle and write face informations                     
                        g.FillRectangle(br, fr.Left - 10, rectTop, fr.Width < 120 ? 120 : fr.Width + 20, 25);
                        g.DrawString(string.Format("{0:0.0} / {1} / {2}", fa.Age, fa.Gender, fa.Emotion.ToRankedList().OrderByDescending(x => x.Value).First().Key),
                              this.Font, Brushes.Black,
                              fr.Left - 8,
                              rectTop + 4);
                    }
                }

                imgBox.Image = faceBitmap;
                System.Diagnostics.Debug.WriteLine("test");
            }

            TimeSpan t = DateTime.Now - time;
            System.Diagnostics.Debug.WriteLine($"Time: {t.TotalMilliseconds} milliseconds.");
        }

        // Return a Rectangle (used for drawing landmarks) 
        private Rectangle GetRectangle(FeatureCoordinate fl)
        {
            return new Rectangle((int)fl.X - 1, (int)fl.Y - 1, 2, 2);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var od = new OpenFileDialog())
            {
                od.Filter = "All files(*.*)|*.*";
                if (od.ShowDialog() == DialogResult.OK)
                {
                    _imagePath = od.FileName;
                    imgBox.Load(_imagePath);
                }
            }
        }

        private void btnPBrowse_Click(object sender, EventArgs e)
        {
            using (var fb = new FolderBrowserDialog())
            {
                if (fb.ShowDialog() == DialogResult.OK)
                    txtImageFolder.Text = fb.SelectedPath;
                else
                    txtImageFolder.Text = "";
            }
        }

        private void btnAddUser_Click(object sender, EventArgs e)
        {
            if (txtNewUser.Text == "") return;
            listUsers.Items.Add(txtNewUser.Text);
        }

        private async void btnCreateGroup_Click(object sender, EventArgs e)
        {
            try
            {
                //_groupId = txtGroupName.Text.ToLower().Replace(" ", "");

                try
                {
                    await faceServiceClient.DeletePersonGroupAsync(_groupId);
                }
                catch { }

                await faceServiceClient.CreatePersonGroupAsync(_groupId, txtGroupName.Text);

                foreach (var u in listUsers.Items)
                {
                    CreatePersonResult person = await faceServiceClient.CreatePersonInPersonGroupAsync(_groupId, u.ToString());
                    foreach (string imagePath in Directory.GetFiles(txtImageFolder.Text + "\\" + u.ToString()))
                    {
                        using (Stream s = File.OpenRead(imagePath))
                        {
                            await faceServiceClient.AddPersonFaceInPersonGroupAsync(_groupId, person.PersonId, s);
                        }
                    }

                    await Task.Delay(1000);
                }

                MessageBox.Show("Group successfully created");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void btnTrain_Click(object sender, EventArgs e)
        {
            try
            {
                await faceServiceClient.TrainPersonGroupAsync(_groupId);

                TrainingStatus trainingStatus = null;
                while (true)
                {
                    trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(_groupId);

                    if (trainingStatus.Status != Status.Running)
                    {
                        break;
                    }

                    await Task.Delay(1000);
                }

                MessageBox.Show("Training successfully completed");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void btnIdentify_Click(object sender, EventArgs e)
        {
            try
            {
                Face[] faces = await UploadAndDetectFaces(_imagePath);
                var faceIds = faces.Select(face => face.FaceId).ToArray();

                var faceBitmap = new Bitmap(imgBox.Image);
                idList.Items.Clear();

                using (var g = Graphics.FromImage(faceBitmap))
                {

                    foreach (var identifyResult in await faceServiceClient.IdentifyAsync(faceIds, _groupId, null, 0.5f, 1))
                    {
                        if (identifyResult.Candidates.Length != 0)
                        {
                            var candidateId = identifyResult.Candidates[0].PersonId;
                            var person = await faceServiceClient.GetPersonInPersonGroupAsync(_groupId, candidateId);

                            // Writes name above face rectangle 
                            var x = faces.FirstOrDefault(y => y.FaceId == identifyResult.FaceId);
                            if (x != null)
                            {
                                g.DrawString(person.Name, this.Font, Brushes.White, x.FaceRectangle.Left, x.FaceRectangle.Top + x.FaceRectangle.Height + 15);
                            }

                            idList.Items.Add(person.Name);
                        }
                        else
                        {
                            idList.Items.Add("< Unknown person >");
                        }

                    }
                }

                imgBox.Image = faceBitmap;
                MessageBox.Show("Identification successfully completed");

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
