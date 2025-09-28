using System.IO.Compression;
using CSLOLTool.Models;
using Newtonsoft.Json;

namespace CSLOLTool.Services;

public class CustomSkinService
{
    private readonly ToolService _toolService;

    public CustomSkinService(ToolService toolService)
    {
        _toolService = toolService;
        GetSkins();
    }

    public List<CustomSkin> ImportedSkins { get; set; } = [];

    public void AddSkin(CustomSkin skin, string path)
    {
        _toolService.Import(path, skin.Name);
        ImportedSkins.Add(skin);
        SaveSkins();
    }

    public void RemoveSkin(CustomSkin skin)
    {
        ImportedSkins.Remove(skin);
        Directory.Delete(Path.Combine("installed", skin.Name), true);
        SaveSkins();
    }

    public void SaveSkins()
    {
        var jsonSkins = JsonConvert.SerializeObject(ImportedSkins);
        File.WriteAllText("customskins.json", jsonSkins);
    }

    public void GetSkins()
    {
        try
        {
            var skins = JsonConvert.DeserializeObject<List<CustomSkin>>(File.ReadAllText("customskins.json"));
            ImportedSkins = skins == null ? new List<CustomSkin>() : skins;
        }
        catch
        {
        }
    }

    public CustomSkin FromFile(string path)
    {
        var infoPath = "META/info.json";

        using (var archive = ZipFile.OpenRead(path))
        {
            var entry = archive.GetEntry(infoPath);
            if (entry != null)
                using (var stream = entry.Open())
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var skin = JsonConvert.DeserializeObject<CustomSkin>(json);
                    if (skin == null)
                        throw new Exception("Wrong META with custom skin: " + path);
                    return skin;
                }

            throw new Exception("Wrong META with custom skin: " + path);
        }
    }
}