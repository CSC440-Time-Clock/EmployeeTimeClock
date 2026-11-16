using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmployeeTimeClock
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // load persisted users and work records before the login window opens -H
            try
            {
                TimeClockStore.LoadFromDatabase();
            }
            catch (Exception ex)
            {
                MessageBox.Show("The timeclock database could not be loaded. Check DatabaseConfig.cs, network access, and the table names" + Environment.NewLine + ex.Message, "Database Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Application.Run(new Login());
        }
    }
}
