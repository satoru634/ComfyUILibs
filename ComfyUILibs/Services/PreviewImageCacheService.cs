using System.IO;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// 生成画像プレビューのローカルキャッシュを管理するクラス。
    /// 画像本体は ComfyUI サーバー側にのみ存在するため、GET /view で取得したバイト列を
    /// ローカルの cacheDirectory に保存し、以降は再取得せずキャッシュファイルを再利用する。
    /// </summary>
    public class PreviewImageCacheService : IPreviewImageCacheService
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp"
        };

        /// <summary>
        /// ファイル名の拡張子からプレビュー対象の画像ファイルかどうかを判定する。
        /// 動画等の非画像出力はプレビュー対象外とする。
        /// </summary>
        /// <param name="filename">判定対象のファイル名。</param>
        public static bool IsImageFile(string filename)
            => ImageExtensions.Contains(Path.GetExtension(filename));

        /// <inheritdoc/>
        public async Task<string?> GetOrFetchAsync(
            IComfyUIClient client, string? promptId, OutputFile output, string cacheDirectory)
        {
            if (!IsImageFile(output.Filename))
                return null;

            var cachePath = Path.Combine(cacheDirectory, BuildCacheFileName(promptId, output));

            if (File.Exists(cachePath))
                return cachePath;

            try
            {
                var bytes = await client.GetImageAsync(output.Filename, output.Subfolder, output.Type);
                Directory.CreateDirectory(cacheDirectory);
                await File.WriteAllBytesAsync(cachePath, bytes);
                return cachePath;
            }
            catch (ComfyUIException)
            {
                // 接続失敗・画像削除済みなどはプレビュー取得失敗として扱い、呼び出し元にはプレースホルダー表示させる
                return null;
            }
        }

        /// <summary>
        /// prompt_id・サブフォルダ・ファイル名からキャッシュファイル名を組み立てる。
        /// 同名ファイルが異なる実行結果で再利用されても衝突しないようにする。
        /// </summary>
        private static string BuildCacheFileName(string? promptId, OutputFile output)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(promptId))
                parts.Add(Sanitize(promptId));
            if (!string.IsNullOrEmpty(output.Subfolder))
                parts.Add(Sanitize(output.Subfolder));
            parts.Add(Sanitize(output.Filename));

            return string.Join("_", parts);
        }

        /// <summary>ファイル名に使用できない文字をアンダースコアに置換する。</summary>
        private static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }
    }
}
