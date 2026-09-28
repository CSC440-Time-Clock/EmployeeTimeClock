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
        public Manager()
        {
            InitializeComponent();
            scheduleGroupBox.BringToFront();
        }

        private void tableLayoutPanel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void clockInButton_Click(object sender, EventArgs e)
        {

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
    }
}
