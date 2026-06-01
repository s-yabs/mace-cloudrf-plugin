using System;
using System.IO;

namespace CloudRFPlugin
{
    internal sealed class CloudRFSettings
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.cloudrf.com";
        public string TemplatePath { get; set; }
        public string OutputDirectory { get; set; }
        public bool AutoImportGeoTiff { get; set; } = true;

        public static string AppDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "MACE",
            "CloudRF");

        public static string SettingsPath => Path.Combine(AppDirectory, "cloudrf-plugin-settings.json");

        public static string DefaultTemplatePath => Path.Combine(AppDirectory, "area-template.json");

        public static string DefaultOutputDirectory => Path.Combine(AppDirectory, "Outputs");

        public static CloudRFSettings Load()
        {
            Directory.CreateDirectory(AppDirectory);
            Directory.CreateDirectory(DefaultOutputDirectory);

            if (!File.Exists(DefaultTemplatePath))
            {
                File.WriteAllText(DefaultTemplatePath, DefaultAreaTemplate);
            }

            if (!File.Exists(SettingsPath))
            {
                return new CloudRFSettings
                {
                    TemplatePath = DefaultTemplatePath,
                    OutputDirectory = DefaultOutputDirectory
                };
            }

            string json = File.ReadAllText(SettingsPath);
            var settings = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<CloudRFSettings>(json);
            settings.TemplatePath = string.IsNullOrWhiteSpace(settings.TemplatePath) ? DefaultTemplatePath : settings.TemplatePath;
            settings.OutputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory) ? DefaultOutputDirectory : settings.OutputDirectory;
            settings.BaseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.cloudrf.com" : settings.BaseUrl;
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(AppDirectory);
            Directory.CreateDirectory(OutputDirectory);
            string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(this);
            File.WriteAllText(SettingsPath, json);
        }

        public const string DefaultAreaTemplate = @"{
  ""version"": ""CloudRF-API-v3.4.0"",
  ""site"": ""MACE"",
  ""network"": ""MACE"",
  ""engine"": 2,
  ""coordinates"": 1,
  ""transmitter"": {
    ""lat"": 0,
    ""lon"": 0,
    ""alt"": 2,
    ""frq"": 868,
    ""txw"": 0.1,
    ""bwi"": 0.1,
    ""powerUnit"": ""W""
  },
  ""receiver"": {
    ""lat"": 0,
    ""lon"": 0,
    ""alt"": 1,
    ""rxg"": 1,
    ""rxs"": -130
  },
  ""feeder"": {
    ""flt"": ""1"",
    ""fll"": 0,
    ""fcc"": 0
  },
  ""antenna"": {
    ""mode"": ""template"",
    ""txg"": 2,
    ""txl"": 0,
    ""ant"": 39,
    ""azi"": 0,
    ""tlt"": 0,
    ""hbw"": 90,
    ""vbw"": 90,
    ""fbr"": 2,
    ""pol"": ""v""
  },
  ""model"": {
    ""pm"": 1,
    ""pe"": 2,
    ""ked"": 1,
    ""rel"": 90
  },
  ""environment"": {
    ""elevation"": 1,
    ""landcover"": 1,
    ""buildings"": 1,
    ""obstacles"": 0,
    ""clt"": ""Minimal.clt""
  },
  ""output"": {
    ""units"": ""m"",
    ""col"": ""LORA.dBm"",
    ""out"": 2,
    ""ber"": 1,
    ""mod"": 1,
    ""nf"": -124,
    ""res"": 30,
    ""rad"": 20
  }
}";
    }
}
