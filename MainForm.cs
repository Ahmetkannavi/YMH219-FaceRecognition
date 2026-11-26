using OpenCvSharp;

namespace FaceRecognition;

internal sealed class MainForm : Form
{
    private readonly PictureBox preview = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Ui.Ink, Dock = DockStyle.Fill, AccessibleName = "Yüz çerçeveleri" };
    private readonly ComboBox source = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Örnek kümesi" };
    private readonly ComboBox name = new() { Dock = DockStyle.Top, MaxLength = 40, AccessibleName = "Kişi etiketi" };
    private readonly ListBox roster = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Ui.Soft, AccessibleName = "Eğitim örnekleri" };
    private readonly Label outcome = Ui.Label("", 12, true), info = Ui.Label(""), mode = Ui.Label("", 11, true);
    private readonly Button train, add, cameraStart, cameraStop, demoTest;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 160 };
    private FaceStudio? studio;
    private VideoCapture? camera;
    private SampleLibrary library = new(true);
    private Mat? current;
    private Rect[] faces = [];
    private int demoIndex;

    public MainForm()
    {
        Ui.Window(this, "Kadraj • Yüz atölyesi", new(975, 650), new(820, 580));
        var root = Ui.Rows(76, 49, -100, 37);
        Controls.Add(root);
        root.Controls.Add(Ui.Header("Kadraj", "Yüz algılama ve yerel tanıma atölyesi"), 0, 0);
        var file = Ui.Button("Görüntü seç…", SelectFile, true);
        cameraStart = Ui.Button("Kamera başlat", () => Guard(StartCamera));
        cameraStop = Ui.Button("Kamera durdur", () => { StopCamera(); info.Text = "Kamera durduruldu."; });
        demoTest = Ui.Button("Test pozu", () => Guard(NextDemo));
        root.Controls.Add(Ui.Bar(file, cameraStart, cameraStop, demoTest), 0, 1);
        var columns = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 275));
        root.Controls.Add(columns, 0, 2);
        var canvas = Ui.Rows(35, -100, 38);
        canvas.Controls.Add(mode, 0, 0);
        canvas.Controls.Add(preview, 0, 1);
        canvas.Controls.Add(outcome, 0, 2);
        columns.Controls.Add(canvas, 0, 0);
        var side = Ui.Rows(37, 32, 27, -100, 33, 30, 33, 43, 43);
        side.Padding = new Padding(15, 0, 0, 0);
        columns.Controls.Add(side, 1, 0);
        source.Items.AddRange(["Sentetik demo", "Kişisel yerel küme"]);
        source.SelectedIndex = 0;
        add = Ui.Button("Bu yüzü örnek ekle", AddSample);
        train = Ui.Button("Modeli eğit", Train, true);
        Control[] controls = [Ui.Label("Eğitim kümesi", 15, true), source, Ui.Label("Ad ve örnek sayısı", 10, true),
            roster, Ui.Label("Her ad için en az 2 ayrı poz"), Ui.Label("Aynı kişi için aynı adı seçin"), name, Ui.Bar(add), Ui.Bar(train)];
        for (int i = 0; i < controls.Length; i++) side.Controls.Add(controls[i], 0, i);
        root.Controls.Add(info, 0, 3);
        source.SelectedIndexChanged += (_, _) => Guard(() =>
        {
            library = new SampleLibrary(source.SelectedIndex == 0);
            studio?.Reset();
            UpdateLibrary();
            if (library.Demo) NextDemo();
            else { StopCamera(); ClearFrame(); info.Text = "Gönüllünün görüntüsünü seçin, adını yazın ve bir örnek ekleyin."; }
            Render();
        });
        timer.Tick += (_, _) => Guard(() =>
        {
            var frame = new Mat();
            if (camera == null || !camera.Read(frame) || frame.Empty())
            { frame.Dispose(); StopCamera(); throw new IOException("Kameradan kare alınamadı. Kamera iznini kontrol edin."); }
            ReplaceFrame(frame);
        });
        Shown += (_, _) => Guard(() => { studio = new FaceStudio(); UpdateLibrary(); NextDemo(); });
        FormClosed += (_, _) => { StopCamera(); timer.Dispose(); ClearFrame(); studio?.Dispose(); };
    }

    private void Guard(Action work)
    {
        try { work(); }
        catch (Exception ex) { info.Text = "İşlem tamamlanamadı. " + ex.Message; }
    }

    private void UpdateLibrary()
    {
        var people = library.People();
        string selected = name.Text;
        name.Items.Clear();
        roster.Items.Clear();
        foreach (var person in people)
        {
            name.Items.Add(person.Name);
            string status = person.Files.Length < 2 ? $" · {2 - person.Files.Length} eksik" : " · hazır";
            roster.Items.Add($"{person.Name}   /   {person.Files.Length} poz{status}");
        }
        if (people.Count < 2) roster.Items.Add($"{2 - people.Count} kişi etiketi daha gerekli");
        name.Text = selected;
        name.Enabled = add.Enabled = !library.Demo;
        demoTest.Enabled = library.Demo;
        train.Enabled = studio != null && people.Count >= 2 && people.All(p => p.Files.Length >= 2);
        cameraStop.Enabled = camera != null;
    }

    private void SelectFile()
    {
        using var dialog = new OpenFileDialog { Filter = "Görüntü (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        Guard(() => { StopCamera(); ReplaceFrame(FaceStudio.Read(dialog.FileName)); info.Text = Path.GetFileName(dialog.FileName); });
    }

    private void NextDemo()
    {
        StopCamera();
        var person = demoIndex++ % 2 == 0 ? "Ada" : "Bora";
        ReplaceFrame(FaceStudio.Read(Path.Combine(SampleLibrary.DemoRoot, "Test", person, "poz-3.jpg")));
        info.Text = $"Sentetik test pozu • Beklenen etiket: {person}    |    Eğitim dışında tutuldu.";
    }

    private void StartCamera()
    {
        StopCamera();
        var opened = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
        if (!opened.IsOpened()) { opened.Dispose(); throw new IOException("Kamera açılamadı. Aygıtı ve Windows kamera izinlerini kontrol edin."); }
        camera = opened;
        cameraStart.Enabled = false;
        cameraStop.Enabled = true;
        info.Text = "Kamera açık • Örnek kaydetmek için bir ad seçip ekleme düğmesine basın.";
        timer.Start();
    }

    private void StopCamera()
    {
        timer.Stop();
        camera?.Dispose();
        camera = null;
        cameraStart.Enabled = true;
        cameraStop.Enabled = false;
    }

    private void AddSample() => Guard(() =>
    {
        if (current == null || faces.Length != 1) throw new InvalidOperationException("Karede tam bir yüz bulunmalı.");
        using var crop = FaceStudio.Crop(current, faces[0]);
        library.Add(name.Text, crop.ImEncode(".png"));
        studio?.Reset();
        UpdateLibrary();
        Render();
        info.Text = "Örnek yerel klasöre eklendi. Güncel örneklerle modeli eğitin.";
    });

    private void Train() => Guard(() =>
    {
        studio?.Train(library);
        Render();
        info.Text = "Model eğitildi. Eğitim dışında bir poz seçebilirsiniz.";
    });

    private void ReplaceFrame(Mat frame)
    {
        current?.Dispose();
        current = frame;
        faces = studio?.Detect(frame) ?? [];
        Render();
    }

    private void ClearFrame()
    {
        current?.Dispose();
        current = null;
        faces = [];
        var old = preview.Image;
        preview.Image = null;
        old?.Dispose();
    }

    private void Render()
    {
        bool trained = studio?.Trained == true;
        mode.Text = trained ? "TANIMA  /  LBPH modeli etkin" : "ALGILAMA  /  Yüz çerçeveleri";
        outcome.Text = current == null ? "Görüntü bekleniyor" : $"{faces.Length} yüz algılandı";
        if (current == null || studio == null) return;
        using var buffer = new MemoryStream(current.ImEncode(".bmp"));
        using var original = new Bitmap(buffer);
        var image = new Bitmap(original);
        var results = new List<string>();
        using (var g = Graphics.FromImage(image))
        using (var pen = new Pen(trained ? Color.SpringGreen : Color.Gold, Math.Max(3, image.Width / 160)))
        {
            foreach (var rect in faces)
            {
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                string label = studio.Describe(current, rect);
                results.Add(label);
                using var font = new Font("Segoe UI", Math.Max(13, image.Width / 35), FontStyle.Bold, GraphicsUnit.Pixel);
                var size = g.MeasureString(label, font);
                float y = Math.Max(0, rect.Y - size.Height);
                g.FillRectangle(Brushes.Black, rect.X, y, size.Width, size.Height);
                g.DrawString(label, font, Brushes.White, rect.X, y);
            }
        }
        if (results.Count > 0 && trained) outcome.Text = string.Join("  /  ", results);
        var prior = preview.Image;
        preview.Image = image;
        prior?.Dispose();
    }
}
