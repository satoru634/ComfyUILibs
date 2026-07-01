using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace ComfyUILibs.Common
{
    public class JsonLoader
    {
        /// <summary>
        /// 指定されたパスからJSONデータを読み込み、指定された型にデシリアライズする。
        /// </summary>
        /// <param name="inputPath">入力パス</param>
        /// <returns></returns>
        static public T ReadJson<T>(string inputPath) where T : new()
        {
            string json = "";
            using (StreamReader sr = new StreamReader(inputPath))
            {
                json = sr.ReadToEnd();
            }

            return DeserializeJson<T>(json);
        }

        /// <summary>
        /// 指定されたストリームからJSONデータを読み込み、指定された型にデシリアライズする。
        /// </summary>
        /// <param name="inputPath">入力パス</param>
        /// <returns></returns>
        static public T ReadJson<T>(Stream inputStream) where T : new()
        {
            string json = "";
            using (StreamReader sr = new StreamReader(inputStream))
            {
                json = sr.ReadToEnd();
            }

            return DeserializeJson<T>(json);
        }

        /// <summary>
        /// 指定されたJSON文字列をデシリアライズして、指定された型のオブジェクトを返す。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        static public T DeserializeJson<T>(string? json) where T : new()
        {
            if (json == null)
            {
                return new T();
            }

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            options.WriteIndented = true;
            T? data = JsonSerializer.Deserialize<T>(json, options);
            return data != null ? data : new T();
        }

        /// <summary>
        /// 指定されたデータをJSON形式でファイルに書き込む。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="outputPath">出力先パス</param>
        /// <param name="data">書き込み対象のデータ</param>
        static public void WriteJson<T>(string outputPath, in T data)
        {
            // JSONのオプションを設定
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            options.WriteIndented = true;

            // ディレクトリが存在しない場合は作成
            if (!Path.Exists(Path.GetDirectoryName(outputPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "");
            }

            // JSONをシリアライズしてファイルに書き込む
            string json = JsonSerializer.Serialize(data, options);
            using (StreamWriter sw = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                sw.WriteLine(json);
            }

            return;
        }
    }
}
