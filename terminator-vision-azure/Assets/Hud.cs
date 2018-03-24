using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.WSA.WebCam;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Common;
using System.Threading.Tasks;

public class Hud : MonoBehaviour {
    public Text InfoPanel;
    public Text AnalysisPanel;
    public Text ThreatAssessmentPanel;
    public Text DiagnosticPanel;

    bool textChanged = false;
    string text = "";
    string filename = string.Format(@"terminator_analysis.jpg");
    float timeLeft = 0.0f;
    float timeInterval = 30.0f;

    MqttClient _client;
    PhotoCapture _photoCaptureObject = null;
    string _subscriptionKey = "3326bad869ce401994724aaf67b854ea";
    string _ocrEndpoint = "https://westcentralus.api.cognitive.microsoft.com/vision/v1.0/ocr";
    string _computerVisionEndpoint = "https://westcentralus.api.cognitive.microsoft.com/vision/v1.0/analyze?visualFeatures=Tags,Faces";
    string _ip = "192.168.1.54";

    private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient("d66655560dea41ea923ed8c771acd67a", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");
    private string _imagePath = "C:\\test.jpg";
    string _groupId = "mygroup";

    void SendImage(byte[] image)
    {
        // IdentifyPython(image);
        IdentifyAzure(image);
    }

    void IdentifyPython(byte[] image)
    {
        _client.Publish("face_recognition/data", image);
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
            //MessageBox.Show(ex.Message);
            return new Face[0];
        }
    }

    async void IdentifyAzure(byte[] image)
    {
        try
        {
            Face[] faces = UploadAndDetectFaces(_imagePath).Result;
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

            // imgBox.Image = faceBitmap;
            // MessageBox.Show("Identification successfully completed");

        }
        catch (Exception ex)
        {
            // MessageBox.Show(ex.Message);
        }
    }

    // Use this for initialization
    void Start () {
        /*
        _client = new MqttClient(_ip);
        _client.MqttMsgPublishReceived += MqttMsgReceived;
        _client.Connect(Guid.NewGuid().ToString());

        _client.Subscribe(new string[] { "face_recognition/response" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
    */
    }

    void MqttMsgReceived(object sender, MqttMsgPublishEventArgs e)
    {
        text = System.Text.Encoding.Default.GetString(e.Message);
        textChanged = true;
    }

    void AnalyzeScene()
    {
        InfoPanel.text = "CALCULATION PENDING";
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        _photoCaptureObject = captureObject;

        Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();

        CameraParameters c = new CameraParameters
        {
            hologramOpacity = 0.0f,
            cameraResolutionWidth = cameraResolution.width,
            cameraResolutionHeight = cameraResolution.height,
            pixelFormat = CapturePixelFormat.BGRA32
        };

        captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
    } 

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if(result.success)
        {
            string filePath = Path.Combine(Application.persistentDataPath, filename);
            _photoCaptureObject.TakePhotoAsync(filePath, PhotoCaptureFileOutputFormat.JPG, OnCapturedPhotoToDisk);
        }
        else
        {
            DiagnosticPanel.text = "DIAGNOSTIC\n**************\n\nUnable to start photo mode.";
            InfoPanel.text = "ABORT";
        }
    }

    void OnCapturedPhotoToDisk(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            string filePath = Path.Combine(Application.persistentDataPath, filename);

            byte[] image = File.ReadAllBytes(filePath);
            GetTagsAndFaces(image);
            //SendImage(image);
            ReadWords(image);
        }
        else
        {
            DiagnosticPanel.text = "DIAGNOSTIC\n**************\n\nFailed to save photo to disk.";
            InfoPanel.text = "ABORT";
        }
        _photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        _photoCaptureObject.Dispose();
        _photoCaptureObject = null;
    }

    public void GetTagsAndFaces(byte[] image)
    {
        StartCoroutine(RunComputerVision(image));
    }

    IEnumerator RunComputerVision(byte[] image)
    {
        var headers = new Dictionary<string, string>()
        {
            { "ocp-apim-subscription-key", _subscriptionKey},
            { "Content-Type", "application/octet-stream"}
        };

        WWW www = new WWW(_computerVisionEndpoint, image, headers);
        yield return www;

        List<string> tags = new List<string>();
        var jsonResults = www.text;
        var myObject = JsonUtility.FromJson<AnalysisResult>(jsonResults);
        foreach (var tag in myObject.tags)
        {
            tags.Add(tag.name);
        }
        AnalysisPanel.text = "ANALYSIS:\n***************\n\n" + string.Join("\n", tags.ToArray());

        List<string> faces = new List<string>();
        foreach (var face in myObject.faces)
        {
            faces.Add(string.Format("{0} scanned: age {1}.", face.gender, face.age));
        }
        if (faces.Count > 0)
        {
            InfoPanel.text = "MATCH";
        }
        else
        {
            InfoPanel.text = "ACTIVE SPATIAL MAPPING";
        }
        ThreatAssessmentPanel.text = "SCAN MODE 43984\nTHREAT ASSESSMENT\n\n" + string.Join("\n", faces.ToArray());
    }

    public void ReadWords(byte[] image)
    {
        StartCoroutine(Read(image));
    }

    IEnumerator Read(byte[] image)
    {
        var headers = new Dictionary<string, string>() {
            { "ocp-apim-subscription-key", _subscriptionKey },
            { "Content-Type", "application/octet-stream" }
        };

        WWW www = new WWW(_ocrEndpoint, image, headers);
        yield return www;

        List<string> words = new List<string>();
        var jsonResults = www.text;
        var myObject = JsonUtility.FromJson<OcrResult>(jsonResults);
        foreach (var region in myObject.regions)
            foreach (var line in region.lines)
                foreach (var word in line.words)
                {
                    words.Add(word.text);
                }

        string textToRead = string.Join(" ", words.ToArray());
        if (myObject.language != "unk")
        {
            DiagnosticPanel.text = "(language=" + myObject.language + ")\n" + textToRead;
        }
        else
        {
            DiagnosticPanel.text = "language unknown.\n" + textToRead;
        }
    }

    // Update is called once per frame
    void Update () {
        timeLeft -= Time.deltaTime;

		if (timeLeft <= 0)
        {
            timeLeft = timeInterval;
            AnalyzeScene();
        }

        if (textChanged)
        {
            //DiagnosticPanel.text = text;
            textChanged = false;
        }
	}
}

[Serializable]
public class AnalysisResult
{
    public Tag[] tags;
    public Face[] faces;
}

[Serializable]
public class Tag
{
    public double confidence;
    public string hint;
    public string name;
}

[Serializable]
public class Face
{
    public int age;
    public FaceRectangle facerectangle;
    public string gender;
}

[Serializable]
public class FaceRectangle
{
    public int height;
    public int left;
    public int top;
    public int width;
}

[Serializable]
public class OcrResult
{
    public string language;
    public float textAngle;
    public string orientation;
    public Region[] regions;
}

[Serializable]
public class Region
{
    public string boundingBox;
    public Line[] lines;
}

[Serializable]
public class Line
{
    public string boundingBox;
    public Word[] words;
}

[Serializable]
public class Word
{
    public string boundingBox;
    public string text;
}