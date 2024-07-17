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
using System;
using System.Runtime.CompilerServices;

namespace array_sensor.Views;

public sealed partial class MainPage : Page
{
    public const ushort row = 10;
    public const ushort col = 4;
    public ushort[] prePressure = new ushort[row * col];

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
        var window = App.MainWindow;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(AppTitleBar);

        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();

        for (int i = 0; i < row; i++)
            for (int j = 0; j < col; j++)
                if ((i == 0 && (j == 0 || j == 3)) || (i == 9 && (j == 0 || j == 3))) 
                    heatmap.Add(new HeatMap_pixel(Visibility.Collapsed));
                else
                    heatmap.Add(new HeatMap_pixel(Visibility.Visible));

        DeviceWatcher deviceWatcher = DeviceInformation.CreateWatcher(SerialDevice.GetDeviceSelector());
        deviceWatcher.Added += (dw, info) =>
            App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                comInfos.Add(info);
                combobox_com.SelectedItem ??= info;
                info_error.IsOpen = false;
            });
        deviceWatcher.Removed += (dw, infoUpdate) =>
            App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
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
            });
        deviceWatcher.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ushortSub(ushort a, ushort b)
    {
        return (ushort)(a > b ? a - b : 0);
    }

    private void heatMapValue2UI(ushort[,] heatmapValue)
    {
        heatmap[1].adcValue = ushortSub(heatmapValue[0, 0], prePressure[1]);
        heatmap[2].adcValue = ushortSub(heatmapValue[0, 1], prePressure[2]);
        heatmap[4].adcValue = ushortSub(heatmapValue[0, 2], prePressure[4]);
        heatmap[5].adcValue = ushortSub(heatmapValue[0, 3], prePressure[5]);
        heatmap[8].adcValue = ushortSub(heatmapValue[1, 0], prePressure[8]);
        heatmap[9].adcValue = ushortSub(heatmapValue[1, 1], prePressure[9]);
        heatmap[6].adcValue = ushortSub(heatmapValue[1, 2], prePressure[6]);
        heatmap[7].adcValue = ushortSub(heatmapValue[1, 3], prePressure[7]);
        heatmap[10].adcValue = ushortSub(heatmapValue[2, 0], prePressure[10]);
        heatmap[11].adcValue = ushortSub(heatmapValue[2, 1], prePressure[11]);
        heatmap[14].adcValue = ushortSub(heatmapValue[2, 2], prePressure[14]);
        heatmap[15].adcValue = ushortSub(heatmapValue[2, 3], prePressure[15]);
        heatmap[18].adcValue = ushortSub(heatmapValue[3, 0], prePressure[18]);
        heatmap[19].adcValue = ushortSub(heatmapValue[3, 1], prePressure[19]);
        heatmap[22].adcValue = ushortSub(heatmapValue[3, 2], prePressure[22]);
        heatmap[23].adcValue = ushortSub(heatmapValue[3, 3], prePressure[23]);
        heatmap[26].adcValue = ushortSub(heatmapValue[4, 0], prePressure[26]);
        heatmap[27].adcValue = ushortSub(heatmapValue[4, 1], prePressure[27]);
        heatmap[30].adcValue = ushortSub(heatmapValue[4, 2], prePressure[30]);
        heatmap[31].adcValue = ushortSub(heatmapValue[4, 3], prePressure[31]);
        heatmap[34].adcValue = ushortSub(heatmapValue[5, 0], prePressure[34]);
        heatmap[35].adcValue = ushortSub(heatmapValue[5, 1], prePressure[35]);
        heatmap[38].adcValue = ushortSub(heatmapValue[5, 2], prePressure[38]);
        heatmap[37].adcValue = ushortSub(heatmapValue[5, 3], prePressure[37]);
        heatmap[33].adcValue = ushortSub(heatmapValue[6, 0], prePressure[33]);
        heatmap[32].adcValue = ushortSub(heatmapValue[6, 1], prePressure[32]);
        heatmap[29].adcValue = ushortSub(heatmapValue[6, 2], prePressure[29]);
        heatmap[28].adcValue = ushortSub(heatmapValue[6, 3], prePressure[28]);
        heatmap[25].adcValue = ushortSub(heatmapValue[7, 0], prePressure[25]);
        heatmap[24].adcValue = ushortSub(heatmapValue[7, 1], prePressure[24]);
        heatmap[21].adcValue = ushortSub(heatmapValue[7, 2], prePressure[21]);
        heatmap[20].adcValue = ushortSub(heatmapValue[7, 3], prePressure[20]);
        heatmap[17].adcValue = ushortSub(heatmapValue[8, 0], prePressure[17]);
        heatmap[16].adcValue = ushortSub(heatmapValue[8, 1], prePressure[16]);
        heatmap[13].adcValue = ushortSub(heatmapValue[8, 2], prePressure[13]);
        heatmap[12].adcValue = ushortSub(heatmapValue[8, 3], prePressure[12]);
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
            }
            catch (Exception error)
            {
                info_error.IsOpen = true;
                info_error.Message = error.ToString();
                return;
            }
            comID = (combobox_com.SelectedItem as DeviceInformation).Id;
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
                    ushort[,] heatmapValue = new ushort[row, col];
                    for (int i = 0; i < row - 1; i++)
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
                        heatMapValue2UI(heatmapValue);
                    });
                }
            });
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
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
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
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

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
                    heatMapValue2UI(heatmapValue);
                }
            }
        }
    }

    private void toggle_removePrePSw(object sender, RoutedEventArgs e)
    {
        if ((sender as ToggleSwitch).IsOn)
            if (!viewModel_Switch.isStartIcon)
                for (int i = 0; i < row; i++)
                    for (int j = 0; j < col; j++)
                        prePressure[i * col + j] = heatmap[i * col + j].adcValue;
            else
                (sender as ToggleSwitch).IsOn = false;
        else
            Array.Clear(prePressure, 0, row * col);
    }
}
