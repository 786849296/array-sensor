using CommunityToolkit.Mvvm.ComponentModel;
using HelixToolkit.SharpDX.Core;
using HelixToolkit.WinUI;
using Microsoft.UI.Xaml;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace array_sensor.ViewModels
{
    public partial class Surf3dVM : ObservableObject
    {
        public const float zZoom = 100f;

        public Camera camera = new PerspectiveCamera
        {
            Position = new(50, 50, 40),
            LookDirection = new(-50, -50, -40),
            UpDirection = new(0, 0, 1),
        };
        public Vector3 lightDirection = new(0, 0, -50);
        
        [ObservableProperty]
        public Vector3 center = default;
        [ObservableProperty]
        public MeshGeometry3D mesh;
        [ObservableProperty]
        public bool isAxisShow = true;
        [ObservableProperty]
        public LineGeometry3D customAxis;

        public Surf3dVM()
        {
            UpdateAxis(new BoundingBox(new Vector3(0, 0, 0), new Vector3(32, 32, 41)));
        }

        public void updateSurf(Vector3[,] points)
        {
            MeshBuilder builder = new();
            builder.AddRectangularMesh(points);
            var surfWithoutColor = builder.ToMeshGeometry3D();
            
            Color4Collection colors = [];
            for (int i = 0; i < points.GetLength(0); i++)
                for (int j = 0; j < points.GetLength(1); j++)
                    colors.Add(HeatMap_pixelHelper.GetColor((ushort)(points[i, j].Z * zZoom)).ToColor4());
            surfWithoutColor.Colors = colors;

            Center = surfWithoutColor.BoundingSphere.Center;
            Mesh = surfWithoutColor;
        }

        private void UpdateAxis(BoundingBox box)
        {
            const float scale = 2;
            const float width = 1f;
            var builder = new LineBuilder();
            builder.AddLine(new Vector3(-1, -1, -1), new Vector3(box.Maximum.X + 1, -1, -1));
            for (float i = 0; i <= 32; i += scale)
                builder.AddLine(new Vector3(i, -1, -1), new Vector3(i, width - 1, -1));
            builder.AddLine(new Vector3(-1, -1, -1), new Vector3(-1, box.Maximum.Y + 1, -1));
            for (float i = 0; i <= 32; i += scale)
                builder.AddLine(new Vector3(-1, i, -1), new Vector3(width - 1, i, -1));
            builder.AddLine(new Vector3(-1, -1, -1), new Vector3(-1, -1, box.Maximum.Z + 1));
            for (float i = 0; i <= box.Maximum.Z; i += scale)
                builder.AddLine(new Vector3(-1, -1, i), new Vector3(width - 1, -1, i));
            var temp = builder.ToLineGeometry3D();
            temp.Colors = [];
            temp.Colors.Resize(temp.Positions.Count, true);
            for (int i = 0; i < 36; i++)
                temp.Colors[i] = Color.Red;
            for (int i = 36; i < 72; i++)
                temp.Colors[i] = Color.Green;
            for (int i = 72; i < temp.Positions.Count; i++)
                temp.Colors[i] = Color.Blue;
            CustomAxis = temp;
        }
    }
}
