namespace ComfyUILibs.Exceptions
{
    /// <summary>
    /// ComfyUI 操作に関するエラーを表す基底例外クラス。
    /// 接続失敗・バリデーションエラー・実行エラーなど、すべての ComfyUI 固有例外はこのクラスを送出する。
    /// </summary>
    public class ComfyUIException : Exception
    {
        /// <summary>エラーメッセージを指定して例外を初期化する。</summary>
        /// <param name="message">エラーの詳細メッセージ。</param>
        public ComfyUIException(string message) : base(message) { }

        /// <summary>エラーメッセージと内部例外を指定して例外を初期化する。</summary>
        /// <param name="message">エラーの詳細メッセージ。</param>
        /// <param name="innerException">このエラーの原因となった例外。</param>
        public ComfyUIException(string message, Exception innerException) : base(message, innerException) { }
    }
}
