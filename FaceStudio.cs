using OpenCvSharp;
using OpenCvSharp.Face;

namespace FaceRecognition;

internal sealed class FaceStudio : IDisposable
{
    private readonly CascadeClassifier detector;
    private LBPHFaceRecognizer? recognizer;
    private string[] labels = [];
    public bool Trained => recognizer != null;
    private const double RejectDistance = 78;

    public FaceStudio()
    {
        string cascade = Path.Combine(AppContext.BaseDirectory, "Assets", "frontalface.xml");
        if (!File.Exists(cascade)) throw new FileNotFoundException("Yüz algılama modeli (Assets/frontalface.xml) eksik.");
        detector = new CascadeClassifier(cascade);
    }

    public static Mat Read(string path)
    {
        var image = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
        if (!image.Empty()) return image;
        image.Dispose();
        throw new InvalidDataException("Görüntü okunamadı. Bir PNG veya JPEG seçin.");
    }

    public Rect[] Detect(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);
        return detector.DetectMultiScale(gray, 1.1, 5, minSize: new OpenCvSharp.Size(44, 44));
    }

    public static Mat Crop(Mat frame, Rect rect)
    {
        using var region = new Mat(frame, rect);
        using var gray = new Mat();
        var sample = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Resize(gray, sample, new OpenCvSharp.Size(128, 128));
        Cv2.EqualizeHist(sample, sample);
        return sample;
    }

    public void Reset() { recognizer?.Dispose(); recognizer = null; labels = []; }

    public void Train(SampleLibrary source)
    {
        var people = source.People();
        if (people.Count < 2 || people.Any(p => p.Files.Length < 2))
            throw new InvalidOperationException("En az iki ad ve her ad için en az iki farklı örnek gerekiyor.");
        List<Mat> samples = [];
        List<int> ids = [];
        try
        {
            for (int id = 0; id < people.Count; id++)
            foreach (string file in people[id].Files)
            {
                if (source.Demo)
                {
                    using var frame = Read(file);
                    var faces = Detect(frame);
                    if (faces.Length != 1) throw new InvalidDataException("Eğitim karesinde tek yüz bulunamadı.");
                    samples.Add(Crop(frame, faces[0]));
                }
                else samples.Add(Cv2.ImDecode(File.ReadAllBytes(file), ImreadModes.Grayscale));
                ids.Add(id);
            }
            var next = LBPHFaceRecognizer.Create();
            try { next.Train(samples, ids); }
            catch { next.Dispose(); throw; }
            Reset();
            recognizer = next;
            labels = people.Select(p => p.Name).ToArray();
        }
        finally { foreach (var sample in samples) sample.Dispose(); }
    }

    public string Describe(Mat frame, Rect rect)
    {
        if (recognizer == null) return "Yüz algılandı";
        using var sample = Crop(frame, rect);
        recognizer.Predict(sample, out int label, out double distance);
        string name = label >= 0 && distance <= RejectDistance ? labels[label] : "Bilinmiyor";
        return $"{name}  ·  uzaklık {distance:F1}";
    }

    public void Dispose() { Reset(); detector.Dispose(); }
}
