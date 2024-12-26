using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Windows.Graphics.Capture;
using Windows.Media.Playback;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ScreenRecorder.UI;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SavePage : Page
{
    private StorageFile _previewFile;

    public SavePage()
    {
        this.InitializeComponent();
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _previewFile = (StorageFile)e.Parameter;

        // Check the file size (if zero, there's nothing to play)
        var props = await _previewFile.GetBasicPropertiesAsync();
        if (props.Size == 0)
        {
            return;
        }
        
        // TODO: Uncomment when implementing Preview Player
        //var media = MediaSource.CreateFromStorageFile(_previewFile);
        //var isValid = ValidateMediaSourceAsync(media);
        //PreviewPlayer.Source = MediaSource.CreateFromStorageFile(_previewFile);
    }

    private Task<bool> ValidateMediaSourceAsync(MediaSource mediaSource)
    {
        var tcs = new TaskCompletionSource<bool>();

        var tempPlayer = new MediaPlayer();

        // When the media is successfully opened
        tempPlayer.MediaOpened += (sender, args) =>
        {
            tcs.TrySetResult(true);
        };

        // If there's an error (unsupported format, corrupted file, etc.)
        tempPlayer.MediaFailed += (sender, args) =>
        {
            // You can check args.Error or args.ExtendedErrorCode for details
            tcs.TrySetResult(false);
        };

        // Assign the MediaSource
        tempPlayer.Source = mediaSource;

        // Optionally, do not actually show the video. We’re just testing playback.
        // If you do want to show it, you'd attach MediaPlayerElement to tempPlayer.

        return tcs.Task;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveVideoAsync();

        // Uncomment below if using PickVideoAsync()
        //DispatcherQueue.TryEnqueue(async () =>
        //{
        //    // Ask the user where they'd like the video to live
        //    var file = await PickVideoAsync();
        //    if (file == null)
        //    {
        //        // The user canceled
        //        return;
        //    }
        //    // Move our video to its new home
        //    //PreviewPlayer.Source = null;
        //    await _previewFile.MoveAndReplaceAsync(file);

        //    GoToMainPage();

        //});

        GoToMainPage();
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        //PreviewPlayer.Source = null;
        await _previewFile.DeleteAsync();

        GoToMainPage();
    }

    private void GoToMainPage()
    {
        Frame.BackStack.Clear();

        Frame.Navigate(typeof(MainPage));
    }

    // Use PickVideoAsync to open a dialog window to let user choice the location of saving video
    private async Task<StorageFile> PickVideoAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
        picker.SuggestedFileName = "recordedVideo";
        picker.DefaultFileExtension = ".mp4";
        picker.FileTypeChoices.Add("MP4 Video", new List<string> { ".mp4" });

        InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.AppWindow));

        return await picker.PickSaveFileAsync();
    }

    // This method automatically save video in documents
    private async Task SaveVideoAsync()
    {
        // Validate the input preview file
        if (_previewFile == null)
        {
            throw new ArgumentNullException(nameof(_previewFile), "Preview file cannot be null.");
        }

        // This could happen behind the scene 
        // Get the path to the user's Documents folder
        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var screenRecorderFolder = ".screenRecorder";
        var fileName = $"video-{Guid.NewGuid()}.mp4";

        // Combine the path to include a subfolder for saving the video
        var outputFolderPath = Path.Combine(documentsFolder, screenRecorderFolder);

        Directory.CreateDirectory(outputFolderPath);
        // Save the file using Windows.Storage
        var sessionFolder = await StorageFolder.GetFolderFromPathAsync(outputFolderPath);
        await _previewFile.CopyAsync(sessionFolder, fileName, NameCollisionOption.ReplaceExisting);

        // Create the full path for the video file
        var videoFilePath = Path.Combine(outputFolderPath, fileName);
        Console.WriteLine($"Video will be saved at: {videoFilePath}");

    }
    
}
