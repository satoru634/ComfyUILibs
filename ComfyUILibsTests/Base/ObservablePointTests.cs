using System.ComponentModel;
using System.Windows;
using ComfyUILibs.Base;

namespace ComfyUILibsTests.Base
{
    public class ObservablePointTests
    {
        // ── コンストラクター ───────────────────────────────────────────────────

        [Fact]
        public void Constructor_Default_InitializesXAndYToZero()
        {
            var obs = new ObservablePoint();

            Assert.Equal(0, obs.X);
            Assert.Equal(0, obs.Y);
        }

        [Fact]
        public void Constructor_WithValues_SetsXAndY()
        {
            var obs = new ObservablePoint(3.5, -7.25);

            Assert.Equal(3.5, obs.X);
            Assert.Equal(-7.25, obs.Y);
        }

        // ── PropertyChanged ───────────────────────────────────────────────────

        [Fact]
        public void X_Set_RaisesPropertyChangedEvent()
        {
            var obs = new ObservablePoint();
            var changedProps = new List<string?>();
            ((INotifyPropertyChanged)obs).PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            obs.X = 10;

            Assert.Contains("X", changedProps);
        }

        [Fact]
        public void Y_Set_RaisesPropertyChangedEvent()
        {
            var obs = new ObservablePoint();
            var changedProps = new List<string?>();
            ((INotifyPropertyChanged)obs).PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            obs.Y = 20;

            Assert.Contains("Y", changedProps);
        }

        // ── ToPoint ───────────────────────────────────────────────────────────

        [Fact]
        public void ToPoint_ReturnsCorrectPoint()
        {
            var obs = new ObservablePoint(12.5, 34.75);

            var result = obs.ToPoint();

            Assert.Equal(new Point(12.5, 34.75), result);
        }

        [Fact]
        public void ToPoint_DefaultConstructor_ReturnsOrigin()
        {
            var obs = new ObservablePoint();

            var result = obs.ToPoint();

            Assert.Equal(new Point(0, 0), result);
        }

        // ── FromPoint ─────────────────────────────────────────────────────────

        [Fact]
        public void FromPoint_SetsXAndY()
        {
            var obs = new ObservablePoint();

            obs.FromPoint(new Point(100, 200));

            Assert.Equal(100, obs.X);
            Assert.Equal(200, obs.Y);
        }

        [Fact]
        public void FromPoint_OverwritesExistingValues()
        {
            var obs = new ObservablePoint(1, 2);

            obs.FromPoint(new Point(50, 75));

            Assert.Equal(50, obs.X);
            Assert.Equal(75, obs.Y);
        }

        [Fact]
        public void FromPoint_RaisesPropertyChangedForXAndY()
        {
            var obs = new ObservablePoint();
            var changedProps = new List<string?>();
            ((INotifyPropertyChanged)obs).PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            obs.FromPoint(new Point(10, 20));

            Assert.Contains("X", changedProps);
            Assert.Contains("Y", changedProps);
        }

        // ── ラウンドトリップ ───────────────────────────────────────────────────

        [Fact]
        public void FromPoint_ThenToPoint_ReturnsSameValues()
        {
            var obs = new ObservablePoint();
            var original = new Point(123.45, -67.89);

            obs.FromPoint(original);
            var result = obs.ToPoint();

            Assert.Equal(original, result);
        }
    }
}
