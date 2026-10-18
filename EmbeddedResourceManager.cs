using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;

namespace PesPluginLoaderInstaller
{
    // Reads translations embedded in the main assembly (Resource.xx.resources) instead of
    // satellite assemblies, so the installer stays a single exe
    public class EmbeddedResourceManager : ResourceManager
    {
        readonly Dictionary<string, ResourceSet> cache = new Dictionary<string, ResourceSet>();

        public EmbeddedResourceManager(string baseName, Assembly assembly) : base(baseName, assembly)
        {
        }

        public static void Install()
        {
            FieldInfo field = typeof(Resource).GetField("resourceMan", BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, new EmbeddedResourceManager("PesPluginLoaderInstaller.Resource", typeof(Resource).Assembly));
        }

        protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
        {
            for (CultureInfo c = culture; c != null && !c.Equals(CultureInfo.InvariantCulture); c = c.Parent)
            {
                lock (cache)
                {
                    if (!cache.TryGetValue(c.Name, out ResourceSet set))
                    {
                        Stream stream = MainAssembly.GetManifestResourceStream($"{BaseNameField}.{c.Name}.resources");
                        set = stream != null ? new ResourceSet(stream) : null;
                        cache[c.Name] = set;
                    }
                    if (set != null)
                        return set;
                }
            }
            return base.InternalGetResourceSet(CultureInfo.InvariantCulture, createIfNotExists, tryParents);
        }
    }
}
