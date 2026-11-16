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
        DateTime[,] schedule = new DateTime[7,6];
        // expanded the employee calendar cache so dates in its sixth week can be displayed -H
        private readonly ComboBox scheduleMonthSelect = new ComboBox();

        private sealed class ScheduleMonthChoice
        {
            public DateTime Start;
            public override string ToString() { return Start.ToString("MMMM yyyy"); }
        }

        public Employee(Form parentForm, int ID)
        {
            // Initializations
            parent = parentForm;
            InitializeComponent();
            // fit the total-hours and attendance-points summary into the existing timesheet area -H
            hrsWorkedLabel.Font = new Font("Microsoft Sans Serif", 12F);

            employeeID = ID;
            // add the same current-through-six-month browsing range used by managers -H
            AddEmployeeScheduleMonthSelector();
            // show the full display name for the account that opened this screen -H
            TimeClockStore.User signedInUser = TimeClockStore.FindUser(employeeID);
            welcomeLabel.Text = "Welcome, " + (signedInUser == null ? "" : signedInUser.Name);
            // initialize employee date controls using the same corrected clock as time punches -H
            timesheetDatePicker.Value = TimeClockStore.Now.Date;
            ptoFromDatePicker.Value = TimeClockStore.Now.Date;
            ptoThroughDatePicker.Value = TimeClockStore.Now.Date;
            isClocked = false;
            isOnBreak = false;
            clockInTimeLabel.Text = "--";
            clockOutTimeLabel.Text = "--";
            startBreakTimeLabel.Text = "--";
            endBreakTimeLabel.Text = "--";
            // show the employee's accumulated points while this proof-of-concept store is in memory -H
            decimal attendancePoints = TimeClockStore.AttendancePoints.Where(item => item.EmployeeId == employeeID).Sum(item => item.Points);
            hrsWorkedLabel.Text = "Total Hours Worked This Week: 0 | Attendance Points: " + attendancePoints.ToString("0.0");
            // Initialize the attendance total shown between the welcome and status controls -H
            updateAttendancePointsLabel();
            // restore an unfinished shift after the employee returns to this form -H
            TimeClockStore.WorkPeriod activePeriod = TimeClockStore.ActivePeriod(employeeID);
            if (activePeriod != null)
            {
                clockInTime = activePeriod.ClockIn;
                isClocked = true;
                isOnBreak = activePeriod.BreakStart.HasValue && !activePeriod.BreakEnd.HasValue;
                clockInTimeLabel.Text = activePeriod.ClockIn.ToString("t");
                if (activePeriod.BreakStart.HasValue) startBreakTimeLabel.Text = activePeriod.BreakStart.Value.ToString("t");
                if (activePeriod.BreakEnd.HasValue) endBreakTimeLabel.Text = activePeriod.BreakEnd.Value.ToString("t");
            }


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
                statusLabel.Text = TimeClockStore.FormatStatus("Status: On Break");
            else if (isClocked)
                statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked In");
            else
                statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked Out");

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

            // populate the employee calendar from the selected month -H
            DateTime monthStart = new DateTime(TimeClockStore.Now.Year, TimeClockStore.Now.Month, 1);
            for (int monthOffset = 0; monthOffset <= 6; monthOffset++)
                scheduleMonthSelect.Items.Add(new ScheduleMonthChoice { Start = monthStart.AddMonths(monthOffset) });
            scheduleMonthSelect.SelectedIndexChanged += scheduleMonthSelect_SelectedIndexChanged;
            if (scheduleMonthSelect.Items.Count > 0) scheduleMonthSelect.SelectedIndex = 0;

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
        // records actual clock time, blocks early punches, and confirms a late-arrival point before saving -H
        private void clockInButton_Click(object sender, EventArgs e) {
            if (TimeClockStore.ActivePeriod(employeeID) != null) { MessageBox.Show("You are already clocked in."); return; }
            // Match today's scheduled shift, then block punches earlier than the allowed 15-minute window -H
            DateTime punchTime = TimeClockStore.Now;
            TimeClockStore.Shift scheduledShift = TimeClockStore.ScheduledShiftForDate(employeeID, punchTime.Date);
            if (scheduledShift == null) { MessageBox.Show("You do not have a scheduled shift today."); return; }
            if (punchTime < scheduledShift.Start.AddMinutes(-TimeClockStore.ClockGraceMinutes))
            {
                MessageBox.Show("You can clock in starting 15 minutes before your scheduled shift.");
                return;
            }
            if (punchTime > scheduledShift.End) { MessageBox.Show("This shift has ended. Please contact a manager if you still need to clock in."); return; }
            // Warn and require confirmation before recording the half point for an arrival beyond the grace period -H
            bool isLate = punchTime > scheduledShift.Start.AddMinutes(TimeClockStore.ClockGraceMinutes);
            if (isLate && MessageBox.Show("You are more than 15 minutes late. Continuing will add 0.5 attendance points. Continue?", "Late Clock In", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            clockInTime = punchTime;
            // A confirmed arrival replaces a previously inferred no-show with the late-arrival point when needed -H
            TimeClockStore.ReverseNoShow(employeeID, scheduledShift.Start);
            if (isLate) TimeClockStore.AddAttendancePoint(employeeID, scheduledShift.Start, 0.5m, "Late arrival");
            // Refresh the visible total after a late-arrival point is accepted -H
            updateAttendancePointsLabel();
            TimeClockStore.WorkPeriod newPeriod = new TimeClockStore.WorkPeriod { EmployeeId = employeeID, ClockIn = clockInTime };
            TimeClockDatabase.SaveWorkPeriod(newPeriod);
            TimeClockStore.WorkPeriods.Add(newPeriod);
            updateStatus(true);
            statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked In");
            clockInTimeLabel.Text = clockInTime.ToString("t");
        }
        // Clock Out Functionality
        // - - - - -    Samuel Griggs
        // confirms point-earning early or late clock-outs, then saves the actual time and worked hours -H
        private void clockOutButton_Click(object sender, EventArgs e) {
            TimeClockStore.WorkPeriod period = TimeClockStore.ActivePeriod(employeeID);
            if (period == null) { MessageBox.Show("You are not clocked in."); return; }
            if (period.BreakStart.HasValue && !period.BreakEnd.HasValue) { MessageBox.Show("End your break before clocking out."); return; }
            // Keep the real punch time and check whether it falls outside the 15-minute end-of-shift window -H
            clockOutTime = TimeClockStore.Now;
            TimeClockStore.Shift scheduledShift = TimeClockStore.ScheduledShiftForDate(employeeID, period.ClockIn.Date);
            if (scheduledShift != null && clockOutTime < scheduledShift.End.AddMinutes(-TimeClockStore.ClockGraceMinutes))
            {
                // Give the employee a chance to cancel before saving an early clock-out and its half point -H
                if (MessageBox.Show("You are clocking out more than 15 minutes before your scheduled end. Continuing will add 0.5 attendance points. Continue?", "Early Clock Out", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                TimeClockStore.AddAttendancePoint(employeeID, scheduledShift.Start, 0.5m, "Left early");
            }
            else if (scheduledShift != null && clockOutTime > scheduledShift.End.AddMinutes(TimeClockStore.ClockGraceMinutes))
            {
                // Warn and apply the same half-point rule when the employee works beyond the allowed end window -H
                if (MessageBox.Show("You are working more than 15 minutes past your scheduled end. Continuing will add 0.5 attendance points. Continue?", "Late Clock Out", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                TimeClockStore.AddAttendancePoint(employeeID, scheduledShift.Start, 0.5m, "Worked past scheduled end");
            }
            period.ClockOut = clockOutTime;
            // persist the completed punch before refreshing its summary -H
            TimeClockDatabase.SaveWorkPeriod(period);
            updateStatus(false);
            updateBreak(false);
            statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked Out");
            decimal points = TimeClockStore.AttendancePoints.Where(item => item.EmployeeId == employeeID).Sum(item => item.Points);
            hrsWorkedLabel.Text = String.Format("Total Hours Worked: {0:#.###} | Attendance Points: {1:0.0}", period.HoursWorked, points);
            // Refresh the visible total after any clock-out attendance point is accepted -H
            updateAttendancePointsLabel();
            clockOutTimeLabel.Text = clockOutTime.ToString("t");
        }


        // Start Break Functionality
        // - - - - -    Samuel Griggs
        // allows lunch after three hours on shift and records the break start -H
        private void breakStartButton_Click(object sender, EventArgs e) {
            TimeClockStore.WorkPeriod period = TimeClockStore.ActivePeriod(employeeID);
            if (period == null) { MessageBox.Show("Clock in before starting a break."); return; }
            if (period.BreakStart.HasValue && !period.BreakEnd.HasValue) { MessageBox.Show("A break is already active."); return; }
            // Enforce the three-hour minimum from the employee's actual clock-in before starting lunch -H
            DateTime earliestBreakTime = period.ClockIn.AddHours(TimeClockStore.MinimumWorkBeforeBreakHours);
            if (TimeClockStore.Now < earliestBreakTime) { MessageBox.Show("You must work at least 3 hours before starting your lunch break."); return; }
            startBreakTime = TimeClockStore.Now;
            // keep the latest break fields for the UI and append a break record for hour calculation -H
            period.BreakStart = startBreakTime;
            period.BreakEnd = null;
            period.Breaks.Add(new TimeClockStore.WorkPeriod.BreakPeriod { Start = startBreakTime });
            // save the new active break so it can resume after the employee logs back in -H
            TimeClockDatabase.SaveWorkPeriod(period);
            updateBreak(true);
            statusLabel.Text = TimeClockStore.FormatStatus("Status: On Break");
            startBreakTimeLabel.Text = startBreakTime.ToString("t");
        }
        // End Break Functionality
        // - - - - -    Samuel Griggs
        // enforces the 30-minute minimum before completing the active break -H
        private void breakEndButton_Click(object sender, EventArgs e) {
            TimeClockStore.WorkPeriod period = TimeClockStore.ActivePeriod(employeeID);
            if (period == null || !period.BreakStart.HasValue || period.BreakEnd.HasValue) { MessageBox.Show("There is no active break to end."); return; }
            endBreakTime = TimeClockStore.Now;
            // Keep the break active until it has lasted the required 30 minutes -H
            if (endBreakTime < period.BreakStart.Value.AddMinutes(TimeClockStore.MinimumBreakMinutes))
            {
                double minutesLeft = Math.Ceiling((period.BreakStart.Value.AddMinutes(TimeClockStore.MinimumBreakMinutes) - endBreakTime).TotalMinutes);
                MessageBox.Show("Your lunch break must be at least 30 minutes. Please wait " + minutesLeft + " more minute(s).");
                return;
            }
            // close both the current-break fields and the matching break entry in the work period -H
            period.BreakEnd = endBreakTime;
            period.Breaks.Last().End = endBreakTime;
            // save the completed lunch interval with the time entry -H
            TimeClockDatabase.SaveWorkPeriod(period);
            updateBreak(false);
            statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked In");
            endBreakTimeLabel.Text = endBreakTime.ToString("t");
        }


        // Paid-Time Off Request Functions
        //      For Employees to request paid time off
        // - - - - -    Samuel Griggs
        // validates and stores the employee's requested day, hours, and reason -H
        // allows one PTO request to cover every date in an inclusive range -H
        private void PTORequestButton_Click(object sender, EventArgs e) {
            PTOgroupBox.BringToFront();
        }
        private void PTOsubmitButton_Click(object sender, EventArgs e) {
            // clear an earlier result before validating this new PTO submission -H
            ptoSubmissionStatusLabel.Text = "";
            int hours;
            // validate every required PTO field before adding any requests to the store -H
            if (!int.TryParse(PTOHoursInput.Text, out hours) || hours < 1 || hours > 24) { MessageBox.Show("Enter a whole number of PTO hours between 1 and 24."); return; }
            DateTime startDate = ptoFromDatePicker.Value.Date;
            DateTime endDate = ptoThroughDatePicker.Value.Date;
            if (startDate < TimeClockStore.Now.Date) { MessageBox.Show("Select a start date that is today or later."); return; }
            if (endDate < startDate) { MessageBox.Show("The through date must be on or after the from date."); return; }
            if (String.IsNullOrWhiteSpace(PTOReasonInput.Text)) { MessageBox.Show("Enter a reason for the PTO request."); return; }
            string reason = PTOReasonInput.Text.Trim();
            DateTime submitted = TimeClockStore.Now;
            List<TimeClockStore.TimeOffRequest> newRequests = new List<TimeClockStore.TimeOffRequest>();
            // treat the chosen endpoints as inclusive and prepare one request row per date -H
            for (DateTime requestDate = startDate; requestDate <= endDate; requestDate = requestDate.AddDays(1))
                newRequests.Add(new TimeClockStore.TimeOffRequest { EmployeeId = employeeID, Day = requestDate, Hours = hours, Reason = reason, Submitted = submitted });
            // write the complete PTO date range before adding it to the employee's visible request list -H
            TimeClockDatabase.SavePtoRequests(newRequests);
            TimeClockStore.TimeOffRequests.AddRange(newRequests);
            int requestCount = newRequests.Count;
            PTOHoursInput.Clear(); PTOReasonInput.Clear();
            ptoFromDatePicker.Value = TimeClockStore.Now.Date;
            ptoThroughDatePicker.Value = TimeClockStore.Now.Date;
            // confirm the saved date range in the existing PTO designer instead of a popup -H
            ptoSubmissionStatusLabel.Text = "PTO request submitted for " + requestCount + " day(s), " + startDate.ToShortDateString() + " through " + endDate.ToShortDateString() + ".";
        }

        // Adjust Availability Functions
        //      For Employees to submit the time each day of the week they are
        //      available to work
        // - - - - -    Samuel Griggs
        // stores a validated availability window for each selected weekday -H
        private void adjustAvailabilityButton_Click(object sender, EventArgs e) {
            availabilityGroupBox.BringToFront();
        }
        private void availabilitySubmitButton_Click(object sender, EventArgs e) {
            // keep controls in Sunday-to-Saturday order so each entry maps to its DayOfWeek value -H
            TextBox[] starts = { sundayStartInput, mondayStartInput, tuesdayStartInput, wednesdayStartInput, thursdayStartInput, fridayStartInput, saturdayStartInput };
            TextBox[] ends = { sundayEndInput, mondayEndInput, tuesdayEndInput, wednesdayEndInput, thursdayEndInput, fridayEndInput, saturdayEndInput };
            var submitted = new List<TimeClockStore.Availability>();
            for (int day = 0; day < starts.Length; day++) {
                // reject half-filled or invalid windows before replacing the employee's saved availability -H
                bool hasStart = !String.IsNullOrWhiteSpace(starts[day].Text);
                bool hasEnd = !String.IsNullOrWhiteSpace(ends[day].Text);
                if (hasStart != hasEnd) { MessageBox.Show("Enter both a start and end time, or leave both blank."); return; }
                if (!hasStart) continue;
                TimeSpan startTime, endTime;
                if (!TryParseAvailabilityTime(starts[day].Text, out startTime) || !TryParseAvailabilityTime(ends[day].Text, out endTime) || endTime <= startTime) {
                    MessageBox.Show("Enter each time in 24-hour HHMM format and make sure the end is after the start."); return;
                }
                submitted.Add(new TimeClockStore.Availability { EmployeeId = employeeID, Day = (DayOfWeek)day, Start = startTime, End = endTime });
            }
            // replace this employee's prior weekly availability only after all entered days pass validation -H
            // save the validated weekday windows before updating the shared availability cache -H
            TimeClockDatabase.ReplaceAvailability(employeeID, submitted);
            TimeClockStore.Availabilities.RemoveAll(item => item.EmployeeId == employeeID);
            TimeClockStore.Availabilities.AddRange(submitted);
            MessageBox.Show("Availability saved.");
        }

        // reads HHMM availability values and rejects invalid clock times -H
        private bool TryParseAvailabilityTime(string value, out TimeSpan time) {
            // convert HHMM text to a time span and accept 2400 only as the end-of-day boundary -H
            time = TimeSpan.Zero;
            int numericTime;
            if (!int.TryParse(value, out numericTime) || numericTime < 0 || numericTime > 2400) return false;
            int hours = numericTime / 100;
            int minutes = numericTime % 100;
            if (hours == 24 && minutes == 0) { time = TimeSpan.FromDays(1); return true; }
            if (hours > 23 || minutes > 59) return false;
            time = new TimeSpan(hours, minutes, 0);
            return true;
        }
        // load the selected date's clock and break values into the timesheet table -H
        private void viewTimesheetButton_Click(object sender, EventArgs e)
        {
            updateEmployeeTimesheetForSelectedDate();
            timesheetGroupBox.BringToFront();
        }

        // refresh the built-in clock and break rows when the selected date changes -H
        private void timesheetDatePicker_ValueChanged(object sender, EventArgs e)
        {
            updateEmployeeTimesheetForSelectedDate();
        }

        // display the saved work period and daily total for the selected calendar date -H
        private void updateEmployeeTimesheetForSelectedDate()
        {
            DateTime selectedDate = timesheetDatePicker.Value.Date;
            List<TimeClockStore.WorkPeriod> periods = TimeClockStore.Timesheet(employeeID, selectedDate, selectedDate).ToList();
            timesheetGroupBox.Text = "Timesheet - " + selectedDate.ToShortDateString();
            TimeClockStore.WorkPeriod period = periods.LastOrDefault();
            if (period == null)
            {
                // make missing clock data clear while keeping the date and table visible -H
                clockInTimeLabel.Text = "No timesheet entry";
                clockOutTimeLabel.Text = "--";
                startBreakTimeLabel.Text = "--";
                endBreakTimeLabel.Text = "--";
            }
            else
            {
                // show each value from the selected employee's saved work period -H
                clockInTimeLabel.Text = period.ClockIn.ToString("h:mm tt");
                clockOutTimeLabel.Text = period.ClockOut.HasValue ? period.ClockOut.Value.ToString("h:mm tt") : "In progress";
                startBreakTimeLabel.Text = period.BreakStart.HasValue ? period.BreakStart.Value.ToString("h:mm tt") : "--";
                endBreakTimeLabel.Text = period.BreakEnd.HasValue ? period.BreakEnd.Value.ToString("h:mm tt") : "--";
            }
            decimal points = TimeClockStore.AttendancePoints.Where(item => item.EmployeeId == employeeID).Sum(item => item.Points);
            hrsWorkedLabel.Text = String.Format("Hours Worked: {0:#.###} | Attendance Points: {1:0.0}", periods.Sum(item => item.HoursWorked), points);
        }

        // Display the employee's current attendance-point total in the header -H
        private void updateAttendancePointsLabel()
        {
            decimal points = TimeClockStore.AttendancePoints
                .Where(item => item.EmployeeId == employeeID)
                .Sum(item => item.Points);
            attendancePointsLabel.Text = String.Format("Attendance Points: {0:0.0}", points);
        }

        // add month selection above the employee calendar without changing its weekday grid -H
        private void AddEmployeeScheduleMonthSelector()
        {
            Label monthLabel = new Label
            {
                Name = "scheduleMonthLabel",
                Text = "Month",
                AutoSize = true,
                Font = new Font("Microsoft Sans Serif", 12F),
                Location = new Point(10, 48)
            };
            scheduleMonthSelect.Name = "scheduleMonthSelect";
            scheduleMonthSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            scheduleMonthSelect.Font = new Font("Microsoft Sans Serif", 12F);
            scheduleMonthSelect.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            scheduleMonthSelect.SetBounds(100, 45, scheduleGroupBox.ClientSize.Width - 112, 32);
            scheduleTable.Dock = DockStyle.None;
            scheduleTable.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            scheduleTable.SetBounds(3, 82, scheduleGroupBox.ClientSize.Width - 6, scheduleGroupBox.ClientSize.Height - 85);
            scheduleGroupBox.Controls.Add(monthLabel);
            scheduleGroupBox.Controls.Add(scheduleMonthSelect);
            scheduleTable.BringToFront();
            scheduleMonthSelect.BringToFront();
            monthLabel.BringToFront();
        }

        // add or remove the sixth week row only when the selected month needs it -H
        private void SetScheduleWeekRows(TableLayoutPanel calendar, string namePrefix, DateTime monthStart)
        {
            int neededWeeks = ((int)monthStart.DayOfWeek + DateTime.DaysInMonth(monthStart.Year, monthStart.Month) + 6) / 7;
            bool needsSixthWeek = neededWeeks > 5;
            List<Control> sixthWeekCells = calendar.Controls.Cast<Control>()
                .Where(control => control.Name.StartsWith(namePrefix, StringComparison.Ordinal))
                .ToList();
            if (needsSixthWeek && sixthWeekCells.Count == 0)
            {
                // create the extra date labels only for months that span six calendar rows -H
                for (int day = 0; day < 7; day++)
                {
                    Label cell = new Label
                    {
                        Name = namePrefix + day,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.TopLeft,
                        Padding = new Padding(6),
                        BorderStyle = BorderStyle.FixedSingle
                    };
                    calendar.Controls.Add(cell, day, 6);
                }
            }
            else if (!needsSixthWeek)
            {
                // remove generated sixth-week cells when switching back to a shorter month -H
                foreach (Control cell in sixthWeekCells)
                {
                    calendar.Controls.Remove(cell);
                    cell.Dispose();
                }
            }
            int displayedWeeks = needsSixthWeek ? 6 : 5;
            calendar.RowStyles.Clear();
            calendar.RowCount = displayedWeeks + 1;
            calendar.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            for (int week = 0; week < displayedWeeks; week++)
                calendar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / displayedWeeks));
        }

        // refresh the employee calendar when its month selection changes -H
        private void scheduleMonthSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScheduleMonthChoice month = scheduleMonthSelect.SelectedItem as ScheduleMonthChoice;
            if (month != null) loadEmployeeScheduleCalendar(month.Start);
        }

        // display the current month dates with saved shifts in the existing employee calendar -H
        private void loadEmployeeScheduleCalendar(DateTime monthStart)
        {
            SetScheduleWeekRows(scheduleTable, "employeeScheduleWeek6", monthStart);
            // clear placeholder text and keep each designer label readable as a calendar cell -H
            for (int week = 1; week <= 6; week++)
                for (int day = 0; day < 7; day++)
                {
                    Label cell = scheduleTable.GetControlFromPosition(day, week) as Label;
                    if (cell == null) continue;
                    cell.Text = "";
                    cell.Dock = DockStyle.Fill;
                    cell.TextAlign = ContentAlignment.TopLeft;
                    cell.Padding = new Padding(6);
                    cell.BorderStyle = BorderStyle.FixedSingle;
                }
            scheduleGroupBox.Text = "Schedule - " + monthStart.ToString("MMMM yyyy");
            // put each date in its weekday column even when no shift has been assigned -H
            int firstWeekday = (int)monthStart.DayOfWeek;
            for (int dayNumber = 1; dayNumber <= DateTime.DaysInMonth(monthStart.Year, monthStart.Month); dayNumber++)
            {
                int position = firstWeekday + dayNumber - 1;
                int week = position / 7 + 1;
                int weekday = position % 7;
                if (week > 6) continue;
                Label cell = scheduleTable.GetControlFromPosition(weekday, week) as Label;
                if (cell == null) continue;
                DateTime date = monthStart.AddDays(dayNumber - 1);
                string[] shiftTimes = TimeClockStore.Shifts
                    .Where(item => item.EmployeeId == employeeID && item.Start.Date == date.Date)
                    .OrderBy(item => item.Start)
                    .Select(item => item.Start.ToString("h:mm tt") + " - " + item.End.ToString("h:mm tt"))
                    .ToArray();
                cell.Text = dayNumber.ToString() + (shiftTimes.Length == 0 ? "" : Environment.NewLine + String.Join(Environment.NewLine, shiftTimes));
            }
        }
        // show the schedule page that already exists in the employee form -H
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
