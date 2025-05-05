using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace dn_test_250505
{

    public class Program
    {
        public static string ByteArrayToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }

        public static DataContractJsonSerializerSettings NewDefaultRuntimeJsonSerializerSettings()
        {
            return new DataContractJsonSerializerSettings()
            {
                DateTimeFormat = new DateTimeFormat("yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK"),
                UseSimpleDictionaryFormat = true,
            };
        }

        public static void ObjectToRuntimeJson(object obj, Stream dst, DataContractJsonSerializerSettings? settings = null)
        {
            if (settings == null) settings = NewDefaultRuntimeJsonSerializerSettings();
            DataContractJsonSerializer d = new DataContractJsonSerializer(obj.GetType(), settings);
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(dst, Encoding.UTF8 , false, true, "  "))
            {
                d.WriteObject(writer, obj);
            }
        }
        public static byte[] ObjectToRuntimeJson(object obj, DataContractJsonSerializerSettings? settings = null)
        {
            MemoryStream ms = new();
            ObjectToRuntimeJson(obj, ms, settings);
            return ms.ToArray();
        }

        public class Test1
        {
            public string Hello = "A";
        }

        static void Main(string[] args)
        {
            Test1 t = new();
            Console.WriteLine("Hello, World!");
            Console.WriteLine(ByteArrayToHexString(ObjectToRuntimeJson(t)));
        }
    }
}
