using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace ComfyUILibs.Base
{
    public partial class ObservablePoint : ObservableObject
    {
        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        public ObservablePoint()
        {
            _x = 0;
            _y = 0;
        }

        public ObservablePoint(double x, double y)
        {
            _x = x;
            _y = y;
        }

        public Point ToPoint()
        {
            return new Point(X, Y);
        }

        public void FromPoint(Point point)
        {
            X = point.X;
            Y = point.Y;
            return;
        }
    }
}
