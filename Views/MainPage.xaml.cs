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
using Microsoft.UI.Dispatching;
using array_sensor.Helpers;
using array_sensor.Contracts.Services;
using array_sensor.Services;
using OpenCvSharp;
using array_sensor.Core.Services;
using SharpDX;
using System.Diagnostics;

namespace array_sensor.Views;

public sealed partial class MainPage : Page
{
    public SerialDevice com;
    public string comID;
    public DataReader readerCom;
    public DispatcherQueueController thread_serialCollect = DispatcherQueueController.CreateOnDedicatedThread();
    public StorageFolder? folder;

    public ObservableCollection<DeviceInformation> comInfos = [];
    internal ViewModel_switch viewModel_Switch = new();
    internal ObservableCollection<HeatMap_pixel> heatmap = [];
    private ICalibrationService? calibration;
    internal HeatmapSliderValueConverterHelper sliderConverter = new();

    public MainViewModel ViewModel
    {
        get;
    }
    public Surf3dVM surf3dVM
    {
        get;
    }

    public MainPage()
    {
        var window = App.MainWindow;
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(AppTitleBar);

        ViewModel = App.GetService<MainViewModel>();
        surf3dVM = App.GetService<Surf3dVM>();
        InitializeComponent();

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

    private void heatMapValue2UI(ushort[,] heatmapValue)
    {
        if (calibration != null)
            heatmapValue = calibration.calibrate(heatmapValue);
        var size = Convert.ToInt32(combobox_gaussKernelSize.SelectedValue);
        Mat mat = Mat.FromPixelData((int)HeatMap_pixelHelper.row, (int)HeatMap_pixelHelper.col, MatType.CV_16U, heatmapValue);
        Cv2.GaussianBlur(mat, mat, new OpenCvSharp.Size(size, size), 0);
        if (grid_heatmap.Visibility == Visibility.Visible)
            for (int i = 0; i < HeatMap_pixelHelper.row; i++)
                for (int j = 0; j < HeatMap_pixelHelper.col; j++)
                    heatmap[(int)(i * HeatMap_pixelHelper.col + j)].adcValue = mat.At<ushort>(i, j);
        else
        {
            Vector3[,] points = new Vector3[HeatMap_pixelHelper.row, HeatMap_pixelHelper.col];
            for (int i = 0; i < HeatMap_pixelHelper.row; i++)
                for (int j = 0; j < HeatMap_pixelHelper.col; j++)
                    points[i, j] = new Vector3(i, j, mat.At<ushort>(i, j) / Surf3dVM.zZoom);
            surf3dVM.updateSurf(points);
            Debug.WriteLine(heatmap3D.FrameRate);
        }
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
            com.BaudRate = Convert.ToUInt32(combobox_baud.SelectedValue);
            com.DataBits = Convert.ToUInt16(combobox_dataBits.SelectedValue);
            com.StopBits = (SerialStopBitCount)combobox_stopBits.SelectedIndex;
            com.Parity = (SerialParity)combobox_parity.SelectedIndex;
            com.ReadTimeout = TimeSpan.FromMilliseconds(400);

            info_error.IsOpen = false;
            viewModel_Switch.isStartIcon = false;
            slider_heatmap.Visibility = Visibility.Collapsed;
            slider_heatmap.IsEnabled = false;
            cb_col.IsEnabled = false;
            cb_row.IsEnabled = false;

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
                            await readerCom.LoadAsync(HeatMap_pixelHelper.row * HeatMap_pixelHelper.col * 2 + 2);
                        if (readerCom.ReadByte() == 0xff)
                            if (readerCom.ReadByte() == 0xff)
                            {
                                if (readerCom.UnconsumedBufferLength < HeatMap_pixelHelper.row * HeatMap_pixelHelper.col * 2)
                                    await readerCom.LoadAsync(HeatMap_pixelHelper.row * HeatMap_pixelHelper.col * 2 - readerCom.UnconsumedBufferLength);
                                break;
                            }
                    }
                    ushort[,] heatmapValue = new ushort[HeatMap_pixelHelper.row, HeatMap_pixelHelper.col];
                    for (int i = 0; i < HeatMap_pixelHelper.row; i++)
                        for (int j = 0; j < HeatMap_pixelHelper.col; j++)
                            heatmapValue[i, j] = readerCom.ReadUInt16();
                    if (folder != null)
                    {
                        string fileName = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss.fff") + ".csv";
                        using (var writer = new StreamWriter(System.IO.Path.Combine(folder.Path, fileName)))
                            for (int i = 0; i < HeatMap_pixelHelper.row; i++)
                            {
                                for (int j = 0; j < HeatMap_pixelHelper.col; j++)
                                    writer.Write(heatmapValue[i, j] + ",");
                                writer.Write('\n');
                            }
                    }
                    this.DispatcherQueue.TryEnqueue(() => heatMapValue2UI(heatmapValue));
                }
            });
        }
        else if (!viewModel_Switch.isStartIcon)
        {
            viewModel_Switch.isStartIcon = true;
            slider_heatmap.IsEnabled = true;
            cb_col.IsEnabled = true;
            cb_row.IsEnabled = true;
        }
    }

    private async void toggle_imageCollectSw(object sender, RoutedEventArgs e)
    {
        ToggleSwitch imageCollectSw = sender as ToggleSwitch;
        if (imageCollectSw.IsOn)
        {
            var picker = App.GetService<FolderPickerService>();
            picker.hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            folder = await picker.openPickerAsync<StorageFolder>(["*"]);
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
        var picker = App.GetService<FolderPickerService>();
        picker.hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        var folder = await picker.openPickerAsync<StorageFolder>([]);
        if (folder != null)
        {
            var files = Directory.EnumerateFiles(folder.Path, "*.csv");
            sliderConverter.dataCsv.Clear();
            foreach (var file in files)
                sliderConverter.dataCsv.Add(file);
            slider_heatmap.Maximum = files.Count() - 1;
            slider_heatmap.Value = 0;
            if (files.Count() > 1)
                slider_heatmap.Visibility = Visibility;
            using (var reader = new StreamReader(sliderConverter.dataCsv[0]))
            {
                string[] colString;
                ushort[,] heatmapValue = new ushort[HeatMap_pixelHelper.row, HeatMap_pixelHelper.col];
                for (int i = 0; i < HeatMap_pixelHelper.row; i++)
                {
                    colString = reader.ReadLine().Split(',');
                    for (int j = 0; j < HeatMap_pixelHelper.col; j++)
                        heatmapValue[i, j] = Convert.ToUInt16(colString[j]);
                }
                heatMapValue2UI(heatmapValue);
            }
            info_error.IsOpen = false;
        }
    }

    private async void toggle_calibrationSw(object sender, RoutedEventArgs e)
    {
        ToggleSwitch sw = sender as ToggleSwitch;
        if (sw.IsOn)
        {
            var picker = App.GetService<FilePickerService>();
            picker.hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var file = await picker.openPickerAsync<StorageFile>([".csv", ".onnx"]);
            if (file != null)
            {
                // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                calibration = file.FileType switch
                {
                    ".csv" => await FuncFitCalibrationService.CreateAsync(file, (para) => para[0] * Math.Pow(para[4] + para[1], 2) + para[2] * Math.Pow(para[4], 0.5) + para[3]),
                    ".onnx" => MLPCalibrationService.Create(file),
                    _ => throw new NotImplementedException("不支持的格式"),
                };
                sw.OnContent = file.Path;
            }
            else
                sw.IsOn = false;
        }
        else
        {
            calibration?.Dispose();
            calibration = null;
        }
    }

    private void ts_3dSurf_Toggled(object sender, RoutedEventArgs e)
    {
        grid_heatmap.Visibility = ts_3dSurf.IsOn ? Visibility.Collapsed : Visibility.Visible;
        heatmap3D.Visibility = ts_3dSurf.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void slider_heatmap_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        int index = (int)(sender as Slider).Value;
        using (var reader = new StreamReader(sliderConverter.dataCsv[index]))
        {
            string[] colString;
            ushort[,] heatmapValue = new ushort[HeatMap_pixelHelper.row, HeatMap_pixelHelper.col];
            for (int i = 0; i < HeatMap_pixelHelper.row; i++)
            {
                colString = reader.ReadLine().Split(',');
                for (int j = 0; j < HeatMap_pixelHelper.col; j++)
                    heatmapValue[i, j] = Convert.ToUInt16(colString[j]);
            }
            heatMapValue2UI(heatmapValue);
        }
    }

    private void rawColCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cb_row == null || cb_col == null)
            return;
        HeatMap_pixelHelper.row = Convert.ToUInt32(cb_row.SelectedValue);
        HeatMap_pixelHelper.col = Convert.ToUInt32(cb_col.SelectedValue);
        if (heatmap.Count != HeatMap_pixelHelper.row * HeatMap_pixelHelper.col)
        {
            heatmap.Clear();
            for (int i = 0; i < HeatMap_pixelHelper.row; i++)
                for (int j = 0; j < HeatMap_pixelHelper.col; j++)
                    heatmap.Add(new HeatMap_pixel(i, j));
        }
        if (grid_heatmap == null)
            return;
        var gridHeight = (grid_heatmap.Parent as Grid).RowDefinitions[0].ActualHeight;
        var gridWidth = (grid_heatmap.Parent as Grid).ColumnDefinitions[1].ActualWidth;
        var size = Math.Min(gridHeight / (HeatMap_pixelHelper.row + 1), gridWidth / (HeatMap_pixelHelper.col + 1));
        var style = new Style(typeof(GridViewItem));
        style.Setters.Add(new Setter(GridViewItem.MaxHeightProperty, size));
        style.Setters.Add(new Setter(GridViewItem.MaxWidthProperty, size));
        style.Setters.Add(new Setter(GridViewItem.MinHeightProperty, size));
        style.Setters.Add(new Setter(GridViewItem.MinWidthProperty, size));
        style.Setters.Add(new Setter(GridViewItem.MarginProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(GridViewItem.PaddingProperty, new Thickness(0)));
        grid_heatmap.ItemContainerStyle = style;
        (grid_heatmap.ItemsPanelRoot as ItemsWrapGrid).MaximumRowsOrColumns  = (int)HeatMap_pixelHelper.col;
    }
}
