using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmployeeTimeClock
{
    public partial class Manager : Form
    {
        private sealed class EmployeeChoice
        {
            // keep the database-style ID with the display name shown in manager selectors -H
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() { return Name; }
        }

        private sealed class WeekChoice
        {
            // store the Sunday that identifies this displayed Sunday-to-Saturday week -H
            public DateTime Start;
            public override string ToString()
            {
                // use one consistent Sunday-based week label with its exact date range -H
                int weekNumber = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(Start, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
                return "Week " + weekNumber + " (" + Start.ToShortDateString() + " - " + Start.AddDays(6).ToShortDateString() + ")";
            }
        }

        private sealed class MonthChoice
        {
            // store the first date of the month rendered in the manager schedule calendar -H
            public DateTime Start;
            public override string ToString() { return Start.ToString("MMMM yyyy"); }
        }

        Form parent;
        int employeeID;     // Needed for Database Access

        bool isClocked;
        DateTime clockInTime;
        DateTime clockOutTime;
        bool isOnBreak;
        DateTime startBreakTime;
        DateTime endBreakTime;
        DateTime[,] emplySchedule;  // 2D Array that holds an Employee's schedule
        // remember the week currently shown and whether its editable table has unsaved changes -H
        DateTime activeScheduleWeek;
        bool scheduleWeekDirty;
        private TimeClockStore.TimeOffRequest selectedPtoRequest;
        private readonly List<Label[]> ptoRequestDataRows = new List<Label[]>();
        private TimeClockStore.Shift selectedScheduleForEdit;

        public Manager(Form parentForm, int ID)
        {
            // Initializations
            parent = parentForm;
            InitializeComponent();

            employeeID = ID;
            // show the full display name for the account that opened this screen -H
            TimeClockStore.User signedInUser = TimeClockStore.FindUser(employeeID);
            welcomeLabel.Text = "Welcome, " + (signedInUser == null ? "" : signedInUser.Name);
            // set manager timesheet date pickers from the synchronized application clock -H
            editTimesheetDatePicker.Value = TimeClockStore.Now.Date;
            viewTimesheetDatePicker.Value = TimeClockStore.Now.Date;
            isClocked = false;
            isOnBreak = false;

            // create display objects once, then reuse them in all manager employee selectors -H
            List<EmployeeChoice> employeeChoices = TimeClockStore.Employees
                .Select(user => new EmployeeChoice { Id = user.Id, Name = user.Name }).ToList();
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
                statusLabel.Text = TimeClockStore.FormatStatus("Status: On Break");
            else if (isClocked)
                statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked In");
            else
                statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked Out");

            //  Set the values of dropdowns within the Form
            //      View Employee Timesheet
            foreach (EmployeeChoice employee in employeeChoices)
            {
                scheduleEmployeeSelect.Items.Add(employee);
                employeeToView.Items.Add(employee);
                comboBox7.Items.Add(employee);
                comboBox2.Items.Add(employee);
                comboBox3.Items.Add(employee);
                comboBox4.Items.Add(employee);
                comboBox6.Items.Add(employee);
            }
            // include manager accounts in the view-only schedule selector -H
            foreach (TimeClockStore.User managerUser in TimeClockStore.Users.Where(user => user.IsActive && user.IsManager))
                scheduleEmployeeSelect.Items.Add(new EmployeeChoice { Id = managerUser.Id, Name = managerUser.Name });
            // list the current month and the next six months available to schedule -H
            DateTime currentMonth = new DateTime(TimeClockStore.Now.Year, TimeClockStore.Now.Month, 1);
            for (int monthOffset = 0; monthOffset <= 6; monthOffset++)
                scheduleMonthSelect.Items.Add(new MonthChoice { Start = currentMonth.AddMonths(monthOffset) });
            if (scheduleMonthSelect.Items.Count > 0) scheduleMonthSelect.SelectedIndex = 0;
            loadManagerScheduleCalendar();

            // list the current Sunday-start week and future weeks through the next six months -H
            scheduleWeekSelect.DropDownStyle = ComboBoxStyle.DropDownList;
            DateTime currentSunday = TimeClockStore.Now.Date.AddDays(-(int)TimeClockStore.Now.DayOfWeek);
            DateTime lastWeek = TimeClockStore.Now.Date.AddMonths(6);
            for (DateTime weekStart = currentSunday; weekStart <= lastWeek; weekStart = weekStart.AddDays(7))
                scheduleWeekSelect.Items.Add(new WeekChoice { Start = weekStart });
            if (scheduleWeekSelect.Items.Count > 0) scheduleWeekSelect.SelectedIndex = 0;
            scheduleWeekSelect.SelectedIndexChanged += scheduleWeekChanged;
            editSchedulePreferencesButton.Click += editSchedulePreferencesButton_Click;

            // capture manual table edits so changing weeks does not discard them -H
            foreach (TextBox input in scheduleSettingInputs().SelectMany(day => day)) input.TextChanged += scheduleSettingChanged;
            loadScheduleWeek(currentSunday);
            button3.Click += calculatePaymentForSelectedEmployee_Click;
            button4.Click += viewSelectedAvailability_Click;
            button5.Click += viewSelectedPtoRequests_Click;
            ptoRequestScrollPanel.Resize += ptoRequestContainer_Resize;
            approvePtoButton.Click += decideSelectedPtoRequest_Click;
            denyPtoButton.Click += decideSelectedPtoRequest_Click;
            generateScheduleButton.Click += saveGeneratedSchedule_Click;
            button6.Click += editSelectedSchedule_Click;
            saveEditedScheduleButton.Click += saveSelectedScheduleEdit_Click;
            removeEditedScheduleButton.Click += removeSelectedScheduleEdit_Click;
            clearScheduleEditButton.Click += clearSelectedScheduleEdit_Click;
            button1.Click += loadSelectedTimesheetForEdit_Click;
            ComboBox[] employeeSelectors = { scheduleEmployeeSelect, employeeToView, comboBox2, comboBox3, comboBox4, comboBox6, comboBox7 };
            foreach (ComboBox selector in employeeSelectors) selector.DropDownStyle = ComboBoxStyle.DropDownList;
            confirmEmployee.Click += updateManagerTimesheetLabels;

            // mark expired scheduled shifts as no-shows when the manager opens the application -H
            recordMissedShiftPoints();


            scheduleGroupBox.BringToFront();
        }

        // calculates pay from completed hours for the selected employee and date range -H
        private void calculatePaymentForSelectedEmployee_Click(object sender, EventArgs e)
        {
            if (comboBox2.SelectedItem == null) { MessageBox.Show("Select an employee."); return; }
            TimeClockStore.User user = TimeClockStore.FindUser(GetSelectedEmployeeId(comboBox2));
            // calculate the previous complete Monday-through-Sunday pay period -H
            int daysSinceMonday = ((int)TimeClockStore.Now.DayOfWeek + 6) % 7;
            DateTime thisMonday = TimeClockStore.Now.Date.AddDays(-daysSinceMonday);
            DateTime from = thisMonday.AddDays(-7);
            DateTime through = thisMonday.AddDays(-1);
            double hours = TimeClockStore.TotalHours(user.Id, from, through);
            // show attendance points alongside pay without changing the calculated wage amount -H
            decimal points = TimeClockStore.AttendancePoints.Where(item => item.EmployeeId == user.Id).Sum(item => item.Points);
            label50.Text = "Total Amount: $" + (hours * (double)user.HourlyRate).ToString("F2") + " (" + hours.ToString("F2") + " hours, " + from.ToShortDateString() + " - " + through.ToShortDateString() + ") | Attendance Points: " + points.ToString("0.0");
        }

        // records a full point for a scheduled shift with no clock-in after its end -H
        private void recordMissedShiftPoints()
        {
            // inspect ended shifts; a clock-in or approved PTO prevents an automatic no-show point -H
            foreach (TimeClockStore.Shift shift in TimeClockStore.Shifts.Where(item => item.End.AddMinutes(15) < TimeClockStore.Now).ToList())
            {
                bool hasClockIn = TimeClockStore.WorkPeriods.Any(item => item.EmployeeId == shift.EmployeeId && item.ClockIn.Date == shift.Start.Date);
                bool hasApprovedPto = TimeClockStore.TimeOffRequests.Any(item => item.EmployeeId == shift.EmployeeId && item.Day.Date == shift.Start.Date && item.Status == "Approved");
                if (!hasClockIn && !hasApprovedPto) TimeClockStore.AddAttendancePoint(shift.EmployeeId, shift.Start, 1.0m, "No show");
            }
        }

        // loads the selected employee and date into the existing edit-time fields -H
        private void loadSelectedTimesheetForEdit_Click(object sender, EventArgs e)
        {
            if (comboBox7.SelectedItem == null) { MessageBox.Show("Select an employee and date."); return; }
            int id = GetSelectedEmployeeId(comboBox7);
            // the date picker permits viewing a date even when the employee has no saved shift yet -H
            DateTime date = editTimesheetDatePicker.Value.Date;
            TimeClockStore.WorkPeriod period = TimeClockStore.WorkPeriods.LastOrDefault(item => item.EmployeeId == id && item.ClockIn.Date == date.Date);
            if (period == null)
            {
                emplyNewClockIn.Clear(); emplyNewClockOut.Clear(); emplyNewBreakStart.Clear(); emplyNewBreakEnd.Clear();
                label59.Text = "No timesheet entry for " + date.ToShortDateString() + ". Enter times and confirm to create one.";
                return;
            }
            emplyNewClockIn.Text = period.ClockIn.ToString("HHmm");
            emplyNewClockOut.Text = period.ClockOut.HasValue ? period.ClockOut.Value.ToString("HHmm") : "";
            TimeClockStore.WorkPeriod.BreakPeriod breakPeriod = period.Breaks.FirstOrDefault(item => item.End.HasValue);
            emplyNewBreakStart.Text = breakPeriod == null ? "" : breakPeriod.Start.ToString("HHmm");
            emplyNewBreakEnd.Text = breakPeriod == null ? "" : breakPeriod.End.Value.ToString("HHmm");
            label59.Text = String.Format("Total Hours Worked: {0:#.###}", period.HoursWorked);
        }

        // displays the selected employee's submitted weekly availability -H
        private void viewSelectedAvailability_Click(object sender, EventArgs e)
        {
            if (comboBox3.SelectedItem == null) { MessageBox.Show("Select an employee."); return; }
            int id = GetSelectedEmployeeId(comboBox3);
            // fill the designer's weekday rows with this employee's submitted availability -H
            Label[] startLabels = { label26, label29, label32, label35, label38, label41, label44 };
            Label[] endLabels = { label27, label30, label33, label36, label39, label42, label45 };
            for (int day = 0; day < 7; day++)
            {
                TimeClockStore.Availability entry = TimeClockStore.Availabilities.FirstOrDefault(item => item.EmployeeId == id && (int)item.Day == day);
                startLabels[day].Text = entry == null ? "N/A" : TimeClockStore.Now.Date.Add(entry.Start).ToString("h:mm tt");
                endLabels[day].Text = entry == null ? "N/A" : TimeClockStore.Now.Date.Add(entry.End).ToString("h:mm tt");
            }
            TimeClockStore.User employee = TimeClockStore.FindUser(id);
            label21.Text = employee.Name + "'s submitted availability";
            viewAvailabilityGroupBox.BringToFront();
        }

        // show selected employee PTO requests in the designer table and allow a row to be selected -H
        private void viewSelectedPtoRequests_Click(object sender, EventArgs e)
        {
            if (comboBox4.SelectedItem == null) { MessageBox.Show("Select an employee."); return; }
            int id = GetSelectedEmployeeId(comboBox4);
            // sort the saved requests by date and submission time before displaying their label rows -H
            List<TimeClockStore.TimeOffRequest> requests = TimeClockStore.TimeOffRequests
                .Where(item => item.EmployeeId == id).OrderBy(item => item.Day).ThenBy(item => item.Submitted).ToList();
            TimeClockStore.User employee = TimeClockStore.FindUser(id);
            groupBox1.Text = employee.Name + " PTO Requests";
            selectedPtoRequest = null;
            // remove only prior data labels while preserving the designer's four column headers -H
            foreach (Label[] row in ptoRequestDataRows)
                foreach (Label cell in row)
                {
                    PTO_Table.Controls.Remove(cell);
                    cell.Dispose();
                }
            ptoRequestDataRows.Clear();
            PTO_Table.SuspendLayout();
            // rebuild a fixed-height row for the headers and every PTO request -H
            PTO_Table.RowCount = requests.Count + 1;
            PTO_Table.RowStyles.Clear();
            PTO_Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            for (int row = 0; row < requests.Count; row++)
                PTO_Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            // add one fixed-height table row of labels for each saved request -H
            for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
            {
                TimeClockStore.TimeOffRequest request = requests[requestIndex];
                int rowIndex = requestIndex + 1;
                string[] values = { request.Day.ToShortDateString(), request.Hours.ToString(), request.Reason, request.Status };
                Label[] cells = new Label[4];
                for (int column = 0; column < cells.Length; column++)
                {
                    cells[column] = new Label
                    {
                        Text = values[column], Tag = request, Dock = DockStyle.Top, AutoSize = false,
                        AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 2, 4, 2),
                        Font = new Font("Microsoft Sans Serif", 12F), BackColor = SystemColors.Window, ForeColor = SystemColors.ControlText
                    };
                    cells[column].Click += ptoRequestCell_Click;
                    PTO_Table.Controls.Add(cells[column], column, rowIndex);
                }
                ptoRequestDataRows.Add(cells);
            }
            PTO_Table.ResumeLayout(true);
            resizePtoRequestTable();
            label46.Text = requests.Count == 0 ? "This employee has no PTO requests" : "Select a request row to approve or deny it";
            updatePtoDecisionButtons();
        }

        // resize the PTO table within a vertical scroll area and keep request rows readable -H
        private void ptoRequestContainer_Resize(object sender, EventArgs e)
        {
            resizePtoRequestTable();
        }

        // use available group box space without adding a horizontal scroll bar -H
        private void resizePtoRequestTable()
        {
            int requestCount = ptoRequestDataRows.Count;
            int availableHeight = Math.Max(98, ptoRequestScrollPanel.ClientSize.Height);
            int rowHeight = requestCount == 0 ? 48 : Math.Min(120, Math.Max(48, (availableHeight - 50) / requestCount));
            int contentHeight = 50 + requestCount * rowHeight;
            PTO_Table.Dock = DockStyle.None;
            PTO_Table.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            PTO_Table.Location = Point.Empty;
            PTO_Table.AutoScroll = false;
            PTO_Table.Width = Math.Max(0, ptoRequestScrollPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
            PTO_Table.Height = Math.Max(availableHeight, contentHeight);
            PTO_Table.RowCount = requestCount + 1;
            PTO_Table.RowStyles.Clear();
            PTO_Table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            for (int row = 0; row < requestCount; row++)
                PTO_Table.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        }

        // select a displayed PTO row and highlight its four label cells -H
        private void ptoRequestCell_Click(object sender, EventArgs e)
        {
            Label clickedCell = sender as Label;
            selectedPtoRequest = clickedCell == null ? null : clickedCell.Tag as TimeClockStore.TimeOffRequest;
            foreach (Label[] row in ptoRequestDataRows)
            {
                bool isSelected = selectedPtoRequest != null && Object.ReferenceEquals(row[0].Tag, selectedPtoRequest);
                foreach (Label cell in row)
                {
                    cell.BackColor = isSelected ? SystemColors.Highlight : SystemColors.Window;
                    cell.ForeColor = isSelected ? SystemColors.HighlightText : SystemColors.ControlText;
                }
            }
            if (selectedPtoRequest != null)
                label46.Text = selectedPtoRequest.Status + " PTO for " + selectedPtoRequest.Day.ToShortDateString();
            updatePtoDecisionButtons();
        }

        // enable approval buttons only when the selected request is pending -H
        private void updatePtoDecisionButtons()
        {
            bool canDecide = selectedPtoRequest != null && selectedPtoRequest.Status == "Pending";
            approvePtoButton.Enabled = canDecide;
            denyPtoButton.Enabled = canDecide;
        }

        // save a manager decision and refresh the selected request label -H
        private void decideSelectedPtoRequest_Click(object sender, EventArgs e)
        {
            TimeClockStore.TimeOffRequest request = selectedPtoRequest;
            if (request == null || request.Status != "Pending") return;
            request.Status = sender == approvePtoButton ? "Approved" : "Denied";
            request.ReviewedBy = employeeID;
            request.ReviewedAt = TimeClockStore.Now;
            // persist the manager decision before updating the visible label cells -H
            TimeClockDatabase.SavePtoDecision(request);
            foreach (Label[] row in ptoRequestDataRows)
                if (Object.ReferenceEquals(row[0].Tag, request)) row[3].Text = request.Status;
            label46.Text = request.Status + " PTO for " + request.Day.ToShortDateString();
            updatePtoDecisionButtons();
        }
        // gets the employee ID associated with a name shown in any manager dropdown -H
        private int GetSelectedEmployeeId(ComboBox selector)
        {
            // convert the selected display object back to the ID used by stored records -H
            EmployeeChoice employee = selector.SelectedItem as EmployeeChoice;
            return employee == null ? -1 : employee.Id;
        }

        // saves the selected week's overrides for business hours and minimum staffing -H
        private void scheduleWeekChanged(object sender, EventArgs e)
        {
            // save the old week only if edited, then load the selected week's values -H
            WeekChoice selectedWeek = scheduleWeekSelect.SelectedItem as WeekChoice;
            if (selectedWeek == null) return;
            if (scheduleWeekDirty && !saveScheduleWeekEdits(activeScheduleWeek))
            {
                int previousIndex = -1;
                for (int index = 0; index < scheduleWeekSelect.Items.Count; index++)
                {
                    WeekChoice item = scheduleWeekSelect.Items[index] as WeekChoice;
                    if (item != null && item.Start.Date == activeScheduleWeek.Date) { previousIndex = index; break; }
                }
                if (previousIndex >= 0 && scheduleWeekSelect.SelectedIndex != previousIndex)
                    scheduleWeekSelect.SelectedIndex = previousIndex;
                return;
            }
            loadScheduleWeek(selectedWeek.Start);
        }

        // marks week-specific schedule settings as changed by the manager -H
        private void scheduleSettingChanged(object sender, EventArgs e)
        {
            // this flag lets week navigation distinguish defaults from intentional overrides -H
            scheduleWeekDirty = true;
        }

        // loads a selected week's saved values or the normal weekday preferences -H
        private void loadScheduleWeek(DateTime weekStart)
        {
            // each day row uses three cells in order: opening, closing, and minimum on-duty staff -H
            TextBox[][] entries = scheduleSettingInputs();
            string[] savedValues;
            if (TimeClockStore.ScheduleWeekEdits.TryGetValue(weekStart.Date, out savedValues))
            {
                // restore an existing week override before falling back to standard preferences -H
                for (int day = 0; day < 7; day++)
                    for (int column = 0; column < 3; column++) entries[day][column].Text = savedValues[day * 3 + column];
            }
            else
            {
                for (int day = 0; day < 7; day++)
                {
                    // use the saved weekday template for weeks without special event changes -H
                    TimeClockStore.ScheduleDayPreference preference = TimeClockStore.SchedulePreferences.First(item => (int)item.Day == day);
                    entries[day][0].Text = preference.IsOpen ? preference.OpeningTime.ToString(@"hhmm") : "";
                    entries[day][1].Text = preference.IsOpen ? preference.ClosingTime.ToString(@"hhmm") : "";
                    entries[day][2].Text = preference.IsOpen ? preference.MinimumStaff.ToString() : "";
                }
            }
            activeScheduleWeek = weekStart.Date;
            scheduleWeekDirty = false;
        }

        // keeps editable day values when a manager changes the selected week -H
        private bool saveScheduleWeekEdits(DateTime weekStart)
        {
            if (weekStart == DateTime.MinValue) return false;
            // flatten the seven table rows into a stable 21-value snapshot keyed by week start -H
            TextBox[][] entries = scheduleSettingInputs();
            string[] values = new string[21];
            for (int day = 0; day < 7; day++)
                for (int column = 0; column < 3; column++) values[day * 3 + column] = entries[day][column].Text.Trim();
            for (int day = 0; day < 7; day++)
            {
                string opening = values[day * 3];
                string closing = values[day * 3 + 1];
                string staff = values[day * 3 + 2];
                bool allBlank = String.IsNullOrWhiteSpace(opening) && String.IsNullOrWhiteSpace(closing) && String.IsNullOrWhiteSpace(staff);
                if (allBlank) continue;
                TimeSpan start, end; int count;
                if (!TryParseScheduleTime(opening, out start) || !TryParseScheduleTime(closing, out end) || end <= start || !Int32.TryParse(staff, out count) || count < 1)
                {
                    MessageBox.Show("Enter valid opening and closing HHMM values and a positive staffing count for each open day, or leave all three fields blank for a closed day.");
                    return false;
                }
            }
            // persist the validated override before replacing its in-memory copy -H
            if (!TimeClockDatabase.SaveScheduleWeek(weekStart, values, employeeID)) return false;
            TimeClockStore.ScheduleWeekEdits[weekStart.Date] = values;
            scheduleWeekDirty = false;
            return true;
        }

        // maps the seven day rows in the schedule designer to their three editable values -H
        private TextBox[][] scheduleSettingInputs()
        {
            // keep the designer's numbered textboxes mapped to weekday rows in display order -H
            return new TextBox[][] {
                new[] { textBox1, textBox2, textBox3 }, new[] { textBox4, textBox5, textBox6 },
                new[] { textBox7, textBox8, textBox9 }, new[] { textBox10, textBox11, textBox12 },
                new[] { textBox13, textBox14, textBox15 }, new[] { textBox16, textBox17, textBox18 },
                new[] { textBox19, textBox20, textBox21 }
            };
        }

        // edits and saves the reusable weekly opening, closing, and staffing preferences -H
        private void editSchedulePreferencesButton_Click(object sender, EventArgs e)
        {
            // retain pending edits before showing the reusable default-hours dialog -H
            if (scheduleWeekDirty && !saveScheduleWeekEdits(activeScheduleWeek)) return;
            using (Form dialog = new Form())
            using (TableLayoutPanel table = new TableLayoutPanel())
            using (Label bufferLabel = new Label())
            using (NumericUpDown setupInput = new NumericUpDown())
            using (NumericUpDown cleanupInput = new NumericUpDown())
            using (Button saveButton = new Button())
            using (Button cancelButton = new Button())
            {
                dialog.Text = "Schedule Preferences"; dialog.StartPosition = FormStartPosition.CenterParent; dialog.ClientSize = new Size(760, 480);
                table.SetBounds(12, 12, 736, 350); table.ColumnCount = 5; table.RowCount = 8; table.Dock = DockStyle.Top;
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
                string[] headers = { "Day", "Open (HHMM)", "Close (HHMM)", "Minimum on duty", "Closed" };
                for (int col = 0; col < headers.Length; col++) table.Controls.Add(new Label { Text = headers[col], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, col, 0);
                var openInputs = new TextBox[7]; var closeInputs = new TextBox[7]; var staffInputs = new NumericUpDown[7]; var closedInputs = new CheckBox[7];
                for (int day = 0; day < 7; day++)
                {
                    // initialize the preference editor from the saved settings for this weekday -H
                    TimeClockStore.ScheduleDayPreference preference = TimeClockStore.SchedulePreferences.First(item => (int)item.Day == day);
                    table.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
                    table.Controls.Add(new Label { Text = ((DayOfWeek)day).ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, day + 1);
                    openInputs[day] = new TextBox { Text = preference.OpeningTime.ToString(@"hhmm"), Dock = DockStyle.Fill };
                    closeInputs[day] = new TextBox { Text = preference.ClosingTime.ToString(@"hhmm"), Dock = DockStyle.Fill };
                    staffInputs[day] = new NumericUpDown { Minimum = 1, Maximum = 50, Value = Math.Max(1, preference.MinimumStaff), Dock = DockStyle.Fill };
                    closedInputs[day] = new CheckBox { Checked = !preference.IsOpen, Text = "Closed", Dock = DockStyle.Fill };
                    table.Controls.Add(openInputs[day], 1, day + 1); table.Controls.Add(closeInputs[day], 2, day + 1);
                    table.Controls.Add(staffInputs[day], 3, day + 1); table.Controls.Add(closedInputs[day], 4, day + 1);
                }
                bufferLabel.Text = "Setup before opening / cleanup after closing (minutes):"; bufferLabel.SetBounds(12, 370, 420, 32);
                setupInput.Minimum = 0; setupInput.Maximum = 120; setupInput.Value = TimeClockStore.SetupMinutes; setupInput.SetBounds(440, 370, 90, 32);
                cleanupInput.Minimum = 0; cleanupInput.Maximum = 120; cleanupInput.Value = TimeClockStore.CleanupMinutes; cleanupInput.SetBounds(550, 370, 90, 32);
                saveButton.Text = "Save Preferences"; saveButton.SetBounds(420, 430, 160, 34);
                cancelButton.Text = "Cancel"; cancelButton.SetBounds(610, 430, 138, 34); cancelButton.DialogResult = DialogResult.Cancel;
                saveButton.Click += (s, args) =>
                {
                    // validate every open day's HHMM times before changing any saved preference -H
                    var updated = new List<TimeClockStore.ScheduleDayPreference>();
                    for (int day = 0; day < 7; day++)
                    {
                        TimeSpan open = TimeSpan.Zero, close = TimeSpan.Zero;
                        if (!closedInputs[day].Checked && (!TryParseScheduleTime(openInputs[day].Text, out open) || !TryParseScheduleTime(closeInputs[day].Text, out close) || close <= open))
                        { MessageBox.Show("Enter valid HHMM opening and closing times with closing after opening, or mark the day closed."); return; }
                        if (closedInputs[day].Checked) { open = TimeSpan.Zero; close = TimeSpan.Zero; }
                        updated.Add(new TimeClockStore.ScheduleDayPreference { Day = (DayOfWeek)day, IsOpen = !closedInputs[day].Checked, OpeningTime = open, ClosingTime = close, MinimumStaff = (int)staffInputs[day].Value });
                    }
                    // save the new reusable preferences and prep buffers together before updating memory -H
                    TimeClockDatabase.SaveSchedulePreferences(updated, (int)setupInput.Value, (int)cleanupInput.Value, employeeID);
                    TimeClockStore.SchedulePreferences.Clear(); TimeClockStore.SchedulePreferences.AddRange(updated);
                    TimeClockStore.SetupMinutes = (int)setupInput.Value; TimeClockStore.CleanupMinutes = (int)cleanupInput.Value;
                    dialog.DialogResult = DialogResult.OK; dialog.Close();
                };
                dialog.Controls.Add(table); dialog.Controls.Add(bufferLabel); dialog.Controls.Add(setupInput); dialog.Controls.Add(cleanupInput);
                dialog.Controls.Add(saveButton); dialog.Controls.Add(cancelButton); dialog.CancelButton = cancelButton;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    WeekChoice week = scheduleWeekSelect.SelectedItem as WeekChoice;
                    if (week != null) loadScheduleWeek(week.Start);
                }
            }
        }

        // generates shifts from the selected week, daily coverage targets, and employee availability -H
        private void saveGeneratedSchedule_Click(object sender, EventArgs e)
        {
            WeekChoice week = scheduleWeekSelect.SelectedItem as WeekChoice;
            if (week == null) { MessageBox.Show("Select a schedule week."); return; }
            // save table edits now and build a temporary schedule before touching stored shifts -H
            if (!saveScheduleWeekEdits(week.Start)) return;
            DateTime sunday = week.Start.Date;
            TextBox[][] entries = scheduleSettingInputs();
            var newShifts = new List<TimeClockStore.Shift>();
            var replaceScheduleDates = new List<DateTime>();
            Dictionary<int, double> scheduledHours = new Dictionary<int, double>();
            List<string> missing = new List<string>();
            List<TimeClockStore.User> employees = TimeClockStore.Employees.ToList();
            List<TimeClockStore.User> managers = TimeClockStore.Users.Where(user => user.IsActive && user.IsManager).ToList();
            const double maximumShiftHours = TimeClockStore.MaximumShiftHours;
            for (int day = 0; day < entries.Length; day++)
            {
                // skip past dates and preserve current-day shifts or time punches -H
                DateTime businessDate = sunday.AddDays(day);
                if (businessDate.Date < TimeClockStore.Now.Date) continue;
                if (businessDate.Date == TimeClockStore.Now.Date && (TimeClockStore.Shifts.Any(item => item.Start.Date == businessDate.Date) || TimeClockStore.WorkPeriods.Any(item => item.ClockIn.Date == businessDate.Date))) continue;
                bool allBlank = entries[day].All(input => String.IsNullOrWhiteSpace(input.Text));
                if (businessDate.Date == TimeClockStore.Now.Date)
                {
                    TimeSpan todayClose;
                    if (allBlank || !TryParseScheduleTime(entries[day][1].Text, out todayClose) || todayClose.Add(TimeSpan.FromMinutes(TimeClockStore.CleanupMinutes)) < TimeClockStore.Now.TimeOfDay) continue;
                }
                replaceScheduleDates.Add(businessDate.Date);
                if (allBlank) continue;
                TimeSpan opening, closing; int minimumStaff;
                if (!TryParseScheduleTime(entries[day][0].Text, out opening) || !TryParseScheduleTime(entries[day][1].Text, out closing) || closing <= opening || !int.TryParse(entries[day][2].Text, out minimumStaff) || minimumStaff < 1)
                { MessageBox.Show("Enter HHMM opening and closing times and a positive minimum on-duty count for each open day."); return; }
                // extend the coverage window by configured prep/cleanup time, then split it into shifts no longer than eight hours -H
                DateTime openTime = businessDate.Add(opening);
                DateTime closeTime = businessDate.Add(closing);
                if (opening < TimeSpan.FromMinutes(TimeClockStore.SetupMinutes) || closing.Add(TimeSpan.FromMinutes(TimeClockStore.CleanupMinutes)) >= TimeSpan.FromDays(1))
                { MessageBox.Show("Opening prep and closing cleanup must fit within the same calendar day."); return; }
                DateTime coverageStart = openTime.AddMinutes(-TimeClockStore.SetupMinutes);
                DateTime coverageEnd = closeTime.AddMinutes(TimeClockStore.CleanupMinutes);
                double spanHours = (coverageEnd - coverageStart).TotalHours;
                // make consecutive eight-hour windows and a final remainder window to span the whole day -H
                var shiftWindows = new List<Tuple<DateTime, DateTime>>();
                DateTime nextShiftStart = coverageStart;
                while ((coverageEnd - nextShiftStart).TotalHours > maximumShiftHours)
                {
                    DateTime shiftEnd = nextShiftStart.AddHours(maximumShiftHours);
                    shiftWindows.Add(Tuple.Create(nextShiftStart, shiftEnd));
                    nextShiftStart = shiftEnd;
                }
                double remainingHours = (coverageEnd - nextShiftStart).TotalHours;
                if (remainingHours > 0)
                {
                    double finalShiftHours = remainingHours;
                    DateTime finalShiftStart = coverageEnd.AddHours(-finalShiftHours);
                    shiftWindows.Add(Tuple.Create(finalShiftStart, coverageEnd));
                }
                // place one available manager in the middle of each open day, separate from employee coverage counts -H
                double storeHours = (closeTime - openTime).TotalHours;
                double managerHours = Math.Min(maximumShiftHours, storeHours);
                DateTime managerStart = openTime.AddHours((storeHours - managerHours) / 2.0);
                DateTime managerEnd = managerStart.AddHours(managerHours);
                TimeClockStore.User selectedManager = managers.Where(manager =>
                    !newShifts.Any(item => item.EmployeeId == manager.Id && item.Start.Date == businessDate.Date) &&
                    !TimeClockStore.TimeOffRequests.Any(request => request.EmployeeId == manager.Id && request.Day.Date == businessDate.Date && request.Status != "Denied"))
                    .OrderBy(manager => scheduledHours.ContainsKey(manager.Id) ? scheduledHours[manager.Id] : 0).ThenBy(manager => manager.Id).FirstOrDefault();
                if (selectedManager == null)
                {
                    missing.Add(businessDate.DayOfWeek + " manager mid-shift " + managerStart.ToString("t") + " - " + managerEnd.ToString("t") + " (needs 1 manager)");
                    break;
                }
                newShifts.Add(new TimeClockStore.Shift { EmployeeId = selectedManager.Id, Start = managerStart, End = managerEnd });
                if (!scheduledHours.ContainsKey(selectedManager.Id)) scheduledHours[selectedManager.Id] = 0;
                scheduledHours[selectedManager.Id] += managerHours;
                foreach (Tuple<DateTime, DateTime> window in shiftWindows)
                {
                    DateTime shiftStart = window.Item1;
                    DateTime shiftEnd = window.Item2;
                    double shiftHours = (shiftEnd - shiftStart).TotalHours;
                    // require full availability, no same-day duplicate, and no active PTO request, then balance hours -H
                    for (int position = 0; position < minimumStaff; position++)
                    {
                        var selected = employees.Where(person =>
                            !newShifts.Any(item => item.EmployeeId == person.Id && item.Start.Date == shiftStart.Date) &&
                            !TimeClockStore.TimeOffRequests.Any(request => request.EmployeeId == person.Id && request.Day.Date == shiftStart.Date && request.Status != "Denied") &&
                            TimeClockStore.Availabilities.Any(available => available.EmployeeId == person.Id && available.Day == shiftStart.DayOfWeek && shiftStart.TimeOfDay >= available.Start && shiftEnd.TimeOfDay <= available.End))
                            .OrderBy(person => scheduledHours.ContainsKey(person.Id) ? scheduledHours[person.Id] : 0).ThenBy(person => person.Id).FirstOrDefault();
                        if (selected == null)
                        {
                            missing.Add(shiftStart.DayOfWeek + " " + shiftStart.ToString("t") + " - " + shiftEnd.ToString("t") + " (needs " + (minimumStaff - position) + " more)");
                            break;
                        }
                        newShifts.Add(new TimeClockStore.Shift { EmployeeId = selected.Id, Start = shiftStart, End = shiftEnd });
                        if (!scheduledHours.ContainsKey(selected.Id)) scheduledHours[selected.Id] = 0;
                        scheduledHours[selected.Id] += shiftHours;
                    }
                    if (missing.Count > 0) break;
                }
                if (missing.Count > 0) break;
            }
            if (missing.Count > 0)
            {
                // keep the current schedule unchanged when any required coverage slot cannot be staffed -H
                MessageBox.Show("Schedule was not saved because the employee availability or PTO requests do not provide enough coverage:\n\n" + String.Join(Environment.NewLine, missing.Take(8)), "Coverage Gaps");
                return;
            }
            DateTime weekEnd = sunday.AddDays(7);
            // replace schedule rows in MariaDB before refreshing the in-memory week -H
            TimeClockDatabase.ReplaceScheduleDates(replaceScheduleDates, newShifts);
            // preserve past shifts and completed timeclock records in the visible schedule -H
            TimeClockStore.Shifts.RemoveAll(item => replaceScheduleDates.Contains(item.Start.Date) && item.Start.Date < weekEnd.Date);
            TimeClockStore.Shifts.AddRange(newShifts);
            MessageBox.Show("Generated " + newShifts.Count + " shifts for " + week.ToString() + ". Employees are assigned only when their availability covers the full shift.", "Schedule Generated");
        }

        // parses 24-hour HHMM values used by the schedule controls -H
        private bool TryParseScheduleTime(string value, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            int numericTime;
            if (!int.TryParse(value, out numericTime) || numericTime < 0 || numericTime > 2359) return false;
            int hours = numericTime / 100;
            int minutes = numericTime % 100;
            if (hours > 23 || minutes > 59) return false;
            time = new TimeSpan(hours, minutes, 0);
            return true;
        }

        // load the selected employee and date into the designer's schedule edit fields -H
        private void editSelectedSchedule_Click(object sender, EventArgs e)
        {
            if (comboBox6.SelectedItem == null) { MessageBox.Show("Select an employee and date."); return; }
            int id = GetSelectedEmployeeId(comboBox6);
            DateTime date = scheduleDatePicker.Value.Date;
            selectedScheduleForEdit = TimeClockStore.Shifts.FirstOrDefault(item => item.EmployeeId == id && item.Start.Date == date);
            if (selectedScheduleForEdit == null)
            {
                // prepare a new in-memory shift so the same time fields can assign an open date -H
                selectedScheduleForEdit = new TimeClockStore.Shift { EmployeeId = id, Start = date, End = date };
                scheduleEditStartInput.Clear();
                scheduleEditEndInput.Clear();
                label49.Text = "No shift exists for this employee and date. Enter times, then click Save Changes.";
                saveEditedScheduleButton.Enabled = true;
                removeEditedScheduleButton.Enabled = false;
                return;
            }
            // show the saved shift's four-digit times in the visible designer controls -H
            scheduleEditStartInput.Text = selectedScheduleForEdit.Start.ToString("HHmm");
            scheduleEditEndInput.Text = selectedScheduleForEdit.End.ToString("HHmm");
            label49.Text = "Editing " + ((EmployeeChoice)comboBox6.SelectedItem).Name + " on " + date.ToShortDateString();
            saveEditedScheduleButton.Enabled = true;
            removeEditedScheduleButton.Enabled = true;
        }

        // validate and save changes from the designer schedule editor -H
        private void saveSelectedScheduleEdit_Click(object sender, EventArgs e)
        {
            TimeClockStore.Shift shift = selectedScheduleForEdit;
            if (shift == null) { MessageBox.Show("Confirm an employee and date first."); return; }
            int id = GetSelectedEmployeeId(comboBox6);
            DateTime updatedDate = scheduleDatePicker.Value.Date;
            bool isNewShift = !TimeClockStore.Shifts.Contains(shift);
            if (id != shift.EmployeeId || updatedDate != shift.Start.Date)
            { MessageBox.Show("Confirm the selected employee and date before saving."); return; }
            TimeSpan start, end;
            if (!TryParseScheduleTime(scheduleEditStartInput.Text, out start) || !TryParseScheduleTime(scheduleEditEndInput.Text, out end) || end <= start)
            { MessageBox.Show("Enter valid HHMM shift times with the end after the start."); return; }
            if ((end - start).TotalHours > TimeClockStore.MaximumShiftHours)
            { MessageBox.Show("A scheduled shift cannot be longer than 8 hours."); return; }
            // prevent two shifts for one employee on the selected date and enforce saved availability -H
            bool hasConflictingShift = TimeClockStore.Shifts.Any(item => item != shift && item.EmployeeId == id && item.Start.Date == updatedDate);
            if (hasConflictingShift) { MessageBox.Show("The employee already has a shift scheduled for that date."); return; }
            TimeClockStore.Availability availability = TimeClockStore.Availabilities.FirstOrDefault(item => item.EmployeeId == id && item.Day == updatedDate.DayOfWeek);
            if (availability == null || start < availability.Start || end > availability.End)
            { MessageBox.Show("The updated shift falls outside the employee's availability."); return; }
            TimeSpan businessOpen, businessClose;
            if (!TryGetBusinessHours(updatedDate, out businessOpen, out businessClose))
            { MessageBox.Show("The store is closed on the selected date."); return; }
            if (businessOpen < TimeSpan.FromMinutes(TimeClockStore.SetupMinutes) || businessClose.Add(TimeSpan.FromMinutes(TimeClockStore.CleanupMinutes)) >= TimeSpan.FromDays(1))
            { MessageBox.Show("The configured opening or closing buffer does not fit within the same day."); return; }
            TimeSpan earliestShift = businessOpen - TimeSpan.FromMinutes(TimeClockStore.SetupMinutes);
            TimeSpan latestShift = businessClose + TimeSpan.FromMinutes(TimeClockStore.CleanupMinutes);
            if (start < earliestShift || end > latestShift)
            { MessageBox.Show("The shift must fit within the store's opening hours and configured setup or cleanup time."); return; }
            shift.Start = updatedDate.Add(start);
            shift.End = updatedDate.Add(end);
            // persist the validated edit before updating the current employee and date selection -H
            TimeClockDatabase.SaveShift(shift);
            if (isNewShift) TimeClockStore.Shifts.Add(shift);
            label49.Text = (isNewShift ? "Shift assigned for " : "Shift updated for ") + updatedDate.ToShortDateString() + ".";
            saveEditedScheduleButton.Enabled = true;
            removeEditedScheduleButton.Enabled = true;
            if (GetSelectedEmployeeId(scheduleEmployeeSelect) == id) loadManagerScheduleCalendar();
        }

        // read the active weekly override or reusable business hours for one date -H
        private bool TryGetBusinessHours(DateTime date, out TimeSpan opening, out TimeSpan closing)
        {
            opening = TimeSpan.Zero;
            closing = TimeSpan.Zero;
            DateTime weekStart = date.Date.AddDays(-(int)date.DayOfWeek);
            string[] weekValues;
            if (TimeClockStore.ScheduleWeekEdits.TryGetValue(weekStart, out weekValues))
            {
                int index = (int)date.DayOfWeek * 3;
                string openText = weekValues[index];
                string closeText = weekValues[index + 1];
                string staffText = weekValues[index + 2];
                if (String.IsNullOrWhiteSpace(openText) && String.IsNullOrWhiteSpace(closeText) && String.IsNullOrWhiteSpace(staffText)) return false;
                return TryParseScheduleTime(openText, out opening) && TryParseScheduleTime(closeText, out closing) && closing > opening;
            }
            TimeClockStore.ScheduleDayPreference preference = TimeClockStore.SchedulePreferences.FirstOrDefault(item => item.Day == date.DayOfWeek);
            if (preference == null || !preference.IsOpen) return false;
            opening = preference.OpeningTime;
            closing = preference.ClosingTime;
            return closing > opening;
        }

        // remove the loaded schedule row from MariaDB and the in-memory calendar -H
        private void removeSelectedScheduleEdit_Click(object sender, EventArgs e)
        {
            TimeClockStore.Shift shift = selectedScheduleForEdit;
            if (shift == null) return;
            TimeClockDatabase.DeleteShift(shift);
            TimeClockStore.Shifts.Remove(shift);
            label49.Text = "Shift removed for " + shift.Start.ToShortDateString() + ".";
            clearSelectedScheduleEdit_Click(null, EventArgs.Empty);
        }

        // clear the designer fields before selecting another schedule row -H
        private void clearSelectedScheduleEdit_Click(object sender, EventArgs e)
        {
            selectedScheduleForEdit = null;
            scheduleEditStartInput.Clear();
            scheduleEditEndInput.Clear();
            saveEditedScheduleButton.Enabled = false;
            removeEditedScheduleButton.Enabled = false;
            label49.Text = "Select an employee and date above, then click Confirm to load a shift.";
        }
        // Clock In Functionality
        // - - - - -    Samuel Griggs
        // records actual manager clock time, blocks early punches, and confirms late-arrival points -H
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
            TimeClockStore.WorkPeriod newPeriod = new TimeClockStore.WorkPeriod { EmployeeId = employeeID, ClockIn = clockInTime };
            TimeClockDatabase.SaveWorkPeriod(newPeriod);
            TimeClockStore.WorkPeriods.Add(newPeriod);
            updateStatus(true);
            statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked In");
        }
        // Clock Out Functionality
        // - - - - -    Samuel Griggs
        // confirms point-earning early or late clock-outs, then saves the manager's actual time -H
        private void clockOutButton_Click(object sender, EventArgs e) {
            TimeClockStore.WorkPeriod period = TimeClockStore.ActivePeriod(employeeID);
            if (period == null) { MessageBox.Show("You are not clocked in."); return; }
            if (period.BreakStart.HasValue && !period.BreakEnd.HasValue) { MessageBox.Show("End your break before clocking out."); return; }
            // Keep the real punch time and check whether it falls outside the 15-minute end-of-shift window -H
            clockOutTime = TimeClockStore.Now;
            TimeClockStore.Shift scheduledShift = TimeClockStore.ScheduledShiftForDate(employeeID, period.ClockIn.Date);
            if (scheduledShift != null && clockOutTime < scheduledShift.End.AddMinutes(-TimeClockStore.ClockGraceMinutes))
            {
                // Give the manager a chance to cancel before saving an early clock-out and its half point -H
                if (MessageBox.Show("You are clocking out more than 15 minutes before your scheduled end. Continuing will add 0.5 attendance points. Continue?", "Early Clock Out", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                TimeClockStore.AddAttendancePoint(employeeID, scheduledShift.Start, 0.5m, "Left early");
            }
            else if (scheduledShift != null && clockOutTime > scheduledShift.End.AddMinutes(TimeClockStore.ClockGraceMinutes))
            {
                // Warn and apply the same half-point rule when working beyond the allowed end window -H
                if (MessageBox.Show("You are working more than 15 minutes past your scheduled end. Continuing will add 0.5 attendance points. Continue?", "Late Clock Out", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                TimeClockStore.AddAttendancePoint(employeeID, scheduledShift.Start, 0.5m, "Worked past scheduled end");
            }
            period.ClockOut = clockOutTime;
            // persist the completed manager punch before changing the status label -H
            TimeClockDatabase.SaveWorkPeriod(period);
            updateStatus(false); updateBreak(false); statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked Out");
        }

        // Start Break Functionality
        // - - - - -    Samuel Griggs
        // allows lunch after three hours on shift and records the manager's break start -H
        private void breakStartButton_Click(object sender, EventArgs e) {
            TimeClockStore.WorkPeriod period = TimeClockStore.ActivePeriod(employeeID);
            if (period == null) { MessageBox.Show("Clock in before starting a break."); return; }
            if (period.BreakStart.HasValue && !period.BreakEnd.HasValue) { MessageBox.Show("A break is already active."); return; }
            // Enforce the three-hour minimum from the manager's actual clock-in before starting lunch -H
            DateTime earliestBreakTime = period.ClockIn.AddHours(TimeClockStore.MinimumWorkBeforeBreakHours);
            if (TimeClockStore.Now < earliestBreakTime) { MessageBox.Show("You must work at least 3 hours before starting your lunch break."); return; }
            startBreakTime = TimeClockStore.Now; period.BreakStart = startBreakTime; period.BreakEnd = null;
            period.Breaks.Add(new TimeClockStore.WorkPeriod.BreakPeriod { Start = startBreakTime });
            // save an active manager break so a relogin restores its state -H
            TimeClockDatabase.SaveWorkPeriod(period);
            updateBreak(true); statusLabel.Text = TimeClockStore.FormatStatus("Status: On Break");
        }
        // End Break Functionality
        // - - - - -    Samuel Griggs
        // enforces the 30-minute minimum before completing the manager's break -H
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
            period.BreakEnd = endBreakTime; period.Breaks.Last().End = endBreakTime;
            // persist the completed manager lunch interval -H
            TimeClockDatabase.SaveWorkPeriod(period);
            updateBreak(false); statusLabel.Text = TimeClockStore.FormatStatus("Status: Clocked In");
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
        // loads the selected employee's saved shifts into the monthly schedule grid -H
        private void viewScheduleButton_Click(object sender, EventArgs e)
        {
            scheduleGroupBox.BringToFront();
        }
        private void scheduleEmployeeSelect_SelectedIndexChanged(object sender, EventArgs e) {
            if (scheduleEmployeeSelect.SelectedItem == null) return;
            int id = GetSelectedEmployeeId(scheduleEmployeeSelect);
            // Show the selected employee's current total while the manager reviews their schedule -H
            decimal attendancePoints = TimeClockStore.AttendancePoints
                .Where(item => item.EmployeeId == id)
                .Sum(item => item.Points);
            scheduleAttendanceLabel.Text = String.Format("Attendance Points: {0:0.0}", attendancePoints);
            loadManagerScheduleCalendar();
        }

        // refresh the manager calendar after selecting another month -H
        private void scheduleMonthSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            loadManagerScheduleCalendar();
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
                        BorderStyle = BorderStyle.FixedSingle,
                        Font = new Font("Microsoft Sans Serif", 12F)
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

        // puts the selected month and employee schedule into the manager calendar -H
        private void loadManagerScheduleCalendar()
        {
            MonthChoice selectedMonth = scheduleMonthSelect.SelectedItem as MonthChoice;
            if (selectedMonth == null) return;
            DateTime monthStart = selectedMonth.Start;
            SetScheduleWeekRows(emplyScheduleTable, "managerScheduleWeek6", monthStart);
            scheduleGroupBox.Text = "Schedule - " + monthStart.ToString("MMMM yyyy");
            // reset all 42 date cells and format them as readable calendar boxes -H
            for (int week = 1; week <= 6; week++)
            {
                for (int day = 0; day < 7; day++)
                {
                    Label cell = emplyScheduleTable.GetControlFromPosition(day, week) as Label;
                    if (cell == null) continue;
                    cell.Text = "";
                    cell.AutoSize = false;
                    cell.Dock = DockStyle.Fill;
                    cell.TextAlign = ContentAlignment.TopLeft;
                    cell.Padding = new Padding(6);
                    cell.BorderStyle = BorderStyle.FixedSingle;
                    cell.Font = new Font("Microsoft Sans Serif", 12F);
                }
            }
            // show dates in their weekday columns and append matching shifts when an employee is selected -H
            int firstWeekday = (int)monthStart.DayOfWeek;
            int selectedEmployeeId = GetSelectedEmployeeId(scheduleEmployeeSelect);
            for (int dayNumber = 1; dayNumber <= DateTime.DaysInMonth(monthStart.Year, monthStart.Month); dayNumber++)
            {
                int position = firstWeekday + dayNumber - 1;
                int week = position / 7 + 1;
                int weekday = position % 7;
                if (week > 6) continue;
                Label cell = emplyScheduleTable.GetControlFromPosition(weekday, week) as Label;
                if (cell == null) continue;
                DateTime date = monthStart.AddDays(dayNumber - 1);
                string entry = dayNumber.ToString();
                if (selectedEmployeeId >= 0)
                {
                    // list all saved shifts for this employee on the selected date -H
                    string[] shiftTimes = TimeClockStore.Shifts
                        .Where(item => item.EmployeeId == selectedEmployeeId && item.Start.Date == date.Date)
                        .OrderBy(item => item.Start)
                        .Select(item => item.Start.ToString("h:mm tt") + " - " + item.End.ToString("h:mm tt"))
                        .ToArray();
                    if (shiftTimes.Length > 0) entry += Environment.NewLine + String.Join(Environment.NewLine, shiftTimes);
                }
                cell.Text = entry;
            }
        }


        

        // Edit Timesheet Functions
        //      Allows the Manager to edit the Timesheet of an Employee
        // - - - - -    Samuel Griggs
        private void editTimesheetButton_Click(object sender, EventArgs e)
        {
            editTimesheetGroupBox.BringToFront();
        }
        // validates, saves, and audits the manager's timesheet correction -H
        private void confirmTimesheetEditButton_Click(object sender, EventArgs e) {
            if (comboBox7.SelectedItem == null) { MessageBox.Show("Select an employee and date, then confirm the selection."); return; }
            int id = GetSelectedEmployeeId(comboBox7);
            DateTime day = editTimesheetDatePicker.Value.Date;
            TimeSpan clockIn, clockOut, breakStart = TimeSpan.Zero, breakEnd = TimeSpan.Zero;
            if (!TryParseScheduleTime(emplyNewClockIn.Text, out clockIn) || !TryParseScheduleTime(emplyNewClockOut.Text, out clockOut) || clockOut <= clockIn) {
                MessageBox.Show("Enter valid clock-in and clock-out times in HHMM format."); return;
            }
            bool hasBreakStart = !String.IsNullOrWhiteSpace(emplyNewBreakStart.Text);
            bool hasBreakEnd = !String.IsNullOrWhiteSpace(emplyNewBreakEnd.Text);
            if (hasBreakStart != hasBreakEnd) { MessageBox.Show("Enter both break times or leave both blank."); return; }
            if (hasBreakStart && (!TryParseScheduleTime(emplyNewBreakStart.Text, out breakStart) || !TryParseScheduleTime(emplyNewBreakEnd.Text, out breakEnd) || breakEnd <= breakStart || breakStart < clockIn || breakEnd > clockOut)) {
                MessageBox.Show("The break must be valid and fall between clock-in and clock-out."); return;
            }
            string reason = PromptForEditReason();
            if (String.IsNullOrWhiteSpace(reason)) { MessageBox.Show("A reason is required to audit the edit."); return; }
            DateTime newClockIn = day.Add(clockIn);
            DateTime newClockOut = day.Add(clockOut);
            TimeClockStore.WorkPeriod period = TimeClockStore.WorkPeriods.LastOrDefault(item => item.EmployeeId == id && item.ClockIn.Date == day);
            bool isNewPeriod = period == null;
            // retain the previous values so the audit record explains the exact correction -H
            string previousValues = formatTimesheetForAudit(period);
            if (isNewPeriod) period = new TimeClockStore.WorkPeriod { EmployeeId = id, ClockIn = newClockIn };
            period.ClockIn = newClockIn;
            period.ClockOut = newClockOut;
            period.BreakStart = hasBreakStart ? (DateTime?)day.Add(breakStart) : null;
            period.BreakEnd = hasBreakEnd ? (DateTime?)day.Add(breakEnd) : null;
            period.Breaks.Clear();
            if (hasBreakStart) period.Breaks.Add(new TimeClockStore.WorkPeriod.BreakPeriod { Start = day.Add(breakStart), End = day.Add(breakEnd) });
            string updatedValues = formatTimesheetForAudit(period);
            // save the corrected punch and manager editor information before writing its audit row -H
            TimeClockDatabase.SaveWorkPeriod(period, employeeID);
            if (isNewPeriod) TimeClockStore.WorkPeriods.Add(period);
            // calculate attendance from the saved shift after a manager corrects a punch -H
            TimeClockStore.Shift scheduledShift = TimeClockStore.ScheduledShiftForDate(id, day);
            TimeClockStore.ReconcileShiftAttendance(id, scheduledShift, period.ClockIn, period.ClockOut);
            TimeClockStore.AuditEntry audit = new TimeClockStore.AuditEntry
            {
                EmployeeId = id, ManagerId = employeeID, ChangedAt = TimeClockStore.Now, Reason = reason.Trim(),
                Change = "Updated " + day.ToShortDateString() + " | Before: " + previousValues + " | After: " + updatedValues,
                RecordId = period.Id, OldValues = previousValues, NewValues = updatedValues
            };
            TimeClockDatabase.SaveAudit(audit);
            TimeClockStore.Audit.Add(audit);
            label59.Text = String.Format("Total Hours Worked: {0:#.###}", period.HoursWorked);
            if (GetSelectedEmployeeId(scheduleEmployeeSelect) == id)
                scheduleAttendanceLabel.Text = String.Format("Attendance Points: {0:0.0}", TimeClockStore.AttendancePoints.Where(item => item.EmployeeId == id).Sum(item => item.Points));
            MessageBox.Show(scheduledShift == null
                ? "Timesheet updated and audit entry recorded. No scheduled shift was found, so attendance points were not changed."
                : "Timesheet updated and audit entry recorded. Attendance points were recalculated against the scheduled shift.");
        }

        // format old or updated times consistently in the manager edit audit -H
        private string formatTimesheetForAudit(TimeClockStore.WorkPeriod period)
        {
            if (period == null) return "No saved entry";
            // include all saved breaks and fall back to the original single-break fields -H
            string breakTimes = period.Breaks.Any(item => item.End.HasValue)
                ? String.Join("; ", period.Breaks.Where(item => item.End.HasValue).Select(item => item.Start.ToString("HHmm") + " - " + item.End.Value.ToString("HHmm")))
                : period.BreakStart.HasValue && period.BreakEnd.HasValue
                    ? period.BreakStart.Value.ToString("HHmm") + " - " + period.BreakEnd.Value.ToString("HHmm")
                    : "No break";
            return "Clock in " + period.ClockIn.ToString("HHmm") + ", clock out " +
                (period.ClockOut.HasValue ? period.ClockOut.Value.ToString("HHmm") : "not set") + ", break " + breakTimes;
        }

        // asks the manager for the reason recorded with a timesheet edit -H
        private string PromptForEditReason()
        {
            using (Form prompt = new Form())
            using (TextBox reasonInput = new TextBox())
            using (Button saveButton = new Button())
            using (Button cancelButton = new Button())
            {
                // require a short audit explanation before a manager's correction is recorded -H
                prompt.Text = "Timesheet Edit Reason";
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.ClientSize = new Size(420, 130);
                reasonInput.SetBounds(12, 12, 396, 28);
                saveButton.Text = "Save"; saveButton.DialogResult = DialogResult.OK; saveButton.SetBounds(230, 70, 85, 32);
                cancelButton.Text = "Cancel"; cancelButton.DialogResult = DialogResult.Cancel; cancelButton.SetBounds(323, 70, 85, 32);
                prompt.Controls.AddRange(new Control[] { reasonInput, saveButton, cancelButton });
                prompt.AcceptButton = saveButton; prompt.CancelButton = cancelButton;
                return prompt.ShowDialog(this) == DialogResult.OK ? reasonInput.Text : "";
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

        // updates the built-in timesheet labels for the selected employee and date -H
        private void updateManagerTimesheetLabels(object sender, EventArgs e)
        {
            if (employeeToView.SelectedIndex < 0) { MessageBox.Show("Select an employee and date."); return; }
            int id = GetSelectedEmployeeId(employeeToView);
            TimeClockStore.User user = TimeClockStore.FindUser(id);
            DateTime selectedDate = viewTimesheetDatePicker.Value.Date;
            var periods = TimeClockStore.Timesheet(id, selectedDate, selectedDate).ToList();
            TimeClockStore.WorkPeriod period = periods.LastOrDefault();
            viewTimesheetGroupBox.Text = "Timesheet - " + (user == null ? "Employee " + id : user.Name) + " - " + selectedDate.ToShortDateString();
            if (period == null)
            {
                clockInTimeLabel.Text = "No timesheet entry for this date"; clockOutTimeLabel.Text = "--";
                startBreakTimeLabel.Text = "--"; endBreakTimeLabel.Text = "--";
            }
            else
            {
                clockInTimeLabel.Text = period.ClockIn.ToString("g");
                clockOutTimeLabel.Text = period.ClockOut.HasValue ? period.ClockOut.Value.ToString("g") : "In progress";
                startBreakTimeLabel.Text = period.BreakStart.HasValue ? period.BreakStart.Value.ToString("t") : "--";
                endBreakTimeLabel.Text = period.BreakEnd.HasValue ? period.BreakEnd.Value.ToString("t") : "--";
            }
            hrsWorkedLabel.Text = String.Format("Total Hours Worked: {0:F2}", periods.Sum(item => item.HoursWorked));
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

        private void tableLayoutPanel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void scheduleEditStartInput_TextChanged(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {

        }
    }
}
