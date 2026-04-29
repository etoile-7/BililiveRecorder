using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BililiveRecorder.Core.Artifacts
{
    internal static class RecordArtifactJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static void WriteAtomic(string path, object value)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = path + ".tmp";
            var json = JsonConvert.SerializeObject(value, Settings);
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }

        public static T? Read<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Encoding.UTF8), Settings);
        }
    }
}
