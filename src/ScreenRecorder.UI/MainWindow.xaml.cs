using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Windowing;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ScreenRecorder.UI;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public static Window AppWindow => _appWindow;
    private static Window _appWindow;
    
    public MainWindow()
    {
        this.InitializeComponent();
        InitializePage();

        // Store the AppWindow for later use
        _appWindow = this;
    }

    private void InitializePage()
    {
        this.ContentFrame.Navigate(typeof(MainPage));
    }


    private void StartSessionButton_Click(object sender, RoutedEventArgs e)
    {
        this.ContentFrame.Navigate(typeof(SettingsPage));
    }


    // Give an error for CapturePreview object. OnNavigatedFrom() do not fire, therefore, do not dispose the object.
    // This happens when user use ExitButton from window.
    //private void ExitButton_Click(object sender, RoutedEventArgs e)
    //{

    //    Application.Current.Exit();
    //}


}
