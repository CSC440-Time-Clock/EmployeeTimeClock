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
    public partial class Login : Form
    {
        public Login()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            if (usernameTextBox.Text == "1")
            {
                Manager managerForm = new Manager();
                managerForm.Show();
                this.Hide();
            }
            else if (usernameTextBox.Text == "2")
            {
                Employee employeeForm = new Employee();
                employeeForm.Show();
                this.Hide();
            }
            else
            {
                MessageBox.Show("Invalid username or password.");
            }
        }
    }
}
