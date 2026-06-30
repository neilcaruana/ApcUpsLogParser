using ApcUpsLogParser.Common.Models;
using ApcUpsLogParser.Common.Services;
using ApcUpsLogParser.Common.Configuration;
using ScottPlot;
using ScottPlot.WinForms;
using SystemColor = System.Drawing.Color;
using SystemLabel = System.Windows.Forms.Label;
using SystemFont = System.Drawing.Font;
using SystemFontStyle = System.Drawing.FontStyle;

namespace ApcUpsLogParser.UI
{
    public class MainForm : Form
    {
        private System.Windows.Forms.Timer? _updateTimer;
        private ToolTip? _tooltip;
        private List<LogEntry>? _currentEntries;
        private List<LogEntry>? _previousEntries;
        private DateTime _lastRefreshTime;
        private FormsPlot? _formsPlot;

        // Settings controls
        private CheckBox _liveCheckBox;
        private NumericUpDown _daysNumeric;
        private CheckBox _todayCheckBox;
        private CheckBox _compareCheckBox;
        private CheckBox _hideGapsCheckBox;
        private NumericUpDown _smoothNumeric;

        // Status controls
        private SystemLabel _statusLabel;
        private SystemLabel _refreshIndicator;

        // Settings from common configuration
        private bool _isInitialized = false;

        // Theme colors - Light theme
        private static readonly SystemColor DarkBackground = SystemColor.FromArgb(240, 240, 240);
        private static readonly SystemColor MediumBackground = SystemColor.FromArgb(230, 230, 230);
        private static readonly SystemColor LightBackground = SystemColor.FromArgb(220, 220, 220);
        private static readonly SystemColor AccentColor = SystemColor.FromArgb(0, 120, 215);
        private static readonly SystemColor TextColor = SystemColor.FromArgb(50, 50, 50);
        private static readonly SystemColor ControlBackground = SystemColor.FromArgb(255, 255, 255);
        private static readonly SystemColor BorderColor = SystemColor.FromArgb(160, 160, 160);

        // Data storage for comparison
        private List<LogEntry>? _todayEntries;
        private List<LogEntry>? _yesterdayEntries;

        public MainForm()
        {
            EnableEmojiSupport();
            InitializeComponent();
            SetDefaultValues();
            _isInitialized = true;
            LoadData();
        }

        private void EnableEmojiSupport()
        {
            this.Font = new SystemFont("Segoe UI Emoji", 9F, SystemFontStyle.Regular, GraphicsUnit.Point);
        }

        private void InitializeComponent()
        {
            Text = "APC UPS Log Parser";
            WindowState = FormWindowState.Maximized;
            BackColor = DarkBackground;
            ForeColor = TextColor;
            
            var mainPanel = new Panel 
            { 
                Dock = DockStyle.Fill,
                BackColor = DarkBackground
            };
            
            // Create settings panel
            var settingsPanel = CreateSettingsPanel();
            
            // Create title and status panel
            var titleStatusPanel = CreateTitleStatusPanel();
            
            // Create ScottPlot chart
            _formsPlot = new FormsPlot 
            { 
                Dock = DockStyle.Fill,
                BackColor = MediumBackground
            };
            
            mainPanel.Controls.Add(_formsPlot);
            mainPanel.Controls.Add(titleStatusPanel);
            mainPanel.Controls.Add(settingsPanel);
            Controls.Add(mainPanel);

            _tooltip = new ToolTip 
            { 
                AutoPopDelay = 5000, 
                InitialDelay = 500, 
                ReshowDelay = 100, 
                ShowAlways = true,
                BackColor = ControlBackground,
                ForeColor = TextColor
            };
            
            SetupEventHandlers();
            ApplyPlotTheme();
        }

        private Panel CreateSettingsPanel()
        {
            var settingsPanel = new Panel 
            { 
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = MediumBackground,
                Padding = new Padding(15, 5, 15, 5)
            };

            var mainRow = new FlowLayoutPanel 
            { 
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = SystemColor.Transparent
            };

            // Live mode
            _liveCheckBox = CreateStyledCheckBox("Live", new Padding(0, 15, 20, 0));
            _liveCheckBox.CheckedChanged += LiveCheckBox_CheckedChanged;
            mainRow.Controls.Add(_liveCheckBox);

            // Days filter
            var lastLabel = CreateStyledLabel("Last:", new Padding(0, 15, 5, 0));
            mainRow.Controls.Add(lastLabel);
            
            _daysNumeric = CreateStyledNumericUpDown(1, 365, 1, 60, new Padding(0, 12, 5, 0));
            _daysNumeric.ValueChanged += SettingsChanged;
            mainRow.Controls.Add(_daysNumeric);
            
            var daysLabel = CreateStyledLabel("days", new Padding(0, 15, 20, 0));
            mainRow.Controls.Add(daysLabel);

            // Today only
            _todayCheckBox = CreateStyledCheckBox("Today", new Padding(0, 15, 20, 0));
            _todayCheckBox.Checked = true;
            _todayCheckBox.CheckedChanged += TodayCheckBox_CheckedChanged;
            mainRow.Controls.Add(_todayCheckBox);

            // Comparison
            _compareCheckBox = CreateStyledCheckBox("Compare", new Padding(0, 15, 20, 0));
            _compareCheckBox.CheckedChanged += CompareCheckBox_CheckedChanged;
            mainRow.Controls.Add(_compareCheckBox);

            // Smoothing
            var smoothLabel = CreateStyledLabel("Smooth:", new Padding(0, 15, 5, 0));
            mainRow.Controls.Add(smoothLabel);
            
            _smoothNumeric = CreateStyledNumericUpDown(0, 999, 75, 60, new Padding(0, 12, 5, 0));
            _smoothNumeric.ValueChanged += SettingsChanged;
            mainRow.Controls.Add(_smoothNumeric);

            // Hide gaps
            _hideGapsCheckBox = CreateStyledCheckBox("Hide Gaps", new Padding(0, 15, 20, 0));
            _hideGapsCheckBox.CheckedChanged += SettingsChanged;
            mainRow.Controls.Add(_hideGapsCheckBox);

            settingsPanel.Controls.Add(mainRow);
            
            return settingsPanel;
        }

