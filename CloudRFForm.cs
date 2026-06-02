using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BSI.MACE;
using BSI.MACE.PlugInNS;

namespace CloudRFPlugin
{
    internal sealed class CloudRFForm : Form
    {
        private const int DefaultRasterTransparency = 50;

        private readonly IMACEPlugInHost _host;
        private CloudRFSettings _settings;
        private CancellationTokenSource _runCancellation;
        private readonly List<string> _importedRasterLayerNames = new List<string>();

        private TextBox _apiKeyTextBox;
        private TextBox _baseUrlTextBox;
        private TextBox _templatePathTextBox;
        private TextBox _outputDirectoryTextBox;
        private CheckBox _autoImportCheckBox;
        private Label _selectedEntityLabel;
        private TextBox _advancedJsonTextBox;
        private TextBox _logTextBox;
        private Button _runButton;
        private Button _removeLastLayerButton;
        private LegendControl _legendControl;
        private PictureBox _downloadedLegendPictureBox;
        private ToolTip _toolTips;

        private NumericUpDown _frequencyMhz;
        private NumericUpDown _txPowerWatts;
        private NumericUpDown _bandwidthMhz;
        private NumericUpDown _txAntennaHeightM;
        private NumericUpDown _txGainDbi;
        private NumericUpDown _txLossDbi;
        private NumericUpDown _azimuthDeg;
        private NumericUpDown _tiltDeg;
        private NumericUpDown _horizontalBeamwidthDeg;
        private NumericUpDown _verticalBeamwidthDeg;
        private NumericUpDown _frontBackRatioDb;
        private ComboBox _antennaPattern;
        private ComboBox _polarization;
        private NumericUpDown _rxHeightM;
        private NumericUpDown _rxGainDbi;
        private NumericUpDown _rxSensitivityDbm;
        private NumericUpDown _radiusKm;
        private NumericUpDown _resolutionM;
        private NumericUpDown _noiseFloorDbm;
        private ComboBox _modulation;
        private ComboBox _bitErrorRate;
        private ComboBox _modelMode;
        private ComboBox _diffractionMode;
        private NumericUpDown _reliability;
        private ComboBox _colorSchema;
        private ComboBox _clutterProfile;
        private ComboBox _elevationModel;
        private ComboBox _landcoverMode;
        private ComboBox _buildingsMode;
        private ComboBox _obstaclesMode;

        public CloudRFForm(IMACEPlugInHost host)
        {
            _host = host;
            _settings = CloudRFSettings.Load();
            InitializeComponent();
            LoadSettingsIntoForm();
            RefreshSelectedEntity();
        }

        private void InitializeComponent()
        {
            Text = "CloudRF";
            Width = 1060;
            Height = 760;
            MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.CenterParent;
            _toolTips = new ToolTip { AutoPopDelay = 30000, InitialDelay = 500, ReshowDelay = 100, ShowAlways = true };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            Controls.Add(root);

            BuildCommandPanel(root);
            BuildTabs(root);
            BuildLog(root);
        }

        private void BuildCommandPanel(TableLayoutPanel root)
        {
            var commandPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            root.Controls.Add(commandPanel, 0, 0);

            var refreshButton = new Button { Text = "Set Transmitter", Width = 130, Height = 28 };
            refreshButton.Click += (sender, args) => RefreshSelectedEntity();
            commandPanel.Controls.Add(refreshButton);

            _runButton = new Button { Text = "Run Area Coverage", Width = 145, Height = 28 };
            _runButton.Click += async (sender, args) => await RunAreaCoverageAsync();
            commandPanel.Controls.Add(_runButton);

            _removeLastLayerButton = new Button { Text = "Remove Last Layer", Width = 135, Height = 28 };
            _removeLastLayerButton.Click += (sender, args) => RemoveLastImportedLayer();
            commandPanel.Controls.Add(_removeLastLayerButton);

            var saveButton = new Button { Text = "Save Settings", Width = 110, Height = 28 };
            saveButton.Click += (sender, args) => SaveSettingsFromForm();
            commandPanel.Controls.Add(saveButton);

            _selectedEntityLabel = new Label { AutoSize = true, Padding = new Padding(16, 7, 0, 0) };
            commandPanel.Controls.Add(_selectedEntityLabel);
        }

        private void BuildTabs(TableLayoutPanel root)
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            root.Controls.Add(tabs, 0, 1);

            var areaTab = new TabPage("Area");
            var settingsTab = new TabPage("Settings");
            var advancedTab = new TabPage("Advanced JSON");
            var legendTab = new TabPage("Legend");
            tabs.TabPages.Add(areaTab);
            tabs.TabPages.Add(settingsTab);
            tabs.TabPages.Add(advancedTab);
            tabs.TabPages.Add(legendTab);

            BuildAreaTab(areaTab);
            BuildSettingsTab(settingsTab);

            _advancedJsonTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font(FontFamily.GenericMonospace, 9f)
            };
            advancedTab.Controls.Add(_advancedJsonTextBox);

