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
            // mask the password characters while the user types credentials -H
            passwordTextBox.UseSystemPasswordChar = true;
        }

        // authenticate against the in-memory accounts, then open the matching role-specific screen -H
        private void LoginButton_Click(object sender, EventArgs e)
        {
            TimeClockStore.User user = TimeClockStore.Authenticate(usernameTextBox.Text.Trim(), passwordTextBox.Text);
            if (user == null)
            {
                MessageBox.Show("Invalid username or password.");
                passwordTextBox.Clear();
                return;
            }

            // synchronize application time before opening screens that record work events -H
            TimeClockStore.SynchronizeTime();

            if (user.IsManager)
            {
                new Manager(this, user.Id).Show();
            }
            else
            {
                new Employee(this, user.Id).Show();
            }
            passwordTextBox.Clear();
            this.Hide();
        }
    }
}
