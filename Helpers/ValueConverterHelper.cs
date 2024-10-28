using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace array_sensor.Helpers
{
    internal class HeatmapSliderValueConverterHelper : IValueConverter
    {
        public List<string> dataCsv = [];
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return System.IO.Path.GetFileName(dataCsv[System.Convert.ToInt32(value)]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