            BuildLegendTab(legendTab);
        }

        private void BuildSettingsTab(TabPage tab)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, RowCount = 5, Padding = new Padding(12), Height = 180 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            tab.Controls.Add(panel);

            AddLabeledTextBox(panel, "API key", out _apiKeyTextBox, 0, true);
            AddLabeledTextBox(panel, "Base URL", out _baseUrlTextBox, 1, false);
            AddLabeledTextBox(panel, "Template", out _templatePathTextBox, 2, false);
            AddBrowseButton(panel, "Browse", 2, BrowseTemplate);
            AddLabeledTextBox(panel, "Output folder", out _outputDirectoryTextBox, 3, false);
            AddBrowseButton(panel, "Browse", 3, BrowseOutputDirectory);

            _autoImportCheckBox = new CheckBox { Text = "Import GeoTIFF into MACE after download", Dock = DockStyle.Fill, Checked = true };
            panel.Controls.Add(new Label(), 0, 4);
            panel.Controls.Add(_autoImportCheckBox, 1, 4);
        }

        private void BuildAreaTab(TabPage tab)
        {
            var scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = SystemColors.Control };
            tab.Controls.Add(scrollPanel);

            var main = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, RowCount = 2, Padding = new Padding(8) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 275));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 255));
            scrollPanel.Controls.Add(main);

            var transmitter = CreateGroup("Transmitter");
            _frequencyMhz = AddNumber(transmitter, "Frequency (MHz)", 0, 100000, 868, 3);
            _txPowerWatts = AddNumber(transmitter, "Power (W)", 0, 100000, 10, 3);
            _bandwidthMhz = AddNumber(transmitter, "Bandwidth (MHz)", 0, 10000, 0.025m, 4);
            _txAntennaHeightM = AddNumber(transmitter, "Antenna height (m)", 0, 100000, 2, 2);
            AddFixedGroup(main, transmitter, 0, 0);

            var receiver = CreateGroup("Receiver");
            _rxHeightM = AddNumber(receiver, "Height (m)", 0, 100000, 1, 2);
            _rxGainDbi = AddNumber(receiver, "Gain (dBi)", -100, 100, 1, 1);
            _rxSensitivityDbm = AddNumber(receiver, "Sensitivity (dBm)", -200, 0, -90, 0);
            AddFixedGroup(main, receiver, 1, 0);

            var antenna = CreateGroup("Antenna");
            _txGainDbi = AddNumber(antenna, "Gain (dBi)", -100, 100, 2, 1);
            _txLossDbi = AddNumber(antenna, "Loss (dB)", 0, 100, 0, 1);
            _azimuthDeg = AddNumber(antenna, "Azimuth (deg)", 0, 360, 0, 0);
            _tiltDeg = AddNumber(antenna, "Tilt (deg)", -90, 90, 0, 0);
            _horizontalBeamwidthDeg = AddNumber(antenna, "Horizontal beamwidth", 1, 360, 90, 0);
            _verticalBeamwidthDeg = AddNumber(antenna, "Vertical beamwidth", 1, 360, 90, 0);
            _frontBackRatioDb = AddNumber(antenna, "Front/back ratio (dB)", 0, 100, 2, 0);
            _antennaPattern = AddOptionCombo(antenna, "Pattern", AntennaPatternChoices());
            SetToolTip(_antennaPattern, "CloudRF antenna pattern ID. DIPOLE.adf is the Web UI default seen in the KMZ request.");
            _polarization = AddOptionCombo(antenna, "Polarisation", PolarisationChoices());
            AddFixedGroup(main, antenna, 2, 0);

            var model = CreateGroup("Model");
            _modelMode = AddOptionCombo(model, "Propagation model", PropagationModelChoices());
            SetToolTip(_modelMode, PropagationModelToolTip());
            _diffractionMode = AddOptionCombo(model, "Diffraction", OffOnChoices());
            SetToolTip(_diffractionMode, "Knife-edge diffraction. OFF matches the Web UI KMZ request field model.ked = 0.");
            _reliability = AddNumber(model, "Reliability (%)", 1, 99, 90, 0);
            SetToolTip(_reliability, "Reliability/time percentage used by supported models such as ITM. Higher values are more conservative.");
            AddFixedGroup(main, model, 0, 1);

            var output = CreateGroup("Output");
            _radiusKm = AddNumber(output, "Radius (km)", 1, 1000, 20, 1);
            _resolutionM = AddNumber(output, "Resolution (m)", 1, 10000, 30, 0);
            _noiseFloorDbm = AddNumber(output, "Noise floor (dBm)", -200, 0, -124, 0);
            _modulation = AddOptionCombo(output, "Modulation", ModulationChoices());
            _modulation.SelectedIndexChanged += (sender, args) => RefreshBitErrorRateChoices();
            _bitErrorRate = AddOptionCombo(output, "Bit error rate", BitErrorRateChoices(false));
            _colorSchema = AddCombo(output, "Colour schema", new[] { "LORA.dBm", "RAINBOW.dBm", "GREEN.dBm", "CELL.dBm" });
            _colorSchema.SelectedIndexChanged += (sender, args) => _legendControl.SchemaName = Convert.ToString(_colorSchema.SelectedItem);
            AddFixedGroup(main, output, 1, 1);

            var environment = CreateGroup("Environment");
            _clutterProfile = AddCombo(environment, "Profile", new[] { "Minimal.clt" });
            SetToolTip(_clutterProfile, "CloudRF clutter profile name. Minimal.clt is the default; paid/custom accounts may use their own saved profile names.");
            _elevationModel = AddOptionCombo(environment, "Elevation", ElevationChoices());
            SetToolTip(_elevationModel, "Surface / DSM uses surface or LiDAR heights when available. Terrain / DTM uses bare earth ground heights.");
            _landcoverMode = AddOptionCombo(environment, "Land cover", OffOnChoices());
            SetToolTip(_landcoverMode, "ON adds CloudRF system 10m land cover clutter. CloudRF may adjust this for large radius or coarse resolution requests.");
            _buildingsMode = AddOptionCombo(environment, "Buildings", OffOnChoices());
            SetToolTip(_buildingsMode, "ON adds CloudRF building data where available. CloudRF may adjust this for large radius or coarse resolution requests.");
            _obstaclesMode = AddOptionCombo(environment, "My obstacles", OffOnChoices());
            SetToolTip(_obstaclesMode, "ON includes custom obstacles saved in the CloudRF account, placed on the selected elevation model.");
            AddFixedGroup(main, environment, 2, 1);
        }

        private static void AddFixedGroup(TableLayoutPanel main, GroupBuilder group, int column, int row)
        {
            group.GroupBox.Width = 315;
            if (row == 0 && column == 2)
            {
                group.GroupBox.Height = 260;
            }
            else
            {
                group.GroupBox.Height = row == 0 ? 132 + (group.RowCount * 24) : 132 + (group.RowCount * 24);
            }
            group.GroupBox.Dock = DockStyle.None;
            group.GroupBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            group.GroupBox.Margin = new Padding(6);
            main.Controls.Add(group.GroupBox, column, row);
        }

        private void BuildLegendTab(TabPage tab)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(8) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            tab.Controls.Add(panel);

            _legendControl = new LegendControl { Dock = DockStyle.Fill, SchemaName = "LORA.dBm" };
            panel.Controls.Add(_legendControl, 0, 0);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(right, 1, 0);
            right.Controls.Add(new Label { Text = "CloudRF chart image, when returned by the API", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

            _downloadedLegendPictureBox = new PictureBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            right.Controls.Add(_downloadedLegendPictureBox, 0, 1);
        }

        private void BuildLog(TableLayoutPanel root)
        {
            _logTextBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font(FontFamily.GenericMonospace, 9f) };
            root.Controls.Add(_logTextBox, 0, 2);
        }

        private static GroupBuilder CreateGroup(string title)
        {
            var groupBox = new GroupBox { Text = title, Dock = DockStyle.None, Padding = new Padding(10) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = false, Padding = new Padding(0, 6, 6, 6) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            groupBox.Controls.Add(table);
            return new GroupBuilder { GroupBox = groupBox, Table = table };
        }

        private static NumericUpDown AddNumber(GroupBuilder group, string label, decimal minimum, decimal maximum, decimal value, int decimals)
        {
            int row = group.Table.RowCount++;
            group.Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            group.Table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Margin = new Padding(3), TextAlign = ContentAlignment.MiddleLeft }, 0, row);

            var control = new NumericUpDown
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimals,
                Increment = decimals > 0 ? 0.1m : 1m,
                Value = Math.Min(maximum, Math.Max(minimum, value)),
                Height = 20,
                Margin = new Padding(3, 1, 3, 1)
            };
            group.Table.Controls.Add(control, 1, row);
            return control;
        }

        private static ComboBox AddCombo(GroupBuilder group, string label, string[] values)
        {
            int row = group.Table.RowCount++;
            group.Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            group.Table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Margin = new Padding(3), TextAlign = ContentAlignment.MiddleLeft }, 0, row);

            var control = new ComboBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, DropDownStyle = ComboBoxStyle.DropDown, Height = 20, Margin = new Padding(3, 1, 3, 1) };
            control.Items.AddRange(values);
            control.SelectedIndex = 0;
            group.Table.Controls.Add(control, 1, row);
            return control;
        }

        private static ComboBox AddOptionCombo(GroupBuilder group, string label, List<OptionItem> values)
        {
            int row = group.Table.RowCount++;
            group.Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            group.Table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Margin = new Padding(3), TextAlign = ContentAlignment.MiddleLeft }, 0, row);

            var control = new ComboBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, DropDownStyle = ComboBoxStyle.DropDownList, Height = 20, Margin = new Padding(3, 1, 3, 1), DropDownWidth = 260 };
            control.Items.AddRange(values.ToArray());
            if (control.Items.Count > 0)
            {
                control.SelectedIndex = 0;
            }
            group.Table.Controls.Add(control, 1, row);
            return control;
        }

        private static CheckBox AddCheck(GroupBuilder group, string label, bool value)
        {
            int row = group.Table.RowCount++;
            group.Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            var control = new CheckBox { Text = label, Dock = DockStyle.Fill, Checked = value, Margin = new Padding(3, 1, 3, 1) };
            group.Table.SetColumnSpan(control, 2);
            group.Table.Controls.Add(control, 0, row);
            return control;
        }

        private static void AddStaticLabel(GroupBuilder group, string text)
        {
            int row = group.Table.RowCount++;
            group.Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            var label = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            group.Table.SetColumnSpan(label, 2);
            group.Table.Controls.Add(label, 0, row);
        }

        private static void AddLabeledTextBox(TableLayoutPanel panel, string label, out TextBox textBox, int row, bool password)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            textBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = password };
            panel.Controls.Add(textBox, 1, row);
        }

        private static void AddBrowseButton(TableLayoutPanel panel, string text, int row, Action action)
        {
            var button = new Button { Text = text, Dock = DockStyle.Fill };
            button.Click += (sender, args) => action();
            panel.Controls.Add(button, 2, row);
        }

        private static List<OptionItem> PropagationModelChoices()
        {
            return new List<OptionItem>
            {
                new OptionItem(1, "ITM / Longley-Rice"),
                new OptionItem(2, "Line of Sight"),
                new OptionItem(3, "Okumura-Hata"),
                new OptionItem(4, "ECC33"),
                new OptionItem(5, "SUI Microwave"),
                new OptionItem(6, "COST231"),
                new OptionItem(7, "Free space path loss"),
                new OptionItem(8, "RADAR"),
                new OptionItem(9, "Ericsson 9999"),
                new OptionItem(10, "Plane earth loss"),
                new OptionItem(11, "Egli VHF/UHF")
            };
        }

        private static string PropagationModelToolTip()
        {
            return "ITM / Longley-Rice: general-purpose terrain model, 20-20,000 MHz." + Environment.NewLine +
                   "Line of Sight: obstruction visibility model; useful for microwave LOS checks." + Environment.NewLine +
                   "Okumura-Hata: urban cellular model, 150-1500 MHz; assumes elevated base stations." + Environment.NewLine +
                   "ECC33: cellular/microwave model, 700-3500 MHz." + Environment.NewLine +
                   "SUI Microwave: WiMAX/microwave model, 1900-11000 MHz." + Environment.NewLine +
                   "COST231: Hata extension for urban systems, 150-2000 MHz." + Environment.NewLine +
                   "Free space path loss: ideal clear-path model with no clutter unless diffraction is added." + Environment.NewLine +
                   "RADAR: radar-oriented model." + Environment.NewLine +
                   "Ericsson 9999: cellular model up to about 1900 MHz." + Environment.NewLine +
                   "Plane earth loss: two-ray/plane-earth style loss model." + Environment.NewLine +
                   "Egli VHF/UHF: rural VHF/UHF model, 30-1000 MHz; more conservative than FSPL.";
        }

        private static List<OptionItem> ModulationChoices()
        {
            return new List<OptionItem>
            {
                new OptionItem(0, "Not set / Web UI default"),
                new OptionItem(1, "4QAM"),
                new OptionItem(2, "16QAM"),
                new OptionItem(3, "64QAM"),
                new OptionItem(4, "256QAM"),
                new OptionItem(5, "1024QAM"),
                new OptionItem(6, "BPSK"),
                new OptionItem(7, "QPSK"),
                new OptionItem(8, "8PSK"),
                new OptionItem(9, "16PSK"),
                new OptionItem(10, "32PSK"),
                new OptionItem(11, "LoRa")
            };
        }

        private static List<OptionItem> AntennaPatternChoices()
        {
            return new List<OptionItem>
            {
                new OptionItem(1, "DIPOLE.adf"),
                new OptionItem(0, "Custom / no pattern")
            };
        }

        private static List<OptionItem> PolarisationChoices()
        {
            return new List<OptionItem>
            {
                new OptionItem("v", "Vertical"),
                new OptionItem("h", "Horizontal"),
                new OptionItem("m", "Mixed"),
                new OptionItem("r", "Right circular"),
                new OptionItem("l", "Left circular")
            };
        }

        private static List<OptionItem> BitErrorRateChoices(bool lora)
        {
            if (lora)
            {
                return new List<OptionItem>
                {
                    new OptionItem(0, "Not set / Web UI default"),
                    new OptionItem(7, "SF7"),
                    new OptionItem(8, "SF8"),
                    new OptionItem(9, "SF9"),
                    new OptionItem(10, "SF10"),
                    new OptionItem(11, "SF11"),
                    new OptionItem(12, "SF12")
                };
            }

            return new List<OptionItem>
            {
                new OptionItem(0, "Not set / Web UI default"),
                new OptionItem(1, "0.1"),
                new OptionItem(2, "0.01"),
                new OptionItem(3, "0.001"),
                new OptionItem(4, "0.0001"),
                new OptionItem(5, "0.00001"),
                new OptionItem(6, "0.000001")
            };
        }

        private static List<OptionItem> ElevationChoices()
        {
            return new List<OptionItem>
            {
                new OptionItem(1, "Surface / DSM"),
                new OptionItem(2, "Terrain / DTM")
            };
        }

        private static List<OptionItem> OffOnChoices()
        {
            return new List<OptionItem>
            {
                new OptionItem(0, "OFF"),
                new OptionItem(1, "ON")
            };
        }

        private void RefreshBitErrorRateChoices()
        {
            int previousValue = GetSelectedOptionValue(_bitErrorRate, 0);
            int modulation = GetSelectedOptionValue(_modulation, 0);
            bool lora = modulation == 11;
            _bitErrorRate.Items.Clear();
            _bitErrorRate.Items.AddRange(BitErrorRateChoices(lora).ToArray());
            SelectOptionValue(_bitErrorRate, previousValue);

            int selectedValue = GetSelectedOptionValue(_bitErrorRate, 0);
            if (modulation == 0 || (lora && selectedValue > 0 && selectedValue < 7) || (!lora && selectedValue > 6))
            {
                _bitErrorRate.SelectedIndex = 0;
            }
        }

        private static void SelectOptionValue(ComboBox comboBox, int value)
        {
            if (comboBox == null)
            {
                return;
            }

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                var item = comboBox.Items[i] as OptionItem;
                if (item != null && item.Value == value)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static void SelectOptionValue(ComboBox comboBox, string code)
        {
            if (comboBox == null)
            {
                return;
            }

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                var item = comboBox.Items[i] as OptionItem;
                if (item != null && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static int GetSelectedOptionValue(ComboBox comboBox, int fallback)
        {
            if (comboBox?.SelectedItem is OptionItem item)
            {
                return item.Value;
            }

            return fallback;
        }

        private static string GetSelectedOptionCode(ComboBox comboBox, string fallback)
        {
            if (comboBox?.SelectedItem is OptionItem item && !string.IsNullOrWhiteSpace(item.Code))
            {
                return item.Code;
            }

            return fallback;
        }

        private void SetToolTip(Control control, string text)
        {
            if (_toolTips != null && control != null)
            {
                _toolTips.SetToolTip(control, text);
            }
        }

        private void LoadSettingsIntoForm()
        {
            _apiKeyTextBox.Text = _settings.ApiKey ?? "";
            _baseUrlTextBox.Text = _settings.BaseUrl ?? "https://api.cloudrf.com";
            _templatePathTextBox.Text = _settings.TemplatePath ?? CloudRFSettings.DefaultTemplatePath;
            _outputDirectoryTextBox.Text = _settings.OutputDirectory ?? CloudRFSettings.DefaultOutputDirectory;
            _autoImportCheckBox.Checked = _settings.AutoImportGeoTiff;
            LoadTemplatePreview();
        }

        private void SaveSettingsFromForm()
        {
            _settings.ApiKey = _apiKeyTextBox.Text.Trim();
            _settings.BaseUrl = _baseUrlTextBox.Text.Trim();
            _settings.TemplatePath = _templatePathTextBox.Text.Trim();
            _settings.OutputDirectory = _outputDirectoryTextBox.Text.Trim();
            _settings.AutoImportGeoTiff = _autoImportCheckBox.Checked;
            _settings.Save();
            Log("Settings saved.");
        }

        private void BrowseTemplate()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON templates (*.json)|*.json|All files (*.*)|*.*";
                dialog.FileName = _templatePathTextBox.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _templatePathTextBox.Text = dialog.FileName;
                    LoadTemplatePreview();
                }
            }
        }

        private void BrowseOutputDirectory()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = Directory.Exists(_outputDirectoryTextBox.Text) ? _outputDirectoryTextBox.Text : CloudRFSettings.DefaultOutputDirectory;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _outputDirectoryTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void LoadTemplatePreview()
        {
            try
            {
                if (!File.Exists(_templatePathTextBox.Text))
                {
                    return;
                }

                string json = File.ReadAllText(_templatePathTextBox.Text);
                _advancedJsonTextBox.Text = JsonTools.PrettyPrint(json);
                LoadControlsFromJson(json);
            }
            catch (Exception ex)
            {
                _advancedJsonTextBox.Text = ex.Message;
            }
        }

        private void LoadControlsFromJson(string json)
        {
            var root = JsonTools.DeserializeObject(json);
            var transmitter = JsonTools.GetObject(root, "transmitter");
            var receiver = JsonTools.GetObject(root, "receiver");
            var antenna = JsonTools.GetObject(root, "antenna");
            var output = JsonTools.GetObject(root, "output");
            var environment = JsonTools.GetObject(root, "environment");
            var model = JsonTools.GetObject(root, "model");

            SetNumber(_frequencyMhz, GetDecimal(transmitter, "frq", _frequencyMhz.Value));
            SetNumber(_txPowerWatts, GetDecimal(transmitter, "txw", _txPowerWatts.Value));
            SetNumber(_bandwidthMhz, GetDecimal(transmitter, "bwi", _bandwidthMhz.Value));
            SetNumber(_txAntennaHeightM, GetDecimal(transmitter, "alt", _txAntennaHeightM.Value));
            SetNumber(_rxHeightM, GetDecimal(receiver, "alt", _rxHeightM.Value));
            SetNumber(_rxGainDbi, GetDecimal(receiver, "rxg", _rxGainDbi.Value));
            SetNumber(_rxSensitivityDbm, GetDecimal(receiver, "rxs", _rxSensitivityDbm.Value));
            SetNumber(_txGainDbi, GetDecimal(antenna, "txg", _txGainDbi.Value));
            SetNumber(_txLossDbi, GetDecimal(antenna, "txl", _txLossDbi.Value));
            SetNumber(_azimuthDeg, GetDecimal(antenna, "azi", _azimuthDeg.Value));
            SetNumber(_tiltDeg, GetDecimal(antenna, "tlt", _tiltDeg.Value));
            SetNumber(_horizontalBeamwidthDeg, GetDecimal(antenna, "hbw", _horizontalBeamwidthDeg.Value));
            SetNumber(_verticalBeamwidthDeg, GetDecimal(antenna, "vbw", _verticalBeamwidthDeg.Value));
            SetNumber(_frontBackRatioDb, GetDecimal(antenna, "fbr", _frontBackRatioDb.Value));
            SelectOptionValue(_antennaPattern, GetInt(antenna, "ant", 1));
            SelectOptionValue(_polarization, JsonTools.GetString(antenna, "pol", "v"));
            SetNumber(_radiusKm, GetDecimal(output, "rad", _radiusKm.Value));
            SetNumber(_resolutionM, GetDecimal(output, "res", _resolutionM.Value));
            SetNumber(_noiseFloorDbm, GetDecimal(output, "nf", _noiseFloorDbm.Value));
            SelectOptionValue(_modulation, GetInt(output, "mod", 0));
            RefreshBitErrorRateChoices();
            SelectOptionValue(_bitErrorRate, GetInt(output, "ber", 0));
            SelectOptionValue(_modelMode, GetInt(model, "pm", 1));
            SelectOptionValue(_diffractionMode, GetInt(model, "ked", 0));
            SetNumber(_reliability, GetDecimal(model, "rel", _reliability.Value));
            _colorSchema.Text = JsonTools.GetString(output, "col", "LORA.dBm");
            _legendControl.SchemaName = _colorSchema.Text;
            _clutterProfile.Text = JsonTools.GetString(environment, "clt", "Minimal.clt");
            SelectOptionValue(_elevationModel, GetInt(environment, "elevation", 1));
            SelectOptionValue(_landcoverMode, GetInt(environment, "landcover", 1));
            SelectOptionValue(_buildingsMode, GetInt(environment, "buildings", 1));
            SelectOptionValue(_obstaclesMode, GetInt(environment, "obstacles", 0));
        }

        private void RefreshSelectedEntity()
        {
            IPhysicalEntity entity = GetSelectedEntity();
            if (entity == null)
            {
                _selectedEntityLabel.Text = "Selected transmitter: none";
                return;
            }

            IGeoPoint position = entity.Position;
            decimal height = Convert.ToDecimal(entity.AntennaHeight_m > 0 ? entity.AntennaHeight_m : Math.Max(1.0, position.AltitudeAGL_meters));
            SetNumber(_txAntennaHeightM, height);
            _selectedEntityLabel.Text = string.Format(CultureInfo.InvariantCulture, "Selected transmitter: {0} ({1:0.000000}, {2:0.000000}, {3:0.0}m AGL)", entity.Name, position.Latitude_degrees, position.Longitude_degrees, height);
        }

        private IPhysicalEntity GetSelectedEntity()
        {
            return _host?.Mission?.Map?.SelectedEntity;
        }

        private async Task RunAreaCoverageAsync()
        {
            IPhysicalEntity entity = GetSelectedEntity();
            if (entity == null)
            {
                MessageBox.Show(this, "Select a MACE platform/entity on the map first.", "CloudRF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveSettingsFromForm();
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                MessageBox.Show(this, "Enter your CloudRF API key first.", "CloudRF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _runButton.Enabled = false;
            _runCancellation = new CancellationTokenSource();

            try
            {
                string requestJson = BuildRequestJson(entity);
                Directory.CreateDirectory(_settings.OutputDirectory);
                File.WriteAllText(Path.Combine(_settings.OutputDirectory, "last-area-request.json"), requestJson);

                var client = new CloudRFClient(_settings);
                string baseName = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-CloudRF-" + entity.Name;

                Log("Posting area calculation to CloudRF...");
                CloudRFAreaResult result = await client.RunAreaAsync(requestJson, _runCancellation.Token);
                File.WriteAllText(Path.Combine(_settings.OutputDirectory, baseName + ".response.json"), result.RawJson);
                _legendControl.SetCloudRFLegend(result.LegendEntries);

                Log("Downloading GeoTIFF...");
                string geoTiffPath = await client.DownloadGeoTiffAsync(result, baseName, _runCancellation.Token);
                Log("GeoTIFF saved: " + geoTiffPath);

                SaveTextLegend(baseName);
                string legendPath = await client.DownloadLegendAsync(result, baseName, _runCancellation.Token);
                if (!string.IsNullOrWhiteSpace(legendPath))
                {
                    _downloadedLegendPictureBox.ImageLocation = legendPath;
                    Log("CloudRF legend saved: " + legendPath);
                }

                if (_settings.AutoImportGeoTiff)
                {
                    List<string> beforeImport = CaptureRasterLayerNames();
                    bool loaded = _host.Mission.Map.LayerManager.AddRasterLayerFromFile(geoTiffPath);
                    TrackImportedRasterLayers(beforeImport, geoTiffPath, loaded);
                    Log(loaded ? "MACE raster layer imported." : "MACE raster import returned false.");
                    _host.DisplayNotification("CloudRF", loaded ? "Coverage GeoTIFF loaded into MACE." : "GeoTIFF downloaded, but MACE did not load it.");
                }
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.Message, "CloudRF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _runButton.Enabled = true;
                _runCancellation.Dispose();
                _runCancellation = null;
            }
        }

        private List<string> CaptureRasterLayerNames()
        {
            var names = new List<string>();
            try
            {
                foreach (IRasterLayer layer in _host.Mission.Map.LayerManager.RasterLayers)
                {
                    if (layer != null && !string.IsNullOrWhiteSpace(layer.LayerName))
                    {
                        names.Add(layer.LayerName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Could not read current raster layers: " + ex.Message);
            }

            return names;
        }

        private void TrackImportedRasterLayers(List<string> beforeImport, string geoTiffPath, bool loaded)
        {
            if (!loaded)
            {
                return;
            }

            var afterImport = CaptureRasterLayerNames();
            var newLayerNames = new List<string>();
            foreach (string layerName in afterImport)
            {
                if (!beforeImport.Contains(layerName))
                {
                    newLayerNames.Add(layerName);
                }
            }

            if (newLayerNames.Count == 0)
            {
                string fileStem = Path.GetFileNameWithoutExtension(geoTiffPath);
                foreach (string layerName in afterImport)
                {
                    if (layerName.IndexOf(fileStem, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        newLayerNames.Add(layerName);
                    }
                }
            }

            foreach (string layerName in newLayerNames)
            {
                if (!_importedRasterLayerNames.Contains(layerName))
                {
                    _importedRasterLayerNames.Add(layerName);
                    ApplyDefaultTransparency(layerName);
                    Log("Tracked CloudRF layer: " + layerName);
                }
            }
        }

        private void ApplyDefaultTransparency(string layerName)
        {
            try
            {
                foreach (IRasterLayer layer in _host.Mission.Map.LayerManager.RasterLayers)
                {
                    if (layer != null && string.Equals(layer.LayerName, layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        layer.Transparency = DefaultRasterTransparency;
                        Log(string.Format(CultureInfo.InvariantCulture, "Set layer transparency to {0}%: {1}", DefaultRasterTransparency, layerName));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Could not set layer transparency: " + ex.Message);
            }
        }

        private void RemoveLastImportedLayer()
        {
            if (_importedRasterLayerNames.Count == 0)
            {
                Log("No CloudRF layers are currently tracked for removal.");
                _host.DisplayNotification("CloudRF", "No tracked CloudRF layer to remove.");
                return;
            }

            string layerName = _importedRasterLayerNames[_importedRasterLayerNames.Count - 1];
            try
            {
                bool removed = _host.Mission.Map.LayerManager.RemoveRasterLayer(layerName);
                if (removed)
                {
                    _importedRasterLayerNames.RemoveAt(_importedRasterLayerNames.Count - 1);
                    Log("Removed MACE raster layer: " + layerName);
                    _host.DisplayNotification("CloudRF", "Removed CloudRF layer from MACE.");
                }
                else
                {
                    Log("MACE did not remove raster layer: " + layerName);
                    _host.DisplayNotification("CloudRF", "MACE did not remove the tracked layer.");
                }
            }
            catch (Exception ex)
            {
                Log("ERROR removing raster layer: " + ex.Message);
                MessageBox.Show(this, ex.Message, "CloudRF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string BuildRequestJson(IPhysicalEntity entity)
        {
            var request = JsonTools.DeserializeObject(_advancedJsonTextBox.Text);
            var transmitter = JsonTools.GetObject(request, "transmitter");
            var receiver = JsonTools.GetObject(request, "receiver");
            var antenna = JsonTools.GetObject(request, "antenna");
            var output = JsonTools.GetObject(request, "output");
            var environment = JsonTools.GetObject(request, "environment");
            var model = JsonTools.GetObject(request, "model");

            IGeoPoint position = entity.Position;
            transmitter["lat"] = position.Latitude_degrees;
            transmitter["lon"] = position.Longitude_degrees;
            transmitter["alt"] = D(_txAntennaHeightM);
            transmitter["frq"] = D(_frequencyMhz);
            transmitter["txw"] = D(_txPowerWatts);
            transmitter["bwi"] = D(_bandwidthMhz);

            receiver["lat"] = 0;
            receiver["lon"] = 0;
            receiver["alt"] = D(_rxHeightM);
            receiver["rxg"] = D(_rxGainDbi);
            receiver["rxs"] = D(_rxSensitivityDbm);

            antenna["txg"] = D(_txGainDbi);
            antenna["txl"] = D(_txLossDbi);
            antenna["azi"] = D(_azimuthDeg);
            antenna["tlt"] = D(_tiltDeg);
            antenna["hbw"] = D(_horizontalBeamwidthDeg);
            antenna["vbw"] = D(_verticalBeamwidthDeg);
            antenna["fbr"] = D(_frontBackRatioDb);
            antenna["ant"] = GetSelectedOptionValue(_antennaPattern, 1);
            antenna["pol"] = GetSelectedOptionCode(_polarization, "v");

            model["pm"] = GetSelectedOptionValue(_modelMode, 1);
            model["ked"] = GetSelectedOptionValue(_diffractionMode, 0);
            model["rel"] = Convert.ToInt32(_reliability.Value);

            output["rad"] = D(_radiusKm);
            output["res"] = D(_resolutionM);
            output["nf"] = D(_noiseFloorDbm);
            int modulation = GetSelectedOptionValue(_modulation, 0);
            int bitErrorRate = GetSelectedOptionValue(_bitErrorRate, 0);
            output["mod"] = modulation == 0 ? null : (object)modulation;
            output["ber"] = bitErrorRate == 0 ? null : (object)bitErrorRate;
            output["col"] = _colorSchema.Text.Trim();

            environment["clt"] = string.IsNullOrWhiteSpace(_clutterProfile.Text) ? "Minimal.clt" : _clutterProfile.Text.Trim();
            environment["elevation"] = GetSelectedOptionValue(_elevationModel, 1);
            environment["landcover"] = GetSelectedOptionValue(_landcoverMode, 1);
            environment["buildings"] = GetSelectedOptionValue(_buildingsMode, 1);
            environment["obstacles"] = GetSelectedOptionValue(_obstaclesMode, 0);

            request["site"] = TruncateForCloudRF(entity.Name, 24);
            if (!request.ContainsKey("network") || string.IsNullOrWhiteSpace(Convert.ToString(request["network"], CultureInfo.InvariantCulture)))
            {
                request["network"] = "MACE";
            }

            string json = JsonTools.SerializeObject(request);
            _advancedJsonTextBox.Text = JsonTools.PrettyPrint(json);
            _legendControl.SchemaName = _colorSchema.Text;
            return json;
        }

        private void SaveTextLegend(string baseName)
        {
            string path = Path.Combine(_settings.OutputDirectory, baseName + "-legend.txt");
            File.WriteAllText(path, _legendControl.GetLegendText());
            Log("Legend saved: " + path);
        }

        private static decimal GetDecimal(Dictionary<string, object> map, string key, decimal fallback)
        {
            if (!map.TryGetValue(key, out object value) || value == null) return fallback;
            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result) ? result : fallback;
        }

        private static int GetInt(Dictionary<string, object> map, string key, int fallback)
        {
            if (!map.TryGetValue(key, out object value) || value == null) return fallback;
            return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        }

        private static void SetNumber(NumericUpDown control, decimal value)
        {
            control.Value = Math.Min(control.Maximum, Math.Max(control.Minimum, value));
        }

        private static double D(NumericUpDown control)
        {
            return Convert.ToDouble(control.Value);
        }

        private static string TruncateForCloudRF(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text)) return "MACE";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }

        private void Log(string message)
        {
            _logTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine);
        }

        private sealed class GroupBuilder
        {
            public GroupBox GroupBox { get; set; }
            public TableLayoutPanel Table { get; set; }
            public int RowCount => Table?.RowCount ?? 0;
        }

        private sealed class OptionItem
        {
            public int Value { get; }
            public string Code { get; }
            public string Text { get; }

            public OptionItem(int value, string text)
            {
                Value = value;
                Code = Convert.ToString(value, CultureInfo.InvariantCulture);
                Text = text;
            }

            public OptionItem(string code, string text)
            {
                Value = 0;
                Code = code;
                Text = text;
            }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class LegendControl : Control
        {
            private string _schemaName;
            private List<CloudRFLegendEntry> _cloudRfLegend = new List<CloudRFLegendEntry>();

            public string SchemaName
            {
                get { return _schemaName; }
                set { _schemaName = string.IsNullOrWhiteSpace(value) ? "LORA.dBm" : value; Invalidate(); }
            }

            public LegendControl()
            {
                DoubleBuffered = true;
                BackColor = Color.White;
            }

            public void SetCloudRFLegend(List<CloudRFLegendEntry> entries)
            {
                _cloudRfLegend = entries ?? new List<CloudRFLegendEntry>();
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.Clear(Color.White);
                using (var titleFont = new Font(Font.FontFamily, 10f, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.Black))
                {
                    g.DrawString("Coverage legend", titleFont, textBrush, 8, 8);
                    g.DrawString(SchemaName, Font, textBrush, 8, 30);
                    g.DrawString("Received power in dBm. Higher is stronger.", Font, textBrush, 8, 52);

                    if (_cloudRfLegend.Count > 0)
                    {
                        int y = 84;
                        foreach (CloudRFLegendEntry entry in _cloudRfLegend)
                        {
                            DrawRow(g, 8, y, Color.FromArgb(entry.R, entry.G, entry.B), entry.Label, "");
                            y += 22;

                            if (y > Height - 28)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        DrawRow(g, 8, 86, Color.FromArgb(49, 163, 84), ">= -70 dBm", "Strong");
                        DrawRow(g, 8, 116, Color.FromArgb(120, 198, 121), "-70 to -85 dBm", "Good");
                        DrawRow(g, 8, 146, Color.FromArgb(255, 237, 111), "-85 to -100 dBm", "Usable");
                        DrawRow(g, 8, 176, Color.FromArgb(253, 141, 60), "-100 to -115 dBm", "Marginal");
                        DrawRow(g, 8, 206, Color.FromArgb(215, 48, 39), "< -115 dBm", "Weak");
                        g.DrawString("Exact colours will update after a CloudRF response is received.", Font, textBrush, new RectangleF(8, 246, Width - 16, 60));
                    }
                }
            }

            private static void DrawRow(Graphics g, int x, int y, Color color, string range, string label)
            {
                using (var brush = new SolidBrush(color))
                using (var pen = new Pen(Color.FromArgb(90, 90, 90)))
                using (var textBrush = new SolidBrush(Color.Black))
                {
                    g.FillRectangle(brush, x, y, 34, 18);
                    g.DrawRectangle(pen, x, y, 34, 18);
                    g.DrawString(range + "  " + label, SystemFonts.DefaultFont, textBrush, x + 44, y + 2);
                }
            }

            public string GetLegendText()
            {
                if (_cloudRfLegend.Count > 0)
                {
                    var lines = new List<string>
                    {
                        "CloudRF coverage legend",
                        "Colour schema: " + SchemaName,
                        "Received power in dBm. Higher is stronger."
                    };

                    foreach (CloudRFLegendEntry entry in _cloudRfLegend)
                    {
                        lines.Add(string.Format(CultureInfo.InvariantCulture, "{0}: rgb({1},{2},{3})", entry.Label, entry.R, entry.G, entry.B));
                    }

                    return string.Join(Environment.NewLine, lines) + Environment.NewLine;
                }

                return "CloudRF coverage legend" + Environment.NewLine +
                       "Colour schema: " + SchemaName + Environment.NewLine +
                       "Received power in dBm. Higher is stronger." + Environment.NewLine +
                       ">= -70 dBm: Strong" + Environment.NewLine +
                       "-70 to -85 dBm: Good" + Environment.NewLine +
                       "-85 to -100 dBm: Usable" + Environment.NewLine +
                       "-100 to -115 dBm: Marginal" + Environment.NewLine +
                       "< -115 dBm: Weak" + Environment.NewLine;
            }
        }
    }
}
