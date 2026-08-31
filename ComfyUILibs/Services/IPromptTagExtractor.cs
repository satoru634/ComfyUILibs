using System.Collections.Generic;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// プロンプト文字列（ComfyUI / A1111 のポジティブプロンプト等）を、
    /// ギャラリーのタグ絞り込みに使える正規化済みタグ列へ変換する。
    /// </summary>
    public interface IPromptTagExtractor
    {
        /// <summary>
        /// プロンプト文字列をカンマ区切りで分割し、重み記法・LoRA 記法・<c>BREAK</c> を
        /// 取り除いた正規化済みタグ列を返す。大文字小文字を無視して重複を排除し、
        /// 初出の表記を採用する。<paramref name="prompt"/> が null／空白のみの場合は空リストを返す。
        /// </summary>
        IReadOnlyList<string> ExtractTags(string? prompt);
    }
}
