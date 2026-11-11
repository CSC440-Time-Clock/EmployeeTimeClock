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
        bool isClocked;
        bool isOnBreak;

        public Employee(Form parentForm)
        {
            // Initializations
            parent = parentForm;
            InitializeComponent();

            isClocked = false;
            isOnBreak = false;

            // Default Hidden Objects
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
        // - - - - -    Samuel Griggs
        private void logoutButton_Click(object sender, EventArgs e)
        {
            LogoutPanel.Show();
            LogoutPanel.BringToFront();
        }
        private void ConfirmLogoutButton_Click(object sender, EventArgs e) {
            parent.Show();
            this.Close();
        }
        private void CancelLogoutButton_Click(object sender, EventArgs e) {
            LogoutPanel.Hide();
        }

        // Clock In Functionality
        // - - - - -    Samuel Griggs
        private void clockInButton_Click(object sender, EventArgs e) {
            if(!isClocked)   {
                updateStatus(true);

                /*                                              *
                 *      Add Database Functionality Here         *
                 *                                              */

                statusLabel.Text = "Status: Clocked In";
            }
        }
        // Clock Out Functionality
        // - - - - -    Samuel Griggs
        private void clockOutButton_Click(object sender, EventArgs e) {
            if(isClocked) {
                updateStatus(false);
                updateBreak(false);

                /*                                              *
                 *      Add Database Functionality Here         *
                 *                                              */

                statusLabel.Text = "Status: Clocked Out";
            }
        }

        // Start Break Functionality
        // - - - - -    Samuel Griggs
        private void breakStartButton_Click(object sender, EventArgs e) {
            if (!isOnBreak && isClocked) {                
                updateBreak(true);

                /*                                          *
                 *      Add Database Functionality Here     *
                 *                                          */

                statusLabel.Text = "Status: On Break";
            }
        }
        // End Break Functionality
        // - - - - -    Samuel Griggs
        private void breakEndButton_Click(object sender, EventArgs e) {
            if(isOnBreak && isClocked) {
                updateBreak(false);

                /*                                          *
                 *      Add Database Functionality Here     *
                 *                                          */

                statusLabel.Text = "Status: Clocked In";
            }
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


        // Miscellaneous Functions

        // updateStatus
        //      Modifies the value of isClocked
        // - - - - -    Samuel Griggs
        private void updateStatus(bool status) {

            isClocked = status;
        }

        // updateBreak
        //      Modifies the value of isOnBreak
        // - - - - -    Samuel Griggs
        private void updateBreak(bool status) {
            isOnBreak = status;
        }


        
    }
}
