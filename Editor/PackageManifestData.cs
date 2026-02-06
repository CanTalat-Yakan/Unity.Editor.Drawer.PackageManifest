#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityEssentials
{
    [Serializable]
    public class PackageManifestData
    {
        public string name;
        public string version;
        public string displayName;
        public string description;
        public string unity;
        public string unityRelease;
        public Dictionary<string, string> dependencies = new();
        public List<string> keywords = new();
        public Author author = new Author();
        public string documentationUrl;
        public string changelogUrl;
        public string licensesUrl;
        public List<Sample> samples = new();
        public bool hideInEditor = true;

        public string ToJson()
        {
            var data = JsonConvert.SerializeObject(this, Formatting.Indented);
            return data;
        }

        [Serializable]
        public class Author
        {
            public string name;
            public string email;
            public string url;
        }

        [Serializable]
        public class Dependency
        {
            public string name;
            public string version;
        }

        [Serializable]
        public class Sample
        {
            public string displayName;
            public string description;
            public string path;
        }
    }
}
#endif