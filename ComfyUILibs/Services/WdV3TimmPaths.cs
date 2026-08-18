using System.IO;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// wdv3-timm（ローカルプロセス版タグ付け）一式の固定配置パスを解決する静的クラス。
    /// アプリの実行ファイルと同じ階層の <c>wdv3-timm</c> フォルダに固定する（ユーザーが
    /// captioning_config.json で個別に指定する方式は廃止した）。
    /// </summary>
    /// <remarks>
    /// wdv3-timm リポジトリ自体の制約により、wdv3_timm.exe は .venv・wdv3_timm.py と同じフォルダに
    /// 置いた状態で使う必要がある（詳細は wdv3-timm リポジトリの .claude/architecture.md を参照）。
    /// そのため実行ファイル単体ではなく <see cref="RootDirectory"/> ごと配置する前提とする。
    /// </remarks>
    public static class WdV3TimmPaths
    {
        /// <summary>wdv3-timm 一式（.venv・wdv3_timm.py・wdv3_timm.exe・setup.bat・build_exe.bat 等）を配置するフォルダ。</summary>
        public static string RootDirectory => Path.Combine(AppContext.BaseDirectory, "wdv3-timm");

        /// <summary>wdv3_timm.exe（<c>--serve</c> 常駐サーバーモードで起動する実行ファイル）のパス。</summary>
        public static string ExeFilePath => Path.Combine(RootDirectory, "wdv3_timm.exe");
    }
}
