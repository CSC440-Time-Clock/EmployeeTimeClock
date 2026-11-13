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
        int employeeID;     // Needed for Database Access

        bool isClocked;
        DateTime clockInTime;
        DateTime clockOutTime;
        bool isOnBreak;
        DateTime startBreakTime;
        DateTime endBreakTime;

        public Manager(Form parentForm, int ID)
        {
            // Initializations
            parent = parentForm;
            InitializeComponent();

            employeeID = ID;
            isClocked = false;
            isOnBreak = false;

            //  Grab Information from the Database
            //      Grab clockIn/Out Time if available
            //      Grab start/end BreakTime if available
            //      Determine if currently Clocked or OnBreak

            // Default Hidden Panels
            logoutPanel.Hide();

            // Status Label
            if (isOnBreak)
                statusLabel.Text = "Status: On Break";
            else if (isClocked)
                statusLabel.Text = "Status: Clocked In";
            //else    statusLabel.Text = "Status: Clocked Out";

            //  Set the values of labels within the Form
            //      Timesheet
            /*if (clockInTime != null)
                clockInTimeLabel.Text = clockInTime.ToString("t");
            if (clockOutTime != null)
                clockOutTimeLabel.Text = clockOutTime.ToString("t");
            if (startBreakTime != null)
                startBreakTimeLabel.Text = startBreakTime.ToString("t");
            if (endBreakTime != null)
                endBreakTimeLabel.Text = endBreakTime.ToString("t");*/


            scheduleGroupBox.BringToFront();
        }
        

        // Clock In Functionality
        // - - - - -    Samuel Griggs
        private void clockInButton_Click(object sender, EventArgs e) {
            if(!isClocked) {
                clockInTime = DateTime.Now;
                updateStatus(true);

                /*                                          *
                 *      Add Database Functionality Here     *
                 *                                          */

                statusLabel.Text = "Status: Clocked In";
                //clockInTimeLabel.Text = clockInTime.ToString("t");
            }
        }
        // Clock Out Functionality
        // - - - - -    Samuel Griggs
        private void clockOutButton_Click(object sender, EventArgs e) {
            if(isClocked) {
                clockOutTime = DateTime.Now;
                updateStatus(false);
                updateBreak(false);

                //float hrsWorked = clockOutTime.Subtract(clockInTime).Hours;
                //hrsWorked += clockOutTime.Subtract(clockInTime).Minutes / 60f;

                /*                                          *
                 *      Add Database Functionality Here     *
                 *                                          */

                statusLabel.Text = "Status: Clocked Out";
                //hrsWorkedLabel.Text = String.Format("Total Hours Worked: {0:#.###}", hrsWorked);
                //clockOutTimeLabel.Text = clockOutTime.ToString("t");
            }
        }

        // Start Break Functionality
        // - - - - -    Samuel Griggs
        private void breakStartButton_Click(object sender, EventArgs e) {
            if(!isOnBreak && isClocked) {
                startBreakTime = DateTime.Now;
                updateBreak(true);

                /*                                          *
                 *      Add Database Functionality Here     *
                 *                                          */

                statusLabel.Text = "Status: On Break";
                //startBreakTimeLabel.Text = startBreakTime.ToString("t");
            }
        }
        // End Break Functionality
        // - - - - -    Samuel Griggs
        private void breakEndButton_Click(object sender, EventArgs e) {
            if(isOnBreak && isClocked) {
                endBreakTime = DateTime.Now;
                updateBreak(false);

                /*                                          *
                 *      Add Database Functionality Here     *
                 *                                          */

                statusLabel.Text = "Status: Clocked In";
                //endBreakTimeLabel.Text = endBreakTime.ToString("t");
            }
        }


        // Logout Functions
        // - - - - -    Samuel Griggs
        private void logoutButton_Click(object sender, EventArgs e) {
            logoutPanel.Show();

            scheduleGroupBox.BringToFront();
            logoutPanel.BringToFront();
        }
        private void ConfirmLogoutButton_Click(object sender, EventArgs e) {
            parent.Show();
            this.Close();
        }
        private void CancelLogoutButton_Click(object sender, EventArgs e) {
            logoutPanel.Hide();
        }


        private void PTORequestButton_Click(object sender, EventArgs e) {
            PTORequestGroupBox.BringToFront();

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

        private void viewTimesheetButton_Click(object sender, EventArgs e)
        {
            viewTimesheetGroupBox.BringToFront();
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
