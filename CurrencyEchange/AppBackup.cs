using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CurrencyEchange
{
    public static class AppBackup
    {
        public static string currencyDataKey = "currencyData";
        public static string currencyHistoryKey = "currencyHistory";

        public static Dictionary<string, object> appState = new Dictionary<string, object>();

        public static void initStorage()
        {
            appState.Add(currencyDataKey, null);
            appState.Add(currencyHistoryKey, null);
        }

        public static async Task loadAppState()
        {
            var file = await ApplicationData.Current.LocalFolder.GetFileAsync("backup.xml");
            if (file == null) return;
            
            using (IInputStream stream = await file.OpenSequentialReadAsync())
            {
                var serializer = new DataContractSerializer(typeof(Dictionary<string, object>), new List<Type>() { typeof(CurrencyData), typeof(CurrencyHistoryView) });
                appState = (Dictionary<string, object>)serializer.ReadObject(stream.AsStreamForRead());
            }
        }
        public static async Task storeAppState()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            var ms = new MemoryStream();
            var serializer = new DataContractSerializer(typeof(Dictionary<string, object>));
            serializer.WriteObject(ms, appState);

            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("backup.xml", CreationCollisionOption.ReplaceExisting);

            using (var fs = await file.OpenStreamForWriteAsync())
            {
                //because we have written to the stream, set the position back to start
                ms.Seek(0, SeekOrigin.Begin);
                await ms.CopyToAsync(fs);
                await fs.FlushAsync();
            }
        }
    }

    static class ObjectSerializer<T>
    {
        // Serialize to xml  
        public static string ToXml(T value)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            StringBuilder stringBuilder = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                Indent = true,
                OmitXmlDeclaration = true,
            };

            using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                serializer.Serialize(xmlWriter, value);
            }
            return stringBuilder.ToString();
        }

        // Deserialize from xml  
        public static T FromXml(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            T value;
            using (StringReader stringReader = new StringReader(xml))
            {
                object deserialized = serializer.Deserialize(stringReader);
                value = (T)deserialized;
            }

            return value;
        }
    }
}
