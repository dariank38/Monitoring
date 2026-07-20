namespace Monitoring
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // MainForm
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(8, 8);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MaximumSize = new Size(8, 8);
            MinimizeBox = false;
            MinimumSize = new Size(8, 8);
            Name = "MainForm";
            Opacity = 1D;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Screen Monitor";
            TopMost = true;
            ResumeLayout(false);
        }

        #endregion
    }
}
