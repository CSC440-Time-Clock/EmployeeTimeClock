using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmployeeTimeClock
{
    public partial class Employee : Form
    {
        Form parent;
        int employeeID;     // Needed for Database access


        bool isClocked;
        DateTime clockInTime;
        DateTime clockOutTime;
        bool isOnBreak;
        DateTime startBreakTime;
        DateTime endBreakTime;

        public Employee(Form parentForm, int ID)
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


            // Default Hidden Objects
            LogoutPanel.Hide();


            // Status Label
            if (isOnBreak)
                statusLabel.Text = "Status: On Break";
            else if (isClocked)
                statusLabel.Text = "Status: Clocked In";
            //else    statusLabel.Text = "Status: Clocked Out";

            //  Set the values of labels within the Form
            //      View Timesheet
            if(!clockInTime.Equals(DateTime.MinValue))
                clockInTimeLabel.Text = clockInTime.ToString("t");
            if(!clockOutTime.Equals(DateTime.MinValue))
                clockOutTimeLabel.Text = clockOutTime.ToString("t");
            if(!startBreakTime.Equals(DateTime.MinValue))
                startBreakTimeLabel.Text = startBreakTime.ToString("t");
            if(!endBreakTime.Equals(DateTime.MinValue))
                endBreakTimeLabel.Text = endBreakTime.ToString("t");
            


                scheduleGroupBox.BringToFront();
        }


        // Log Out Functions
        // - - - - -    Samuel Griggs
        private void logoutButton_Click(object sender, EventArgs e)
        {
            LogoutPanel.Show();
            
            scheduleGroupBox.BringToFront();
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
                clockInTime = DateTime.Now;
                updateStatus(true);

                /*                                              *
                 *      Add Database Functionality Here         *
                 *                                              */

                statusLabel.Text = "Status: Clocked In";
                clockInTimeLabel.Text = clockInTime.ToString("t");
            }
        }
        // Clock Out Functionality
        // - - - - -    Samuel Griggs
        private void clockOutButton_Click(object sender, EventArgs e) {
            if(isClocked) {
                clockOutTime = DateTime.Now;
                updateStatus(false);
                updateBreak(false);

                float hrsWorked = clockOutTime.Subtract(clockInTime).Hours;
                hrsWorked += clockOutTime.Subtract(clockInTime).Minutes / 60f;

                //hrsWorked = 0.25f;

                /*                                              *
                 *      Add Database Functionality Here         *
                 *      input clockOutTime                      *
                 *                                              */

                statusLabel.Text = "Status: Clocked Out";
                hrsWorkedLabel.Text = String.Format("Total Hours Worked: {0:#.###}",hrsWorked);
                clockOutTimeLabel.Text = clockOutTime.ToString("t");
            }
        }


        // Start Break Functionality
        // - - - - -    Samuel Griggs
        private void breakStartButton_Click(object sender, EventArgs e) {
            if (!isOnBreak && isClocked) {         
                startBreakTime = DateTime.Now;
                updateBreak(true);

                /*                                          *
                 *      Add Database Functionality Here     *
                 *                                          */

                statusLabel.Text = "Status: On Break";
                startBreakTimeLabel.Text = startBreakTime.ToString("t");
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
                endBreakTimeLabel.Text = endBreakTime.ToString("t");
            }
        }


        // Paid-Time Off Request Functions
        //      For Employees to request paid time off
        // - - - - -    Samuel Griggs
        private void PTORequestButton_Click(object sender, EventArgs e) {
            PTOgroupBox.BringToFront();
        }
        private void PTOsubmitButton_Click(object sender, EventArgs e) {
            int PTOhours;
            try{
                PTOhours = int.Parse(PTOHoursInput.Text);
                String PTOday = PTODayInput.SelectedItem+"";
                String PTOreason = PTOReasonInput.Text;

                /*                                          *
                 *      Add Database Functionality          *
                 *      Input PTOday, reason, and hours     *
                 *                                          *
                 *      Use a MessageBox to inform the      *
                 *      Employee that their request did     *
                 *      or did not successfully complete    *
                 *                                          */

                // Clear Inputs
                PTOHoursInput.Text = "";
                PTOReasonInput.Text = "";
                
            }
            catch(FormatException) {
                MessageBox.Show("Inputted hours must be numerical integers!");
            }
            catch(Exception exc) {
                MessageBox.Show(exc.ToString());
            }
        }

        private void adjustAvailabilityButton_Click(object sender, EventArgs e) {
            availabilityGroupBox.BringToFront();
        }

        private void viewTimesheetButton_Click(object sender, EventArgs e)
        {
            timesheetGroupBox.BringToFront();
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
