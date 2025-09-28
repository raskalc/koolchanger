using CSLOLTool.Models;

namespace KoolChanger.Helpers;

internal class Config
{
    public Dictionary<string, Skin> SelectedSkins { get; set; } = new();
    public string GamePath { get; set; } = "";
}