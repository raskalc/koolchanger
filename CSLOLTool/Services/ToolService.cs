using System.Diagnostics;

namespace CSLOLTool.Services;

public class ToolService
{
    private readonly Tool _tool;

    public ToolService(string gamePath)
    {
        _tool = new Tool(gamePath);

        if (!Directory.Exists("skins"))
            return;
    }

    public event Action<string>? OverlayRunned;
    public event Action<string>? ChromaInstalled;
    public event Action<string>? SkinInstalled;

    public Process Run(IEnumerable<string> mods)
    {
        _tool.StatusChanged += data => OverlayRunned?.Invoke(data);
        _tool.SaveOverlay("default", mods, false);
        return _tool.RunOverlay("default");
    }

    public void Import(string path, string name)
    {
        _tool.Import(path, name);
    }
}