namespace EmployeeTimeClock
{
    partial class Login
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.LoginButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.usernameTextBox = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.passwordTextBox = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // LoginButton
            // 
            this.LoginButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 25F);
            this.LoginButton.Location = new System.Drawing.Point(647, 448);
            this.LoginButton.Name = "LoginButton";
            this.LoginButton.Size = new System.Drawing.Size(206, 70);
            this.LoginButton.TabIndex = 0;
            this.LoginButton.Text = "Submit";
            this.LoginButton.UseVisualStyleBackColor = true;
            this.LoginButton.Click += new System.EventHandler(this.LoginButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.SystemColors.Window;
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F);
            this.label1.Location = new System.Drawing.Point(3, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(258, 37);
            this.label1.TabIndex = 1;
            this.label1.Text = "Enter Username:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // usernameTextBox
            // 
            this.usernameTextBox.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.usernameTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F);
            this.usernameTextBox.Location = new System.Drawing.Point(3, 100);
            this.usernameTextBox.Name = "usernameTextBox";
            this.usernameTextBox.Size = new System.Drawing.Size(263, 44);
            this.usernameTextBox.TabIndex = 2;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.usernameTextBox);
            this.groupBox1.Location = new System.Drawing.Point(614, 48);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(269, 147);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "groupBox1";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.passwordTextBox);
            this.groupBox2.Location = new System.Drawing.Point(611, 236);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(269, 147);
            this.groupBox2.TabIndex = 4;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "groupBox2";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.SystemColors.Window;
            this.label2.Dock = System.Windows.Forms.DockStyle.Top;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F);
            this.label2.Location = new System.Drawing.Point(3, 16);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(252, 37);
            this.label2.TabIndex = 1;
            this.label2.Text = "Enter Password:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // passwordTextBox
            // 
            this.passwordTextBox.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.passwordTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F);
            this.passwordTextBox.Location = new System.Drawing.Point(3, 100);
            this.passwordTextBox.Name = "passwordTextBox";
            this.passwordTextBox.Size = new System.Drawing.Size(263, 44);
            this.passwordTextBox.TabIndex = 2;
            // 
            // Login
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DodgerBlue;
            this.ClientSize = new System.Drawing.Size(1502, 688);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.LoginButton);
            this.Name = "Login";
            this.Text = "Form1";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button LoginButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox usernameTextBox;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox passwordTextBox;
    }
}

