// Copyright © ItzJustLars
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoClicker
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ClickerForm());
        }
    }

    public class ClickerForm : Form
    {
        private DataGridView dgv;
        private Button btnStart, btnStop, btnPick;
        private Label lblStatus, lblDelay, lblRepeat;
        private NumericUpDown nudDelay, nudRepeat;
        private CancellationTokenSource cts;

        // P/Invoke for mouse and keyboard interaction
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        public ClickerForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Auto Clicker";
            this.Size = new Size(450, 440);

            dgv = new DataGridView
            {
                Location = new Point(10, 10),
                Size = new Size(410, 200),
                AllowUserToAddRows = true,
                ColumnCount = 2,
                RowHeadersVisible = false
            };
            dgv.Columns[0].Name = "X";
            dgv.Columns[1].Name = "Y";
            dgv.Columns[0].Width = 180;
            dgv.Columns[1].Width = 180;

            btnStart = new Button { Text = "Start", Location = new Point(10, 220) };
            btnStart.Click += BtnStart_Click;

            btnStop = new Button { Text = "Stop", Location = new Point(100, 220), Enabled = false };
            btnStop.Click += BtnStop_Click;

            btnPick = new Button { Text = "Pick location (P)", Location = new Point(190, 220) };
            btnPick.Click += BtnPick_Click;

            lblDelay = new Label { Text = "Delay (s):", Location = new Point(310, 220), Size = new Size(60, 23) };
            nudDelay = new NumericUpDown { Location = new Point(380, 220), Minimum = 0, Maximum = 60, Value = 3, Width = 40 };

            lblRepeat = new Label { Text = "Repeats (0=∞):", Location = new Point(10, 260), Size = new Size(100, 23) };
            nudRepeat = new NumericUpDown { Location = new Point(120, 260), Minimum = 0, Maximum = 1000000, Value = 0, Width = 60 };

            lblStatus = new Label { Text = string.Empty, Location = new Point(10, 300), Size = new Size(410, 30) };

            this.Controls.Add(dgv);
            this.Controls.Add(btnStart);
            this.Controls.Add(btnStop);
            this.Controls.Add(btnPick);
            this.Controls.Add(lblDelay);
            this.Controls.Add(nudDelay);
            this.Controls.Add(lblRepeat);
            this.Controls.Add(nudRepeat);
            this.Controls.Add(lblStatus);
        }

        private async void BtnPick_Click(object sender, EventArgs e)
        {
            btnPick.Enabled = btnStart.Enabled = btnStop.Enabled = false;
            lblStatus.Text = "Move the mouse and press P to pick coordinates, or Esc to cancel...";

            Point p;
            while (true)
            {
                await Task.Delay(100);
                if ((GetAsyncKeyState((int)Keys.P) & 0x8000) != 0)
                {
                    GetCursorPos(out p);
                    break;
                }
                if ((GetAsyncKeyState((int)Keys.Escape) & 0x8000) != 0)
                {
                    lblStatus.Text = "Pick cancelled.";
                    btnPick.Enabled = btnStart.Enabled = true;
                    btnStop.Enabled = false;
                    return;
                }
            }

            this.Invoke((Action)(() =>
            {
                dgv.Rows.Add(p.X, p.Y);
                lblStatus.Text = $"Picked: X={p.X}, Y={p.Y}";
                btnPick.Enabled = btnStart.Enabled = true;
                btnStop.Enabled = false;
            }));
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            var positions = new List<Point>();
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                if (int.TryParse(row.Cells[0].Value?.ToString(), out int x) && int.TryParse(row.Cells[1].Value?.ToString(), out int y))
                {
                    positions.Add(new Point(x, y));
                }
            }

            if (positions.Count == 0)
            {
                MessageBox.Show("Please add at least one (X, Y) pair.", "No coordinates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int delayMs = (int)nudDelay.Value * 1000;
            int repeatCount = (int)nudRepeat.Value;

            btnStart.Enabled = false;
            btnPick.Enabled = false;
            btnStop.Enabled = true;
            lblStatus.Text = "Clicking started... Press Esc to stop.";
            cts = new CancellationTokenSource();

            try
            {
                int loops = repeatCount == 0 ? int.MaxValue : repeatCount;
                for (int i = 0; i < loops && !cts.Token.IsCancellationRequested; i++)
                {
                    foreach (var pt in positions)
                    {
                        if (cts.Token.IsCancellationRequested) break;
                        if ((GetAsyncKeyState((int)Keys.Escape) & 0x8000) != 0)
                        {
                            cts.Cancel();
                            break;
                        }
                        LeftClick(pt.X, pt.Y);
                        await Task.Delay(delayMs, cts.Token);
                    }
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                btnStart.Enabled = btnPick.Enabled = true;
                btnStop.Enabled = false;
                lblStatus.Text = "Clicking stopped.";
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            cts?.Cancel();
        }

        private static void LeftClick(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(100);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, UIntPtr.Zero);
        }
    }
}
