using System.Windows;
using KoolChanger.Helpers;

namespace KoolChanger;

/// <summary>
///     Логика взаимодействия для Preloader.xaml
/// </summary>
public partial class Preloader : Window
{
    public Preloader()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            DataContext = new WindowBlurEffect(this, AccentState.ACCENT_ENABLE_BLURBEHIND)
            {
                BlurOpacity = 100
            };
        };
    }

    public void SetStatus(string text)
    {
        Dispatcher.Invoke(() => StatusLabel.Content = text);
    }
}