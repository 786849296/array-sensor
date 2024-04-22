using System.Collections.ObjectModel;
using array_sensor.ViewModels;

using Microsoft.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.Storage;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Microsoft.UI.Dispatching;
using array_sensor.Helpers;

namespace array_sensor.Views;

public sealed partial class MainPage : Page
{
    public const ushort row = 32;
    public const ushort col = 32;
    public ushort[,] heatmapValue = new ushort[row, col];

    public SerialDevice com;
    public string comID;
    public DataReader readerCom;
    public DispatcherQueueController thread_serialCollect = DispatcherQueueController.CreateOnDedicatedThread();
    public StorageFolder folder;

    public ObservableCollection<DeviceInformation> comInfos = [];
    internal ViewModel_switch viewModel_Switch = new();
    internal ObservableCollection<HeatMap_pixel> heatmap = [];

    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        DeviceWatcher deviceWatcher = DeviceInformation.CreateWatcher(SerialDevice.GetDeviceSelector());
        deviceWatcher.Added += (dw, info) => {
            comInfos.Add(info);
            combobox_com.SelectedItem ??= info;
            info_error.IsOpen = false;
        };
        deviceWatcher.Removed += (dw, infoUpdate) => {
            foreach (DeviceInformation comInfo in comInfos)
                if (comInfo.Id == infoUpdate.Id)
                {
                    comInfos.Remove(comInfo);
                    if (infoUpdate.Id == comID && viewModel_Switch.isStartIcon)
                        viewModel_Switch.isStartIcon = false;
                    if (comInfos.Count == 0)
                    {
                        info_error.IsOpen = true;
                        info_error.Message = "未找到串口";
                    }
                    break;
                }
        };
        deviceWatcher.Start();

        var window = App.MainWindow;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(AppTitleBar);

        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();

