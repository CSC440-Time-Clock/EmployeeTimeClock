using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmployeeTimeClock
{
    public partial class Employee : Form
    {
        Form parent;

        public Employee(Form parentForm)
        {
            parent = parentForm;
            InitializeComponent();
            LogoutPanel.Hide();
            scheduleGroupBox.BringToFront();
        }

        private void tableLayoutPanel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click_1(object sender, EventArgs e)
        {
            availabilityGroupBox.BringToFront();
        }


        // Log Out Functions
        // - - - -  Samuel Griggs
        private void logoutButton_Click(object sender, EventArgs e)
        {
            LogoutPanel.Show();
        }
        private void ConfirmLogoutButton_Click(object sender, EventArgs e) {
            parent.Show();
            this.Close();
        }
        private void CancelLogoutButton_Click(object sender, EventArgs e) {
            LogoutPanel.Hide();
        }


        private void viewTimesheetButton_Click(object sender, EventArgs e)
        {
            timesheetGroupBox.BringToFront();
        }

        private void PTOgroupBox_Enter(object sender, EventArgs e)
        {

        }

        private void label16_Click(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel11_Paint(object sender, PaintEventArgs e)
        {

        }

        private void PTORequestButton_Click(object sender, EventArgs e)
        {
            PTOgroupBox.BringToFront();
        }

        private void viewSchedule_Click(object sender, EventArgs e)
        {
            scheduleGroupBox.BringToFront();
        }

        
    }
}
