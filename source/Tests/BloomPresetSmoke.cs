using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

// ゲームを起動せず、両版の実アセンブリで新旧のブルーム設定XMLを往復させる。
internal static class BloomPresetSmoke
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("プラグインDLLとゲームのManagedフォルダを指定してください");
            AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
            {
                var path = Path.Combine(args[1], new AssemblyName(request.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            var plugin = Assembly.LoadFrom(args[0]);
            foreach (var name in new[] { "BloomSetting", "CinematicBloomSetting", "FilmicBloomSetting" })
            {
                var type = plugin.GetType("COM3D25.PostEffects.Plugin." + name, true);
                var value = Activator.CreateInstance(type);
                var splitField = type.GetField("separation");
                var split = splitField.GetValue(value);
                Require(!(bool)Get(split, "enabled"), "既定値で分離が有効になっています");
                Set(split, "enabled", true);
                Set(split, "charactersEnabled", false);
                Set(split, "backgroundEnabled", true);
                Set(split, "characterIntensity", 4.25f);
                Set(split, "characterThreshold", 0.25f);
                Set(split, "characterRadius", 2.5f);
                Set(value, "intensity", 0.75f);
                if (name == "BloomSetting")
                {
                    Set(value, "hdr", Enum.Parse(type.GetField("hdr").FieldType, "On"));
                }
                var serializer = new XmlSerializer(type);
                var writer = new StringWriter();
                serializer.Serialize(writer, value);
                var xml = writer.ToString();
                var restored = serializer.Deserialize(new StringReader(xml));
                var restoredSplit = splitField.GetValue(restored);
                foreach (var field in split.GetType().GetFields())
                    Require(Equals(field.GetValue(split), field.GetValue(restoredSplit)),
                        "分離設定の保存復元が不一致です: " + field.Name);
                Require(Equals(Get(restored, "intensity"), 0.75f), "背景の強度が失われました");
                if (name == "BloomSetting")
                {
                    Require(Get(restored, "hdr").ToString() == "On", "共通のHDR Onが失われました");
                    // 一時的に追加したキャラ専用設定があるXMLも、共通HDRを保って読み込む。
                    var splitHdrXml = new XmlDocument();
                    splitHdrXml.LoadXml(xml);
                    var oldHdr = splitHdrXml.CreateElement("characterHdr");
                    oldHdr.InnerText = "Off";
                    splitHdrXml.DocumentElement.AppendChild(oldHdr);
                    var migrated = serializer.Deserialize(new StringReader(splitHdrXml.OuterXml));
                    Require(Get(migrated, "hdr").ToString() == "On", "旧キャラHDRが共通HDRを変更しました");
                }

                // 旧形式には separation 要素がなく、既存の数値だけが入っている。
                var document = new XmlDocument();
                document.LoadXml(xml);
                var element = document.DocumentElement.SelectSingleNode("separation");
                document.DocumentElement.RemoveChild(element);
                var legacy = serializer.Deserialize(new StringReader(document.OuterXml));
                if (name == "BloomSetting")
                {
                    Require(Get(legacy, "hdr").ToString() == "On", "旧設定の共通HDRが変化しました");
                }
                Require(!(bool)Get(splitField.GetValue(legacy), "enabled"), "旧設定で分離が有効になりました");
                Require(Equals(Get(legacy, "intensity"), 0.75f), "旧設定の強度が変化しました");
                Require(!ReferenceEquals(split, splitField.GetValue(legacy)), "分離設定が別インスタンスと共有されています");
                Console.WriteLine(name + ": 新形式往復・旧形式互換・独立性が成功しました");
            }
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("ブルーム設定の検証に失敗しました: " + e);
            return 1;
        }
    }

    private static object Get(object target, string field) { return target.GetType().GetField(field).GetValue(target); }
    private static void Set(object target, string field, object value) { target.GetType().GetField(field).SetValue(target, value); }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
