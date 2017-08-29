using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Hellosam.Net.TransparentPaint
{
    [DataContract]
    internal class ConfigData
    {
        [DataMember]
        public double Height;
        [DataMember]
        public double Width;
        [DataMember]
        public double RatioHeight;
        [DataMember]
        public double RatioWidth;
        [DataMember]
        public int Port;
        [DataMember]
        public bool AlwaysOnTop;
        [DataMember]
        public bool EnableSnap;
        [DataMember]
        public string SnapName;

        public ConfigData(MainViewModel vm)
        {
            Height = vm.Height;
            Width = vm.Width;
            RatioHeight = vm.RatioHeight;
            RatioWidth = vm.RatioWidth;
            Port = vm.Port;
            AlwaysOnTop = vm.AlwaysOnTop;
            EnableSnap = vm.EnableSnap;
            SnapName = vm.SnapName;
        }

        public ConfigData()
        {
            Width = 512;
            Height = 512;
            RatioHeight = 9;
            RatioWidth = 16;
            Port = 8010;
            AlwaysOnTop = true;
        }

        public void AppliesTo(MainViewModel vm)
        {
            vm.Height = Height;
            vm.Width = Width;
            vm.RatioHeight = RatioHeight;
            vm.RatioWidth = RatioWidth;
            vm.Port = Port;
            vm.AlwaysOnTop = AlwaysOnTop;
            vm.EnableSnap = EnableSnap;
            vm.SnapName = SnapName;
        }
    }

    internal static class Config
    {
        public const string CONFIG_FILE = "Config.xml";

        public static string ConfigPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hellosam.Net.TransparentPaint");
            }
        }

        public static ConfigData CreateFromSystemOrDefault()
        {
            var p = Path.Combine(ConfigPath, CONFIG_FILE);
            var t = new ConfigData();
            if (!File.Exists(p))
                return t;

            string data;
            try
            {
                data = File.ReadAllText(p, Encoding.UTF8);
            }
            catch (UnauthorizedAccessException)
            {
                return t;
            }
            catch (IOException)
            {
                return t;
            }
            try
            {
                t = Deserialize(data);
                // t.IsLoaded = true;
            }
            catch (ArgumentException)
            {
                return t;
            }
            return t;
        }

        public static void SaveToSystem(ConfigData config)
        {
            try
            {
                Directory.CreateDirectory(ConfigPath);
                var p = Path.Combine(ConfigPath, CONFIG_FILE);
                File.WriteAllText(p, Serialize(config), Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static ConfigData Deserialize(string data)
        {
            var serializer = GetSerializer();

            using (var s = new MemoryStream())
            using (var sw = new StreamWriter(s))
            {
                sw.Write(data);
                sw.Flush();
                s.Flush();
                s.Position = 0;
                try
                {
                    var o = serializer.ReadObject(s);
                    if (!(o is ConfigData) || o == null)
                        throw new ArgumentOutOfRangeException("The data is not ConfigData");
                    return (ConfigData)o;
                }
                catch (XmlException ex)
                {
                    throw new ArgumentOutOfRangeException("The data cannot be parsed", ex);
                }
                catch (SerializationException ex)
                {
                    throw new ArgumentOutOfRangeException("The data cannot be parsed", ex);
                }
            }
        }

        static string Serialize(ConfigData config)
        {
            var serializer = GetSerializer();

            using (var s = new MemoryStream())
            using (var sw = new StringWriter())
            using (var wr = new XmlTextWriter(sw))
            {
                serializer.WriteObject(s, config);

                XmlDocument document = new XmlDocument();
                s.Flush();
                s.Position = 0;
                document.Load(s);

                wr.Formatting = Formatting.Indented;
                wr.IndentChar = ' ';
                wr.Indentation = 4;

                document.WriteContentTo(wr);
                wr.Flush();
                return sw.ToString();
            }
        }
        
        private static DataContractSerializer GetSerializer()
        {
            var serializer = new DataContractSerializer(typeof(ConfigData));
            return serializer;
        }

    }
}
