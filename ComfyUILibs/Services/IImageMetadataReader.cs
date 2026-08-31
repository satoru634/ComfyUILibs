using ComfyUILibs.Models;

namespace ComfyUILibs.Services
{
    /// <summary>
    /// PNG に埋め込まれた ComfyUI / A1111 メタデータを読み取り、生成情報を抽出する。
    /// </summary>
    public interface IImageMetadataReader
    {
        /// <summary>
        /// 指定画像の <c>tEXt</c> / <c>iTXt</c> / <c>zTXt</c> チャンク（ComfyUI の
        /// <c>prompt</c> / <c>workflow</c>、または A1111 形式の <c>parameters</c>）を解析し、
        /// <see cref="ComfyImageMetadata"/> を返す。
        /// メタデータが無い・構造が想定外でリンクを辿り切れない場合も例外にはせず、
        /// <see cref="ComfyMetadataParseStatus.None"/> / <see cref="ComfyMetadataParseStatus.Partial"/>
        /// を設定して返す。ファイルが存在しない・ファイル読み取り自体に失敗した場合は
        /// <see cref="Exceptions.ComfyUIException"/> を送出する。
        /// </summary>
        ComfyImageMetadata Read(string imagePath);
    }
}
