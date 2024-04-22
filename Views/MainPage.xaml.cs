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
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Globalization;

namespace array_sensor.Views;

public sealed partial class MainPage : Page
{
    public const ushort row = 16;
    public const ushort col = 20;
    public ushort[,] heatmapValue = new ushort[row, col];

    public SerialDevice com;
    public string comID;
    public DataReader readerCom;
    public DataWriter writerCom;
    public DispatcherQueueController thread_serialCollect = DispatcherQueueController.CreateOnDedicatedThread();
    public StorageFolder folder;

    public TimeSliderValueConverter sliderConverter = new();
    public ObservableCollection<DeviceInformation> comInfos = [];
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
                heatmap.Add(new HeatMap_pixel(i, j));

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

    private void heatMapValue2UI(StreamReader reader)
    {
        string[] colString;
        ushort[,] heatmapValue = new ushort[row, col];
        for (int i = 0; i < row; i++)
        {
            colString = reader.ReadLine().Split(',');
            for (int j = 0; j < col; j++)
            {
                heatmapValue[i, j] = Convert.ToUInt16(colString[j]);
                heatmap[i * col + j].adcValue = heatmapValue[i, j];
            }
        }
    }

    private void click_splitViewPaneBtn(object sender, RoutedEventArgs e)
    {
        sv.IsPaneOpen = true;
    }

    private async void click_sendBtn(object sender, RoutedEventArgs e)
    {
        var sendButton = sender as ToggleButton;
        if ((bool)sendButton.IsChecked && combobox_com.SelectedItem != null)
        {
            if (!(numberBox_secends.Value > 0))
            {
                info_error.IsOpen = true;
                info_error.Severity = InfoBarSeverity.Error;
                info_error.Message = "请输入有效数据";
                sendButton.IsChecked = false;
                return;
            }

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
            if (folder == null)
            {
                info_error.IsOpen = true;
                info_error.Severity = InfoBarSeverity.Error;
                info_error.Message = "请选择导出文件夹";
                sendButton.IsChecked = false;
                return;
            }
            StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);

            try
            {
                com = await SerialDevice.FromIdAsync((combobox_com.SelectedItem as DeviceInformation).Id);
                comID = (combobox_com.SelectedItem as DeviceInformation).Id;

            }
            catch (Exception error)
            {
                info_error.IsOpen = true;
                info_error.Severity = InfoBarSeverity.Error;
                info_error.Message = error.ToString();
                sendButton.IsChecked = false;
            }
            if (com != null)
            {
                readerCom = new(com.InputStream)
                {
                    ByteOrder = ByteOrder.BigEndian,
                };
                writerCom = new(com.OutputStream)
                {
                    ByteOrder = ByteOrder.BigEndian,
                };
                com.BaudRate = Convert.ToUInt32(combobox_baud.SelectedValue);
                com.DataBits = Convert.ToUInt16(combobox_dataBits.SelectedValue);
                com.StopBits = (SerialStopBitCount)combobox_stopBits.SelectedIndex;
                com.Parity = (SerialParity)combobox_parity.SelectedIndex;
                com.ReadTimeout = TimeSpan.FromMilliseconds(105);

                info_error.IsOpen = false;
                ts_readData.IsOn = false;

                short secends = (short)numberBox_secends.Value;
                progressBar_collect.Visibility = Visibility.Visible;

                sliderConverter.dataCsv.Clear();
                slider_time.IsEnabled = false;
                DateTime time = new();
                thread_serialCollect.DispatcherQueue.TryEnqueue(async () =>
                {
                    writerCom.WriteString(secends.ToString() + '\n');
                    writerCom.StoreAsync();

                    for (int s = 0; s < secends; s++)
                    {
                        while (true)
                        {
                            while (readerCom.UnconsumedBufferLength < 2)
                                await readerCom.LoadAsync(row * col * 2 + 32);
                            if (readerCom.ReadByte() == 0xff)
                                if (readerCom.ReadByte() == 0xff)
                                {
                                    if (readerCom.UnconsumedBufferLength < row * col * 2 + 30)
                                        await readerCom.LoadAsync(row * col * 2 + 30 - readerCom.UnconsumedBufferLength);
                                    break;
                                }
                        }

                        if (s == 0)
                        {
                            string timeBuf = readerCom.ReadString(22);
                            readerCom.ReadBytes(new byte[8]);
                            time = DateTime.ParseExact(timeBuf, "yyyy-MM-dd'(1)' HH.mm.ss", CultureInfo.InvariantCulture);
                        }
                        else
                            readerCom.ReadBytes(new byte[30]);

                        for (int i = 0; i < row; i++)
                            for (int j = 0; j < col; j++)
                                heatmapValue[i, j] = readerCom.ReadUInt16();
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            string fileName = time.ToString("yyyy-MM-dd HH.mm.ss") + ".csv";
                            time = time.AddSeconds(-1);
                            sliderConverter.dataCsv.Add(fileName);
                            using (var writer = new StreamWriter(System.IO.Path.Combine(folder.Path, fileName)))
                                for (int i = 0; i < row; i++)
                                {
                                    for (int j = 0; j < col; j++)
                                        writer.Write(heatmapValue[i, j] + ",");
                                    writer.Write('\n');
                                }
                        });
                    }
                    com.Dispose();
                    readerCom.Dispose();
                    writerCom.Dispose();
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        progressBar_collect.Visibility = Visibility.Collapsed;
                        slider_time.Maximum = secends - 1;
                        slider_time.Value = 0;
                        slider_time.IsEnabled = true;
                        sliderConverter.dataCsv.Reverse();
                        using (var reader = new StreamReader(System.IO.Path.Combine(folder.Path, sliderConverter.dataCsv[0])))
                            heatMapValue2UI(reader);
                    });
                });
            }
        }
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
            using (var reader = new StreamReader(stream.AsStreamForRead()))
                heatMapValue2UI(reader);
        }
    }

    private void slider_time_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int index = (int)(sender as Slider).Value;
        using (var reader = new StreamReader(System.IO.Path.Combine(folder.Path, sliderConverter.dataCsv[index])))
            heatMapValue2UI(reader);
    }

    private async void ts_readData_Toggled(object sender, RoutedEventArgs e)
    {
        ToggleSwitch readDataTS = sender as ToggleSwitch;
        if (readDataTS.IsOn)
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
                readDataTS.OnContent = folder.Path;
                var files = Directory.EnumerateFiles(folder.Path, "*.csv");
                sliderConverter.dataCsv.Clear();
                foreach (var file in files)
                    sliderConverter.dataCsv.Add(Path.GetFileName(file));
                slider_time.Maximum = files.Count() - 1;
                slider_time.Value = 0;
                slider_time.IsEnabled = true;
                using (var reader = new StreamReader(System.IO.Path.Combine(folder.Path, sliderConverter.dataCsv[0])))
                    heatMapValue2UI(reader);
            }
            else
                readDataTS.IsOn = false;
        }
    }
}
