using System.Windows;
using Tracealyzer.ViewModels;

namespace Tracealyzer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _ = vm.StopLiveStream();
        }
    }
}