        private CheckBox CreateStyledCheckBox(string text, Padding margin)
        {
            var checkBox = new CheckBox
            {
                Text = text,
                AutoSize = true,
                ForeColor = TextColor,
                BackColor = SystemColor.Transparent,
                Margin = margin,
                FlatStyle = FlatStyle.Standard,
                Font = new SystemFont("Segoe UI Emoji", 9F)
            };
            
            checkBox.FlatAppearance.CheckedBackColor = AccentColor;
            checkBox.FlatAppearance.BorderColor = BorderColor;
            
            return checkBox;
        }

        private SystemLabel CreateStyledLabel(string text, Padding margin)
        {
            return new SystemLabel
            {
                Text = text,
                AutoSize = true,
                ForeColor = TextColor,
                BackColor = SystemColor.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = margin,
                Font = new SystemFont("Segoe UI Emoji", 9F)
            };
        }

        private NumericUpDown CreateStyledNumericUpDown(decimal min, decimal max, decimal value, int width, Padding margin)
        {
            var numeric = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                Width = width,
                Margin = margin,
                BackColor = ControlBackground,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new SystemFont("Segoe UI", 9F)
            };
            return numeric;
        }

        private Panel CreateTitleStatusPanel()
        {
            var titleStatusPanel = new Panel 
            { 
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = LightBackground,
                Padding = new Padding(5, 5, 5, 5)
            };

            var titleLabel = new SystemLabel
            {
                Text = "APC UPS Voltage Monitor",
                Font = new SystemFont("Segoe UI Emoji", 16, SystemFontStyle.Bold),
                AutoSize = false,
                Height = 50,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = SystemColor.Transparent,
                ForeColor = TextColor
            };

            var statusPanel = new Panel 
            { 
                Height = 30, 
                Dock = DockStyle.Bottom,
                BackColor = SystemColor.Transparent
            };
            
            _refreshIndicator = new SystemLabel
            {
                Text = "",
                ForeColor = AccentColor,
                Font = new SystemFont("Segoe UI Emoji", 10, SystemFontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right
            };
            
            _statusLabel = new SystemLabel
            {
                Text = "Loading today's data...",
                ForeColor = TextColor,
                Font = new SystemFont("Segoe UI Emoji", 10),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right
            };
            
            statusPanel.Controls.Add(_statusLabel);
            statusPanel.Controls.Add(_refreshIndicator);

            titleStatusPanel.Controls.Add(statusPanel);
            titleStatusPanel.Controls.Add(titleLabel);
            
            return titleStatusPanel;
        }

        private void SetDefaultValues()
        {
            _lastRefreshTime = DateTime.Now;
            _compareCheckBox.Enabled = _todayCheckBox.Checked;
        }

        private void LiveCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            bool isLive = _liveCheckBox.Checked;
            _daysNumeric.Enabled = !isLive;
            _todayCheckBox.Enabled = !isLive;
            _compareCheckBox.Enabled = !isLive;
            
            if (isLive)
            {
                _todayCheckBox.Checked = false;
                _compareCheckBox.Checked = false;
                StartLiveMode();
            }
            else
            {
                StopLiveMode();
                LoadData();
            }
        }

        private void TodayCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (_todayCheckBox.Checked)
            {
                _daysNumeric.Enabled = false;
                _compareCheckBox.Enabled = true;
            }
            else if (!_liveCheckBox.Checked)
            {
                _daysNumeric.Enabled = true;
                _compareCheckBox.Enabled = false;
                _compareCheckBox.Checked = false;
            }
            
