using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;
using CSLOLTool.Services;
using KoolChanger.Helpers;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

namespace KoolChanger;

public partial class SettingsWindow : Window
{
    private readonly ChampionService _championService = new();
    private readonly SkinService _skinService = new();
    private readonly UpdateService _updateService = new();
    private string _gamePath = string.Empty;
    private Preloader _preloader = new();
    private ToolService _toolService;

    public SettingsWindow(string gamePath)
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            DataContext = new WindowBlurEffect(this, AccentState.ACCENT_ENABLE_BLURBEHIND)
            {
                BlurOpacity = 100
            };
        };
        _gamePath = gamePath;
        _toolService = new ToolService(_gamePath);

        _updateService.OnUpdating += message => _preloader.SetStatus(message);

        _skinService.OnDownloaded += message => _preloader.SetStatus(message);
        _championService.OnDownloaded += message => _preloader.SetStatus(message);

        Loaded += (_, _) => _preloader = new Preloader { Owner = this };
        Closed += (_, _) => _preloader.Close();
    }

    public event Action<string>? PathSelected;

    private void ShowPreloader()
    {
        Effect = new BlurEffect { Radius = 10 };
        IsEnabled = false;
        _preloader.Show();
    }

    private void HidePreloader()
    {
        Effect = null;
        IsEnabled = true;
        _preloader.Hide();
    }

    private async void DownloadSkins(object sender, RoutedEventArgs e)
    {
        ShowPreloader();
        await _updateService.DownloadSkins();
        HidePreloader();
        StatusLabel.Content = "Finished downloading skins";
    }

    private async void GetChampionData(object sender, RoutedEventArgs e)
    {
        ShowPreloader();

        var champions = await _skinService.GetAllSkinsAsync(await _championService.GetChampionsAsync());
        champions.Sort((x, y) => x.Name.CompareTo(y.Name));

        StatusLabel.Content = "Finished getting info";

        File.WriteAllText("champion-data.json", JsonConvert.SerializeObject(champions));

        HidePreloader();
    }

    private void Close(object sender, MouseButtonEventArgs e)
    {
        _preloader.Close();
        Close();
    }

    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            try
            {
                DragMove();
            }
            catch
            {
            }
    }

    private void SelectGameFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Title = "Select league of legends game path",
            InitialDirectory = "C:\\"
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            _gamePath = dialog.FileName;
            if (!_gamePath.Contains("egends\\Game"))
                if (Directory.GetFiles(_gamePath).Contains("LeagueClient.exe"))
                    _gamePath = Path.Combine(_gamePath, "Game");
            _toolService = new ToolService(_gamePath);
            PathSelected?.Invoke(_gamePath);
        }
    }

    private async void DownloadSkinsPreview(object sender, RoutedEventArgs e)
    {
        ShowPreloader();

        await _championService.DownloadAllPreviews();
        StatusLabel.Content = "Finished downloading champion previews";
        HidePreloader();
    }
}