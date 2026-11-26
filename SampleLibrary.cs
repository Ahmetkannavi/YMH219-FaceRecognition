using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace FaceRecognition;

internal sealed record Person(string Name, string Folder, string[] Files);

internal sealed class SampleLibrary(bool demo)
{
    public static readonly string DemoRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Demo");
    private static readonly string LocalRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YMH219", "Kadraj", "Ornekler");
    public bool Demo { get; } = demo;

    public List<Person> People()
    {
        string root = Demo ? Path.Combine(DemoRoot, "Egitim") : LocalRoot;
        if (!Directory.Exists(root)) return [];
        var people = new List<Person>();
        foreach (string dir in Directory.GetDirectories(root))
        {
            var label = Path.Combine(dir, "etiket.txt");
            if (!Demo && !File.Exists(label)) continue;
            var name = Demo ? Path.GetFileName(dir) : File.ReadAllText(label);
            people.Add(new Person(name, dir, Directory.GetFiles(dir, Demo ? "*.jpg" : "*.png")));
        }
        return people.OrderBy(p => p.Name).ToList();
    }

    public void Add(string name, byte[] crop)
    {
        name = name.Trim().Normalize();
        if (Demo || name.Length is < 1 or > 40) throw new InvalidOperationException("Kişisel kümede 1–40 karakterlik bir ad seçin.");
        var key = Encoding.UTF8.GetBytes(name.ToUpper(CultureInfo.GetCultureInfo("tr-TR")));
        string dir = Path.Combine(LocalRoot, Convert.ToHexString(SHA256.HashData(key)));
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, Convert.ToHexString(SHA256.HashData(crop)) + ".png");
        if (File.Exists(target)) throw new IOException("Bu kare aynı adda zaten var. Yeni bir poz seçin.");
        string labelFile = Path.Combine(dir, "etiket.txt");
        if (!File.Exists(labelFile)) File.WriteAllText(labelFile, name);
        File.WriteAllBytes(target, crop);
    }
}
