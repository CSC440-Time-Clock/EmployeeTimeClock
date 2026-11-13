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
        DateTime[,] schedule = new DateTime[7,5];   // 5 7-day weeks

        public Employee(Form parentForm, int ID)
        {
            // Initializations
            parent = parentForm;
            InitializeComponent();

            employeeID = ID;
            isClocked = false;
            isOnBreak = false;


            /*  Grab Information from the Database                      *
            *       Grab clockIn/Out Time if available                  *
            *       Grab start/end BreakTime if available               *
            *       Determine if currently Clocked or OnBreak           *
            *                                                           *
            *       Grab current month's schedule into schedule[,]      *
            *       [Day, Week] leave as DateTime.MinValue() if no      *
            *       scheduled time.                                     *
            *                                                           */


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

            //      View Schedule
            for(int d = 0; d < 7; d++) {
                for(int w = 0; w < 5; w++) {
                    // If this day of the week isn't assigned (ie. last/previous month or not yet scheduled), skip
                    if (schedule[d, w].Equals(DateTime.MinValue))
                        continue;

                    scheduleTable.GetControlFromPosition(d, w+1).Text = schedule[d, w].ToString("t");
                }
            }


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

        // Adjust Availability Functions
        //      For Employees to submit the time each day of the week they are
        //      available to work
        // - - - - -    Samuel Griggs
        private void adjustAvailabilityButton_Click(object sender, EventArgs e) {
            availabilityGroupBox.BringToFront();
        }
        private void availabilitySubmitButton_Click(object sender, EventArgs e) {
            // Start and End Time Variables
            string sundayStart, sundayEnd;
            string mondayStart, mondayEnd;
            string tuesdayStart, tuesdayEnd;
            string wednesdayStart, wednesdayEnd;
            string thursdayStart, thursdayEnd;
            string fridayStart, fridayEnd;
            string saturdayStart, saturdayEnd;

            // TextInputs - Values must be in 2400 format or just ""
            //      0 < Input < 2400  And  Start < End  or  .Equals("")
            try {
                // Sunday
                if (String.IsNullOrEmpty(sundayStartInput.Text) || (int.Parse(sundayStartInput.Text) >= 0 && int.Parse(sundayStartInput.Text) <= 2400)) {
                    sundayStart = sundayStartInput.Text;
                }
                else throw new FormatException();
                if (String.IsNullOrEmpty(sundayEndInput.Text) || (int.Parse(sundayEndInput.Text) >= 0 && int.Parse(sundayEndInput.Text) <= 2400 && int.Parse(sundayEndInput.Text) > int.Parse(sundayStartInput.Text))) {
                    sundayStart = sundayEndInput.Text;
                }
                else throw new FormatException();
                // Monday
                if (String.IsNullOrEmpty(mondayStartInput.Text) || (int.Parse(mondayStartInput.Text) >= 0 && int.Parse(mondayStartInput.Text) <= 2400)) {
                    mondayStart = mondayStartInput.Text;
                }
                else throw new FormatException();
                if (String.IsNullOrEmpty(mondayEndInput.Text) || (int.Parse(mondayEndInput.Text) >= 0 && int.Parse(mondayEndInput.Text) <= 2400 && int.Parse(mondayEndInput.Text) > int.Parse(mondayStartInput.Text))) {
                    mondayEnd = mondayEndInput.Text;
                }
                else throw new FormatException();
                // Tuesday
                if (String.IsNullOrEmpty(tuesdayStartInput.Text) || (int.Parse(tuesdayStartInput.Text) >= 0 && int.Parse(tuesdayStartInput.Text) <= 2400)) {
                    tuesdayStart = tuesdayStartInput.Text;
                }
                else throw new FormatException();
                if (String.IsNullOrEmpty(tuesdayEndInput.Text) || (int.Parse(tuesdayEndInput.Text) >= 0 && int.Parse(tuesdayEndInput.Text) <= 2400 && int.Parse(tuesdayEndInput.Text) > int.Parse(tuesdayStartInput.Text))) {
                    tuesdayEnd = tuesdayEndInput.Text;
                }
                else throw new FormatException();
                // Wednesday
                if (String.IsNullOrEmpty(wednesdayStartInput.Text) || (int.Parse(wednesdayStartInput.Text) >= 0 && int.Parse(wednesdayStartInput.Text) <= 2400)) {
                    wednesdayStart = wednesdayStartInput.Text;
                }
                else throw new FormatException();
                if (String.IsNullOrEmpty(wednesdayEndInput.Text) || (int.Parse(wednesdayEndInput.Text) >= 0 && int.Parse(wednesdayEndInput.Text) <= 2400 && int.Parse(wednesdayEndInput.Text) > int.Parse(wednesdayStartInput.Text))) {
                    wednesdayEnd = wednesdayEndInput.Text;
                }
                else throw new FormatException();
                // Thursday
                if (String.IsNullOrEmpty(thursdayStartInput.Text) || (int.Parse(thursdayStartInput.Text) >= 0 && int.Parse(thursdayStartInput.Text) <= 2400)) {
                    thursdayStart = thursdayStartInput.Text;
                }
                else throw new FormatException();
                if (String.IsNullOrEmpty(thursdayEndInput.Text) || (int.Parse(thursdayEndInput.Text) >= 0 && int.Parse(thursdayEndInput.Text) <= 2400 && int.Parse(thursdayEndInput.Text) > int.Parse(thursdayStartInput.Text))) {
                    thursdayEnd = thursdayEndInput.Text;
                }
                else throw new FormatException();
                // Friday
                if (String.IsNullOrEmpty(fridayStartInput.Text) || (int.Parse(fridayStartInput.Text) >= 0 && int.Parse(fridayStartInput.Text) <= 2400)) {
                    fridayStart = fridayStartInput.Text;
                }
                else throw new FormatException();
                if (String.IsNullOrEmpty(fridayEndInput.Text) || (int.Parse(fridayEndInput.Text) >= 0 && int.Parse(fridayEndInput.Text) <= 2400 && int.Parse(fridayEndInput.Text) > int.Parse(fridayStartInput.Text))) {
                    fridayEnd = fridayEndInput.Text;
                }
                else throw new FormatException();
                // Saturday
                if (String.IsNullOrEmpty(saturdayStartInput.Text) || (int.Parse(saturdayStartInput.Text) >= 0 && int.Parse(saturdayStartInput.Text) <= 2400)) {
                    saturdayStart = saturdayStartInput.Text;
                }
                else throw new FormatException();
                if (String.IsNullOrEmpty(saturdayEndInput.Text) || (int.Parse(saturdayEndInput.Text) >= 0 && int.Parse(saturdayEndInput.Text) <= 2400 && int.Parse(saturdayEndInput.Text) > int.Parse(saturdayStartInput.Text))) {
                saturdayEnd = saturdayEndInput.Text;
            }
                else throw new FormatException();


                //  Input times are valid.

                /*                                              *
                 *      Add Database Functionality Here         *
                 *                                              *
                 *      Input the start times for each day      *
                 *      into the database.                      *
                 *                                              */

            }
            catch (FormatException) {
                MessageBox.Show("One of your times are invalid. Please enter in 2400 Hour format or leave blank.");
            }
            catch {

            }
            
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
