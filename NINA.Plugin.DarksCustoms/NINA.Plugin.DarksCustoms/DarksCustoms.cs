using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.DarksCustoms
{
    /// <summary>
    /// This class exports the IPlugin interface and will be used for the general plugin information and options
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class DarksCustoms : PluginBase
    {

        [ImportingConstructor]
        public DarksCustoms()
        {
        }
    }
}
