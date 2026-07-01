using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace ComfyUILibs.Base
{
    public partial class ObservableSize : ObservableObject
    {
        [ObservableProperty]
        private double _width;

        [ObservableProperty]
        private double _height;

        public ObservableSize()
        {
            _width = 0;
            _height = 0;
        }

        public ObservableSize(double width, double height)
        {
            _width = width;
            _height = height;
        }

        public Size ToSize()
        {
            return new Size(Width, Height);
        }

        public void FromSize(Size size)
        {
            Width = size.Width;
            Height = size.Height;
        }
    }
}
