using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace array_sensor;

public class KeyFrames
{
    // Info：和数据集最大值挂钩，用于归一化，注意及时修改
    private const float _max = 3533;
    public const int framesNum = 9;

    [ColumnName("input")]
    [VectorType(framesNum, 10, 16)]
    public float[] frames = new float[framesNum * 10 * 16];

    public static KeyFrames createFromFrames(ushort[][,] frames)
    {
        KeyFrames tensor = new();
        long[] shape = [frames.GetLength(0), frames[0].GetLength(0), frames[0].GetLength(1)];
        for (int i = 0; i < shape[0]; i++)
            for (int j = 0; j < shape[1]; j++)
                for (int k = 0; k < shape[2]; k++)
                    tensor.frames[i * shape[1] * shape[2] + j * shape[2] + k] = frames[i][j, k] / _max;
        return tensor;
    }
}

public class Label
{
    private const int _classNum = 7;
    private readonly string[] _labels =
    [
        "cube-gum",
        "cube-sponge",
        "cylinder-gum",
        "cylinder-sponge",
        "empty",
        "steel-ball",
        "tennis-ball"
    ];

    [ColumnName("output")]
    [VectorType(_classNum)]
    public float[] scores;

    private static float Sigmoid(float value)
    {
        var k = (float)Math.Exp(value);
        return k / (1.0f + k);
    }
    private static float[] Softmax(float[] values)
    {
        var maxVal = values.Max();
        var exp = values.Select(v => Math.Exp(v - maxVal));
        var sumExp = exp.Sum();
        return exp.Select(v => (float)(v / sumExp)).ToArray();
    }

    public (string, float) getResult() 
    {
        var percent = Softmax(scores);
        float max = 0;
        int index = 0;
        for (int i = 0; i < _classNum; i++)
            if (max < percent[i])
            {
                max = percent[i];
                index = i;
            }
        return (_labels[index], max * 100);
    }
}

public class PressureFrames
{
    [ColumnName("clusterInput")]
    [VectorType(10, 16)]
    public float[] frames = new float[10 * 16];

    public static List<PressureFrames> createFromFrames(Queue<ushort[,]> frames)
    {
        List<PressureFrames> tensors = [];
        foreach (var frame in frames)
        {
            PressureFrames tensor = new();
            for (int i = 0; i < frame.GetLength(0); i++)
                for (int j = 0; j < frame.GetLength(1); j++)
                    tensor.frames[i * frame.GetLength(1) + j] = frame[i, j];
            tensors.Add(tensor);
        }
        return tensors;
    }
}
public class ClusterResult
{
    [ColumnName("PredictedLabel")]
    public uint PredictedClusterId;

    [ColumnName("Score")]
    public float[] Distances;
}

//using Microsoft.ML;
internal class GloveNet : IDisposable
{
    public string modelLocation = "Assets\\GloveNet.onnx";

    private readonly MLContext mlContext = new();
    public PredictionEngine<KeyFrames, Label> predictor;

    public static string GetAbsolutePath(string relativePath)
    {
        FileInfo _dataRoot = new(typeof(Program).Assembly.Location);
        string assemblyFolderPath = _dataRoot.Directory.FullName;
        string fullPath = Path.Combine(assemblyFolderPath, relativePath);
        return fullPath;
    }

    public void loadModel(string modelLocation)
    {
        var data = mlContext.Data.LoadFromEnumerable(new List<KeyFrames>());
        // 相对路径不行？
        var pipeline = mlContext.Transforms.ApplyOnnxModel(GetAbsolutePath(modelLocation));
        var model = pipeline.Fit(data);
        // 创建一个 PredictionEngine 对象，用于对单个数据实例进行预测
        predictor = mlContext.Model.CreatePredictionEngine<KeyFrames, Label>(model);
    }

    public Label predict(Queue<ushort[,]> frames)
    {
        var data1 = mlContext.Data.LoadFromEnumerable(PressureFrames.createFromFrames(frames));
        var pipeline = //mlContext.Transforms.ProjectToPrincipalComponents("Features", "clusterInput", rank: 10)
            mlContext.Transforms.NormalizeMinMax("Features", "clusterInput", fixZero: false)
            .Append(mlContext.Clustering.Trainers.KMeans(numberOfClusters: KeyFrames.framesNum));
        var model = pipeline.Fit(data1);
        var transformedData = model.Transform(data1);
        var results = mlContext.Data.CreateEnumerable<ClusterResult>(transformedData, reuseRowObject: false);

        ushort[][,] keyFrames = new ushort[KeyFrames.framesNum][,];
        bool[] isHaveCluster = new bool[KeyFrames.framesNum];
        int len = 0;
        for (int i = 0; i < results.Count(); i++)
            if (!isHaveCluster[results.ElementAt(i).PredictedClusterId - 1])
            {
                //keyFrames[len] = new ushort[10, 16];
                keyFrames[len++] = frames.ElementAt(i);
                isHaveCluster[results.ElementAt(i).PredictedClusterId - 1] = true;
            }
        var data2 = KeyFrames.createFromFrames(keyFrames);
        var output = predictor.Predict(data2);
        return output;
    }

    public void Dispose()
    {
        predictor.Dispose();
    }
}
