using System.ComponentModel;
using System.Windows;
using ComfyUILibs.Base;

namespace ComfyUILibsTests.Base
{
    public class ObservableSizeTests
    {
        // ── コンストラクター ───────────────────────────────────────────────────

        [Fact]
        public void Constructor_Default_InitializesWidthAndHeightToZero()
        {
            var obs = new ObservableSize();

            Assert.Equal(0, obs.Width);
            Assert.Equal(0, obs.Height);
        }

        [Fact]
        public void Constructor_WithValues_SetsWidthAndHeight()
        {
            var obs = new ObservableSize(100.5, 200.5);

            Assert.Equal(100.5, obs.Width);
            Assert.Equal(200.5, obs.Height);
        }

        // ── PropertyChanged ───────────────────────────────────────────────────

        [Fact]
        public void Width_Set_RaisesPropertyChangedEvent()
        {
            var obs = new ObservableSize();
            var changedProps = new List<string?>();
            ((INotifyPropertyChanged)obs).PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            obs.Width = 50;

            Assert.Contains("Width", changedProps);
        }

        [Fact]
        public void Height_Set_RaisesPropertyChangedEvent()
        {
            var obs = new ObservableSize();
            var changedProps = new List<string?>();
            ((INotifyPropertyChanged)obs).PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            obs.Height = 75;

            Assert.Contains("Height", changedProps);
        }

        // ── ToSize ────────────────────────────────────────────────────────────

        [Fact]
        public void ToSize_ReturnsCorrectSize()
        {
            var obs = new ObservableSize(320, 240);

            var result = obs.ToSize();

            Assert.Equal(new Size(320, 240), result);
        }

        [Fact]
        public void ToSize_DefaultConstructor_ReturnsZeroSize()
        {
            var obs = new ObservableSize();

            var result = obs.ToSize();

            Assert.Equal(new Size(0, 0), result);
        }

        // ── FromSize ──────────────────────────────────────────────────────────

        [Fact]
        public void FromSize_SetsWidthAndHeight()
        {
            var obs = new ObservableSize();

            obs.FromSize(new Size(1920, 1080));

            Assert.Equal(1920, obs.Width);
            Assert.Equal(1080, obs.Height);
        }

        [Fact]
        public void FromSize_OverwritesExistingValues()
        {
            var obs = new ObservableSize(10, 20);

            obs.FromSize(new Size(800, 600));

            Assert.Equal(800, obs.Width);
            Assert.Equal(600, obs.Height);
        }

        [Fact]
        public void FromSize_RaisesPropertyChangedForWidthAndHeight()
        {
            var obs = new ObservableSize();
            var changedProps = new List<string?>();
            ((INotifyPropertyChanged)obs).PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            obs.FromSize(new Size(100, 200));

            Assert.Contains("Width", changedProps);
            Assert.Contains("Height", changedProps);
        }

        // ── ラウンドトリップ ───────────────────────────────────────────────────

        [Fact]
        public void FromSize_ThenToSize_ReturnsSameValues()
        {
            var obs = new ObservableSize();
            var original = new Size(1280, 720);

            obs.FromSize(original);
            var result = obs.ToSize();

            Assert.Equal(original, result);
        }
    }
}
