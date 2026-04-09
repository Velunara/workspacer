using System;
using System.Drawing;
using System.Timers;
using System.Windows.Forms;

namespace workspacer.Bar
{
    public partial class BarForm : Form
    {
        private IMonitor _monitor;
        private BarPluginConfig _config;
        private System.Timers.Timer _timer;
        private FlowLayoutPanel leftPanel;
        private FlowLayoutPanel rightPanel;

        private BarSection _left;
        private BarSection _right;

        public BarForm(IMonitor monitor, BarPluginConfig config)
        {
            _monitor = monitor;
            _config = config;
            _timer = new System.Timers.Timer(50);
            _timer.Elapsed += Redraw;

            this.Text = config.BarTitle;
            this.ControlBox = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = ColorToColor(config.Background);

            if (config.IsTransparent)
            {
                this.AllowTransparency = true;
                this.TransparencyKey = ColorToColor(config.TransparencyKey);
            }

            this.Load += OnLoad;

            InitializeComponent();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // turn on WS_EX_TOOLWINDOW style bit
                cp.ExStyle |= (int) Win32.WS_EX.WS_EX_TOOLWINDOW;
                
                // turn on WS_EX_TOPMOST if the topbar does not reserve space.
                if (_config is not null && !_config.BarReservesSpace)
                    cp.ExStyle |= (int) (Win32.WS_EX.WS_EX_TOPMOST | Win32.WS_EX.WS_EX_LAYERED);
                return cp;
            }
        }

        public void Initialize(IBarWidget[] left, IBarWidget[] right, IConfigContext context)
        {
            _left = new BarSection(false, leftPanel, left, _monitor, context,
                _config.DefaultWidgetForeground, _config.DefaultWidgetBackground, _config.FontName, _config.FontSize, _config.BarMargin);
            _right = new BarSection(true, rightPanel, right, _monitor, context,
                _config.DefaultWidgetForeground, _config.DefaultWidgetBackground, _config.FontName, _config.FontSize, _config.BarMargin);
        }

        private System.Drawing.Color ColorToColor(Color color)
        {
            return System.Drawing.Color.FromArgb(255, color.R, color.G, color.B);
        }

        private void OnLoad(object sender, EventArgs e)
        {
            this.Height = _config.BarHeight;
            var titleBarHeight = this.ClientRectangle.Height - this.Height;
           
            this.Location = _config.BarIsTop
                ? new Point(_monitor.X, _monitor.Y - titleBarHeight)
                : new Point(_monitor.X, _monitor.Y + _monitor.Height - _config.BarHeight);

            _timer.Enabled = true;

            this.Height = _config.BarHeight;
            this.Width = _monitor.Width;

        }

        private void InitializeComponent()
        {
            leftPanel = new FlowLayoutPanel();
            rightPanel = new FlowLayoutPanel();
            SuspendLayout();
            // 
            // leftPanel
            // 
            leftPanel.Anchor = (AnchorStyles.Top | AnchorStyles.Bottom) 
                               | AnchorStyles.Left;
            leftPanel.AutoSize = true;
            leftPanel.BackColor = ColorToColor(_config.DefaultWidgetBackground);
            leftPanel.Location = new Point(0, 0);
            leftPanel.Margin = new Padding(0);
            leftPanel.Name = "leftPanel";
            leftPanel.Size = new Size(50, 50);
            leftPanel.TabIndex = 0;
            leftPanel.WrapContents = false;
            // 
            // rightPanel
            // 
            rightPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom 
                                                 | AnchorStyles.Right;
            rightPanel.AutoSize = true;
            rightPanel.BackColor = ColorToColor(_config.DefaultWidgetBackground);
            rightPanel.FlowDirection = FlowDirection.RightToLeft;
            rightPanel.Location = new Point(_monitor.Width - _monitor.Width / 1848 * 72, 0);
            rightPanel.Margin = new Padding(0);
            rightPanel.Name = "rightPanel";
            rightPanel.Size = new Size(50, 50);
            rightPanel.TabIndex = 2;
            rightPanel.WrapContents = false;
            // 
            // BarForm
            // 
            ClientSize = new Size(1898, 50);
            Controls.Add(leftPanel);
            Controls.Add(rightPanel);
            Name = "BarForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            ResumeLayout(false);
            PerformLayout();

        }

        private void Redraw(object sender, ElapsedEventArgs args)
        {
            try
            {
                if (IsHandleCreated)
                {
                    Invoke((MethodInvoker)(() =>
                    {
                        _left.Draw();
                        _right.Draw();
                    }));
                }
            }
            catch (ObjectDisposedException)
            {
                // Sometimes after waking from sleep, BarForm has been disposed of.
            }
        }
    }
}
