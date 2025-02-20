using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace dn_timecard_util
{

    public partial class Form1 : Form
    {
        public class ModeItem
        {
            public string Title;
            public Icon Icon;
        }

        public class StateData
        {
            public string Title;
            public DateTime StartDt;
        }

        Random random = new Random(Environment.TickCount);

        List<ModeItem> ModeList = new List<ModeItem>();

        NotifyIcon Notify = new NotifyIcon();
        ContextMenuStrip PopupMenu = new ContextMenuStrip();

        ModeItem CurrentSelected = null;

        ModeItem IdleItem;

        System.Globalization.CultureInfo Japanese = new System.Globalization.CultureInfo("ja-JP");

        public Form1()
        {
            ModeList.Add(new ModeItem { Title = "IPA", Icon = Properties.Resources.IPA });
            ModeList.Add(new ModeItem { Title = "NTT 東日本", Icon = Properties.Resources.NTT });
            ModeList.Add(new ModeItem { Title = "筑波大学", Icon = Properties.Resources.UT });
            ModeList.Add(new ModeItem { Title = "休止中", Icon = Properties.Resources.Idle });

            IdleItem = ModeList.Last();

            foreach (var item in ModeList)
            {
                this.PopupMenu.Items.Add(item.Title, item.Icon.ToBitmap(), (sender, args) =>
                {
                    var clicked = sender as ToolStripMenuItem;
                    if (clicked != null)
                    {
                        var clickedItem = clicked.Tag as ModeItem;
                        if (clickedItem != null)
                        {
                            SetState(item, true);
                        }
                    }
                }).Tag = item;
            }

            Notify.MouseClick += Notify_MouseClick;

            InitializeComponent();
        }

        private void Notify_MouseClick(object sender, MouseEventArgs e)
        {
            timer1_Tick(null, null);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var currentState = LoadStateDataFromFile();

            var currentItem = ModeList.Where(x => x.Title == currentState?.Title).FirstOrDefault();

            if (currentItem == null)
            {
                currentItem = IdleItem;
            }

            SetState(currentItem, false, true);

            // フォームが起動したときから最小化＆タスクバーに表示しない
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;
        }

        void SetState(ModeItem item, bool manualSet, bool initial = false)
        {
            bool changed = false;
            if (initial || CurrentSelected == null)
            {
                changed = true;
            }
            else
            {
                if (CurrentSelected != item)
                {
                    changed = true;
                }
            }

            if (changed == false)
            {
                return;
            }

            if (manualSet)
            {
                StateData newState = new StateData
                {
                    StartDt = DateTime.Now,
                    Title = item.Title,
                };

                var lastState = LoadStateDataFromFile();

                if (lastState == null)
                {
                    return;
                }

                if (SaveStateDataToFile(newState) == false)
                {
                    return;
                }

                if (lastState.Title != IdleItem.Title)
                {
                    AppendTimecardLogToFile(lastState.StartDt, newState.StartDt, lastState.Title);
                    AppendTimecardLogToFile2(lastState.StartDt, newState.StartDt, lastState.Title);
                }
            }

            CurrentSelected = item;

            Notify.Icon = item.Icon;
            Notify.Text = $"{item.Title} - タスク状態";

            foreach (ToolStripItem menuItem in PopupMenu.Items)
            {
                if (menuItem.Tag == item)
                {
                    menuItem.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
                }
                else
                {
                    menuItem.Font = new Font("Meiryo UI", 9, FontStyle.Regular);
                }
            }

            Notify.ContextMenuStrip = PopupMenu;
            Notify.Visible = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Notify.Visible = false;
            Notify.Dispose();
        }

        const string StateFileName = @"H:\TimeCard\State.txt";
        const string StateLogBaseDir = @"H:\TimeCard\";

        readonly TimeSpan TimeUnit = new TimeSpan(0, 5, 0);

        void AppendTimecardLogToFile(DateTime start, DateTime end, string title)
        {
            try
            {
                if (string.IsNullOrEmpty(title) || title == IdleItem.Title)
                {
                    return;
                }

                start = new DateTime(start.Ticks / TimeUnit.Ticks * TimeUnit.Ticks);
                end = new DateTime(end.Ticks / TimeUnit.Ticks * TimeUnit.Ticks);

                string yyyymm = start.ToString("yyyyMM");

                string fullPath = Path.Combine(StateLogBaseDir, title, yyyymm + ".txt");

                string dirPath = Path.GetDirectoryName(fullPath);

                try
                {
                    Directory.CreateDirectory(dirPath);
                }
                catch { };

                List<string> lines = new List<string>();

                string startStr = start.ToString("MM/dd HH:mm");
                string endStr = end.ToString("MM/dd HH:mm");

                if (start.Date == end.Date)
                {
                    endStr = end.ToString("HH:mm");
                }

                lines.Add(startStr + "-" + endStr);

                if (start != end)
                {
                    File.AppendAllLines(fullPath, lines);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void AppendTimecardLogToFile2(DateTime start, DateTime end, string title)
        {
            try
            {
                if (string.IsNullOrEmpty(title) || title == IdleItem.Title)
                {
                    return;
                }

                string fullPath = Path.Combine(StateLogBaseDir, "_detail", title + ".log");

                string dirPath = Path.GetDirectoryName(fullPath);

                try
                {
                    Directory.CreateDirectory(dirPath);
                }
                catch { };

                List<string> lines = new List<string>();

                string startStr = start.ToString("yyyy/MM/dd HH:mm:ss");
                string endStr = end.ToString("yyyy/MM/dd HH:mm:ss");

                lines.Add(startStr + "," + endStr + "," + Environment.MachineName);
                File.AppendAllLines(fullPath, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        StateData LoadStateDataFromFile()
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(StateFileName);

                if (fileData.Length == 0)
                {
                    StateData ret2 = new StateData();
                    ret2.Title = IdleItem.Title;

                    return ret2;
                }

                string fileString = Encoding.UTF8.GetString(fileData);

                StringReader sr = new StringReader(fileString);

                string title = sr.ReadLine();
                string dtStr = sr.ReadLine();
                string eofstr = sr.ReadLine();

                if (eofstr != "EOF")
                {
                    MessageBox.Show($"File {StateFileName} is broken.");
                    return null;
                }

                if (DateTime.TryParse(dtStr, Japanese, System.Globalization.DateTimeStyles.AssumeLocal, out DateTime dt) == false)
                {
                    title = IdleItem.Title;
                }

                if (string.IsNullOrEmpty(title))
                {
                    title = IdleItem.Title;
                }

                StateData ret = new StateData();
                ret.StartDt = dt;
                ret.Title = title;

                return ret;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        bool SaveStateDataToFile(StateData data)
        {
            try
            {
                StringWriter w = new StringWriter();
                w.WriteLine(data.Title);
                w.WriteLine(data.StartDt.ToString(Japanese));
                w.WriteLine("EOF");

                File.WriteAllBytes(StateFileName, Encoding.UTF8.GetBytes(w.ToString()));

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;

            try
            {
                var currentState = LoadStateDataFromFile();

                var currentItem = ModeList.Where(x => x.Title == currentState?.Title).FirstOrDefault();

                if (currentItem == null)
                {
                    currentItem = IdleItem;
                }

                SetState(currentItem, false, false);
            }
            finally
            {
                timer1.Interval = (int)(random.NextDouble() * 5 * 60 * 1000);
                timer1.Enabled = true;
            }
        }
    }
}
