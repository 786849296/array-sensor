using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace array_sensor.Contracts.Services
{
    internal interface ICalibrationService : IDisposable
    {
        StorageFile calibrationFile { get; set; }
        ushort[,] calibrate(ushort[,] raw);
    }
}
