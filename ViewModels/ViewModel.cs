using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace array_sensor;

//转换器，报错
//internal class Icon_switch_converter : IValueConverter
//{
//    public object Convert(object value, Type targetType, object parameter, string language)
//    {
//        if ((string)value == "\uE768")
//            return true;
//        else
//            return false;
//    }

//    public object ConvertBack(object value, Type targetType, object parameter, string language)
//    {
//        if ((bool)value)
//            return "\uE768";
//        else
//            return "\uE769";
//    }
//}

internal class ViewModel_switch : INotifyPropertyChanged
{
    private bool _isStartIcon;
    public event PropertyChangedEventHandler PropertyChanged = delegate { };
    public ViewModel_switch()
    {
        isStartIcon = true;
    }
    public bool isStartIcon
    {
        get
        {
            return _isStartIcon;
        }
        set
        {
            _isStartIcon = value; OnPropertyChanged(); /*OnPropertyChanged("boolToGlyph");*/
        }
    }

    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        // Raise the PropertyChanged event, passing the name of the property whose value has changed.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static string boolToGlyph(bool isStart)
    {
        if (isStart)
            return "\uE768";
        else
            return "\uE769";
    }
}

internal class HeatMap_pixel(Visibility visibility) : INotifyPropertyChanged
{
    public Visibility visibility = visibility;
    public ViewModel_lineChart chartLine = null;
    public event PropertyChangedEventHandler PropertyChanged = delegate { };
    private ushort _adcValue = 0;
    public ushort adcValue
    {
        set
        {
            _adcValue = value;
            OnPropertyChanged();
        }
        get
        {
            return _adcValue;
        }
    }

    public void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        // Raise the PropertyChanged event, passing the name of the property whose value has changed.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal static class HeatMap_pixelHelper
{
    public static readonly Color[] linearGradientColors =
    [
        Colors.DarkBlue,
            Colors.Blue,
            Colors.Cyan,
            Colors.Yellow,
            Colors.Red,
            Colors.DarkRed,
        ];
    public static ushort range = 4095;
    public static bool isStart = false;

    public static Color GetColor(ushort adcValue)
    {
        float offset = (float)adcValue / (float)range;
        if (!isStart)
            return Colors.White;
        if (offset > 1)
            offset = 1;
        float region = offset * (linearGradientColors.Length - 1);
        if (region == (int)region)
            return linearGradientColors[(int)region];
        else
        {
            byte r = (byte)(linearGradientColors[(int)region].R * (1 - region + (int)region) + linearGradientColors[(int)region + 1].R * (region - (int)region));
            byte g = (byte)(linearGradientColors[(int)region].G * (1 - region + (int)region) + linearGradientColors[(int)region + 1].G * (region - (int)region));
            byte b = (byte)(linearGradientColors[(int)region].B * (1 - region + (int)region) + linearGradientColors[(int)region + 1].B * (region - (int)region));
            return Color.FromArgb(0xff, r, g, b);
        }
    }
}

internal class ViewModel_lineChart(HeatMap_pixel parent)
{
    readonly double[] coffeB = [0.00157882781368838, 0, -0.00789413906844189, 0, 0.0157882781368838, 0, -0.0157882781368838, 0, 0.00789413906844189, 0, -0.00157882781368838];
    readonly double[] coffeA = [1, -7.58607012789104, 26.1581569876096, -54.0882910339851, 74.3720632958967, -71.1151485731343, 47.9081399804196, -22.4531763183455, 7.00594318511866, -1.31411670605260, 0.112500021665274];
    public HeatMap_pixel parent = parent;
    private readonly List<DateTimePoint> _value = [];

    public bool isFilter = false;

    public ISeries[] series = [
        new LineSeries<DateTimePoint>
            {
                //Values = _value,
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0
            }];
    public ICartesianAxis[] xAxes = [
        new DateTimeAxis(TimeSpan.FromMilliseconds(200), Formatter)
            {
                CustomSeparators = GetSeparators(),
                AnimationsSpeed = TimeSpan.FromMilliseconds(30),
                //SeparatorsPaint = new SolidColorPaint(SKColors.Black.WithAlpha(100))
            }];
    public ICartesianAxis[] yAxes = [
        new Axis
            {
                //Name = "adcRaw",
                //MinLimit = 0,
                //MaxLimit = 3000,
            }];
    public long tokenLegend;

    public void chartUpdate(ushort value)
    {
        _value.Add(new DateTimePoint(DateTime.Now, value));
        if (_value.Count > 150)
            _value.RemoveAt(0);
        if (isFilter)
        {
            List<DateTimePoint> copy = [];
            foreach (var item in _value)
                copy.Add(new DateTimePoint(item.DateTime, item.Value));
            var data = copy.Select(x => x.Value.GetValueOrDefault());
            data = FiltfiltSharp.FiltfiltHelper.DoFiltfilt([.. coffeB], [.. coffeA], data.ToList());
            for (int i = 0; i < copy.Count; i++)
                copy[i].Value = data.ElementAt(i);
            series[0].Values = copy;
        }
        else
            series[0].Values = _value;
        xAxes[0].CustomSeparators = GetSeparators();
        //yAxes[0].MinLimit = (series[0].Values as List<DateTimePoint>).Min(x => x.Value).Value - 20;
        //yAxes[0].MaxLimit = yAxes[0].MinLimit + 100;
    }

    private static double[] GetSeparators()
    {
        var now = DateTime.Now;

        return
        [
            now.AddMilliseconds(-4000).Ticks,
                now.AddMilliseconds(-2000).Ticks,
                now.Ticks
        ];
    }

    private static string Formatter(DateTime date)
    {
        var msAgo = (DateTime.Now - date).TotalMilliseconds;

        return msAgo < 200
            ? "now"
            : $"{msAgo:N0}ms ago";
    }
}
