using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace dn_timecard_util
{
    internal static class Program
    {
        static Mutex _mutex;

        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        /// 

        [STAThread]
        static void Main()
        {
            bool createdNew = false;
            // 同じ名前で排他制御を行う
            _mutex = new Mutex(true, "dn-timecard-util-app", out createdNew);

            if (!createdNew)
            {
                // すでに同名のMutexが存在する（＝アプリケーションが起動中）場合
                MessageBox.Show("dn-timecard-util アプリケーションはすでに起動しています。",
                    "二重起動防止", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return; // アプリケーションを終了
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