            if (_isInitialized) LoadData();
        }

        private void CompareCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (_compareCheckBox.Checked)
            {
                if (!_todayCheckBox.Checked)
                {
                    _todayCheckBox.Checked = true;
                }
                
                _liveCheckBox.Enabled = false;
                _daysNumeric.Enabled = false;
            }
            else
            {
                _liveCheckBox.Enabled = true;
                if (!_todayCheckBox.Checked && !_liveCheckBox.Checked)
                {
                    _daysNumeric.Enabled = true;
                }
            }
            
            if (_isInitialized) LoadData();
        }

        private void SettingsChanged(object? sender, EventArgs e)
        {
            if (_isInitialized && !_liveCheckBox.Checked) LoadData();
        }

        private void ShowRefreshIndicator()
        {
            _refreshIndicator.Text = "*";
            _refreshIndicator.Refresh();
        }

        private void HideRefreshIndicator()
        {
            var hideTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            hideTimer.Tick += (s, e) =>
            {
                _refreshIndicator.Text = "";
                hideTimer.Stop();
                hideTimer.Dispose();
            };
            hideTimer.Start();
        }

        private void LoadData()
        {
            if (!File.Exists(Constants.DataFilePath))
            {
                _statusLabel.Text = "File not found";
                _formsPlot?.Plot.Clear();
                _formsPlot?.Refresh();
                return;
            }

            try
            {
                List<LogEntry> currentEntries;
                AxisLimits? currentLimits = null;
                
                if (_formsPlot != null && _liveCheckBox.Checked && _currentEntries != null && _currentEntries.Any())
                {
                    try { currentLimits = _formsPlot.Plot.Axes.GetLimits(); }
                    catch { }
                }
                
                if (_liveCheckBox.Checked)
                {
                    ShowRefreshIndicator();
                    currentEntries = LoadLiveData();
                    UpdateStatusLabel(currentEntries);
                    HideRefreshIndicator();
                    _todayEntries = null;
                    _yesterdayEntries = null;
                }
                else if (_compareCheckBox.Checked)
                {
                    _todayEntries = LoadTodayData();
                    _yesterdayEntries = LoadYesterdayData();
                    currentEntries = _todayEntries;
                    
                    var todayCount = _todayEntries?.Count ?? 0;
                    var yesterdayCount = _yesterdayEntries?.Count ?? 0;
                    _statusLabel.Text = $"Comparing Today ({todayCount} pts) vs Yesterday ({yesterdayCount} pts)";
                }
                else
                {
                    currentEntries = LoadStaticData();
                    var modeText = _todayCheckBox.Checked ? "today" : $"last {_daysNumeric.Value} days";
                    _statusLabel.Text = $"Showing {currentEntries.Count} points from {modeText}";
                }

                _currentEntries = currentEntries;
                RenderPlot(currentEntries, currentLimits);

                if (_liveCheckBox.Checked) 
                    _previousEntries = currentEntries.ToList();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                _formsPlot?.Plot.Clear();
                _formsPlot?.Refresh();
            }
        }

        private void RenderPlot(List<LogEntry> currentEntries, AxisLimits? currentLimits)
        {
            if (_formsPlot == null) return;

            _formsPlot.Plot.Clear();
            ApplyPlotTheme();
            
            if (currentEntries.Any() || (_compareCheckBox.Checked && (_todayEntries?.Any() == true || _yesterdayEntries?.Any() == true)))
            {
                var oADates = currentEntries.Select(e => e.Timestamp.ToOADate()).ToArray();
                var voltages = currentEntries.Select(e => e.Voltage).ToArray();

                RenderDataSeries(currentEntries, oADates, voltages);
                ConfigurePlotAxes();
                AddStatistics(voltages);
                RestoreZoomLevel(currentLimits);
                _formsPlot.Refresh();
            }
            else
            {
                _formsPlot.Plot.Title("No data to display");
                _formsPlot.Plot.HideLegend();
                _formsPlot.Refresh();
            }
            
            if (!_compareCheckBox.Checked)
            {
                _formsPlot.Plot.HideLegend();
            }
        }

        private void RenderDataSeries(List<LogEntry> currentEntries, double[] oADates, double[] voltages)
        {
            if (!_compareCheckBox.Checked)
            {
                AddNominalVoltageStandards(oADates);
                AddVoltageComplianceShading(oADates, voltages);
                if (!_hideGapsCheckBox.Checked) AddDataGapMarkers(currentEntries);
            }
            else
            {
                var allDates = new List<double>();
                if (_todayEntries?.Any() == true)
                    allDates.AddRange(_todayEntries.Select(e => e.Timestamp.ToOADate()));
                if (_yesterdayEntries?.Any() == true)
                {
                    var baseDate = DateTime.Now.Date;
                    allDates.AddRange(_yesterdayEntries.Select(e => 
                    {
                        var timeOfDay = e.Timestamp.TimeOfDay;
                        var comparisonDateTime = baseDate.Add(timeOfDay);
                        return comparisonDateTime.ToOADate();
                    }));
                }

                if (allDates.Any())
                {
                    AddNominalVoltageStandards(allDates.ToArray());
                }

                if (!_hideGapsCheckBox.Checked)
                {
                    if (_todayEntries != null) AddDataGapMarkers(_todayEntries);
                    if (_yesterdayEntries != null) AddComparisonDataGapMarkers(_yesterdayEntries);
                }
            }
            
            if (_compareCheckBox.Checked && _todayEntries != null && _yesterdayEntries != null)
            {
                RenderComparisonData();
            }
            else if (_liveCheckBox.Checked && _previousEntries != null && _previousEntries.Any())
            {
                var previousTimestamps = _previousEntries.Select(e => e.Timestamp).ToHashSet();
                var newEntries = currentEntries.Where(e => !previousTimestamps.Contains(e.Timestamp)).ToList();
                
                var allScatter = _formsPlot.Plot.Add.Scatter(oADates, voltages);
                allScatter.MarkerSize = 0;
                allScatter.LineWidth = 2;
                allScatter.Color = ScottPlot.Colors.Blue;

                if (newEntries.Any())
                {
                    var newDates = newEntries.Select(e => e.Timestamp.ToOADate()).ToArray();
                    var newVoltages = newEntries.Select(e => e.Voltage).ToArray();
                    var newScatter = _formsPlot.Plot.Add.Scatter(newDates, newVoltages);
                    newScatter.MarkerSize = 4;
                    newScatter.LineWidth = 3;
                    newScatter.Color = ScottPlot.Colors.Lime;
                    newScatter.MarkerColor = ScottPlot.Colors.Green;
                }
            }
            else
            {
                var scatter = _formsPlot.Plot.Add.Scatter(oADates, voltages);
                scatter.MarkerSize = 0;
                scatter.LineWidth = 2;
                scatter.Color = ScottPlot.Colors.Blue;
            }
        }

        private void ConfigurePlotAxes()
        {
            _formsPlot.Plot.Axes.DateTimeTicksBottom();
            _formsPlot.Plot.XLabel("Time");
            _formsPlot.Plot.YLabel("Voltage (V)");
            
            var darkColor = ScottPlot.Color.FromHex("#323232");
            _formsPlot.Plot.Axes.Left.Label.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Bottom.Label.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Left.TickLabelStyle.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Bottom.TickLabelStyle.ForeColor = darkColor;
        }

        private void RestoreZoomLevel(AxisLimits? currentLimits)
        {
            if (currentLimits.HasValue && _liveCheckBox.Checked)
            {
                try { _formsPlot.Plot.Axes.SetLimits(currentLimits.Value); }
                catch { _formsPlot.Plot.Axes.AutoScale(); }
            }
            else
            {
                _formsPlot.Plot.Axes.AutoScale();
            }
        }

        private void StartLiveMode()
        {
            _updateTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _updateTimer.Tick += (s, args) => LoadData();
            _updateTimer.Start();
            LoadData();
            
            if (_formsPlot != null)
            {
                _formsPlot.Plot.Axes.AutoScale();
                _formsPlot.Refresh();
            }
        }

        private void StopLiveMode()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _updateTimer = null;
            _refreshIndicator.Text = "";
        }

        private List<LogEntry> LoadLiveData()
        {
            var cutoffTime = DateTime.Now.AddHours(-3);
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            var lastHourEntries = allEntries.Where(e => e.Timestamp >= cutoffTime).ToList();
            var smooth = _smoothNumeric.Value > 0 ? (int?)_smoothNumeric.Value : null;
            return DataProcessor.ProcessData(lastHourEntries, null, false, smooth);
        }

        private List<LogEntry> LoadStaticData()
        {
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            var days = !_todayCheckBox.Checked ? (int?)_daysNumeric.Value : null;
            var smooth = _smoothNumeric.Value > 0 ? (int?)_smoothNumeric.Value : null;
            return DataProcessor.ProcessData(allEntries, days, _todayCheckBox.Checked, smooth);
        }

        private List<LogEntry> LoadTodayData()
        {
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            var smooth = _smoothNumeric.Value > 0 ? (int?)_smoothNumeric.Value : null;
            return DataProcessor.ProcessData(allEntries, null, true, smooth);
        }

        private List<LogEntry> LoadYesterdayData()
        {
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            var smooth = _smoothNumeric.Value > 0 ? (int?)_smoothNumeric.Value : null;
            
            var yesterday = DateTime.Now.Date.AddDays(-1);
            var yesterdayEntries = allEntries
                .Where(e => e.Timestamp.Date == yesterday)
                .OrderBy(e => e.Timestamp)
                .ToList();
            
            if (smooth.HasValue && smooth.Value > 0 && yesterdayEntries.Count > smooth.Value)
            {
                var smoothedVoltages = new double[yesterdayEntries.Count];
                for (int i = 0; i < yesterdayEntries.Count; i++)
                {
                    var window = yesterdayEntries.Skip(Math.Max(0, i - smooth.Value / 2)).Take(smooth.Value).ToList();
                    smoothedVoltages[i] = window.Average(e => e.Voltage);
                }
                for (int i = 0; i < yesterdayEntries.Count; i++)
                    yesterdayEntries[i].Voltage = smoothedVoltages[i];
            }
            
            return yesterdayEntries;
        }

        private void UpdateStatusLabel(List<LogEntry> currentEntries)
        {
            _lastRefreshTime = DateTime.Now;
            _statusLabel.Text = $"Live: {_lastRefreshTime:HH:mm:ss} | {currentEntries.Count} points | Last 3 hours";
        }

        private void SetupEventHandlers()
        {
            if (_formsPlot == null) return;

            _formsPlot.MouseMove += (sender, e) =>
            {
                if (_currentEntries == null || !_currentEntries.Any()) return;

                try
                {
                    var coordinate = _formsPlot.Plot.GetCoordinates(e.X, e.Y);
                    var mouseDateTime = DateTime.FromOADate(coordinate.X);
                    var mouseVoltage = coordinate.Y;

                    var closestEntry = FindClosestDataPoint(mouseDateTime, mouseVoltage, e.X, e.Y);

                    if (closestEntry.HasValue)
                    {
                        var isNewPoint = _liveCheckBox.Checked && _previousEntries != null && 
                                       !_previousEntries.Any(p => p.Timestamp == closestEntry.Value.entry.Timestamp);
                        var pointType = isNewPoint ? " [NEW]" : "";
                        
                        var tooltipText = "";
                        
                        if (_compareCheckBox.Checked)
                        {
                            var datasetLabel = closestEntry.Value.isToday ? "TODAY" : "YESTERDAY";
                            var originalDate = closestEntry.Value.isToday ? 
                                closestEntry.Value.entry.Timestamp : 
                                closestEntry.Value.entry.Timestamp.Date.AddDays(1).Add(closestEntry.Value.entry.Timestamp.TimeOfDay);
                                
                            tooltipText = $"{datasetLabel}: {originalDate:dddd, dd/MM/yyyy HH:mm:ss}\n" +
                                        $"Voltage: {closestEntry.Value.entry.Voltage:F2}V\n" +
                                        $"Time: {closestEntry.Value.entry.Timestamp.TimeOfDay}";
                        }
                        else
                        {
                            tooltipText = $"Time: {closestEntry.Value.entry.Timestamp:dddd, dd/MM/yyyy HH:mm:ss}{pointType}\n" +
                                        $"Voltage: {closestEntry.Value.entry.Voltage:F2}V\n" +
                                        $"Point: {closestEntry.Value.index + 1} of {_currentEntries.Count}";
                        }
                        
                        _tooltip?.Show(tooltipText, _formsPlot, e.X + 15, e.Y - 60);
                        return;
                    }
                    
                    _tooltip?.Hide(_formsPlot);
                }
                catch
                {
                    _tooltip?.Hide(_formsPlot);
                }
            };

            _formsPlot.MouseLeave += (sender, e) => _tooltip?.Hide(_formsPlot);

            FormClosed += (sender, e) =>
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                _tooltip?.Dispose();
            };
        }

        private (LogEntry entry, int index, bool isToday)? FindClosestDataPoint(DateTime mouseDateTime, double mouseVoltage, int mouseX, int mouseY)
        {
            if (_formsPlot == null) return null;

            const int PIXEL_TOLERANCE = 15;
            const int PIXEL_TOLERANCE_SQUARED = PIXEL_TOLERANCE * PIXEL_TOLERANCE;
            
            (LogEntry entry, int index, double distanceSquared, bool isToday)? bestCandidate = null;

            // In comparison mode, check both datasets
            if (_compareCheckBox.Checked && _todayEntries != null && _yesterdayEntries != null)
            {
                // Check today's entries
                for (int i = 0; i < _todayEntries.Count; i++)
                {
                    var entry = _todayEntries[i];
                    var candidate = CheckDataPoint(entry, i, mouseX, mouseY, true);
                    if (candidate.HasValue && (!bestCandidate.HasValue || candidate.Value.distanceSquared < bestCandidate.Value.distanceSquared))
                    {
                        bestCandidate = candidate;
                    }
                }

                // Check yesterday's entries (with shifted dates)
                var baseDate = DateTime.Now.Date;
                for (int i = 0; i < _yesterdayEntries.Count; i++)
                {
                    var entry = _yesterdayEntries[i];
                    var timeOfDay = entry.Timestamp.TimeOfDay;
                    var shiftedEntry = new LogEntry 
                    { 
                        Timestamp = baseDate.Add(timeOfDay), 
                        Voltage = entry.Voltage 
                    };
                    var candidate = CheckDataPoint(shiftedEntry, i, mouseX, mouseY, false);
                    if (candidate.HasValue && (!bestCandidate.HasValue || candidate.Value.distanceSquared < bestCandidate.Value.distanceSquared))
                    {
                        bestCandidate = (entry, i, candidate.Value.distanceSquared, false);
                    }
                }
            }
            else if (_currentEntries != null)
            {
                for (int i = 0; i < _currentEntries.Count; i++)
                {
                    var entry = _currentEntries[i];
                    var candidate = CheckDataPoint(entry, i, mouseX, mouseY, true);
                    if (candidate.HasValue && (!bestCandidate.HasValue || candidate.Value.distanceSquared < bestCandidate.Value.distanceSquared))
                    {
                        bestCandidate = candidate;
                    }
                }
            }

            return bestCandidate.HasValue ? (bestCandidate.Value.entry, bestCandidate.Value.index, bestCandidate.Value.isToday) : null;
        }

        private (LogEntry entry, int index, double distanceSquared, bool isToday)? CheckDataPoint(LogEntry entry, int index, int mouseX, int mouseY, bool isToday)
        {
            if (_formsPlot == null) return null;

            const int PIXEL_TOLERANCE = 15;
            const int PIXEL_TOLERANCE_SQUARED = PIXEL_TOLERANCE * PIXEL_TOLERANCE;

            try
            {
                var dataCoordinate = new Coordinates(entry.Timestamp.ToOADate(), entry.Voltage);
                var pixelPoint = _formsPlot.Plot.GetPixel(dataCoordinate);

                var deltaX = pixelPoint.X - mouseX;
                var deltaY = pixelPoint.Y - mouseY;
                
                if (Math.Abs(deltaX) > PIXEL_TOLERANCE || Math.Abs(deltaY) > PIXEL_TOLERANCE)
                    return null;

                var distanceSquared = deltaX * deltaX + deltaY * deltaY;

                if (distanceSquared <= PIXEL_TOLERANCE_SQUARED)
                {
                    return (entry, index, distanceSquared, isToday);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private void ApplyPlotTheme()
        {
            if (_formsPlot?.Plot == null) return;
            
            _formsPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#F0F0F0");
            _formsPlot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
            
            _formsPlot.Plot.Axes.Color(ScottPlot.Color.FromHex("#323232"));
            _formsPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D0D0D0");
            _formsPlot.Plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#E8E8E8");
            
            var darkColor = ScottPlot.Color.FromHex("#323232");
            
            _formsPlot.Plot.Axes.Left.Label.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Bottom.Label.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Top.Label.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Right.Label.ForeColor = darkColor;
            
            _formsPlot.Plot.Axes.Left.TickLabelStyle.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Bottom.TickLabelStyle.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Top.TickLabelStyle.ForeColor = darkColor;
            _formsPlot.Plot.Axes.Right.TickLabelStyle.ForeColor = darkColor;
        }

        private void AddNominalVoltageStandards(double[] oADates)
        {
            if (oADates.Length == 0) return;

            var minDate = oADates.Min();
            var maxDate = oADates.Max();

            var standardLine = _formsPlot.Plot.Add.HorizontalLine(Constants.NominalVoltage);
            standardLine.Color = ScottPlot.Colors.Red;
            standardLine.LineWidth = 2;
            standardLine.LinePattern = ScottPlot.LinePattern.Solid;

            var upperToleranceLine = _formsPlot.Plot.Add.HorizontalLine(Constants.NominalVoltage + Constants.VoltageTolerance);
            upperToleranceLine.Color = ScottPlot.Colors.Orange;
            upperToleranceLine.LineWidth = 1;
            upperToleranceLine.LinePattern = ScottPlot.LinePattern.Dashed;

            var lowerToleranceLine = _formsPlot.Plot.Add.HorizontalLine(Constants.NominalVoltage - Constants.VoltageTolerance);
            lowerToleranceLine.Color = ScottPlot.Colors.Orange;
            lowerToleranceLine.LineWidth = 1;
            lowerToleranceLine.LinePattern = ScottPlot.LinePattern.Dashed;

            var standardText = _formsPlot.Plot.Add.Text("Nominal Voltage: 230V", minDate + (maxDate - minDate) * 0.02, Constants.NominalVoltage + 2);
            standardText.Color = ScottPlot.Colors.Red;
            standardText.FontSize = 10;

            var toleranceText = _formsPlot.Plot.Add.Text("±10% Range (207V-253V)", minDate + (maxDate - minDate) * 0.02, Constants.NominalVoltage + Constants.VoltageTolerance + 2);
            toleranceText.Color = ScottPlot.Colors.Orange;
            toleranceText.FontSize = 9;
        }

        private void AddVoltageComplianceShading(double[] oADates, double[] voltages)
        {
            if (oADates.Length == 0 || voltages.Length == 0) return;

            var aboveStandardX = new List<double>();
            var aboveStandardY = new List<double>();
            var belowStandardX = new List<double>();
            var belowStandardY = new List<double>();

            for (int i = 0; i < voltages.Length; i++)
            {
                if (voltages[i] > Constants.NominalVoltage)
                {
                    if (aboveStandardX.Count == 0)
                    {
                        aboveStandardX.Add(oADates[i]);
                        aboveStandardY.Add(Constants.NominalVoltage);
                    }
                    aboveStandardX.Add(oADates[i]);
                    aboveStandardY.Add(voltages[i]);
                }
                else if (aboveStandardX.Count > 0)
                {
                    aboveStandardX.Add(oADates[i - 1]);
                    aboveStandardY.Add(Constants.NominalVoltage);
                    
                    var abovePolygon = _formsPlot.Plot.Add.Polygon(aboveStandardX.ToArray(), aboveStandardY.ToArray());
                    abovePolygon.FillColor = ScottPlot.Color.FromHex("#FF0000").WithAlpha(0.2);
                    abovePolygon.LineWidth = 0;
                    
                    aboveStandardX.Clear();
                    aboveStandardY.Clear();
                }
            }

            if (aboveStandardX.Count > 0)
            {
                aboveStandardX.Add(aboveStandardX.Last());
                aboveStandardY.Add(Constants.NominalVoltage);
                
                var abovePolygon = _formsPlot.Plot.Add.Polygon(aboveStandardX.ToArray(), aboveStandardY.ToArray());
                abovePolygon.FillColor = ScottPlot.Color.FromHex("#FF0000").WithAlpha(0.2);
                abovePolygon.LineWidth = 0;
            }

            for (int i = 0; i < voltages.Length; i++)
            {
                if (voltages[i] < Constants.NominalVoltage)
                {
                    if (belowStandardX.Count == 0)
                    {
                        belowStandardX.Add(oADates[i]);
                        belowStandardY.Add(Constants.NominalVoltage);
                    }
                    belowStandardX.Add(oADates[i]);
                    belowStandardY.Add(voltages[i]);
                }
                else if (belowStandardX.Count > 0)
                {
                    belowStandardX.Add(oADates[i - 1]);
                    belowStandardY.Add(Constants.NominalVoltage);
                    
                    var belowPolygon = _formsPlot.Plot.Add.Polygon(belowStandardX.ToArray(), belowStandardY.ToArray());
                    belowPolygon.FillColor = ScottPlot.Color.FromHex("#FFA500").WithAlpha(0.2);
                    belowPolygon.LineWidth = 0;
                    
                    belowStandardX.Clear();
                    belowStandardY.Clear();
                }
            }

            if (belowStandardX.Count > 0)
            {
                belowStandardX.Add(belowStandardX.Last());
                belowStandardY.Add(Constants.NominalVoltage);
                
                var belowPolygon = _formsPlot.Plot.Add.Polygon(belowStandardX.ToArray(), belowStandardY.ToArray());
                belowPolygon.FillColor = ScottPlot.Color.FromHex("#FFA500").WithAlpha(0.2);
                belowPolygon.LineWidth = 0;
            }
        }

        private void AddDataGapMarkers(List<LogEntry> entries)
        {
            if (entries == null || entries.Count < 2) return;
            
            var sortedEntries = entries.OrderBy(e => e.Timestamp).ToList();
            
            for (int i = 1; i < sortedEntries.Count; i++)
            {
                var timeDiff = sortedEntries[i].Timestamp - sortedEntries[i - 1].Timestamp;
                
                if (timeDiff.TotalMinutes > Constants.GAP_THRESHOLD_MINUTES)
                {
                    var gapTime = sortedEntries[i - 1].Timestamp.AddMinutes(Constants.GAP_THRESHOLD_MINUTES);
                    var gapOADate = gapTime.ToOADate();
                    
                    var gapLine = _formsPlot.Plot.Add.VerticalLine(gapOADate);
                    gapLine.Color = ScottPlot.Colors.Red;
                    gapLine.LineWidth = 2;
                    gapLine.LinePattern = ScottPlot.LinePattern.Dashed;
                    
                    var gapDurationText = FormatGapDuration(timeDiff);
                    var gapText = _formsPlot.Plot.Add.Text($"Gap: {gapDurationText}", gapOADate, GetTextPositionForGap());
                    gapText.Color = ScottPlot.Colors.Red;
                    gapText.FontSize = 9;
                    gapText.Rotation = 90;
                    gapText.Alignment = Alignment.LowerCenter;
                }
            }
        }

        private void AddComparisonDataGapMarkers(List<LogEntry> yesterdayEntries)
        {
            if (yesterdayEntries == null || yesterdayEntries.Count < 2) return;

            var baseDate = DateTime.Now.Date;
            var sortedEntries = yesterdayEntries.OrderBy(e => e.Timestamp).ToList();
            
            for (int i = 1; i < sortedEntries.Count; i++)
            {
                var timeDiff = sortedEntries[i].Timestamp - sortedEntries[i - 1].Timestamp;
                
                if (timeDiff.TotalMinutes > Constants.GAP_THRESHOLD_MINUTES)
                {
                    var originalGapTime = sortedEntries[i - 1].Timestamp.AddMinutes(Constants.GAP_THRESHOLD_MINUTES);
                    var shiftedGapTime = baseDate.Add(originalGapTime.TimeOfDay);
                    var gapOADate = shiftedGapTime.ToOADate();
                    
                    var gapLine = _formsPlot.Plot.Add.VerticalLine(gapOADate);
                    gapLine.Color = ScottPlot.Colors.DarkRed;
                    gapLine.LineWidth = 1;
                    gapLine.LinePattern = ScottPlot.LinePattern.Dotted;
                    
                    var gapDurationText = FormatGapDuration(timeDiff);
                    var gapText = _formsPlot.Plot.Add.Text($"Y-Gap: {gapDurationText}", gapOADate, GetTextPositionForYesterdayGap());
                    gapText.Color = ScottPlot.Colors.DarkRed;
                    gapText.FontSize = 8;
                    gapText.Rotation = 90;
                    gapText.Alignment = Alignment.LowerCenter;
                }
            }
        }

        private void RenderComparisonData()
        {
            if (_todayEntries == null || _yesterdayEntries == null || _formsPlot == null) return;

            if (_todayEntries.Any())
            {
                var todayDates = _todayEntries.Select(e => e.Timestamp.ToOADate()).ToArray();
                var todayVoltages = _todayEntries.Select(e => e.Voltage).ToArray();
                var todayScatter = _formsPlot.Plot.Add.Scatter(todayDates, todayVoltages);
                todayScatter.MarkerSize = 0;
                todayScatter.LineWidth = 2;
                todayScatter.Color = ScottPlot.Colors.Blue;
                todayScatter.LegendText = "Today";
            }

            if (_yesterdayEntries.Any())
            {
                var baseDate = DateTime.Now.Date;
                var yesterdayCompareDates = _yesterdayEntries.Select(e => 
                {
                    var timeOfDay = e.Timestamp.TimeOfDay;
                    var comparisonDateTime = baseDate.Add(timeOfDay);
                    return comparisonDateTime.ToOADate();
                }).ToArray();
                
                var yesterdayVoltages = _yesterdayEntries.Select(e => e.Voltage).ToArray();
                var yesterdayScatter = _formsPlot.Plot.Add.Scatter(yesterdayCompareDates, yesterdayVoltages);
                yesterdayScatter.MarkerSize = 0;
                yesterdayScatter.LineWidth = 2;
                yesterdayScatter.Color = ScottPlot.Colors.Red;
                yesterdayScatter.LegendText = "Yesterday";
            }

            _formsPlot.Plot.ShowLegend();
        }

        private void AddStatistics(double[] voltages)
        {
            if (_compareCheckBox.Checked && _todayEntries != null && _yesterdayEntries != null)
            {
                AddComparisonStatistics();
                return;
            }

            var maxVoltage = voltages.Max();
            var minVoltage = voltages.Min();
            var avgVoltage = voltages.Average();
            var lastVoltage = voltages.Last();
            var voltageRange = maxVoltage - minVoltage;

            var withinStandard = voltages.Count(v => Math.Abs(v - Constants.NominalVoltage) <= Constants.VoltageTolerance);
            var compliancePercentage = (double)withinStandard / voltages.Length * 100;

            var aboveStandardCount = voltages.Count(v => v > Constants.NominalVoltage);
            var belowStandardCount = voltages.Count(v => v < Constants.NominalVoltage);
            
            double hoursAbove230 = 0;
            double hoursBelow230 = 0;

            if (_currentEntries != null && _currentEntries.Count > 1)
            {
                var timeSpan = _currentEntries.Last().Timestamp - _currentEntries.First().Timestamp;
                var totalHours = timeSpan.TotalHours;
                var avgIntervalHours = totalHours / (_currentEntries.Count - 1);
                hoursAbove230 = aboveStandardCount * avgIntervalHours;
                hoursBelow230 = belowStandardCount * avgIntervalHours;
            }

            var statsText = $"Max: {maxVoltage:F2}V\nMin: {minVoltage:F2}V\nRange: {voltageRange:F2}V\n" +
                           $"Avg: {avgVoltage:F2}V\nLast: {lastVoltage:F2}V\n" +
                           $"Voltage Compliance: {compliancePercentage:F1}%\n" +
                           $"Hours Above 230V: {hoursAbove230:F1}h\n" +
                           $"Hours Below 230V: {hoursBelow230:F1}h\n" +
                           $"Standard: 230V ±10%";
            var annotation = _formsPlot.Plot.Add.Annotation(statsText);
            annotation.Alignment = Alignment.UpperRight;
            
            annotation.LabelFontColor = ScottPlot.Color.FromHex("#323232");
            annotation.LabelBackgroundColor = ScottPlot.Color.FromHex("#F0F0F0").WithAlpha(0.9);
            annotation.LabelBorderColor = ScottPlot.Color.FromHex("#A0A0A0");
            annotation.LabelBorderWidth = 1;
        }

        private void AddComparisonStatistics()
        {
            if (_todayEntries == null || _yesterdayEntries == null || _formsPlot == null) return;

            var todayVoltages = _todayEntries.Select(e => e.Voltage).ToArray();
            var yesterdayVoltages = _yesterdayEntries.Select(e => e.Voltage).ToArray();

            var todayAvg = todayVoltages.Any() ? todayVoltages.Average() : 0;
            var todayMax = todayVoltages.Any() ? todayVoltages.Max() : 0;
            var todayMin = todayVoltages.Any() ? todayVoltages.Min() : 0;
            var todayCompliance = todayVoltages.Any() ? 
                (double)todayVoltages.Count(v => Math.Abs(v - Constants.NominalVoltage) <= Constants.VoltageTolerance) / todayVoltages.Length * 100 : 0;

            var yesterdayAvg = yesterdayVoltages.Any() ? yesterdayVoltages.Average() : 0;
            var yesterdayMax = yesterdayVoltages.Any() ? yesterdayVoltages.Max() : 0;
            var yesterdayMin = yesterdayVoltages.Any() ? yesterdayVoltages.Min() : 0;
            var yesterdayCompliance = yesterdayVoltages.Any() ? 
                (double)yesterdayVoltages.Count(v => Math.Abs(v - Constants.NominalVoltage) <= Constants.VoltageTolerance) / yesterdayVoltages.Length * 100 : 0;

            var avgDiff = todayAvg - yesterdayAvg;
            var complianceDiff = todayCompliance - yesterdayCompliance;

            var statsText = $"TODAY vs YESTERDAY\n" +
                           $"Avg: {todayAvg:F2}V vs {yesterdayAvg:F2}V ({avgDiff:+0.00;-0.00;0.00})\n" +
                           $"Max: {todayMax:F2}V vs {yesterdayMax:F2}V\n" +
                           $"Min: {todayMin:F2}V vs {yesterdayMin:F2}V\n" +
                           $"Compliance: {todayCompliance:F1}% vs {yesterdayCompliance:F1}% ({complianceDiff:+0.0;-0.0;0.0}%)\n" +
                           $"Data Points: {todayVoltages.Length} vs {yesterdayVoltages.Length}\n" +
                           $"Standard: 230V ±10%";

            var annotation = _formsPlot.Plot.Add.Annotation(statsText);
            annotation.Alignment = Alignment.UpperRight;
            
            annotation.LabelFontColor = ScottPlot.Color.FromHex("#323232");
            annotation.LabelBackgroundColor = ScottPlot.Color.FromHex("#F0F0F0").WithAlpha(0.9);
            annotation.LabelBorderColor = ScottPlot.Color.FromHex("#A0A0A0");
            annotation.LabelBorderWidth = 1;
        }

        private string FormatGapDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{duration.TotalHours:F1}h";
            }
            else
            {
                return $"{duration.TotalMinutes:F0}m";
            }
        }

        private double GetTextPositionForGap()
        {
            if (_formsPlot?.Plot == null) return Constants.NominalVoltage + 10;
            
            try
            {
                var limits = _formsPlot.Plot.Axes.GetLimits();
                var yRange = limits.Top - limits.Bottom;
                return limits.Bottom + (yRange * 0.9);
            }
            catch
            {
                return Constants.NominalVoltage + 10;
            }
        }

        private double GetTextPositionForYesterdayGap()
        {
            if (_formsPlot?.Plot == null) return Constants.NominalVoltage + 5;
            
            try
            {
                var limits = _formsPlot.Plot.Axes.GetLimits();
                var yRange = limits.Top - limits.Bottom;
                return limits.Bottom + (yRange * 0.85);
            }
            catch
            {
                return Constants.NominalVoltage + 5;
            }
        }
    }
}