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
using Windows.Devices.Bluetooth;
using System.Runtime.InteropServices.WindowsRuntime;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.SKCharts;
using OpenCvSharp;
using LiveChartsCore.SkiaSharpView.WinUI;

namespace array_sensor.Views;

public sealed partial class MainPage : Page
{
    public const ushort row = 32;
    public const ushort col = 32;

    private ushort[,] prePressure = new ushort[row, col];
    private StorageFolder folder;
    private DataReader reader;
    private DataWriter writer;
    private SerialDevice com;
    private nint? handleBle = null;
    private InMemoryRandomAccessStream bleReadStream;
    private int armControl = 0;

    public ObservableCollection<DeviceInformation> comInfos = [];
    public ObservableCollection<DeviceInformation> bleInfos = [];
    internal ViewModel_switch viewModel_Switch = new();
    internal ObservableCollection<HeatMap_pixel> palm = [];
    internal ObservableCollection<HeatMap_pixel> f1 = [];
    internal ObservableCollection<HeatMap_pixel> f2 = [];
    internal ObservableCollection<HeatMap_pixel> f3 = [];
    internal ObservableCollection<HeatMap_pixel> f4 = [];
    internal ObservableCollection<ViewModel_lineChart> lineCharts = [];
    internal bool info_comOpen = true;
    internal bool info_bleOpen = true;
    private int lastPivotIndex;

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

        for (int i = 0; i < 7; i++)
            for (int j = 0; j < 8; j++)
                if ((i == 3 && j == 7) || (i > 4 && j == 0) || (i > 3 && j > 4))
                    palm.Add(new HeatMap_pixel(Visibility.Collapsed));
                else
                    palm.Add(new HeatMap_pixel(Visibility.Visible));
        for (int i = 0; i < 24; i++)
            for (int j = 0; j < 8; j++)
            {
                f1.Add(new HeatMap_pixel(Visibility.Visible));
                f2.Add(new HeatMap_pixel(Visibility.Visible));
                f3.Add(new HeatMap_pixel(Visibility.Visible));
                f4.Add(new HeatMap_pixel(Visibility.Visible));
            }

        lastPivotIndex = pivot.SelectedIndex;

