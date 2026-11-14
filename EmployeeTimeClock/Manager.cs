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
        int[] emplyList;    // List of Employee ID's to be picked from
        DateTime[,] emplySchedule;  // 2D Array that holds an Employee's schedule

        public Manager(Form parentForm, int ID)
        {
            // Initializations
            parent = parentForm;
            InitializeComponent();

            employeeID = ID;
            isClocked = false;
            isOnBreak = false;

            emplyList = new int[5];  // Testing array. Remove after database functionality implemented
            emplySchedule = new DateTime[7, 5];

            //  Grab Information from the Database
            //      Grab clockIn/Out Time if available
            //      Grab start/end BreakTime if available
            //      Determine if currently Clocked or OnBreak
            //      Grab list of all Employee IDs

            // Default Hidden Panels
            logoutPanel.Hide();

            // Status Label
            if (isOnBreak)
                statusLabel.Text = "Status: On Break";
            else if (isClocked)
                statusLabel.Text = "Status: Clocked In";
            //else    statusLabel.Text = "Status: Clocked Out";

            //  Set the values of dropdowns within the Form
            //      View Employee Timesheet
            foreach (int i in emplyList)
                scheduleEmployeeSelect.Items.Add(i);
            //      Edit Employee Timesheet
            foreach (int i in emplyList)
                editTimesheetSelect.Items.Add(i);


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


        // View Employee Schedule Functions
        //      Allows the Manager to view the Schedule of their Employee's
        // - - - - -    Samuel Griggs
        private void viewScheduleButton_Click(object sender, EventArgs e)
        {
            scheduleGroupBox.BringToFront();
        }
        private void scheduleEmployeeSelect_SelectedIndexChanged(object sender, EventArgs e) {
            int ID = scheduleEmployeeSelect.SelectedIndex;  // Chosen Employee

            /*                                                          *
             *      Add Database Functionality Here                     *
             *                                                          *
             *      Grab the schedule of the selected Employee          *
             *      into emplySchedule[day, week]                       *
             *                                                          */

            for (int d = 0; d < 7; d++) {
                for (int w = 0; w < 5; w++) {
                    // If this day of the week isn't assigned (ie. last/previous month or not yet scheduled), skip
                    if (emplySchedule[d, w].Equals(DateTime.MinValue))
                        continue;

                    emplyScheduleTable.GetControlFromPosition(d, w + 1).Text = emplySchedule[d, w].ToString("t");
                }
            }
        }


        

        // Edit Timesheet Functions
        //      Allows the Manager to edit the Timesheet of an Employee
        // - - - - -    Samuel Griggs
        private void editTimesheetButton_Click(object sender, EventArgs e)
        {
            editTimesheetGroupBox.BringToFront();
        }
        private void confirmTimesheetEditButton_Click(object sender, EventArgs e) {
            int ID = int.Parse(editTimesheetSelect.SelectedItem.ToString());
            string newClockIn, newClockOut;
            string newBreakStart, newBreakEnd;
            try {
                // Values must be between 0 and 2400
                if (int.Parse(emplyNewClockIn.Text) >= 0 && int.Parse(emplyNewClockIn.Text) <= 2400 && int.Parse(emplyNewClockOut.Text) >= 0 && int.Parse(emplyNewClockOut.Text) <= 2400) {
                    // Clock In and Clock out are valid values
                    if (int.Parse(emplyNewClockOut.Text) > int.Parse(emplyNewClockIn.Text)) {
                        // Clock In and Clock Out are within a valid time frame
                        newClockIn = emplyNewClockIn.Text;
                        newClockOut = emplyNewClockOut.Text;
                    }
                    else
                        throw new FormatException();


                }
                else
                    throw new FormatException();

                // Clock In and Out must be valid values at this point, otherwise an Exception would have been thrown
                if(int.Parse(emplyNewBreakStart.Text) >= int.Parse(newClockIn) && int.Parse(emplyNewBreakEnd.Text) <= int.Parse(newClockOut)) {
                    // Break start and end within the clockin and out times
                    if(int.Parse(emplyNewBreakStart.Text) <= int.Parse(emplyNewClockOut.Text)) {
                        // Break over a valid period
                        newBreakStart = emplyNewBreakStart.Text;
                        newBreakEnd = emplyNewBreakEnd.Text;
                    }
                    else
                        throw new FormatException();
                }
                else 
                    throw new FormatException();

                // New values must be valid as no exceptions have been thrown yet.

                /*                                              *
                 *      Add Database Functionality Here         *
                 *                                              */


            }
            catch(FormatException) {
                MessageBox.Show("Invalid times. Please enter all values in 2400 Hour Format.");
            }
            catch {

            }
        }

        private void editScheduleButton_Click(object sender, EventArgs e) {
            editScheduleGroupBox.BringToFront();
        }

        private void calculatePaymentButton_Click(object sender, EventArgs e)
        {
            calculatePaymentGroupBox.BringToFront();
        }

        private void createSchedule_Click(object sender, EventArgs e)
        {
            generateScheduleGroupBox.BringToFront();
        }

        

        private void availabilityButton_Click(object sender, EventArgs e)
        {
            viewAvailabilityGroupBox.BringToFront();
        }

        private void viewTimesheetButton_Click(object sender, EventArgs e)
        {
            viewTimesheetGroupBox.BringToFront();
        }

        private void PTORequestButton_Click(object sender, EventArgs e) {
            PTORequestGroupBox.BringToFront();

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
