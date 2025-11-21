using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class JsonManager
{
    // Jsonのファイルパス
    public string JsonFilePath = Path.Combine(AppContext.BaseDirectory, "Settings", "settings.json");

    // Jsonの読み込み→Parserオブジェクトに変換
    public Object LoadJson<Object>(bool createIfNotExists = true)
    {
        // ファイルの存在確認
        if (!File.Exists(JsonFilePath))
        {
            if (createIfNotExists)
            {
               // ファイルが存在しない場合、新規作成
                Directory.CreateDirectory(Path.GetDirectoryName(JsonFilePath)!);

                Object @object = Activator.CreateInstance<Object>()!; // 型のデフォルトインスタンスを作成

                string parsed = System.Text.Json.JsonSerializer.Serialize(@object, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(JsonFilePath, parsed); // 空のJSONオブジェクトを作成
            }
            else
            {
                throw new FileNotFoundException($"指定されたJSONファイルが見つかりません: {JsonFilePath}");
            }
        }

        // JSONファイルの内容を読み込み、指定された型にデシリアライズ
        string jsonString = File.ReadAllText(JsonFilePath);
        Object? obj = System.Text.Json.JsonSerializer.Deserialize<Object>(jsonString);

        if (obj == null)
        {
            throw new InvalidOperationException("JSONのデシリアライズに失敗しました。");
        }

        return obj!;
    }

    // ParserオブジェクトをJsonに変換→保存
    public void SaveJson<Object>(Object obj)
    {
        // オブジェクトをJSON文字列にシリアライズ
        string jsonString = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }); 

        if (!Directory.Exists(Path.GetDirectoryName(JsonFilePath)!))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(JsonFilePath)!);
        }

        // JSON文字列をファイルに書き込み
        File.WriteAllText(JsonFilePath, jsonString);
    }
}
