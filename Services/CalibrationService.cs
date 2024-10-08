using array_sensor.Contracts.Services;
using array_sensor.Views;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Onnx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace array_sensor.Services
{
    internal class FuncFitCalibrationService : ICalibrationService
    {
        private StorageFile _calibrationFile;
        private Func<double[], double> func;
        private readonly double[,][] para = new double[MainPage.row,MainPage.col][];

        public StorageFile calibrationFile { 
            get => _calibrationFile; 
            set => _calibrationFile = value;
        }

        public static async Task<FuncFitCalibrationService> CreateAsync(StorageFile calibrationFile, Func<double[], double> func)
        {
            var ret = new FuncFitCalibrationService
            {
                calibrationFile = calibrationFile,
                func = func
            };
            using var stream = await calibrationFile.OpenReadAsync();
            using var fr = new StreamReader(stream.AsStreamForRead());
            string[] colString;
            for (int i = 0; i < ret.para.GetLength(0); i++)
                for (int j = 0; j < ret.para.GetLength(1); j++)
                {
                    colString = fr.ReadLine().Split(',');
                    ret.para[i, j] = new double[colString.Length];
                    for (int k = 0; k < colString.Length - 1; k++)
                        ret.para[i, j][k] = Convert.ToDouble(colString[k]);
                }
            return ret;
        }

        public ushort[,] calibrate(ushort[,] raw)
        {
            for (int i = 0; i < raw.GetLength(0); i++)
                for (int j = 0; j < raw.GetLength(1); j++)
                {
                    para[i, j][^1] = raw[i, j];
                    double y = func(para[i, j]);
                    raw[i, j] = (ushort)(y < 0 ? 0 : y);
                }
            return raw;
        }

        public void Dispose()
        {
            
        }
    }

    internal class MLPCalibrationService : ICalibrationService
    {
        private class Feature
        {
            private const float mean = 716.7011f;
            private const float std = 438.8998f;

            [ColumnName("input")]
            [VectorType(3)]
            public float[] feature = new float[3];

            public static List<Feature> createFromArray(ushort[,] raw)
            {
                List<Feature> ret = [];
                for (int i = 0; i < raw.GetLength(0); i++)
                    for (int j = 0; j < raw.GetLength(1); j++)
                        ret.Add(new Feature
                        {
                            feature = [(raw[i, j] - mean) / std, i, j]
                        });
                return ret;
            }
        }

        private class Label
        {
            [ColumnName("output")]
            [VectorType(1)]
            public float[] label;

            public static ushort[,] antilog(List<Label> labels)
            {
                ushort[,] ret = new ushort[MainPage.row, MainPage.col];
                for (int i = 0; i < MainPage.row; i++)
                    for (int j = 0; j < MainPage.col; j++)
                        ret[i, j] = (ushort)(Math.Pow(10, labels[i * MainPage.col + j].label[0]) - 1);
                return ret;
            }
        }

        private StorageFile _calibrationFile;
        private MLContext mlContext;
        private OnnxTransformer model;

        public StorageFile calibrationFile
        {
            get => _calibrationFile;
            set => _calibrationFile = value;
        }

        public static MLPCalibrationService Create(StorageFile calibrationFile)
        {
            var ret = new MLPCalibrationService
            {
                calibrationFile = calibrationFile,
                mlContext = new MLContext()
            };
            var data = ret.mlContext.Data.LoadFromEnumerable(new List<Feature>());
            var pipeline = ret.mlContext.Transforms.ApplyOnnxModel(calibrationFile.Path);
            ret.model = pipeline.Fit(data);
            return ret;
        }

        public ushort[,] calibrate(ushort[,] raw)
        {
            var x = Feature.createFromArray(raw);
            var predictions = model.Transform(mlContext.Data.LoadFromEnumerable(x));
            var y = mlContext.Data.CreateEnumerable<Label>(predictions, reuseRowObject: false).ToList();
            return Label.antilog(y);
        }

        public void Dispose()
        {
            model.Dispose();
        }
    }
}