        for (int i = 0; i < row; i++)
            for (int j = 0; j < col; j++)
                heatmap.Add(new HeatMap_pixel(i, j));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
    }

    private void click_splitViewPaneBtn(object sender, RoutedEventArgs e)
    {
        sv.IsPaneOpen = true;
    }

    private async void click_startBtn(object sender, RoutedEventArgs e)
    {
        if (viewModel_Switch.isStartIcon && combobox_com.SelectedItem != null)
        {
            try
            {
                com = await SerialDevice.FromIdAsync((combobox_com.SelectedItem as DeviceInformation).Id);
                comID = (combobox_com.SelectedItem as DeviceInformation).Id;
                if (com != null)
                {
                    readerCom = new(com.InputStream)
                    {
                        ByteOrder = ByteOrder.BigEndian
                    };
                    info_error.IsOpen = false;
                    com.BaudRate = Convert.ToUInt32(combobox_baud.SelectedValue);
                    com.DataBits = Convert.ToUInt16(combobox_dataBits.SelectedValue);
                    com.StopBits = (SerialStopBitCount)combobox_stopBits.SelectedIndex;
                    com.Parity = (SerialParity)combobox_parity.SelectedIndex;
                    com.ReadTimeout = TimeSpan.FromMilliseconds(400);

                    info_error.IsOpen = false;
                    viewModel_Switch.isStartIcon = false;

                    thread_serialCollect.DispatcherQueue.TryEnqueue(async () =>
                    {
                        while (true)
                        {
                            if (viewModel_Switch.isStartIcon)
                            {
                                //thread_collect.ShutdownQueueAsync();
                                com.Dispose();
                                readerCom.Dispose();
                                return;
                            }
                            while (true)
                            {
                                if (readerCom.UnconsumedBufferLength < 2)
                                    await readerCom.LoadAsync(row * col * 2 + 2);
                                if (readerCom.ReadByte() == 0xff)
                                    if (readerCom.ReadByte() == 0xff)
                                    {
                                        if (readerCom.UnconsumedBufferLength < row * col * 2)
                                            await readerCom.LoadAsync(row * col * 2 - readerCom.UnconsumedBufferLength);
                                        break;
                                    }
                            }
                            for (int i = 0; i < row; i++)
                                for (int j = 0; j < col; j++)
                                    heatmapValue[i, j] = readerCom.ReadUInt16();
                            this.DispatcherQueue.TryEnqueue(() => {
                                if (folder != null)
                                {
                                    string fileName = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss.fff") + ".csv";
                                    using (var writer = new StreamWriter(System.IO.Path.Combine(folder.Path, fileName)))
                                        for (int i = 0; i < row; i++)
                                        {
                                            for (int j = 0; j < col; j++)
                                                writer.Write(heatmapValue[i, j] + ",");
                                            writer.Write('\n');
                                        }
                                }
                                for (int i = 0; i < row; i++)
                                    for (int j = 0; j < col; j++)
                                        heatmap[i * col + j].adcValue = heatmapValue[i, j];
                            });
                        }
                    });
                }
            }
            catch (Exception error)
            {
                info_error.IsOpen = true;
                info_error.Message = error.ToString();
            }
        }
        else if (!viewModel_Switch.isStartIcon)
            viewModel_Switch.isStartIcon = true;
    }

    private async void toggle_imageCollectSw(object sender, RoutedEventArgs e)
    {
        ToggleSwitch imageCollectSw = sender as ToggleSwitch;
        if (imageCollectSw.IsOn)
        {
            FolderPicker folderPicker = new();
            //var window = WindowHelper.GetWindowForElement(this);
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Initialize the folder picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);
            // Set options for your folder picker
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");
            // Open the picker for the user to pick a folder
            folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);
                ts_imageCollect.OnContent = folder.Path;
            }
            else
                imageCollectSw.IsOn = false;
        }
        else
            folder = null;
    }

    //created by bing
    private void pointerEntered_splitViewPaneBtn(object sender, PointerRoutedEventArgs e)
    {
        //// 创建一个Storyboard对象
        //Storyboard storyboard = new Storyboard();
        //// 创建一个DoubleAnimation对象，用于改变RotateTransform对象的Angle属性
        //DoubleAnimation animation = new DoubleAnimation();
        //// 设置动画的目标属性为Angle
        //Storyboard.SetTargetProperty(animation, "Angle");
        //// 设置动画的目标对象为RotateTransform对象
        //Storyboard.SetTarget(animation, splitViewPaneBtn_rotate);
        //// 设置动画的开始值为0
        //animation.From = 0;
        //// 设置动画的结束值为360
        //animation.To = 60;
        //// 设置动画的持续时间为1秒
        //animation.Duration = new Duration(TimeSpan.FromSeconds(0.2));
        //// 将动画添加到Storyboard对象中
        //storyboard.Children.Add(animation);
        //// 启动Storyboard对象
        //storyboard.Begin();
        icon_setting.Rotation = icon_setting.Rotation + 120 % 360;
    }

    private void selectionChanged_rangeCb(object sender, SelectionChangedEventArgs e)
    {
        ushort range = Convert.ToUInt16((sender as ComboBox).SelectedValue);
        HeatMap_pixelHelper.range = range;
        if (legendRange != null)
            legendRange.Text = range.ToString();
    }

    private async void click_recurrentBtn(object sender, RoutedEventArgs e)
    {
        // Create a file picker
        var openPicker = new Windows.Storage.Pickers.FileOpenPicker();

        // Retrieve the window handle (HWND) of the current WinUI 3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Initialize the file picker with the window handle (HWND).
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

        // Set options for your file picker
        openPicker.FileTypeFilter.Add(".csv");

        // Open the picker for the user to pick a file
        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
            CachedFileManager.DeferUpdates(file);
            using var stream = await file.OpenReadAsync();
            using var fr = new StreamReader(stream.AsStreamForRead());
            string[] colString;
            ushort[,] heatmapValue = new ushort[row, col];
            for (int i = 0; i < row; i++)
            {
                colString = fr.ReadLine().Split(',');
                for (int j = 0; j < col; j++)
                {
                    heatmapValue[i, j] = Convert.ToUInt16(colString[j]);
                    heatmap[i * col + j].adcValue = heatmapValue[i, j];
                }
            }
        }
    }
}
