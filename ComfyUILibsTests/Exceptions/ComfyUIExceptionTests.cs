using ComfyUILibs.Exceptions;

namespace ComfyUILibsTests.Exceptions
{
    public class ComfyUIExceptionTests
    {
        [Fact]
        public void Constructor_WithMessage_SetsMessage()
        {
            var ex = new ComfyUIException("テストエラー");

            Assert.Equal("テストエラー", ex.Message);
        }

        [Fact]
        public void Constructor_WithMessageAndInner_SetsMessageAndInnerException()
        {
            var inner = new InvalidOperationException("内部エラー");
            var ex = new ComfyUIException("外部エラー", inner);

            Assert.Equal("外部エラー", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void ComfyUIException_IsException()
        {
            var ex = new ComfyUIException("msg");

            Assert.IsAssignableFrom<Exception>(ex);
        }
    }
}
