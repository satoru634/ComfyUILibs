using System.IO;
using System.Text;
using ComfyUILibs.Exceptions;
using ComfyUILibs.Models;
using ComfyUILibs.Resources;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// ディレクトリ内の画像を <see cref="Wd14TaggerRunner"/> で一括タグ付けし、
    /// 同名の .txt キャプションファイルを生成するクラス。
    /// Python 版 captioning_tool.py の CaptioningTool クラスを移植したもの。
    /// </summary>
    public class CaptioningService
    {
        /// <summary>タグ付け対象とする画像ファイルの拡張子（大文字小文字は無視）。</summary>
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        /// <summary>タグ集計レポートの出力ファイル名。集計対象からも除外する。</summary>
        public const string ReportFileName = "tags_report.txt";

        private readonly Wd14TaggerRunner _taggerRunner;
        private readonly IReadOnlyList<string> _prependTags;
        private readonly IReadOnlyList<string> _excludeTags;

        /// <summary>
        /// タグ付け実行に使用する <see cref="Wd14TaggerRunner"/> と、フィルタに使用する
        /// prepend/exclude タグを受け取って初期化する。
        /// prepend_tags/exclude_tags（設定ファイル由来）と、GUI 等の追加指定タグとの union は
        /// 呼び出し側で解決してから渡すこと（本クラスは設定ファイルを読み込まない）。
        /// </summary>
        /// <param name="taggerRunner">画像 1 枚のタグ取得に使用するランナー。</param>
        /// <param name="prependTags">全画像の冒頭に挿入するタグ。</param>
        /// <param name="excludeTags">全画像から除去するタグ（完全一致・大文字小文字無視）。</param>
        public CaptioningService(
            Wd14TaggerRunner taggerRunner,
            IReadOnlyList<string>? prependTags = null,
            IReadOnlyList<string>? excludeTags = null)
        {
            _taggerRunner = taggerRunner;
            _prependTags = prependTags ?? Array.Empty<string>();
            _excludeTags = excludeTags ?? Array.Empty<string>();
        }

        /// <summary>
        /// ディレクトリ内の対応画像を収集し、順に <see cref="Wd14TaggerRunner.TagAsync"/> でタグ付けして
        /// 同名 .txt に書き込む。1 ファイル処理するたびに <paramref name="progress"/> へ通知する。
        /// </summary>
        /// <param name="directory">処理対象ディレクトリ。</param>
        /// <param name="recursive">サブディレクトリも再帰的に処理するか。</param>
        /// <param name="overwrite">既存の .txt を上書きするか（false の場合はスキップ）。</param>
        /// <param name="progress">1 ファイルごとの処理結果通知先。</param>
        /// <returns>(処理数, スキップ数, エラー数)。</returns>
        /// <exception cref="ComfyUIException">ディレクトリが存在しない場合。</exception>
        public async Task<(int Processed, int Skipped, int Errors)> ProcessDirectoryAsync(
            string directory,
            bool recursive,
            bool overwrite,
            IProgress<CaptioningProgress>? progress = null)
        {
            if (!Directory.Exists(directory))
                throw new ComfyUIException(
                    Messages.Get("CaptioningService_DirectoryNotFound_Format", directory));

            var images = CollectImageFiles(directory, recursive);
            int processed = 0, skipped = 0, errors = 0;

            for (int i = 0; i < images.Count; i++)
            {
                var (result, errorMessage) = await ProcessImageAsync(images[i], overwrite);
                switch (result)
                {
                    case CaptioningResult.Processed: processed++; break;
                    case CaptioningResult.Skipped: skipped++; break;
                    case CaptioningResult.Error: errors++; break;
                }

                progress?.Report(new CaptioningProgress(
                    i + 1, images.Count, Path.GetFileName(images[i]), result, errorMessage));
            }

            return (processed, skipped, errors);
        }

        /// <summary>
        /// ディレクトリ内の全 .txt からタグを集計し、出現回数の多い順（同数はアルファベット順）で
        /// <see cref="ReportFileName"/> に書き出す。
        /// </summary>
        /// <param name="directory">処理対象ディレクトリ。</param>
        /// <param name="recursive">サブディレクトリも集計対象に含めるか。</param>
        public async Task GenerateReportAsync(string directory, bool recursive)
        {
            var tagCounts = CollectAllTags(directory, recursive);
            var lines = tagCounts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}: {kv.Value}");

            var reportPath = Path.Combine(directory, ReportFileName);
            await File.WriteAllLinesAsync(reportPath, lines, Encoding.UTF8);
        }

        /// <summary>
        /// 単一画像を処理する。既存 .txt があり overwrite が false ならスキップ、
        /// それ以外はタグ取得・フィルタ適用・書き込みを行い、例外はすべて捕捉して Error として返す
        /// （バッチ処理を継続させるため）。
        /// </summary>
        private async Task<(CaptioningResult Result, string? ErrorMessage)> ProcessImageAsync(
            string imagePath, bool overwrite)
        {
            var txtPath = Path.ChangeExtension(imagePath, ".txt");
            if (!overwrite && File.Exists(txtPath))
                return (CaptioningResult.Skipped, null);

            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var tags = await _taggerRunner.TagAsync(imageBytes, Path.GetFileName(imagePath));
                var filtered = ApplyTagFilters(tags);
                await File.WriteAllTextAsync(txtPath, filtered, Encoding.UTF8);
                return (CaptioningResult.Processed, null);
            }
            catch (Exception ex)
            {
                return (CaptioningResult.Error, ex.Message);
            }
        }

        /// <summary>
        /// WD14 出力タグ文字列に対し、(1) exclude タグの除去 → (2) prepend と重複するタグの除去
        /// → (3) prepend タグの先頭挿入、の順にフィルタを適用する。
        /// </summary>
        /// <param name="tags">WD14 Tagger が返したカンマ区切りタグ文字列。</param>
        /// <returns>フィルタ適用後のカンマ区切りタグ文字列。</returns>
        internal string ApplyTagFilters(string tags)
        {
            var wdTags = SplitTags(tags);

            var excludeSet = new HashSet<string>(_excludeTags.Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);
            var prependSet = new HashSet<string>(_prependTags.Select(t => t.Trim()), StringComparer.OrdinalIgnoreCase);

            var filtered = wdTags
                .Where(t => !excludeSet.Contains(t))
                .Where(t => !prependSet.Contains(t));

            var result = _prependTags.Select(t => t.Trim()).Concat(filtered);
            return string.Join(", ", result);
        }

        /// <summary>ディレクトリ内の全 .txt ファイル（<see cref="ReportFileName"/> 自身は除外）からタグを集計する。</summary>
        /// <param name="directory">集計対象ディレクトリ。</param>
        /// <param name="recursive">サブディレクトリも対象に含めるか。</param>
        /// <returns>タグ名をキー、出現回数を値とする辞書（大文字小文字は区別する）。</returns>
        internal Dictionary<string, int> CollectAllTags(string directory, bool recursive)
        {
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var txtPath in Directory.EnumerateFiles(directory, "*.txt", option))
            {
                if (Path.GetFileName(txtPath).Equals(ReportFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var content = File.ReadAllText(txtPath, Encoding.UTF8);
                foreach (var tag in SplitTags(content))
                    counts[tag] = counts.TryGetValue(tag, out var count) ? count + 1 : 1;
            }

            return counts;
        }

        /// <summary>指定ディレクトリ内の対応拡張子の画像ファイルを収集する（ファイルパス昇順）。</summary>
        private static List<string> CollectImageFiles(string directory, bool recursive)
        {
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(directory, "*", option)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>カンマ区切りタグ文字列を trim・空要素除去したリストに分割する。</summary>
        private static List<string> SplitTags(string tags)
            => tags.Split(',')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
    }
}