        DeviceWatcher deviceWatcherCOM = DeviceInformation.CreateWatcher(SerialDevice.GetDeviceSelector());
        deviceWatcherCOM.Added += (dw, info) => {
            App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                comInfos.Add(info);
                combobox_com.SelectedItem ??= info;
                info_comOpen = false;
                info_com.IsOpen = false;
            });
        };
        deviceWatcherCOM.Removed += (dw, infoUpdate) => {
            App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                foreach (DeviceInformation comInfo in comInfos)
                    if (comInfo.Id == infoUpdate.Id)
                    {
                        if (pivot.SelectedIndex == 0)
                        {
                            if (infoUpdate.Id == (combobox_com.SelectedItem as DeviceInformation).Id && viewModel_Switch.isStartIcon)
                                viewModel_Switch.isStartIcon = true;
                            if (comInfos.Count == 0)
                            {
                                info_comOpen = true;
                                info_com.IsOpen = true;
                                info_com.Message = "未找到串口";
                            }
                            break;
                        }
                        comInfos.Remove(comInfo);
                    }

            });
        };

        // Query for extra properties you want returned
        string[] requestedProperties = ["System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected"];
        DeviceWatcher deviceWatcherBLE = DeviceInformation.CreateWatcher(
            BluetoothLEDevice.GetDeviceSelectorFromDeviceName("CH9143BLE2U"),
            requestedProperties,
            DeviceInformationKind.AssociationEndpoint);
        deviceWatcherBLE.Added += (dw, info) =>
        {
            App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                bleInfos.Add(info);
                combobox_ble.SelectedItem ??= info;
                info_bleOpen = false;
                info_ble.IsOpen = false;
            });
        };
        deviceWatcherBLE.Removed += (dw, infoUpdate) => {
            App.MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => {
                for (int i = 0; i < bleInfos.Count; i++)
                    if (bleInfos[i].Id == infoUpdate.Id)
                    {
                        if (pivot.SelectedIndex == 1)
                        {
                            if (infoUpdate.Id == (combobox_ble.SelectedItem as DeviceInformation).Id && viewModel_Switch.isStartIcon)
                                viewModel_Switch.isStartIcon = true;
                            if (bleInfos.Count == 0)
                            {
                                info_bleOpen = true;
                                info_ble.IsOpen = true;
                                info_ble.Message = "未找到蓝牙";
                            }
                            break;
                        }
                        bleInfos.Remove(bleInfos[i]);
                    }
            });
        };
        deviceWatcherCOM.Start();
        deviceWatcherBLE.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);
    }

    private void heatMapValue2UI(ushort[,] heatmapValue)
    {
        if (ts_removePreP.IsOn)
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    heatmapValue[i, j] = (ushort)(heatmapValue[i, j] > prePressure[i, j] ? heatmapValue[i, j] - prePressure[i, j] : 0);
        var size = Convert.ToInt32(combobox_gaussKernelSize.SelectedValue);
        Mat mat = new(row, col, MatType.CV_16U, heatmapValue);
        Cv2.GaussianBlur(mat, mat, new OpenCvSharp.Size(size, size), 0);
        for (int i = 0; i < row; i++)
            for (int j = 0; j < col; j++)
                if (i < 24)
                    switch (j / 8)
                    {
                        case 0:
                            f1[(23 - i) * 8 + 7 - j].adcValue = mat.At<ushort>(i, j);
                            f1[(23 - i) * 8 + 7 - j].chartLine?.chartUpdate(heatmapValue[i, j]);
                            break;
                        case 1:
                            f2[(23 - i) * 8 + 15 - j].adcValue = mat.At<ushort>(i, j);
                            f2[(23 - i) * 8 + 15 - j].chartLine?.chartUpdate(heatmapValue[i, j]);
                            break;
                        case 2:
                            f3[(23 - i) * 8 + 23 - j].adcValue = mat.At<ushort>(i, j);
                            f3[(23 - i) * 8 + 23 - j].chartLine?.chartUpdate(heatmapValue[i, j]);
                            break;
                        case 3:
                            f4[(23 - i) * 8 + 31 - j].adcValue = mat.At<ushort>(i, j);
                            f4[(23 - i) * 8 + 31 - j].chartLine?.chartUpdate(heatmapValue[i, j]);
                            break;
                    }
                else if (i < 31 && j < 8)
                {
                    palm[(i - 24) * 8 + j].adcValue = mat.At<ushort>(i, j);
                    palm[(i - 24) * 8 + j].chartLine?.chartUpdate(heatmapValue[i, j]);
                }
    }

    private void click_splitViewPaneBtn(object sender, RoutedEventArgs e)
    {
        sv.IsPaneOpen = true;
    }

    private async void click_startBtn(object sender, RoutedEventArgs e)
    {
        var mode = lastPivotIndex;
        if (viewModel_Switch.isStartIcon)
        {
            switch (lastPivotIndex)
            {
                case 0:
                    if (combobox_com.SelectedItem != null)
                    {
                        try
                        {
                            com = await SerialDevice.FromIdAsync((combobox_com.SelectedItem as DeviceInformation).Id);
                            if (com != null)
                            {
                                com.BaudRate = Convert.ToUInt32(combobox_baud.SelectedValue);
                                com.DataBits = Convert.ToUInt16(combobox_dataBits.SelectedValue);
                                com.StopBits = (SerialStopBitCount)combobox_stopBits.SelectedIndex;
                                com.Parity = (SerialParity)combobox_parity.SelectedIndex;
                                com.ReadTimeout = TimeSpan.FromMilliseconds(100);
                                reader = new(com.InputStream) { ByteOrder = ByteOrder.BigEndian };
                                writer = new(com.OutputStream);

                                info_comOpen = false;
                                info_com.IsOpen = false;
                            }
                        }
                        catch (Exception error)
                        {
                            info_comOpen = true;
                            info_com.IsOpen = true;
                            info_com.Message = error.ToString();
                            return;
                        }
                    }
                    break;

                case 1:
                    if (handleBle == null)
                    {
                        info_ble.Message = "蓝牙未连接";
                        info_ble.IsOpen = true;
                        info_bleOpen = true;
                        return;
                    }
                    //这里转换需要注意，目前将停止位和校验位定死
                    var success = CH9140.CH9140UartSetSerialBaud((nint)handleBle, Convert.ToInt32(combobox_baud.SelectedValue), Convert.ToInt32(combobox_dataBits.SelectedValue), 1, 0);
                    bleReadStream.Size = 0;
                    bleReadStream.Seek(0);
                    reader = new(bleReadStream.GetInputStreamAt(0)) { ByteOrder = ByteOrder.BigEndian };
                    info_ble.IsOpen = false;
                    info_bleOpen = false;
                    break;

                default:
                    return;
            }
            viewModel_Switch.isStartIcon = false;
            HeatMap_pixelHelper.isStart = true;
            //thread_collect.DispatcherQueue.TryEnqueue(async () =>
            _ = Windows.System.Threading.ThreadPool.RunAsync(async (item) =>
            {
                if (mode == 1)
                    System.Threading.Thread.Sleep(165);
                // TODO: 串口读取部分代码更新，merge到其他分支
                while (true)
                {
                    if (viewModel_Switch.isStartIcon)
                    {
                        //thread_collect.ShutdownQueueAsync();
                        if (mode == 0)
                            com.Dispose();
                        reader.Dispose();

                        armControl = 0;
                        writer.Dispose();
                        return;
                    }
                    //Stopwatch stopwatch = new();
                    //stopwatch.Start();
                    while (true)
                    {
                        if (reader.UnconsumedBufferLength < 2)
                            await reader.LoadAsync(row * col * 2 + 2);
                        if (reader.ReadByte() == 0xff)
                            if (reader.ReadByte() == 0xff)
                            {
                                if (reader.UnconsumedBufferLength < row * col * 2)
                                    await reader.LoadAsync(row * col * 2 - reader.UnconsumedBufferLength);
                                break;
                            }
                    }
                    ushort[,] heatmapValue = new ushort[row, col];
                    for (int i = 0; i < row; i++)
                        for (int j = 0; j < col; j++)
                            heatmapValue[i, j] = reader.ReadUInt16();
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (folder != null)
                        {
                            string fileName = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss.fff") + ".csv";
                            using var writer = new StreamWriter(System.IO.Path.Combine(folder.Path, fileName));
                            for (int i = 0; i < row; i++)
                            {
                                for (int j = 0; j < col; j++)
                                    writer.Write(heatmapValue[i, j] + ",");
                                writer.Write('\n');
                            }
                        }
                        heatMapValue2UI(heatmapValue);
                    });
                    // refer to https://learn.microsoft.com/zh-cn/windows-hardware/design/device-experiences/sensors-adaptive-brightness
                    ushort[,] bucketCurve = new ushort[,]
                    {
                            {0, 400},
                            {300, 700},
                            {600, 1000},
                            {900, 1300},
                            {1200, 4096}
                    };
                    ushort[] arm = [heatmapValue[31, 29], heatmapValue[31, 30], heatmapValue[31, 31]];
                    // Debug.WriteLine(arm[0] + " " + arm[1] + " " + arm[2]);
                    var avg = (arm[0] + arm[1] + arm[2] - arm.Min()) / 2;
                    while (true)
                    {
                        if (avg < bucketCurve[armControl, 0])
                        {
                            armControl--;
                            continue;
                        }
                        else if (avg > bucketCurve[armControl, 1])
                        {
                            armControl++;
                            continue;
                        }
                        break;
                    }
                    writer.WriteString($":pulse{(5 - armControl) * 5}");
                    writer.StoreAsync();
                }
            });
        }
        else if (!viewModel_Switch.isStartIcon)
        {
            viewModel_Switch.isStartIcon = true;
            HeatMap_pixelHelper.isStart = false;
            info_com.Title = "串口错误";
            info_com.Severity = InfoBarSeverity.Error;
            info_com.IsOpen = false;
        }
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

    private void selectionChanged_pivot(object sender, SelectionChangedEventArgs e)
    {
        switch (pivot.SelectedIndex)
        {
            case 0:
                lastPivotIndex = 0;
                info_ble.IsOpen = false;
                if (info_comOpen)
                    info_com.IsOpen = true;
                break;
            case 1:
                lastPivotIndex = 1;
                info_com.IsOpen = false;
                if (info_bleOpen)
                    info_ble.IsOpen = true;
                break;
        }
    }

    private void toggled_bleConn(object sender, RoutedEventArgs e)
    {
        if ((sender as ToggleSwitch).IsOn)
            if (combobox_ble.SelectedItem != null)
            {
                ((sender as ToggleSwitch).OnContent as ProgressRing).IsActive = true;
                var id = (combobox_ble.SelectedItem as DeviceInformation).Id;
                _ = Windows.System.Threading.ThreadPool.RunAsync((item) =>
                {
                    bleReadStream = new();
                    handleBle = CH9140.CH9140UartOpenDevice(id, null, null, (p, buf, len) =>
                    {
                        byte[] data = new byte[len];
                        unsafe
                        {
                            //fixed (byte* source = buf)
                            fixed (byte* destin = data)
                                System.Buffer.MemoryCopy((void*)buf, destin, (int)len, (int)len);
                        }
                        bleReadStream.WriteAsync(WindowsRuntimeBufferExtensions.AsBuffer(data, 0, (int)len));
                    });
                    this.DispatcherQueue.TryEnqueue(() => {
                        ((sender as ToggleSwitch).OnContent as ProgressRing).IsActive = false;
                        if (handleBle == null)
                            (sender as ToggleSwitch).IsOn = false;
                    });
                });
            }
            else
                (sender as ToggleSwitch).IsOn = false;
        else if (handleBle != null)
        {
            CH9140.CH9140CloseDevice((nint)handleBle);
            bleReadStream.Dispose();
            handleBle = null;
        }
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
                    heatmapValue[i, j] = Convert.ToUInt16(colString[j]);
            }
            heatMapValue2UI(heatmapValue);
        }
    }

    private void click_heatMapItem(object sender, ItemClickEventArgs e)
    {
        var pixel = (e.ClickedItem as HeatMap_pixel);
        if (pixel.chartLine == null)
        {
            pixel.chartLine = new(pixel);
            //pixel.chartLine.yAxes[0].MaxLimit = Convert.ToInt32(legendRange.Text);
            lineCharts.Add(pixel.chartLine);
            //pixel.chartLine.tokenLegend = legendRange.RegisterPropertyChangedCallback(TextBlock.TextProperty, (s, dp) => {
            //    if (dp == TextBlock.TextProperty)
            //        pixel.chartLine.yAxes[0].MaxLimit = Convert.ToInt32((s as TextBlock).Text);
            //});
        }
    }

    private async void click_CBFSaveIcon(object sender, RoutedEventArgs e)
    {
        var lineChart = (sender as AppBarButton).CommandParameter as CartesianChart;
        FileSavePicker savePicker = new();
        // Retrieve the window handle (HWND) of the current WinUI 3 window.
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        // Initialize the file picker with the window handle (HWND).
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
        // Dropdown of file types the user can save the file as
        savePicker.FileTypeChoices.Add("line chart", [".png", ".txt"]);
        // Open the picker for the user to pick a file
        StorageFile file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
            CachedFileManager.DeferUpdates(file);
            using var stream = await file.OpenStreamForWriteAsync();
            switch (file.FileType)
            {
                case ".png":
                    var skChart = new SKCartesianChart(lineChart);
                    skChart.SaveImage(stream);
                    break;
                case ".txt":
                    using (var sw = new StreamWriter(stream))
                    {
                        var values = (lineChart.Series.First().Values as List<DateTimePoint>);
                        foreach (var item in values)
                            sw.WriteLine(item.Value);
                    }
                    break;
            }
        }
        lineChart.ContextFlyout.Hide();
    }

    private void click_CBFDeleteIcon(object sender, RoutedEventArgs e)
    {
        var chartLine = (sender as AppBarButton).CommandParameter as ViewModel_lineChart;
        lineCharts.Remove(chartLine);
        (chartLine.series[0].Values as List<DateTimePoint>).Clear();
        legendRange.UnregisterPropertyChangedCallback(TextBlock.TextProperty, chartLine.tokenLegend);
        chartLine.parent.chartLine = null;
    }

    private void toggle_removePrePSw(object sender, RoutedEventArgs e)
    {
        if ((sender as ToggleSwitch).IsOn)
            if (!viewModel_Switch.isStartIcon)
                for (int i = 0; i < row; i++)
                    for (int j = 0; j < col; j++)
                    {
                        if (i < 24)
                            switch (j / 8)
                            {
                                case 0:
                                    prePressure[i, j] = f1[(23 - i) * 8 + 7 - j].adcValue;
                                    break;
                                case 1:
                                    prePressure[i, j] = f2[(23 - i) * 8 + 15 - j].adcValue;
                                    break;
                                case 2:
                                    prePressure[i, j] = f3[(23 - i) * 8 + 23 - j].adcValue;
                                    break;
                                case 3:
                                    prePressure[i, j] = f4[(23 - i) * 8 + 31 - j].adcValue;
                                    break;
                            }
                        else if (i < 31 && j < 8)
                            prePressure[i, j] = palm[(i - 24) * 8 + j].adcValue;
                    }
            else
                (sender as ToggleSwitch).IsOn = false;
        else
            Array.Clear(prePressure, 0, row * col);
    }
}

