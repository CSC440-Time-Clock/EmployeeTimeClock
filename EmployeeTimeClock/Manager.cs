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
    public partial class Manager : Form
    {
        Form parent;
        bool isClocked;

        public Manager(Form parentForm)
        {
            // Initialization
            parent = parentForm;
            InitializeComponent();

            // Default Hidden Panels
            logoutPanel.Hide();

            scheduleGroupBox.BringToFront();
        }

        private void tableLayoutPanel3_Paint(object sender, PaintEventArgs e)
        {

        }

        // Clock In Functionality
        // - - - - -    Samuel Griggs
        private void clockInButton_Click(object sender, EventArgs e)
        {
            updateStatus(true);

            /*                                          *
             *      Add database Functionality Here     *
             *                                          */

            statusLabel.Text = "Status: Clocked In";
        }
        // Clock Out Functionality
        // - - - - -    Samuel Griggs
        private void clockOutButton_Click(object sender, EventArgs e) {
            updateStatus(false);

            /*                                          *
             *      Add Database Functionality Here     *
             *                                          */

            statusLabel.Text = "Status: Clocked Out";
        }
        private void textBox10_TextChanged(object sender, EventArgs e)
        {

        }

        private void viewScheduleButton_Click(object sender, EventArgs e)
        {
            scheduleGroupBox.BringToFront();
        }

        private void editTimesheetButton_Click(object sender, EventArgs e)
        {
            editTimesheetGroupBox.BringToFront();
        }

        private void calculatePaymentButton_Click(object sender, EventArgs e)
        {
            calculatePaymentGroupBox.BringToFront();
        }

        private void createSchedule_Click(object sender, EventArgs e)
        {
            generateScheduleGroupBox.BringToFront();
        }

        private void editScheduleButton_Click(object sender, EventArgs e)
        {
            editScheduleGroupBox.BringToFront();
        }

        private void availabilityButton_Click(object sender, EventArgs e)
        {
            viewAvailabilityGroupBox.BringToFront();
        }

        private void PTORequestButton_Click(object sender, EventArgs e)
        {
            PTORequestGroupBox.BringToFront();
        }

        private void viewTimesheetButton_Click(object sender, EventArgs e)
        {
            viewTimesheetGroupBox.BringToFront();
        }

        // Logout Functions
        // - - - - -    Samuel Griggs
        private void logoutButton_Click(object sender, EventArgs e) {
            logoutPanel.Show();
        }
        private void ConfirmLogoutButton_Click(object sender, EventArgs e) {
            parent.Show();
            this.Close();
        }
        private void CancelLogoutButton_Click(object sender, EventArgs e) {
            logoutPanel.Hide();
        }

        // Miscellaneous Functions

        private void updateStatus(bool status) {
            isClocked = status;
        }

        
    }
}
