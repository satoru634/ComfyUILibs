using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// <see cref="IPromptTagExtractor"/> の既定実装。
    /// SD / ComfyUI で一般的なプロンプト記法を前提に、次の順で正規化する。
    /// <list type="number">
    ///   <item><c>&lt;lora:...&gt;</c> などのアングルブラケット記法を除去する（LoRA はタグにしない）。</item>
    ///   <item>カンマで分割し、各要素の前後空白を除去する。</item>
    ///   <item>単語境界の <c>BREAK</c> を除去する。</item>
    ///   <item>要素全体を囲む重み記法 <c>(tag:1.2)</c> / <c>(tag)</c> / <c>[tag]</c> を再帰的に剥がす。
    ///         エスケープされた <c>\(</c> <c>\)</c> は囲み記号とみなさない。</item>
    ///   <item>連続空白を 1 個に畳み、エスケープ <c>\(</c> 等を文字へ戻す。</item>
    ///   <item>空になった要素を捨て、大文字小文字を無視して重複を排除する（初出の表記を採用）。</item>
    /// </list>
    /// </summary>
    public class PromptTagExtractor : IPromptTagExtractor
    {
        /// <summary><c>&lt;lora:name:1&gt;</c> / <c>&lt;lyco:...&gt;</c> / <c>&lt;hypernet:...&gt;</c> 等のアングルブラケット記法。</summary>
        private static readonly Regex AngleToken = new(@"<[^>\r\n]*>", RegexOptions.Compiled);

        /// <summary>単語境界で囲まれた <c>BREAK</c>（A1111 のプロンプト区切りキーワード）。</summary>
        private static readonly Regex BreakToken = new(@"\bBREAK\b", RegexOptions.Compiled);

        /// <summary>要素全体を囲む重み付き強調 <c>(tag:1.2)</c>。<c>.+</c> はバックトラックして末尾の <c>:数値)</c> に合わせる。</summary>
        private static readonly Regex WeightedEmphasis = new(@"^\((.+):\s*-?\d*\.?\d+\s*\)$", RegexOptions.Compiled);

        /// <summary>要素全体を囲む強調 <c>(tag)</c>。</summary>
        private static readonly Regex ParenEmphasis = new(@"^\((.+)\)$", RegexOptions.Compiled);

        /// <summary>要素全体を囲む弱調 <c>[tag]</c>。</summary>
        private static readonly Regex BracketEmphasis = new(@"^\[(.+)\]$", RegexOptions.Compiled);

        /// <summary>連続する空白文字。</summary>
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        /// <inheritdoc/>
        public IReadOnlyList<string> ExtractTags(string? prompt)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(prompt))
                return result;

            var withoutAngle = AngleToken.Replace(prompt, " ");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawPiece in withoutAngle.Split(','))
            {
                var piece = BreakToken.Replace(rawPiece, " ");
                piece = StripEmphasis(piece);
                piece = Whitespace.Replace(piece, " ").Trim();
                piece = Unescape(piece);

                if (piece.Length == 0)
                    continue;
                if (seen.Add(piece))
                    result.Add(piece);
            }

            return result;
        }

        /// <summary>要素全体を囲む強調記号（重み付き含む）を、変化しなくなるまで繰り返し剥がす。</summary>
        private static string StripEmphasis(string value)
        {
            var current = value.Trim();
            while (true)
            {
                var weighted = WeightedEmphasis.Match(current);
                if (weighted.Success)
                {
                    current = weighted.Groups[1].Value.Trim();
                    continue;
                }

                var paren = ParenEmphasis.Match(current);
                if (paren.Success)
                {
                    current = paren.Groups[1].Value.Trim();
                    continue;
                }

                var bracket = BracketEmphasis.Match(current);
                if (bracket.Success)
                {
                    current = bracket.Groups[1].Value.Trim();
                    continue;
                }

                return current;
            }
        }

        /// <summary>プロンプト中でエスケープされていた括弧類（<c>\(</c> 等）を通常の文字へ戻す。</summary>
        private static string Unescape(string value)
            => value
                .Replace(@"\(", "(")
                .Replace(@"\)", ")")
                .Replace(@"\[", "[")
                .Replace(@"\]", "]");
    }
}
