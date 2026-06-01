using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using BSI.MACE;
using BSI.MACE.PlugInNS;

namespace CloudRFPlugin
{
    public sealed class CloudRFPlugin : IMACEPlugIn
    {
        private IMACEPlugInHost _host;
        private CloudRFForm _form;

        public string Name
        {
            get { return "CloudRF"; }
        }

        public bool Initialize(IMACEPlugInHost host)
        {
            try
            {
                _host = host;
                _form = new CloudRFForm(_host);

                Icon icon = LoadCloudRfIcon();
                return _host.AddButton(this, "Info/Status Windows", "CloudRF", "Run CloudRF RF coverage and import GeoTIFF output", icon);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return false;
            }
        }

        public void Show()
        {
            try
            {
                if (_form == null || _form.IsDisposed)
                {
                    _form = new CloudRFForm(_host);
                }

                _form.Show();
                _form.BringToFront();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        public void Close()
        {
            try
            {
                _form?.Close();
                _form = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private static Icon LoadCloudRfIcon()
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CloudRFPlugin.Resources.CloudRF.ico"))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }

            return SystemIcons.Information;
        }
    }
}
